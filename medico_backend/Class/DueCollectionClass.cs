using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace medico_backend.Class
{
    /// <summary>
    /// HMS Due Collection Service
    ///
    /// Tables used (all confirmed against real DDL):
    ///   lab_request_master        — bill master; paidamount is updated on every settlement
    ///   receipt_master            — one row per payment event
    ///   receipt_details           — links receipt → bill
    ///   receipt_advances          — advance ledger (deposit / usage / refund rows)
    ///   balancecollectionby       — cash-flow ledger (both legacy + new columns written)
    ///   balancecollectionbytest   — test-level settlement flag per tcode
    ///   billno_master             — receipt number sequence configuration
    ///   billno_sequence           — sequence counter (SELECT FOR UPDATE, race-safe)
    ///   counter_timing            — validates open shift before any collection
    ///   doctor_master             — doctor name for preview enrichment
    ///
    /// Advance logic (FIFO):
    ///   Oldest advance deposit is consumed first.
    ///   A deposit can be partially consumed across multiple collections.
    ///   Available balance = SUM(deposit rows) − SUM(usage + refund rows).
    ///
    /// All collection operations run inside a single NpgsqlTransaction.
    /// On any error the transaction is rolled back — the DB is always left consistent.
    /// </summary>
    public class HmsDueCollectionClass
    {
        private readonly string _conn;
        private readonly ILogger<HmsDueCollectionClass> _logger;

        public HmsDueCollectionClass(
            IConfiguration cfg,
            ILogger<HmsDueCollectionClass> logger)
        {
            _conn = cfg.GetConnectionString("conn")
                ?? throw new InvalidOperationException("Connection string 'conn' not found.");
            _logger = logger;
        }

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ═══════════════════════════════════════════════════════════════════════
        //  1. DRY-RUN PREVIEW  (no DB writes)
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsDuePreviewResponse? data)> GetDuePreview(
            string requestguid, double requestedAdvance, string tenantCode)
        {
            try
            {
                using var db = GetConnection();

                var bill = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT lrm.*, dm.name AS doctor_name
                        FROM lab_request_master lrm
                        LEFT JOIN doctor_master dm
                               ON dm.dcode = lrm.dcode AND dm.tenant_code = lrm.tenant_code
                       WHERE lrm.requestguid  = @rg
                         AND lrm.tenant_code  = @t
                         AND COALESCE(lrm.isdeleted, false) = false
                         AND COALESCE(lrm.deleted,   false) = false
                       LIMIT 1",
                    new { rg = requestguid, t = tenantCode });

                if (bill == null)
                    return ("Bill not found or has been cancelled.", null);

                double totalBill = bill.totalamount ?? 0;
                double previousPaid = bill.paidamount ?? 0;
                double currentDue = Math.Round(Math.Max(totalBill - previousPaid, 0), 2);

                if (currentDue <= 0.01)
                    return ("This bill has no outstanding due amount.", null);

                double advanceAvail = await GetPatientAdvanceBalance(
                    db, (decimal?)bill.custid, tenantCode);

                double advanceToUse = Math.Round(
                    Math.Min(requestedAdvance, Math.Min(advanceAvail, currentDue)), 2);
                double cashNeeded = Math.Round(Math.Max(currentDue - advanceToUse, 0), 2);
                double dueAfter = Math.Round(Math.Max(currentDue - advanceToUse - cashNeeded, 0), 2);

                return ("SUCCESS", new HmsDuePreviewResponse
                {
                    requestguid = requestguid,
                    bill_no = bill.requestsnoprint,
                    bill_date = bill.requestdatetime,
                    bill_type = bill.bill_category,
                    custid = bill.custid,
                    patient_name = bill.name,
                    mobileno = bill.mobileno,
                    opvisitid = bill.opvisitid,
                    doctor_name = bill.doctor_name,
                    dcode = bill.dcode == null ? (int?)null : Convert.ToInt32((decimal)bill.dcode),
                    total_bill_amount = totalBill,
                    previous_paid = previousPaid,
                    current_due = currentDue,
                    advance_available = advanceAvail,
                    advance_to_use = advanceToUse,
                    amount_to_collect = cashNeeded,
                    due_after = dueAfter
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDuePreview failed for requestguid={rg}", requestguid);
                return (ex.Message, null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  2. COLLECT DUE — atomic transaction
        //
        //  Scenario 1 — Cash only     : advance_to_use=0,    cash_collected=3000
        //  Scenario 2 — Advance only  : advance_to_use=2000, cash_collected=0
        //  Scenario 3 — Both          : advance_to_use=2000, cash_collected=1000
        //
        //  Advance logic:
        //    - Deducts from receipt_advances deposit rows (FIFO)
        //    - Writes usage rows so remaining advance balance is trackable
        //    - Cancel revert soft-deletes usage rows → balance is restored
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsDueCollectionResponse? data)> CollectDue(
    HmsDueCollectionRequest req, string tenantCode)
        {
            var validErr = ValidateDueRequest(req);
            if (validErr != null) return (validErr, null);

            // Resolve provision flags — same pattern as LIMS DueCollectionClass.Save
            bool isbillFlag = req.isbill ?? true;                          // due-collection always settles a bill
            bool isrefferalFlag = req.isrefferal ?? false;
            bool ismonthlyFlag = req.ismonthly ?? false;
            bool ispatientFlag = req.ispatient ?? !isrefferalFlag;          // patient by default unless marked referral

            // Only attach bank/cheque details when the payment mode actually is a cheque
            // (mirrors LIMS' isCheque detection — avoids writing stray bank_name on CASH/UPI rows)
            bool isCheque = req.collection_type.Equals("CHEQUE", StringComparison.OrdinalIgnoreCase)
                || req.cheque_date != null
                || !string.IsNullOrWhiteSpace(req.bank_name);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Lock bill row to prevent concurrent collection
                var bill = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    @"SELECT * FROM lab_request_master
               WHERE requestguid = @rg AND tenant_code = @t
               FOR UPDATE",
                    new { rg = req.requestguid, t = tenantCode }, tx);

                if (bill == null)
                    return ("Bill not found.", null);
                if (bill.isdeleted == true || bill.deleted == true)
                    return ("Cannot collect due on a cancelled bill.", null);

                double totalBill = bill.totalamount ?? 0;
                double previousPaid = bill.paidamount ?? 0;
                double currentDue = Math.Round(Math.Max(totalBill - previousPaid, 0), 2);

                if (currentDue <= 0.01)
                    return ("This bill has no outstanding due amount.", null);

                // Validate advance inside transaction (balance locked via advisory or row lock)
                double advanceBalance = await GetPatientAdvanceBalance(db, bill.custid, tenantCode, tx);
                double advanceToUse = Math.Round(req.advance_to_use, 2);
                double cashCollected = Math.Round(req.cash_collected, 2);

                if (advanceToUse > advanceBalance + 0.01)
                    return ($"Advance requested ({advanceToUse:F2}) exceeds " +
                            $"available advance ({advanceBalance:F2}).", null);
                if (advanceToUse > currentDue + 0.01)
                    return ($"Advance requested ({advanceToUse:F2}) exceeds " +
                            $"current due ({currentDue:F2}).", null);

                double dueAfterAdvance = Math.Round(Math.Max(currentDue - advanceToUse, 0), 2);

                if (cashCollected > dueAfterAdvance + 0.01)
                    return ($"Payment ({cashCollected:F2}) exceeds remaining due " +
                            $"after advance adjustment ({dueAfterAdvance:F2}).", null);

                double totalSettled = Math.Round(advanceToUse + cashCollected, 2);
                double dueAfter = Math.Round(Math.Max(currentDue - totalSettled, 0), 2);

                // Validate open counter shift
                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
               WHERE bhcode = @bh AND cntcode = @cnt
                 AND todate IS NULL AND tenant_code = @t
               LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found for this branch/counter. " +
                            "Please open a shift before collecting.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
               WHERE isreceiptno = true AND deleted = false AND tenant_code = @t
               LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt number sequence configuration not found in billno_master.", null);

                string receiptGuid = Guid.NewGuid().ToString();
                string? advReceiptGuid = null;
                HmsNumberResult? seqInfo = null;

                // ── A. CASH / CARD / UPI / CHEQUE / BANK receipt ──────────────
                if (cashCollected > 0.01)
                {
                    seqInfo = await GetNextReceiptSequence(
                        db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                    var cashReceipt = new HmsDueReceiptMaster
                    {
                        receiptguid = receiptGuid,
                        receiptdate = DateTime.UtcNow,
                        receiptsno = seqInfo.sno,
                        receiptsnoprint = seqInfo.snoprint,
                        receiptbarcode = seqInfo.barcode,
                        receiptcovertedbarcode = seqInfo.barcode,
                        cntcode = req.cntcode,
                        cnttid = shift.cnttid,
                        tmcode = req.tmcode ?? bill.tmcode,
                        pmcode = req.pmcode ?? bill.pmcode,
                        ctcode = req.ctcode ?? bill.ctcode,
                        payledgercode = req.payledgercode,
                        bankname = isCheque ? req.bank_name : null,
                        paymentreference = req.reference_no,
                        cardno = req.card_no,
                        chequedate = isCheque ? req.cheque_date : null,
                        carddate = req.card_date,
                        amountpaid = cashCollected,
                        amountadjusted = cashCollected,
                        amounttotal = cashCollected,
                        deleted = false,
                        isdeleted = false,
                        isbill = isbillFlag,
                        ispatient = ispatientFlag,
                        isrefund = req.isrefund ?? false,
                        isrefferal = isrefferalFlag,
                        ismonthly = ismonthlyFlag,
                        receipttype = "DUE",
                        custid = (int?)bill.custid,
                        opvisitid = bill.opvisitid,
                        enteredbhcode = req.enteredbhcode,
                        usercode = req.usercode,
                        computercode = req.computercode,
                        entereddate = DateTime.UtcNow,
                        ibsdate = DateTime.UtcNow,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(cashReceipt, tx);

                    var detail = new HmsDueReceiptDetail
                    {
                        receiptdetailsid = Guid.NewGuid().ToString(),
                        receiptguid = receiptGuid,
                        requestguid = req.requestguid,
                        receiptamount = cashCollected,
                        discount_amount = 0,
                        refund_amount = 0,
                        deleted = false,
                        usercode = req.usercode,
                        computercode = req.computercode,
                        entereddate = DateTime.UtcNow,
                        ibsdate = DateTime.UtcNow,
                        tenant_code = tenantCode,
                        custid = (int?)bill.custid,
                        opvisitid = bill.opvisitid,
                        enteredbhcode = req.enteredbhcode,
                        isbill = isbillFlag,
                        ispatient = ispatientFlag,
                        isrefund = req.isrefund ?? false
                    };
                    await db.InsertAsync(detail, tx);

                    await InsertBalanceCollectionRow(
                        db, tx,
                        bhcode: req.enteredbhcode,
                        collectionType: req.collection_type.ToUpper(),
                        receiptGuid: receiptGuid,
                        requestGuid: req.requestguid,
                        amount: cashCollected,
                        shift: shift,
                        req: req,
                        bill: bill,
                        tenantCode: tenantCode);
                }

                // ── B. ADVANCE ADJUSTMENT (FIFO) ─────────────────────────────
                if (advanceToUse > 0.01)
                {
                    var deposits = (await db.QueryAsync<dynamic>(
                        @"SELECT ra.receiptadvanceid,
                         ra.receiptguid,
                         ra.receiptamount,
                         COALESCE(
                             (SELECT SUM(u.receiptamount)
                                FROM receipt_advances u
                               WHERE u.receiptguid  = ra.receiptguid
                                 AND u.requestguid IS NOT NULL
                                 AND COALESCE(u.deleted, false) = false),
                         0) AS used_amount
                    FROM receipt_advances ra
                   WHERE ra.requestguid IS NULL
                     AND COALESCE(ra.deleted, false) = false
                     AND ra.receiptguid IN (
                         SELECT rm.receiptguid
                           FROM receipt_master rm
                          WHERE rm.custid      = @custid
                            AND rm.tenant_code = @t
                            AND rm.receipttype = 'ADVANCE'
                            AND COALESCE(rm.isdeleted, false) = false
                     )
                   ORDER BY ra.receiptadvanceid ASC",
                        new { custid = (int?)bill.custid, t = tenantCode }, tx)).ToList();

                    double remaining = advanceToUse;

                    foreach (var dep in deposits)
                    {
                        if (remaining <= 0.01) break;

                        double available = Math.Round((double)dep.receiptamount - (double)dep.used_amount, 2);
                        if (available <= 0.01) continue;

                        double debit = Math.Round(Math.Min(available, remaining), 2);

                        await db.ExecuteAsync(
                            @"INSERT INTO receipt_advances
                        (receiptadvanceid, receiptguid, requestguid, receiptamount,
                         deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                      VALUES
                        (@id, @rg, @billGuid, @amt,
                         false, @t, @uc, @cc, @dt, @dt)",
                            new
                            {
                                id = Guid.NewGuid().ToString(),
                                rg = (string)dep.receiptguid,
                                billGuid = req.requestguid,
                                amt = debit,
                                t = tenantCode,
                                uc = req.usercode,
                                cc = req.computercode,
                                dt = DateTime.UtcNow
                            }, tx);

                        await InsertBalanceCollectionRow(
                            db, tx,
                            bhcode: req.enteredbhcode,
                            collectionType: "ADVANCE",
                            receiptGuid: (string)dep.receiptguid,
                            requestGuid: req.requestguid,
                            amount: debit,
                            shift: shift,
                            req: req,
                            bill: bill,
                            tenantCode: tenantCode);

                        if (advReceiptGuid == null)
                            advReceiptGuid = (string)dep.receiptguid;

                        remaining = Math.Round(remaining - debit, 2);
                    }

                    if (remaining > 0.01)
                    {
                        tx.Rollback();
                        return ("Advance balance insufficient to complete adjustment. " +
                                "Please refresh and try again.", null);
                    }
                }

                // ── C. Update lab_request_master ──────────────────────────────
                await db.ExecuteAsync(
                    @"UPDATE lab_request_master
                 SET paidamount     = COALESCE(paidamount,     0) + @total,
                     paidviareceipt = COALESCE(paidviareceipt, 0) + @cash
               WHERE requestguid = @rg AND tenant_code = @t",
                    new
                    {
                        total = totalSettled,
                        cash = cashCollected,
                        rg = req.requestguid,
                        t = tenantCode
                    }, tx);

                // ── D. Mark tests settled when bill is fully paid ──────────────
                if (dueAfter <= 0.05)
                {
                    await db.ExecuteAsync(
                        @"UPDATE balancecollectionbytest
                     SET requeststatus = true
                   WHERE balancecollectionbyid IN (
                       SELECT balancecollectionbyid
                         FROM balancecollectionby
                        WHERE request_guid = @rg
                          AND tenant_code  = @t
                          AND COALESCE(deleted, false) = false
                   )",
                        new { rg = req.requestguid, t = tenantCode }, tx);
                }

                tx.Commit();

                return ("SUCCESS", new HmsDueCollectionResponse
                {
                    receipt_guid = cashCollected > 0.01 ? receiptGuid : null,
                    receipt_no = cashCollected > 0.01 ? seqInfo?.snoprint : null,
                    receipt_barcode = cashCollected > 0.01 ? seqInfo?.barcode : null,
                    advance_receipt_guid = advReceiptGuid,
                    requestguid = req.requestguid,
                    bill_no = bill.requestsnoprint,
                    patient_name = bill.name,
                    mobileno = bill.mobileno,
                    total_bill_amount = totalBill,
                    due_before = currentDue,
                    advance_used = advanceToUse,
                    cash_collected = cashCollected,
                    total_settled = totalSettled,
                    due_after = dueAfter,
                    is_fully_settled = dueAfter <= 0.05,
                    collection_type = req.collection_type.ToUpper(),
                    reference_no = req.reference_no,
                    collected_date = DateTime.UtcNow,
                    remarks = req.remarks
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CollectDue transaction failed for requestguid={rg}", req.requestguid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  2b. BULK COLLECT DUE
        //
        //  Collects payment for multiple bills in a single atomic transaction.
        //  One shared receipt_master row is created for the batch.
        //  Each bill gets its own receipt_details and balancecollectionby row.
        //
        //  Advance (FIFO) is applied per-bill in order — if advance runs out
        //  mid-batch, remaining bills use cash only.
        //
        //  POST /api/HmsDueCollection/collect/bulk
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsBulkDueCollectionResponse? data)> BulkCollectDue(
    HmsBulkDueCollectionRequest req, string tenantCode)
        {
            if (req.items == null || !req.items.Any())
                return ("No items provided for bulk collection.", null);

            if (req.enteredbhcode == null || req.cntcode == null)
                return ("Branch code (enteredbhcode) and counter code (cntcode) are required.", null);
            if (req.usercode == null)
                return ("usercode is required.", null);
            if (string.IsNullOrWhiteSpace(req.collection_type))
                return ("collection_type is required.", null);

            var refRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "CARD", "UPI", "CHEQUE", "BANK" };

            foreach (var item in req.items)
            {
                if (string.IsNullOrWhiteSpace(item.requestguid))
                    return ("Each item must include requestguid.", null);
                if (item.advance_to_use < 0)
                    return ($"Advance amount cannot be negative for bill {item.requestguid}.", null);
                if (item.cash_collected < 0)
                    return ($"Cash collected cannot be negative for bill {item.requestguid}.", null);

                string effType = (item.collection_type ?? req.collection_type).ToUpper();
                string? effRef = item.reference_no ?? req.reference_no;

                if (item.cash_collected > 0
                    && refRequired.Contains(effType)
                    && string.IsNullOrWhiteSpace(effRef))
                {
                    return ($"Transaction reference number is required for bill {item.requestguid} " +
                            $"with payment mode '{effType}'.", null);
                }
            }

            // Resolve batch-level default provision flags — same pattern as LIMS Save
            bool batchIsbillFlag = req.isbill ?? true;
            bool batchIsrefferalFlag = req.isrefferal ?? false;
            bool batchIsmonthlyFlag = req.ismonthly ?? false;
            bool batchIspatientFlag = req.ispatient ?? !batchIsrefferalFlag;

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
               WHERE bhcode = @bh AND cntcode = @cnt
                 AND todate IS NULL AND tenant_code = @t
               LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found. Please open a shift before collecting.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
               WHERE isreceiptno = true AND deleted = false AND tenant_code = @t
               LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt number sequence configuration not found in billno_master.", null);

                var itemResults = new List<HmsBulkDueItemResult>();
                double batchAdvanceUsed = 0;
                double batchCashCollected = 0;
                string? firstReceiptGuid = null;
                string? firstReceiptNo = null;
                string? firstReceiptBarcode = null;

                // 1. Group items by patient (custid)
                var itemsByPatient = new Dictionary<int, List<HmsBulkDueCollectionItem>>();
                foreach (var item in req.items)
                {
                    var billCustId = await db.QueryFirstOrDefaultAsync<int?>(
                        "SELECT custid FROM lab_request_master WHERE requestguid = @rg AND tenant_code = @t",
                        new { rg = item.requestguid, t = tenantCode }, tx) ?? 0;
                    if (!itemsByPatient.ContainsKey(billCustId))
                        itemsByPatient[billCustId] = new List<HmsBulkDueCollectionItem>();
                    itemsByPatient[billCustId].Add(item);
                }

                // 2. Create a separate receipt for EACH patient
                foreach (var kvp in itemsByPatient)
                {
                    int patientCustId = kvp.Key;
                    var patientItems = kvp.Value;
                    double patientCashTotal = patientItems.Sum(i => Math.Round(i.cash_collected, 2));

                    if (patientCashTotal <= 0 && !patientItems.Any(i => i.advance_to_use > 0))
                        continue;

                    // Generate distinct receipt number and GUID for this patient
                    var seqInfo = await GetNextReceiptSequence(
                        db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                    string patientReceiptGuid = Guid.NewGuid().ToString();

                    if (firstReceiptGuid == null)
                    {
                        firstReceiptGuid = patientReceiptGuid;
                        firstReceiptNo = seqInfo.snoprint;
                        firstReceiptBarcode = seqInfo.barcode;
                    }

                    var patientReceipt = new HmsDueReceiptMaster
                    {
                        receiptguid = patientReceiptGuid,
                        receiptdate = DateTime.UtcNow,
                        receiptsno = seqInfo.sno,
                        receiptsnoprint = seqInfo.snoprint,
                        receiptbarcode = seqInfo.barcode,
                        receiptcovertedbarcode = seqInfo.barcode,
                        cntcode = req.cntcode,
                        cnttid = shift.cnttid,
                        tmcode = req.tmcode,
                        pmcode = req.pmcode,
                        ctcode = req.ctcode,
                        payledgercode = req.payledgercode,
                        bankname = req.bank_name,
                        paymentreference = req.reference_no,
                        cardno = req.card_no,
                        chequedate = req.cheque_date,
                        carddate = req.card_date,
                        amountpaid = patientCashTotal,
                        amountadjusted = patientCashTotal,
                        amounttotal = patientCashTotal,
                        deleted = false,
                        isdeleted = false,
                        isbill = batchIsbillFlag,
                        ispatient = batchIspatientFlag,
                        isrefund = req.isrefund ?? false,
                        isrefferal = batchIsrefferalFlag,
                        ismonthly = batchIsmonthlyFlag,
                        receipttype = "DUE",
                        custid = patientCustId > 0 ? patientCustId : null,
                        enteredbhcode = req.enteredbhcode,
                        usercode = req.usercode,
                        computercode = req.computercode,
                        entereddate = DateTime.UtcNow,
                        ibsdate = DateTime.UtcNow,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(patientReceipt, tx);

                    foreach (var item in patientItems)
                    {
                        // Resolve effective per-item values (item override â†’ batch default)
                        string itemCollectionType = (item.collection_type ?? req.collection_type).ToUpper();
                        string? itemReferenceNo = item.reference_no ?? req.reference_no;
                        string? itemBankName = item.bank_name ?? req.bank_name;
                        string? itemCardNo = item.card_no ?? req.card_no;
                        DateTime? itemChequeDate = item.cheque_date ?? req.cheque_date;
                        string? itemRemarks = item.remarks ?? req.remarks;

                        // Per-item provision flags, falling back to batch defaults
                        bool itemIsbillFlag = item.isbill ?? batchIsbillFlag;
                        bool itemIsrefferalFlag = item.isrefferal ?? batchIsrefferalFlag;
                        bool itemIsmonthlyFlag = item.ismonthly ?? batchIsmonthlyFlag;
                        bool itemIspatientFlag = item.ispatient
                            ?? (item.isrefferal.HasValue ? !itemIsrefferalFlag : batchIspatientFlag);
                        bool itemIsrefundFlag = item.isrefund ?? (req.isrefund ?? false);

                        // Lock this bill
                        var bill = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                            @"SELECT * FROM lab_request_master
                       WHERE requestguid = @rg AND tenant_code = @t
                       FOR UPDATE",
                            new { rg = item.requestguid, t = tenantCode }, tx);

                        if (bill == null)
                        {
                            itemResults.Add(new HmsBulkDueItemResult
                            {
                                requestguid = item.requestguid,
                                status = "skipped",
                                message = "Bill not found.",
                                receipt_guid = patientReceiptGuid,
                                receipt_no = seqInfo.snoprint,
                                collection_type = itemCollectionType,
                                reference_no = itemReferenceNo,
                                bank_name = itemBankName,
                                card_no = itemCardNo,
                                cheque_date = itemChequeDate,
                                remarks = itemRemarks
                            });
                            continue;
                        }

                        if (bill.isdeleted == true || bill.deleted == true)
                        {
                            itemResults.Add(new HmsBulkDueItemResult
                            {
                                requestguid = item.requestguid,
                                bill_no = bill.requestsnoprint,
                                patient_name = bill.name,
                                status = "skipped",
                                message = "Bill is cancelled.",
                                receipt_guid = patientReceiptGuid,
                                receipt_no = seqInfo.snoprint,
                                collection_type = itemCollectionType,
                                reference_no = itemReferenceNo,
                                bank_name = itemBankName,
                                card_no = itemCardNo,
                                cheque_date = itemChequeDate,
                                remarks = itemRemarks
                            });
                            continue;
                        }

                        double totalBill = bill.totalamount ?? 0;
                        double previousPaid = bill.paidamount ?? 0;
                        double currentDue = Math.Round(Math.Max(totalBill - previousPaid, 0), 2);

                        if (currentDue <= 0.01)
                        {
                            itemResults.Add(new HmsBulkDueItemResult
                            {
                                requestguid = item.requestguid,
                                bill_no = bill.requestsnoprint,
                                patient_name = bill.name,
                                status = "skipped",
                                message = "No outstanding due.",
                                due_before = 0,
                                due_after = 0,
                                is_fully_settled = true,
                                receipt_guid = patientReceiptGuid,
                                receipt_no = seqInfo.snoprint,
                                collection_type = itemCollectionType,
                                reference_no = itemReferenceNo,
                                bank_name = itemBankName,
                                card_no = itemCardNo,
                                cheque_date = itemChequeDate,
                                remarks = itemRemarks
                            });
                            continue;
                        }

                        double itemAdvanceToUse = Math.Round(item.advance_to_use, 2);
                        double itemCashCollected = Math.Round(item.cash_collected, 2);

                        if (itemAdvanceToUse > 0.01)
                        {
                            double advBal = await GetPatientAdvanceBalance(db, bill.custid, tenantCode, tx);
                            if (itemAdvanceToUse > advBal + 0.01)
                                itemAdvanceToUse = Math.Round(advBal, 2);
                            if (itemAdvanceToUse > currentDue + 0.01)
                                itemAdvanceToUse = Math.Round(currentDue, 2);
                        }

                        double dueAfterAdvance = Math.Round(Math.Max(currentDue - itemAdvanceToUse, 0), 2);

                        if (itemCashCollected > dueAfterAdvance + 0.01)
                            itemCashCollected = Math.Round(dueAfterAdvance, 2);

                        double itemTotalSettled = Math.Round(itemAdvanceToUse + itemCashCollected, 2);
                        double itemDueAfter = Math.Round(Math.Max(currentDue - itemTotalSettled, 0), 2);

                        // receipt_details â€” link this bill to the patient's receipt, carry per-item attribution
                        await db.InsertAsync(new HmsDueReceiptDetail
                        {
                            receiptdetailsid = Guid.NewGuid().ToString(),
                            receiptguid = patientReceiptGuid,
                            requestguid = item.requestguid,
                            receiptamount = itemCashCollected,
                            discount_amount = 0,
                            refund_amount = 0,
                            deleted = false,
                            usercode = req.usercode,
                            computercode = req.computercode,
                            entereddate = DateTime.UtcNow,
                            ibsdate = DateTime.UtcNow,
                            tenant_code = tenantCode,
                            custid = (int?)bill.custid,
                            opvisitid = bill.opvisitid,
                            enteredbhcode = req.enteredbhcode,
                            isbill = itemIsbillFlag,
                            ispatient = itemIspatientFlag,
                            isrefund = itemIsrefundFlag
                        }, tx);

                        // balancecollectionby â€” cash portion (per-item payment mode/reference)
                        if (itemCashCollected > 0.01)
                        {
                            await db.ExecuteAsync(
                                @"INSERT INTO balancecollectionby (
                              balancecollectionbyid,
                              bhcode,
                              collecteddate, collectiontype, receiptguid, requestguid,
                              collected_date, collection_type, receipt_guid, request_guid,
                              collectedamount, deleted,
                              usercode, computercode,
                              entereddate, ibsdate,
                              tmcode, cntcode, cnttid, pmcode, ctcode,
                              tenant_code)
                          VALUES (
                              @id, @bh,
                              @dt, @ct, @rg, @req,
                              @dt, @ct, @rg, @req,
                              @amt, false,
                              @uc, @cc,
                              @dt, @dt,
                              @tm, @cnt, @cnttid, @pm, @cct,
                              @t)",
                                new
                                {
                                    id = Guid.NewGuid().ToString(),
                                    bh = req.enteredbhcode,
                                    dt = DateTime.UtcNow,
                                    ct = itemCollectionType,
                                    rg = patientReceiptGuid,
                                    req = item.requestguid,
                                    amt = itemCashCollected,
                                    uc = req.usercode,
                                    cc = req.computercode,
                                    tm = item.tmcode ?? req.tmcode ?? bill.tmcode,
                                    cnt = req.cntcode,
                                    cnttid = shift.cnttid,
                                    pm = item.pmcode ?? req.pmcode ?? bill.pmcode,
                                    cct = item.ctcode ?? req.ctcode ?? bill.ctcode,
                                    t = tenantCode
                                }, tx);
                        }

                        // Advance FIFO for this item
                        string? itemAdvReceiptGuid = null;
                        if (itemAdvanceToUse > 0.01)
                        {
                            var deposits = (await db.QueryAsync<dynamic>(
                                @"SELECT ra.receiptadvanceid,
                                 ra.receiptguid,
                                 ra.receiptamount,
                                 COALESCE(
                                     (SELECT SUM(u.receiptamount)
                                        FROM receipt_advances u
                                       WHERE u.receiptguid  = ra.receiptguid
                                         AND u.requestguid IS NOT NULL
                                         AND COALESCE(u.deleted, false) = false),
                                 0) AS used_amount
                            FROM receipt_advances ra
                           WHERE ra.requestguid IS NULL
                             AND COALESCE(ra.deleted, false) = false
                             AND ra.receiptguid IN (
                                 SELECT rm.receiptguid
                                   FROM receipt_master rm
                                  WHERE rm.custid      = @custid
                                    AND rm.tenant_code = @t
                                    AND rm.receipttype = 'ADVANCE'
                                    AND COALESCE(rm.isdeleted, false) = false
                             )
                           ORDER BY ra.receiptadvanceid ASC",
                                new { custid = (int?)bill.custid, t = tenantCode }, tx)).ToList();

                            double remaining = itemAdvanceToUse;

                            foreach (var dep in deposits)
                            {
                                if (remaining <= 0.01) break;

                                double avail = Math.Round((double)dep.receiptamount - (double)dep.used_amount, 2);
                                if (avail <= 0.01) continue;

                                double debit = Math.Round(Math.Min(avail, remaining), 2);

                                await db.ExecuteAsync(
                                    @"INSERT INTO receipt_advances
                                (receiptadvanceid, receiptguid, requestguid, receiptamount,
                                 deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                              VALUES
                                (@id, @rg, @billGuid, @amt,
                                 false, @t, @uc, @cc, @dt, @dt)",
                                    new
                                    {
                                        id = Guid.NewGuid().ToString(),
                                        rg = (string)dep.receiptguid,
                                        billGuid = item.requestguid,
                                        amt = debit,
                                        t = tenantCode,
                                        uc = req.usercode,
                                        cc = req.computercode,
                                        dt = DateTime.UtcNow
                                    }, tx);

                                await db.ExecuteAsync(
                                    @"INSERT INTO balancecollectionby (
                                  balancecollectionbyid,
                                  bhcode,
                                  collecteddate, collectiontype, receiptguid, requestguid,
                                  collected_date, collection_type, receipt_guid, request_guid,
                                  collectedamount, deleted,
                                  usercode, computercode,
                                  entereddate, ibsdate,
                                  tmcode, cntcode, cnttid, pmcode, ctcode,
                                  tenant_code)
                              VALUES (
                                  @id, @bh,
                                  @dt, 'ADVANCE', @rg, @req,
                                  @dt, 'ADVANCE', @rg, @req,
                                  @amt, false,
                                  @uc, @cc,
                                  @dt, @dt,
                                  @tm, @cnt, @cnttid, @pm, @cct,
                                  @t)",
                                    new
                                    {
                                        id = Guid.NewGuid().ToString(),
                                        bh = req.enteredbhcode,
                                        dt = DateTime.UtcNow,
                                        rg = (string)dep.receiptguid,
                                        req = item.requestguid,
                                        amt = debit,
                                        uc = req.usercode,
                                        cc = req.computercode,
                                        tm = item.tmcode ?? req.tmcode ?? bill.tmcode,
                                        cnt = req.cntcode,
                                        cnttid = shift.cnttid,
                                        pm = item.pmcode ?? req.pmcode ?? bill.pmcode,
                                        cct = item.ctcode ?? req.ctcode ?? bill.ctcode,
                                        t = tenantCode
                                    }, tx);

                                if (itemAdvReceiptGuid == null)
                                    itemAdvReceiptGuid = (string)dep.receiptguid;

                                remaining = Math.Round(remaining - debit, 2);
                            }

                            itemAdvanceToUse = Math.Round(itemAdvanceToUse - remaining, 2);
                            itemTotalSettled = Math.Round(itemAdvanceToUse + itemCashCollected, 2);
                            itemDueAfter = Math.Round(Math.Max(currentDue - itemTotalSettled, 0), 2);
                        }

                        // Update lab_request_master
                        await db.ExecuteAsync(
                            @"UPDATE lab_request_master
                         SET paidamount     = COALESCE(paidamount,     0) + @total,
                             paidviareceipt = COALESCE(paidviareceipt, 0) + @cash
                       WHERE requestguid = @rg AND tenant_code = @t",
                            new
                            {
                                total = itemTotalSettled,
                                cash = itemCashCollected,
                                rg = item.requestguid,
                                t = tenantCode
                            }, tx);

                        // Mark tests settled if fully paid
                        if (itemDueAfter <= 0.05)
                        {
                            await db.ExecuteAsync(
                                @"UPDATE balancecollectionbytest
                             SET requeststatus = true
                           WHERE balancecollectionbyid IN (
                               SELECT balancecollectionbyid
                                 FROM balancecollectionby
                                WHERE request_guid = @rg
                                  AND tenant_code  = @t
                                  AND COALESCE(deleted, false) = false
                           )",
                                new { rg = item.requestguid, t = tenantCode }, tx);
                        }

                        batchAdvanceUsed += itemAdvanceToUse;
                        batchCashCollected += itemCashCollected;

                        itemResults.Add(new HmsBulkDueItemResult
                        {
                            requestguid = item.requestguid,
                            bill_no = bill.requestsnoprint,
                            patient_name = bill.name,
                            advance_used = itemAdvanceToUse,
                            cash_collected = itemCashCollected,
                            total_settled = itemTotalSettled,
                            due_before = currentDue,
                            due_after = itemDueAfter,
                            is_fully_settled = itemDueAfter <= 0.05,
                            advance_receipt_guid = itemAdvReceiptGuid,
                            receipt_guid = patientReceiptGuid,
                            receipt_no = seqInfo.snoprint,
                            collection_type = itemCollectionType,
                            reference_no = itemReferenceNo,
                            bank_name = itemBankName,
                            card_no = itemCardNo,
                            cheque_date = itemChequeDate,
                            remarks = itemRemarks,
                            status = "collected",
                            message = "OK"
                        });
                    }
                }

                tx.Commit();

                return ("SUCCESS", new HmsBulkDueCollectionResponse
                {
                    batch_receipt_guid = firstReceiptGuid ?? "",
                    receipt_no = firstReceiptNo,
                    receipt_barcode = firstReceiptBarcode,
                    collection_type = req.collection_type.ToUpper(),
                    total_advance_used = Math.Round(batchAdvanceUsed, 2),
                    total_cash_collected = Math.Round(batchCashCollected, 2),
                    total_settled = Math.Round(batchAdvanceUsed + batchCashCollected, 2),
                    items_processed = itemResults.Count(r => r.status == "collected"),
                    items_skipped = itemResults.Count(r => r.status == "skipped"),
                    collected_date = DateTime.UtcNow,
                    items = itemResults
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "BulkCollectDue failed");
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  3. DEPOSIT ADVANCE
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsAdvanceReceiptResponse? data)> DepositAdvance(
            HmsAdvanceDepositRequest req, string tenantCode)
        {
            if (req.amount <= 0)
                return ("Advance deposit amount must be greater than zero.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE bhcode = @bh AND cntcode = @cnt
                         AND todate IS NULL AND tenant_code = @t LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found. Please open a shift first.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
                       WHERE isreceiptno = true AND deleted = false AND tenant_code = @t LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt number sequence configuration not found.", null);

                var seqInfo = await GetNextReceiptSequence(
                    db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                string receiptGuid = Guid.NewGuid().ToString();

                var receipt = new HmsDueReceiptMaster
                {
                    receiptguid = receiptGuid,
                    receiptdate = DateTime.UtcNow,
                    receiptsno = seqInfo.sno,
                    receiptsnoprint = seqInfo.snoprint,
                    receiptbarcode = seqInfo.barcode,
                    receiptcovertedbarcode = seqInfo.barcode,
                    cntcode = req.cntcode,
                    cnttid = shift.cnttid,
                    tmcode = req.tmcode,
                    pmcode = req.pmcode,
                    ctcode = req.ctcode,
                    bankname = req.bank_name,
                    paymentreference = req.reference_no,
                    cardno = req.card_no,
                    chequedate = req.cheque_date,
                    amountpaid = req.amount,
                    amountadjusted = req.amount,
                    amounttotal = req.amount,
                    deleted = false,
                    isdeleted = false,
                    isbill = false,
                    ispatient = true,
                    isrefund = false,
                    receipttype = "ADVANCE",
                    custid = (int)req.custid,
                    opvisitid = req.opvisitid,
                    enteredbhcode = req.enteredbhcode,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(receipt, tx);

                // Deposit row — requestguid NULL = unallocated credit
                await db.ExecuteAsync(
                    @"INSERT INTO receipt_advances
                        (receiptadvanceid, receiptguid, requestguid, receiptamount,
                         deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                      VALUES
                        (@id, @rg, NULL, @amt, false, @t, @uc, @cc, @dt, @dt)",
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        rg = receiptGuid,
                        amt = req.amount,
                        t = tenantCode,
                        uc = req.usercode,
                        cc = req.computercode,
                        dt = DateTime.UtcNow
                    }, tx);

                // balancecollectionby — advance deposit shows in daily cash collection
                await db.ExecuteAsync(
                    @"INSERT INTO balancecollectionby (
                          balancecollectionbyid,
                          bhcode, collecteddate, collectiontype, receiptguid, requestguid,
                          collected_date, collection_type, receipt_guid, request_guid,
                          collectedamount, deleted, usercode, computercode,
                          entereddate, ibsdate, tmcode, cntcode, cnttid, pmcode, ctcode,
                          tenant_code)
                      VALUES (
                          @id,
                          @bh, @dt, @ct, @rg, NULL,
                          @dt, @ct, @rg, NULL,
                          @amt, false, @uc, @cc,
                          @dt, @dt, @tm, @cnt, @cnttid, @pm, @cct,
                          @t)",
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        bh = req.enteredbhcode,
                        dt = DateTime.UtcNow,
                        ct = req.collection_type.ToUpper(),
                        rg = receiptGuid,
                        amt = req.amount,
                        uc = req.usercode,
                        cc = req.computercode,
                        tm = req.tmcode,
                        cnt = req.cntcode,
                        cnttid = shift.cnttid,
                        pm = req.pmcode,
                        cct = req.ctcode,
                        t = tenantCode
                    }, tx);

                tx.Commit();

                return ("SUCCESS", new HmsAdvanceReceiptResponse
                {
                    receipt_guid = receiptGuid,
                    receipt_no = seqInfo.snoprint,
                    receipt_barcode = seqInfo.barcode,
                    receipt_type = "ADVANCE",
                    custid = req.custid,
                    patient_name = req.patient_name,
                    amount = req.amount,
                    collection_type = req.collection_type.ToUpper(),
                    receipt_date = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "DepositAdvance failed for custid={c}", req.custid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  4. REFUND ADVANCE
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsAdvanceReceiptResponse? data)> RefundAdvance(
            HmsAdvanceRefundRequest req, string tenantCode)
        {
            if (req.refund_amount <= 0)
                return ("Refund amount must be greater than zero.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                double available = await GetPatientAdvanceBalance(db, req.custid, tenantCode, tx);

                if (req.refund_amount > available + 0.01)
                    return ($"Refund amount ({req.refund_amount:F2}) exceeds " +
                            $"available advance balance ({available:F2}).", null);

                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE bhcode = @bh AND cntcode = @cnt
                         AND todate IS NULL AND tenant_code = @t LIMIT 1",
                    new { bh = req.enteredbhcode, cnt = req.cntcode, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
                       WHERE isreceiptno = true AND deleted = false AND tenant_code = @t LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt sequence configuration not found.", null);

                var seqInfo = await GetNextReceiptSequence(
                    db, tx, receiptCfg, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                string receiptGuid = Guid.NewGuid().ToString();

                var refundReceipt = new HmsDueReceiptMaster
                {
                    receiptguid = receiptGuid,
                    receiptdate = DateTime.UtcNow,
                    receiptsno = seqInfo.sno,
                    receiptsnoprint = seqInfo.snoprint,
                    receiptbarcode = seqInfo.barcode,
                    receiptcovertedbarcode = seqInfo.barcode,
                    cntcode = req.cntcode,
                    cnttid = shift.cnttid,
                    amountpaid = req.refund_amount,
                    amountadjusted = req.refund_amount,
                    amounttotal = req.refund_amount,
                    deleted = false,
                    isdeleted = false,
                    isbill = false,
                    ispatient = true,
                    isrefund = true,
                    receipttype = "ADVANCE_REFUND",
                    custid = (int)req.custid,
                    opvisitid = req.opvisitid,
                    enteredbhcode = req.enteredbhcode,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(refundReceipt, tx);

                var deposits = (await db.QueryAsync<dynamic>(
                    @"SELECT ra.receiptadvanceid,
                             ra.receiptguid,
                             ra.receiptamount,
                             COALESCE(
                                 (SELECT SUM(u.receiptamount)
                                    FROM receipt_advances u
                                   WHERE u.receiptguid  = ra.receiptguid
                                     AND u.requestguid IS NOT NULL
                                     AND COALESCE(u.deleted, false) = false),
                             0) AS used_amount
                        FROM receipt_advances ra
                       WHERE ra.requestguid IS NULL
                         AND COALESCE(ra.deleted, false) = false
                         AND ra.receiptguid IN (
                             SELECT rm.receiptguid FROM receipt_master rm
                              WHERE rm.custid      = @custid
                                AND rm.tenant_code = @t
                                AND rm.receipttype = 'ADVANCE'
                                AND COALESCE(rm.isdeleted, false) = false
                         )
                       ORDER BY ra.receiptadvanceid ASC",
                    new { custid = (int)req.custid, t = tenantCode }, tx)).ToList();

                double remaining = req.refund_amount;

                foreach (var dep in deposits)
                {
                    if (remaining <= 0.01) break;

                    double avail = Math.Round((double)dep.receiptamount - (double)dep.used_amount, 2);
                    if (avail <= 0.01) continue;

                    double deduct = Math.Round(Math.Min(avail, remaining), 2);

                    await db.ExecuteAsync(
                        @"INSERT INTO receipt_advances
                            (receiptadvanceid, receiptguid, requestguid, receiptamount,
                             deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                          VALUES
                            (@id, @rg, @refundGuid, @amt,
                             false, @t, @uc, @cc, @dt, @dt)",
                        new
                        {
                            id = Guid.NewGuid().ToString(),
                            rg = (string)dep.receiptguid,
                            refundGuid = receiptGuid,
                            amt = deduct,
                            t = tenantCode,
                            uc = req.usercode,
                            cc = req.computercode,
                            dt = DateTime.UtcNow
                        }, tx);

                    remaining = Math.Round(remaining - deduct, 2);
                }

                tx.Commit();

                return ("SUCCESS", new HmsAdvanceReceiptResponse
                {
                    receipt_guid = receiptGuid,
                    receipt_no = seqInfo.snoprint,
                    receipt_barcode = seqInfo.barcode,
                    receipt_type = "ADVANCE_REFUND",
                    custid = req.custid,
                    amount = req.refund_amount,
                    receipt_date = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "RefundAdvance failed for custid={c}", req.custid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  5. CANCEL DUE COLLECTION RECEIPT
        //
        //  Example: Bill = 1200, paid 300, due = 900
        //    Cancel receipt → soft-delete receipt rows
        //                   → revert paidamount by 300
        //                   → bill due goes back to 1200
        //    If advance was used in that collection:
        //                   → soft-delete usage rows in receipt_advances
        //                   → advance balance is restored automatically
        // 
        //  The amount to revert is read from balancecollectionby rows tied to
        //  this receipt (cash + advance rows combined) so the revert is always
        //  exact regardless of how the payment was split.
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<string> CancelDueCollection(
    string receiptGuid, int? usercode, string? reason, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var receipt = await db.QueryFirstOrDefaultAsync<HmsDueReceiptMaster>(
                    @"SELECT * FROM receipt_master
               WHERE receiptguid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                if (receipt == null) return "Receipt not found.";
                if (receipt.isdeleted == true) return "Receipt is already cancelled.";
                if (receipt.receipttype != "DUE")
                    return "Only DUE receipts can be cancelled through this endpoint.";

                // ── Get ALL bill linkages under this receipt (bulk = multiple rows) ──
                var details = (await db.QueryAsync<HmsDueReceiptDetail>(
                    @"SELECT * FROM receipt_details
               WHERE receiptguid = @rg AND tenant_code = @t
                 AND COALESCE(deleted, false) = false",
                    new { rg = receiptGuid, t = tenantCode }, tx)).ToList();

                // ── Process each bill individually ────────────────────────────────────
                foreach (var detail in details)
                {
                    string? requestGuid = detail.requestguid;
                    if (requestGuid == null) continue;

                    // Step 1: Restore advance usage rows for THIS bill only
                    var advanceRows = await db.QueryAsync<dynamic>(
                        @"SELECT receipt_guid, collectedamount
                    FROM balancecollectionby
                   WHERE request_guid    = @rg
                     AND tenant_code     = @t
                     AND collection_type = 'ADVANCE'
                     AND COALESCE(deleted, false) = false",
                        new { rg = requestGuid, t = tenantCode }, tx);

                    foreach (var adv in advanceRows)
                    {
                        await db.ExecuteAsync(
                            @"UPDATE receipt_advances
                         SET deleted = true
                       WHERE receiptguid = @advRg
                         AND requestguid = @billRg
                         AND COALESCE(deleted, false) = false",
                            new { advRg = (string)adv.receipt_guid, billRg = requestGuid }, tx);
                    }

                    // Step 2: Get cash amount for THIS bill only (scoped to request_guid)
                    // ✅ FIX: use rd.receiptamount directly — this is per-bill share
                    double cashAmount = detail.receiptamount ?? 0;

                    // Step 3: Get advance amount for THIS bill only
                    double advanceAmount = await db.ExecuteScalarAsync<double>(
                        @"SELECT COALESCE(SUM(collectedamount), 0)
                    FROM balancecollectionby
                   WHERE request_guid    = @rg
                     AND tenant_code     = @t
                     AND collection_type = 'ADVANCE'",
                        new { rg = requestGuid, t = tenantCode }, tx);

                    double totalRevert = Math.Round(cashAmount + advanceAmount, 2);

                    // Step 4: Revert lab_request_master for THIS bill only
                    await db.ExecuteAsync(
                        @"UPDATE lab_request_master
                     SET paidamount     = GREATEST(COALESCE(paidamount,     0) - @total, 0),
                         paidviareceipt = GREATEST(COALESCE(paidviareceipt, 0) - @cash,  0)
                   WHERE requestguid = @rg AND tenant_code = @t",
                        new { total = totalRevert, cash = cashAmount, rg = requestGuid, t = tenantCode }, tx);

                    // Step 5: Un-settle balancecollectionbytest for THIS bill only
                    await db.ExecuteAsync(
                        @"UPDATE balancecollectionbytest
                     SET requeststatus = false
                   WHERE balancecollectionbyid IN (
                       SELECT balancecollectionbyid
                         FROM balancecollectionby
                        WHERE request_guid = @rg AND tenant_code = @t
                   )",
                        new { rg = requestGuid, t = tenantCode }, tx);
                }

                // ── Soft-delete receipt_master, receipt_details, balancecollectionby ──
                await db.ExecuteAsync(
                    @"UPDATE receipt_master
                 SET isdeleted = true, deleted = true
               WHERE receiptguid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                await db.ExecuteAsync(
                    @"UPDATE receipt_details
                 SET deleted = true
               WHERE receiptguid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                await db.ExecuteAsync(
                    @"UPDATE balancecollectionby
                 SET deleted = true
               WHERE receipt_guid = @rg AND tenant_code = @t",
                    new { rg = receiptGuid, t = tenantCode }, tx);

                tx.Commit();
                return "SUCCESS";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CancelDueCollection failed for receiptGuid={rg}", receiptGuid);
                return $"Cancellation error: {ex.Message}";
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  6. PAGINATED DUE COLLECTION LIST  — FIXED (PERFORMANCE)
        //
        //  Root cause of 15-second query:
        //    The old version ran COUNT(*) and the enrichment query (WITH adv_agg +
        //    col_type CTEs) over the ENTIRE tenant table on every page load.
        //    balancecollectionby has no direct receipt-level index path when
        //    scanning all rows — it fan-scanned millions of rows per request.
        //
        //  Fix: mirrors GetDailyCollectionReport's two-step pattern exactly:
        //
        //    STEP 1 — COUNT + paged ID fetch.
        //      Light joins only (receipt_master → receipt_details → lab_request_master).
        //      No balancecollectionby, no doctor_master, no branch_master.
        //      This is index-driven and returns in < 200 ms even on large tenants.
        //
        //    STEP 2 — Full enrichment scoped to this page's IDs only.
        //      balancecollectionby aggregation uses ANY(@rguids) / ANY(@bguids) —
        //      the planner does an index scan on exactly those rows, not a seq scan
        //      of the full table.
        //
        //  Filter support added (optional — pass null/default to get all DUE receipts):
        //    • bhcode, cntcode, custid
        //    • fromdate, todate
        //    • search (patient name / mobile / bill no / receipt no)
        //
        //  All other logic (bulk receipt handling, LATERAL doctor/branch joins,
        //  col_type fallback) is unchanged from the original.
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsDueCollectionSummary> data, int totalCount)> GetDueCollectionList(
            HmsDueCollectionFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            // ── Base condition — DUE receipts only, not deleted ───────────────────────
            string where = @"WHERE r.tenant_code = @t
       AND COALESCE(r.isdeleted, false) = false
       AND r.receipttype = 'DUE' ";

            // ── Optional filters ──────────────────────────────────────────────────────
            if (filter.bhcode.HasValue) { where += " AND r.enteredbhcode = @bh "; p.Add("bh", filter.bhcode); }
            if (filter.cntcode.HasValue) { where += " AND r.cntcode = @cnt "; p.Add("cnt", filter.cntcode); }
            if (filter.custid.HasValue) { where += " AND r.custid = @custid "; p.Add("custid", (int)filter.custid.Value); }
            if (filter.fromdate.HasValue) { where += " AND r.receiptdate >= @from "; p.Add("from", filter.fromdate.Value.Date); }
            if (filter.todate.HasValue) { where += " AND r.receiptdate <= @to "; p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1)); }

            if (!string.IsNullOrEmpty(filter.search))
            {
                where += @" AND (lrm.name          ILIKE @s
                      OR lrm.mobileno      ILIKE @s
                      OR lrm.requestsnoprint ILIKE @s
                      OR r.receiptsnoprint  ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }

            // ── LIGHT joins — used for COUNT and the paged-ID fetch ──────────────────
            // No balancecollectionby, no doctor_master, no branch_master here.
            // Keeps both queries index-driven on receipt_master + receipt_details.
            string lightJoins = @"
FROM receipt_master r
INNER JOIN receipt_details rd
       ON rd.receiptguid = r.receiptguid
      AND rd.tenant_code = r.tenant_code
      AND COALESCE(rd.deleted, false) = false
LEFT JOIN lab_request_master lrm
       ON lrm.requestguid = rd.requestguid
      AND lrm.tenant_code = rd.tenant_code";

            // ── STEP 1a: COUNT — no heavy joins, no aggregation ───────────────────────
            int total = await db.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) {lightJoins} {where}", p);

            if (total == 0)
                return (new List<HmsDueCollectionSummary>(), 0);

            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            // ── STEP 1b: cheap paged ID fetch — returns ONLY this page's guids ───────
            var pagedIds = (await db.QueryAsync<(string receiptguid, string requestguid, DateTime? receiptdate)>(
                $@"SELECT r.receiptguid, rd.requestguid, r.receiptdate
           {lightJoins} {where}
           ORDER BY r.receiptdate DESC, lrm.name ASC
           LIMIT @limit OFFSET @offset", p)).ToList();

            if (!pagedIds.Any())
                return (new List<HmsDueCollectionSummary>(), total);

            // Narrow all subsequent queries to ONLY this page's rows
            var receiptGuids = pagedIds.Select(x => x.receiptguid).Distinct().ToArray();
            var requestGuids = pagedIds.Select(x => x.requestguid).Where(x => x != null).Distinct().ToArray();

            p.Add("rguids", receiptGuids);
            p.Add("bguids", requestGuids);

            // ── STEP 2: full enrichment — balancecollectionby scoped to page IDs ─────
            // ANY(@rguids) / ANY(@bguids) lets the planner do index seeks on exactly
            // those rows instead of a sequential scan of the full bcb table.
            var rows = (await db.QueryAsync<HmsDueCollectionSummary>($@"
    WITH adv_agg AS (
        SELECT bcb.request_guid,
               SUM(bcb.collectedamount) AS advance_used
          FROM balancecollectionby bcb
         WHERE bcb.tenant_code     = @t
           AND bcb.collection_type = 'ADVANCE'
           AND bcb.request_guid    = ANY(@bguids)          -- ← scoped to this page
           AND COALESCE(bcb.deleted, false) = false
         GROUP BY bcb.request_guid
    ),
    col_type AS (
        SELECT DISTINCT ON (bcb.receipt_guid, bcb.request_guid)
               bcb.receipt_guid,
               bcb.request_guid,
               bcb.collection_type
          FROM balancecollectionby bcb
         WHERE bcb.tenant_code  = @t
           AND bcb.receipt_guid = ANY(@rguids)             -- ← scoped to this page
           AND COALESCE(bcb.deleted, false) = false
         ORDER BY bcb.receipt_guid, bcb.request_guid, bcb.collection_type
    )
    SELECT
        r.receiptguid                                               AS receipt_guid,
        r.receiptsnoprint                                           AS receipt_no,
        COALESCE(crm.custcode, '')                                  AS patient_id,
        rd.requestguid                                              AS bill_guid,
        lrm.name                                                    AS patient_name,
        lrm.mobileno,
        lrm.requestsnoprint                                         AS bill_no,
        dm.name                                                     AS doctor_name,
        COALESCE(rd.receiptamount, 0)                               AS amount_paid,
        COALESCE(adv.advance_used, 0)                               AS advance_used,
        COALESCE(
            ct.collection_type,
            (SELECT bcb3.collection_type
               FROM balancecollectionby bcb3
              WHERE bcb3.receipt_guid = r.receiptguid
                AND bcb3.tenant_code  = @t
                AND COALESCE(bcb3.deleted, false) = false
              LIMIT 1)
        )                                                           AS collection_type,
        r.receipttype                                               AS receipt_type,
        r.receiptdate                                               AS receipt_date,
        COALESCE(lrm.totalamount, 0)                                AS total_bill_amount,
        GREATEST(COALESCE(lrm.totalamount, 0)
               - COALESCE(lrm.paidamount,  0), 0)                  AS current_due,
        CASE WHEN (COALESCE(lrm.totalamount, 0)
                 - COALESCE(lrm.paidamount,  0)) <= 0.05
             THEN true ELSE false END                               AS is_fully_settled,
        bm.name                                                     AS branch_name
    FROM receipt_master r
    INNER JOIN receipt_details rd
           ON rd.receiptguid = r.receiptguid
          AND rd.tenant_code = r.tenant_code
          AND COALESCE(rd.deleted, false) = false
    LEFT JOIN lab_request_master lrm
           ON lrm.requestguid = rd.requestguid
          AND lrm.tenant_code = rd.tenant_code
    LEFT JOIN customerdb.customer_registration_master crm
           ON crm.custid = lrm.custid
          AND (crm.tenant_code = r.tenant_code OR crm.tenant_code IS NULL)
    LEFT JOIN LATERAL (
        SELECT name FROM doctor_master
         WHERE dcode = lrm.dcode AND tenant_code = lrm.tenant_code
         LIMIT 1
    ) dm ON true
    LEFT JOIN LATERAL (
        SELECT name FROM mastertenant.branch_master
         WHERE bh_code = r.enteredbhcode AND tenant_code = r.tenant_code
         LIMIT 1
    ) bm ON true
    LEFT JOIN adv_agg adv ON adv.request_guid = rd.requestguid
    LEFT JOIN col_type ct  ON ct.receipt_guid  = r.receiptguid
                           AND ct.request_guid = rd.requestguid
    WHERE r.receiptguid = ANY(@rguids)                              -- ← scoped to this page
    ORDER BY r.receiptdate DESC, lrm.name ASC", p)).ToList();

            return (rows, total);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  7. PATIENT ADVANCE SUMMARY
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<HmsPatientAdvanceSummary> GetPatientAdvanceSummary(
    decimal custid, string tenantCode)
        {
            using var db = GetConnection();

            var patientName = await db.ExecuteScalarAsync<string>(
                @"SELECT name FROM lab_request_master
           WHERE custid = @c AND tenant_code = @t
             AND COALESCE(isdeleted, false) = false
           ORDER BY requestdatetime DESC LIMIT 1",
                new { c = (int)custid, t = tenantCode });

            // Classify each receipt_advances row correctly:
            //   requestguid IS NULL                              -> DEPOSIT
            //   requestguid points to a receipt_master row with
            //     receipttype = 'ADVANCE_REFUND'                 -> REFUND
            //   requestguid points to anything else (a bill guid) -> USED
            var rows = (await db.QueryAsync<HmsAdvanceLedgerRow>(
                @"SELECT ra.receiptadvanceid,
                 ra.receiptguid,
                 ra.requestguid,
                 ra.receiptamount,
                 rm.receiptdate,
                 rm.receiptsnoprint,
                 lrm.requestsnoprint AS bill_no,
                 CASE
                     WHEN ra.requestguid IS NULL THEN 'DEPOSIT'
                     WHEN rref.receipttype = 'ADVANCE_REFUND' THEN 'REFUND'
                     ELSE 'USED'
                 END AS transaction_type
            FROM receipt_advances ra
           INNER JOIN receipt_master rm
                   ON ra.receiptguid  = rm.receiptguid
                  AND rm.tenant_code  = @t
            LEFT JOIN receipt_master rref
                   ON rref.receiptguid  = ra.requestguid
                  AND rref.tenant_code  = @t
                  AND rref.receipttype  = 'ADVANCE_REFUND'
            LEFT JOIN lab_request_master lrm
                   ON lrm.requestguid   = ra.requestguid
                  AND lrm.tenant_code   = @t
           WHERE rm.custid      = @c
             AND rm.tenant_code = @t
             AND rm.receipttype = 'ADVANCE'
             AND COALESCE(rm.isdeleted, false) = false
             AND COALESCE(ra.deleted,   false) = false
           ORDER BY ra.receiptadvanceid ASC",
                new { c = (int)custid, t = tenantCode })).ToList();

            // No more manual transaction_type assignment here — SQL already set it.

            double totalDeposited = rows.Where(r => r.transaction_type == "DEPOSIT").Sum(r => r.receiptamount);
            double totalUsed = rows.Where(r => r.transaction_type == "USED").Sum(r => r.receiptamount);
            double totalRefunded = rows.Where(r => r.transaction_type == "REFUND").Sum(r => r.receiptamount);

            // available = deposits − usage − refunds, computed purely from the ledger
            // (no separate receipt_master ADVANCE_REFUND aggregate, so nothing is double-counted)
            double available = Math.Round(Math.Max(totalDeposited - totalUsed - totalRefunded, 0), 2);

            return new HmsPatientAdvanceSummary
            {
                custid = custid,
                patient_name = patientName,
                total_advance_deposited = totalDeposited,
                total_advance_used = totalUsed,
                total_advance_refunded = totalRefunded,
                available_balance = available,
                ledger = rows
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  8. GET ALL DUE BILLS
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsAllDueBillRow> data, int totalCount, HmsAllDueSummary summary)>
    GetAllDueBills(HmsAllDueFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @"WHERE lrm.tenant_code = @t
                   AND COALESCE(lrm.isdeleted, false) = false
                   AND COALESCE(lrm.deleted,   false) = false
                   AND lrm.bill_category = 'HMS'
                   AND (COALESCE(lrm.totalamount, 0)
                      - COALESCE(lrm.paidamount,  0)) > 0.05 ";

            if (filter.bhcode.HasValue) { where += " AND lrm.enteredbhcode = @bh "; p.Add("bh", filter.bhcode); }
            if (filter.cntcode.HasValue) { where += " AND lrm.cntcode = @cnt "; p.Add("cnt", filter.cntcode); }
            if (filter.custid.HasValue) { where += " AND lrm.custid = @custid "; p.Add("custid", (int)filter.custid.Value); }
            if (filter.dcode.HasValue) { where += " AND lrm.dcode = @dcode "; p.Add("dcode", filter.dcode); }
            if (filter.fromdate.HasValue) { where += " AND lrm.requestdatetime >= @from "; p.Add("from", filter.fromdate.Value.Date); }
            if (filter.todate.HasValue) { where += " AND lrm.requestdatetime <= @to "; p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1)); }
            if (filter.min_due.HasValue) { where += " AND (COALESCE(lrm.totalamount,0) - COALESCE(lrm.paidamount, 0)) >= @mindue "; p.Add("mindue", filter.min_due.Value); }
            if (!string.IsNullOrEmpty(filter.search))
            {
                where += @" AND (lrm.name ILIKE @s OR lrm.mobileno ILIKE @s
              OR lrm.requestsnoprint ILIKE @s OR CAST(lrm.custid AS TEXT) ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }

            // FIXED: LATERAL JOIN guarantees exactly 1 row per bill regardless of
            // how many doctor_master rows exist for the same dcode + tenant_code
            string joins = @"
        FROM lab_request_master lrm
        LEFT JOIN LATERAL (
            SELECT name FROM doctor_master
            WHERE dcode = lrm.dcode AND tenant_code = lrm.tenant_code
            LIMIT 1
        ) dm ON true
        LEFT JOIN LATERAL (
            SELECT name FROM mastertenant.branch_master
            WHERE bh_code = lrm.enteredbhcode AND tenant_code = lrm.tenant_code
            LIMIT 1
        ) bm ON true";

            // FIXED: COUNT(DISTINCT) prevents inflated totals when joins fan out
            var agg = await db.QueryFirstAsync<dynamic>(
                $@"SELECT COUNT(DISTINCT lrm.requestguid) AS total_count,
               COALESCE(SUM(DISTINCT lrm.totalamount), 0) AS total_billed,
               COALESCE(SUM(DISTINCT lrm.paidamount),  0) AS total_paid,
               COALESCE(SUM(DISTINCT lrm.totalamount - COALESCE(lrm.paidamount, 0)), 0) AS total_due
           {joins} {where}", p);

            int totalCount = (int)agg.total_count;

            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            var rows = (await db.QueryAsync<HmsAllDueBillRow>(
                $@"SELECT
               lrm.requestguid,
               lrm.requestsnoprint AS bill_no,
               lrm.requestdatetime AS bill_date,
               lrm.bill_category   AS bill_type,
               NULLIF(lrm.custid, 0) AS custid,
               lrm.name            AS patient_name,
               lrm.mobileno,
               lrm.opvisitid,
               lrm.dcode,
               dm.name             AS doctor_name,
               lrm.enteredbhcode,
               bm.name             AS branch_name,
               COALESCE(lrm.totalamount, 0)                        AS total_bill_amount,
               COALESCE(lrm.paidamount,  0)                        AS paid_amount,
               COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0) AS due_amount,
               COALESCE((
                   SELECT
                       SUM(CASE WHEN ra.requestguid IS NULL THEN ra.receiptamount ELSE 0 END)
                     - SUM(CASE WHEN ra.requestguid IS NOT NULL THEN ra.receiptamount ELSE 0 END)
                   FROM receipt_advances ra
                   INNER JOIN receipt_master rm ON ra.receiptguid = rm.receiptguid
                   WHERE rm.custid = lrm.custid AND rm.tenant_code = lrm.tenant_code
                     AND rm.receipttype = 'ADVANCE'
                     AND COALESCE(rm.isdeleted, false) = false
                     AND COALESCE(ra.deleted, false) = false
               ), 0) AS advance_available,
               (SELECT MAX(rm2.receiptdate)
                  FROM receipt_master rm2
                 INNER JOIN receipt_details rd2 ON rd2.receiptguid = rm2.receiptguid
                  WHERE rd2.requestguid = lrm.requestguid AND rm2.tenant_code = lrm.tenant_code
                    AND COALESCE(rm2.isdeleted, false) = false
               ) AS last_paid_date
           {joins} {where}
           ORDER BY lrm.requestdatetime DESC
           LIMIT @limit OFFSET @offset", p)).ToList();

            return (rows, totalCount, new HmsAllDueSummary
            {
                total_bills = totalCount,
                total_billed = Math.Round((double)agg.total_billed, 2),
                total_paid = Math.Round((double)agg.total_paid, 2),
                total_due = Math.Round((double)agg.total_due, 2)
            });
        }


        // ═══════════════════════════════════════════════════════════════════════
        //  9. PAID HISTORY  (FIXED — matches GetDueCollectionList exactly)
        //
        //  Fix summary vs old version:
        //    1. COUNT(*) counts (receipt × bill) pairs, not COUNT(DISTINCT receiptguid).
        //       COUNT(DISTINCT) under-counts when a bulk receipt has multiple bills
        //       and the pagination offset is based on row count, not receipt count.
        //
        //    2. total_due_collected aggregate uses SUM(rd.receiptamount) — each bill's
        //       own share — NOT SUM(rm.amountpaid) which is the batch total.
        //       For a bulk receipt paying 3 bills at 1000 each:
        //         OLD: 3 rows × 3000 = 9000   ← WRONG (tripled)
        //         NEW: 3 rows × 1000 = 3000   ← CORRECT
        //
        //    3. collection_type sub-query checks receipt_guid + request_guid first
        //       (per-item mode for bulk), then falls back to receipt_guid only.
        //       Matches GetDueCollectionList exactly.
        //
        //    4. JOIN structure unchanged — receipt_details stays as a NORMAL INNER JOIN
        //       (not LATERAL / LIMIT 1) so bulk receipts with N bills still produce N rows.
        //
        //  POST /api/HmsDueCollection/paid-history
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsPaidHistoryRow> data, int totalCount, HmsPaidHistorySummary summary)>
            GetPaidHistory(HmsPaidHistoryFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @"WHERE rm.tenant_code = @t AND COALESCE(rm.isdeleted, false) = false ";

            if (filter.bhcode.HasValue) { where += " AND rm.enteredbhcode = @bh "; p.Add("bh", filter.bhcode); }
            if (filter.cntcode.HasValue) { where += " AND rm.cntcode = @cnt "; p.Add("cnt", filter.cntcode); }
            if (filter.custid.HasValue) { where += " AND rm.custid = @custid "; p.Add("custid", (int)filter.custid.Value); }
            if (filter.fromdate.HasValue) { where += " AND rm.receiptdate >= @from "; p.Add("from", filter.fromdate.Value.Date); }
            if (filter.todate.HasValue) { where += " AND rm.receiptdate <= @to "; p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1)); }

            if (!string.IsNullOrEmpty(filter.receipt_type) && filter.receipt_type.ToUpper() != "ALL")
            {
                where += " AND rm.receipttype = @rtype ";
                p.Add("rtype", filter.receipt_type.ToUpper());
            }
            if (!string.IsNullOrEmpty(filter.collection_type))
            {
                where += @" AND EXISTS (SELECT 1 FROM balancecollectionby bcb
             WHERE bcb.receipt_guid = rm.receiptguid AND bcb.collection_type = @coltype
               AND bcb.tenant_code = @t AND COALESCE(bcb.deleted, false) = false) ";
                p.Add("coltype", filter.collection_type.ToUpper());
            }
            if (!string.IsNullOrEmpty(filter.search))
            {
                where += @" AND (lrm.name ILIKE @s OR lrm.mobileno ILIKE @s
          OR rm.receiptsnoprint ILIKE @s OR lrm.requestsnoprint ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }

            // ── JOINS ────────────────────────────────────────────────────────────────
            //
            //  receipt_details : NORMAL INNER JOIN — one output row per (receipt × bill).
            //    A bulk receipt with 2 bills → 2 rows. DO NOT add LIMIT 1 here.
            //
            //  doctor_master / branch_master : LATERAL + LIMIT 1 only to guard against
            //    duplicate master rows per dcode / bh_code. These do NOT collapse bill rows.
            // ─────────────────────────────────────────────────────────────────────────
            string joins = @"
FROM receipt_master rm
INNER JOIN receipt_details rd
       ON rd.receiptguid = rm.receiptguid
      AND rd.tenant_code = rm.tenant_code
      AND COALESCE(rd.deleted, false) = false
LEFT JOIN lab_request_master lrm
       ON lrm.requestguid = rd.requestguid
      AND lrm.tenant_code = rm.tenant_code
LEFT JOIN customerdb.customer_registration_master crm
       ON crm.custid = lrm.custid
      AND (crm.tenant_code = rm.tenant_code OR crm.tenant_code IS NULL)
LEFT JOIN LATERAL (
    SELECT name FROM doctor_master
     WHERE dcode = lrm.dcode AND tenant_code = lrm.tenant_code
     LIMIT 1
) dm ON true
LEFT JOIN LATERAL (
    SELECT name FROM mastertenant.branch_master
     WHERE bh_code = rm.enteredbhcode AND tenant_code = rm.tenant_code
     LIMIT 1
) bm ON true";

            // ── COUNT ────────────────────────────────────────────────────────────────
            //  COUNT(*) counts (receipt × bill) pairs — consistent with what is displayed.
            //  COUNT(DISTINCT rm.receiptguid) would under-count for bulk receipts:
            //    bulk receipt with 3 bills → COUNT(DISTINCT) = 1, COUNT(*) = 3.
            //  Pagination offsets are row-based, so COUNT(*) is correct here.
            // ─────────────────────────────────────────────────────────────────────────
            int total = await db.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) {joins} {where}", p);

            // ── AGGREGATE ────────────────────────────────────────────────────────────
            //
            //  total_due_collected: SUM(rd.receiptamount) — per-bill share, NOT rm.amountpaid.
            //    rm.amountpaid is the batch total for bulk receipts. Summing it over N bill
            //    rows multiplies the amount N times.
            //    rd.receiptamount is the exact amount settled against this specific bill.
            //
            //  total_advance_deposited / total_advance_refunded: these live on receipt_master
            //    (no bill-level split exists for advance receipts) so rm.amountpaid is correct.
            // ─────────────────────────────────────────────────────────────────────────
            var agg = await db.QueryFirstAsync<dynamic>(
                $@"SELECT
       COUNT(*) AS total_receipts,
       COALESCE(SUM(CASE WHEN rm.receipttype = 'DUE'
                         THEN rd.receiptamount ELSE 0 END), 0) AS total_due_collected,
       COALESCE(SUM(CASE WHEN rm.receipttype = 'ADVANCE'
                         THEN rm.amountpaid   ELSE 0 END), 0) AS total_advance_deposited,
       COALESCE(SUM(CASE WHEN rm.receipttype = 'ADVANCE_REFUND'
                         THEN rm.amountpaid   ELSE 0 END), 0) AS total_advance_refunded
   {joins} {where}", p, commandTimeout: 60);

            int totalCount = (int)agg.total_receipts;
            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            // ── DATA QUERY ───────────────────────────────────────────────────────────
            //
            //  amount → rd.receiptamount (this bill's share), NOT rm.amountpaid.
            //
            //  collection_type sub-query:
            //    1st attempt — receipt_guid + request_guid (per-item mode for bulk receipts)
            //    Fallback    — receipt_guid only            (single-bill or legacy receipts)
            //    Matches GetDueCollectionList exactly.
            // ─────────────────────────────────────────────────────────────────────────
            var rows = (await db.QueryAsync<HmsPaidHistoryRow>(
                $@"SELECT
       rm.receiptguid,
       rm.receiptsnoprint          AS receipt_no,
       rm.receiptbarcode           AS receipt_barcode,
       rm.receipttype              AS receipt_type,
       rm.receiptdate,

       -- Per-bill share of this receipt (NOT the batch total)
       COALESCE(rd.receiptamount, 0) AS amount,

       rm.custid,
       COALESCE(crm.custcode, '')  AS patient_id,
       lrm.name                    AS patient_name,
       lrm.mobileno,
       lrm.requestsnoprint         AS bill_no,
       lrm.requestguid             AS bill_guid,
       lrm.requestdatetime         AS bill_date,
       dm.name                     AS doctor_name,
       bm.name                     AS branch_name,
       rm.bankname                 AS bank_name,
       rm.paymentreference         AS reference_no,
       rm.chequedate,

       -- Payment mode: per-item (receipt + bill) first, fallback to receipt-level
       COALESCE(
           (SELECT bcb.collection_type
              FROM balancecollectionby bcb
             WHERE bcb.receipt_guid = rm.receiptguid
               AND bcb.request_guid = rd.requestguid
               AND bcb.tenant_code  = @t
               AND COALESCE(bcb.deleted, false) = false
             LIMIT 1),
           (SELECT bcb2.collection_type
              FROM balancecollectionby bcb2
             WHERE bcb2.receipt_guid = rm.receiptguid
               AND bcb2.tenant_code  = @t
               AND COALESCE(bcb2.deleted, false) = false
             LIMIT 1)
       )                           AS collection_type,

       CASE WHEN lrm.requestguid IS NOT NULL
            THEN GREATEST(COALESCE(lrm.totalamount, 0)
                        - COALESCE(lrm.paidamount,  0), 0)
            ELSE NULL END          AS remaining_due,

       CASE WHEN lrm.requestguid IS NOT NULL
            THEN (COALESCE(lrm.totalamount, 0)
                - COALESCE(lrm.paidamount,  0)) <= 0.05
            ELSE NULL END          AS is_bill_settled

   {joins} {where}
   ORDER BY rm.receiptdate DESC, lrm.name ASC
   LIMIT @limit OFFSET @offset", p, commandTimeout: 60)).ToList();

            return (rows, totalCount, new HmsPaidHistorySummary
            {
                total_receipts = totalCount,
                total_due_collected = Math.Round((double)agg.total_due_collected, 2),
                total_advance_deposited = Math.Round((double)agg.total_advance_deposited, 2),
                total_advance_refunded = Math.Round((double)agg.total_advance_refunded, 2),
                net_collected = Math.Round(
                    (double)agg.total_due_collected
                  + (double)agg.total_advance_deposited
                  - (double)agg.total_advance_refunded, 2)
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes available advance balance for a patient.
        ///   Balance = SUM(deposit rows where requestguid IS NULL)
        ///           − SUM(usage+refund rows where requestguid IS NOT NULL)
        /// Soft-deleted rows are excluded, so cancelling a collection and
        /// soft-deleting its usage rows automatically restores the balance.
        /// </summary>
        private async Task<double> GetPatientAdvanceBalance(
            IDbConnection db, decimal? custid, string tenantCode,
            IDbTransaction? tx = null)
        {
            if (custid == null) return 0;

            var result = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT
                      COALESCE(SUM(CASE WHEN ra.requestguid IS NULL
                                        THEN ra.receiptamount ELSE 0 END), 0) AS deposited,
                      COALESCE(SUM(CASE WHEN ra.requestguid IS NOT NULL
                                        THEN ra.receiptamount ELSE 0 END), 0) AS used
                    FROM receipt_advances ra
                   INNER JOIN receipt_master rm ON ra.receiptguid = rm.receiptguid
                   WHERE rm.custid      = @c
                     AND rm.tenant_code = @t
                     AND rm.receipttype = 'ADVANCE'
                     AND COALESCE(rm.isdeleted,  false) = false
                     AND COALESCE(ra.deleted,    false) = false",
                new { c = (int?)custid, t = tenantCode }, tx);

            if (result == null) return 0;
            double balance = (double)result.deposited - (double)result.used;
            return Math.Round(Math.Max(balance, 0), 2);
        }

        /// <summary>
        /// Race-safe receipt sequence using SELECT FOR UPDATE on billno_sequence.
        /// </summary>
        private async Task<HmsNumberResult> GetNextReceiptSequence(
            IDbConnection db, IDbTransaction tx,
            HmsBillNoMaster cfg, int bhcode, int cntcode, string tenantCode)
        {
            var seq = await db.QueryFirstOrDefaultAsync<HmsBillNoSequence>(
    @"SELECT seq_id, bncode, bhcode, cntcode, orderno,
             last_used_date::timestamp AS last_used_date,
             tenant_code, snoprint
      FROM billno_sequence
      WHERE bncode = @bn AND bhcode = @bh AND cntcode = @cnt AND tenant_code = @t
      FOR UPDATE",
    new { bn = cfg.bncode, bh = bhcode, cnt = cntcode, t = tenantCode }, tx);

            int nextOrder;

            if (seq == null)
            {
                nextOrder = cfg.orderno;
                await db.InsertAsync(new HmsBillNoSequence
                {
                    bncode = cfg.bncode,
                    bhcode = bhcode,
                    cntcode = cntcode,
                    orderno = nextOrder,
                    last_used_date = DateTime.UtcNow.Date,
                    tenant_code = tenantCode
                }, tx);
            }
            else
            {
                nextOrder = seq.orderno + 1;
                seq.orderno = nextOrder;
                seq.last_used_date = DateTime.UtcNow.Date;
                await db.UpdateAsync(seq, tx);
            }

            string prefix = cfg.shortname ?? "RCP";
            string snoprint = $"{prefix}-{DateTime.UtcNow:yyMM}-{nextOrder:D5}";
            string barcode = $"{cfg.bncode}{bhcode}{cntcode}{nextOrder}";

            return new HmsNumberResult
            {
                sno = nextOrder,
                snoprint = snoprint,
                barcode = barcode,
                used_bncode = cfg.bncode
            };
        }

        /// <summary>
        /// Inserts a balancecollectionby row writing BOTH legacy and new-style columns
        /// for backward compatibility with existing LIMS/HMS reports.
        /// </summary>
        private async Task InsertBalanceCollectionRow(
            IDbConnection db, IDbTransaction tx,
            int? bhcode, string collectionType,
            string receiptGuid, string? requestGuid,
            double amount, HmsCounterTiming shift,
            HmsDueCollectionRequest req, HmsLabRequestMaster bill,
            string tenantCode)
        {
            await db.ExecuteAsync(
                @"INSERT INTO balancecollectionby (
                      balancecollectionbyid,
                      bhcode,
                      collecteddate, collectiontype, receiptguid, requestguid,
                      collected_date, collection_type, receipt_guid, request_guid,
                      collectedamount, deleted,
                      usercode, computercode,
                      entereddate, ibsdate,
                      tmcode, cntcode, cnttid, pmcode, ctcode,
                      tenant_code)
                  VALUES (
                      @id, @bh,
                      @dt, @ct, @rg, @req,
                      @dt, @ct, @rg, @req,
                      @amt, false,
                      @uc, @cc,
                      @dt, @dt,
                      @tm, @cnt, @cnttid, @pm, @cct,
                      @t)",
                new
                {
                    id = Guid.NewGuid().ToString(),
                    bh = bhcode,
                    dt = DateTime.UtcNow,
                    ct = collectionType,
                    rg = receiptGuid,
                    req = requestGuid,
                    amt = amount,
                    uc = req.usercode,
                    cc = req.computercode,
                    tm = req.tmcode ?? bill.tmcode,
                    cnt = req.cntcode,
                    cnttid = shift.cnttid,
                    pm = req.pmcode ?? bill.pmcode,
                    cct = req.ctcode ?? bill.ctcode,
                    t = tenantCode
                }, tx);
        }

        private static string? ValidateDueRequest(HmsDueCollectionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.requestguid))
                return "Bill reference (requestguid) is required.";
            if (req.advance_to_use < 0)
                return "Advance amount cannot be negative.";
            if (req.cash_collected < 0)
                return "Cash collected cannot be negative.";
            if (req.advance_to_use == 0 && req.cash_collected == 0)
                return "At least one of advance_to_use or cash_collected must be greater than zero.";

            var refRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "CARD", "UPI", "CHEQUE", "BANK" };
            if (req.cash_collected > 0
                && refRequired.Contains(req.collection_type)
                && string.IsNullOrWhiteSpace(req.reference_no))
                return $"Transaction reference number is required for payment mode '{req.collection_type}'.";

            if (req.enteredbhcode == null || req.cntcode == null)
                return "Branch code (enteredbhcode) and counter code (cntcode) are required.";
            if (req.usercode == null)
                return "usercode is required (collected_by identity).";

            return null;
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  10. ADVANCED PAID HISTORY FILTER
        //
        //  Supports: isBill, isPatient, isRefferal, isMonthly flags,
        //            Branch, Counter, Year, BillNo, SampleNo, ReceiptNo,
        //            Patient, Doctor, Receipt Date From/To with Time
        //
        //  POST /api/HmsDueCollection/paid-history/filter
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsPaidHistoryRow> data, int totalCount, HmsPaidHistorySummary summary)>
            GetAdvancedPaidHistory(HmsPaidHistoryAdvancedFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @"WHERE rm.tenant_code = @t
                       AND COALESCE(rm.isdeleted, false) = false ";

            // ── Flag filters ──────────────────────────────────────────────────
            if (filter.isbill.HasValue)
            {
                where += " AND COALESCE(rm.isbill, false) = @isbill ";
                p.Add("isbill", filter.isbill.Value);
            }
            if (filter.ispatient.HasValue)
            {
                where += " AND COALESCE(rm.ispatient, false) = @ispatient ";
                p.Add("ispatient", filter.ispatient.Value);
            }
            if (filter.isrefferal.HasValue)
            {
                where += " AND COALESCE(rm.isrefferal, false) = @isrefferal ";
                p.Add("isrefferal", filter.isrefferal.Value);
            }
            if (filter.ismonthly.HasValue)
            {
                where += " AND COALESCE(rm.ismonthly, false) = @ismonthly ";
                p.Add("ismonthly", filter.ismonthly.Value);
            }

            // ── Location filters ──────────────────────────────────────────────
            if (filter.bhcode.HasValue)
            {
                where += " AND rm.enteredbhcode = @bh ";
                p.Add("bh", filter.bhcode.Value);
            }
            if (filter.cntcode.HasValue)
            {
                where += " AND rm.cntcode = @cnt ";
                p.Add("cnt", filter.cntcode.Value);
            }

            // ── Year filter ───────────────────────────────────────────────────
            if (filter.year.HasValue)
            {
                where += " AND EXTRACT(YEAR FROM rm.receiptdate) = @yr ";
                p.Add("yr", filter.year.Value);
            }

            // ── Number filters ────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.bill_no))
            {
                where += " AND lrm.requestsnoprint ILIKE @billno ";
                p.Add("billno", $"%{filter.bill_no}%");
            }
            if (!string.IsNullOrWhiteSpace(filter.sample_no))
            {
                where += " AND lrm.requestbarcode ILIKE @sampleno ";
                p.Add("sampleno", $"%{filter.sample_no}%");
            }
            if (!string.IsNullOrWhiteSpace(filter.receipt_no))
            {
                where += " AND rm.receiptsnoprint ILIKE @receiptno ";
                p.Add("receiptno", $"%{filter.receipt_no}%");
            }

            // ── Person filters ────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.patient_name))
            {
                where += " AND lrm.name ILIKE @pname ";
                p.Add("pname", $"%{filter.patient_name}%");
            }
            if (filter.custid.HasValue)
            {
                where += " AND rm.custid = @custid ";
                p.Add("custid", (int)filter.custid.Value);
            }
            if (filter.dcode.HasValue)
            {
                where += " AND lrm.dcode = @dcode ";
                p.Add("dcode", filter.dcode.Value);
            }

            // ── Receipt Type filter ───────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.receipt_type)
                && !filter.receipt_type.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                where += " AND rm.receipttype = @rtype ";
                p.Add("rtype", filter.receipt_type.ToUpper());
            }
            if (!string.IsNullOrWhiteSpace(filter.collection_type))
            {
                where += @" AND EXISTS (
            SELECT 1 FROM balancecollectionby bcb
             WHERE bcb.receipt_guid = rm.receiptguid
               AND bcb.collection_type = @coltype
               AND bcb.tenant_code = @t
               AND COALESCE(bcb.deleted, false) = false) ";
                p.Add("coltype", filter.collection_type.ToUpper());
            }

            // ── Date + Time range ─────────────────────────────────────────────
            if (filter.receipt_date_from.HasValue)
            {
                // Combine date + time_from if provided, else start of day
                DateTime fromDt = filter.receipt_date_from.Value.Date;
                if (filter.time_from.HasValue)
                    fromDt = fromDt.Add(filter.time_from.Value);

                where += " AND rm.receiptdate >= @from ";
                p.Add("from", fromDt);
            }
            if (filter.receipt_date_to.HasValue)
            {
                // Combine date + time_to if provided, else end of day
                DateTime toDt = filter.receipt_date_to.Value.Date;
                if (filter.time_to.HasValue)
                    toDt = toDt.Add(filter.time_to.Value);
                else
                    toDt = toDt.AddDays(1).AddSeconds(-1);

                where += " AND rm.receiptdate <= @to ";
                p.Add("to", toDt);
            }

            string joins = @"
        FROM receipt_master rm
        LEFT JOIN receipt_details rd
               ON rd.receiptguid = rm.receiptguid
              AND rd.tenant_code = rm.tenant_code
              AND COALESCE(rd.deleted, false) = false
        LEFT JOIN lab_request_master lrm
               ON lrm.requestguid = rd.requestguid
              AND lrm.tenant_code  = rd.tenant_code
        LEFT JOIN doctor_master dm
               ON dm.dcode       = lrm.dcode
              AND dm.tenant_code  = lrm.tenant_code
        LEFT JOIN mastertenant.branch_master bm
               ON bm.bh_code     = rm.enteredbhcode
              AND bm.tenant_code  = rm.tenant_code";

            // ── Aggregate summary ─────────────────────────────────────────────
            var agg = await db.QueryFirstAsync<dynamic>(
                $@"SELECT
               COUNT(DISTINCT rm.receiptguid) AS total_receipts,
               COALESCE(SUM(CASE WHEN rm.receipttype = 'DUE'
                                 THEN rm.amountpaid ELSE 0 END), 0) AS total_due_collected,
               COALESCE(SUM(CASE WHEN rm.receipttype = 'ADVANCE'
                                 THEN rm.amountpaid ELSE 0 END), 0) AS total_advance_deposited,
               COALESCE(SUM(CASE WHEN rm.receipttype = 'ADVANCE_REFUND'
                                 THEN rm.amountpaid ELSE 0 END), 0) AS total_advance_refunded
           {joins} {where}", p);

            int totalCount = (int)agg.total_receipts;
            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            var rows = (await db.QueryAsync<HmsPaidHistoryRow>(
                $@"SELECT
               rm.receiptguid,
               rm.receiptsnoprint          AS receipt_no,
               rm.receiptbarcode           AS receipt_barcode,
               rm.receipttype              AS receipt_type,
               rm.receiptdate,
               rm.amountpaid               AS amount,
               rm.custid,
               lrm.name                    AS patient_name,
               lrm.mobileno,
               lrm.requestsnoprint         AS bill_no,
               lrm.requestguid             AS bill_guid,
               lrm.requestdatetime         AS bill_date,
               dm.name                     AS doctor_name,
               bm.name                     AS branch_name,
               rm.bankname                 AS bank_name,
               rm.paymentreference         AS reference_no,
               rm.chequedate,
               (SELECT bcb.collection_type
                  FROM balancecollectionby bcb
                 WHERE bcb.receipt_guid = rm.receiptguid
                   AND bcb.tenant_code  = @t
                   AND COALESCE(bcb.deleted, false) = false
                 LIMIT 1)                  AS collection_type,
               CASE WHEN lrm.requestguid IS NOT NULL
                    THEN GREATEST(COALESCE(lrm.totalamount,0)
                               - COALESCE(lrm.paidamount, 0), 0)
                    ELSE NULL END          AS remaining_due,
               CASE WHEN lrm.requestguid IS NOT NULL
                    THEN (COALESCE(lrm.totalamount,0)
                        - COALESCE(lrm.paidamount, 0)) <= 0.05
                    ELSE NULL END          AS is_bill_settled
           {joins} {where}
           ORDER BY rm.receiptdate DESC
           LIMIT @limit OFFSET @offset", p)).ToList();
               
            return (rows, totalCount, new HmsPaidHistorySummary
            {
                total_receipts = totalCount,
                total_due_collected = Math.Round((double)agg.total_due_collected, 2),
                total_advance_deposited = Math.Round((double)agg.total_advance_deposited, 2),
                total_advance_refunded = Math.Round((double)agg.total_advance_refunded, 2),
                net_collected = Math.Round(
                    (double)agg.total_due_collected
                  + (double)agg.total_advance_deposited
                  - (double)agg.total_advance_refunded, 2)
            });
        }
        // ═══════════════════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════════════════
        //  11. DAILY COLLECTION REPORT  (UNIFIED — same response shape as
        //      GetDueCollectionList / GetPaidHistory)
        //
        //  Filters unchanged: From, To, Branch, Customer, Monthly, Doctor, Report Type.
        //
        //  Changes:
        //    1. Returns (List<HmsDueCollectionSummary>, int, HmsPaidHistorySummary) —
        //       same shape as list/paid-history, so all 3 screens share one model.
        //    2. Same two-step pattern: cheap paged-ID fetch first (light joins,
        //       no balancecollectionby), then enrichment scoped to ONLY this
        //       page's receipt/bill guids via ANY(@rguids/@bguids).
        //    3. Summary aggregates also scoped to the full FILTERED id set
        //       (not the whole tenant table) — same approach as before, just
        //       narrowed to what the WHERE clause actually matched.
        //
        //  POST /api/HmsDueCollection/history/filter
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(List<HmsDueCollectionSummary> data, int totalCount, HmsPaidHistorySummary summary)>
     GetDailyCollectionReport(HmsDailyCollectionReportFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @"WHERE rm.tenant_code = @t
                       AND COALESCE(rm.isdeleted, false) = false ";

            // ── FROM / TO date ────────────────────────────────────────────────
            if (filter.fromdate.HasValue)
            {
                where += " AND rm.receiptdate >= @from ";
                p.Add("from", filter.fromdate.Value.Date);
            }
            if (filter.todate.HasValue)
            {
                where += " AND rm.receiptdate <= @to ";
                p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1));
            }

            // ── BRANCH ────────────────────────────────────────────────────────
            if (filter.bhcode.HasValue)
            {
                where += " AND rm.enteredbhcode = @bh ";
                p.Add("bh", filter.bhcode.Value);
            }

            // ── CUSTOMER — search by custid, name, or mobile ──────────────────
            if (filter.custid.HasValue)
            {
                where += " AND rm.custid = @custid ";
                p.Add("custid", (int)filter.custid.Value);
            }
            else if (!string.IsNullOrWhiteSpace(filter.customer_search))
            {
                where += @" AND (lrm.name    ILIKE @csearch
                      OR lrm.mobileno ILIKE @csearch
                      OR CAST(rm.custid AS TEXT) ILIKE @csearch) ";
                p.Add("csearch", $"%{filter.customer_search}%");
            }

            // ── DOCTOR — search by dcode or name ─────────────────────────────
            if (filter.dcode.HasValue)
            {
                where += " AND lrm.dcode = @dcode ";
                p.Add("dcode", filter.dcode.Value);
            }
            else if (!string.IsNullOrWhiteSpace(filter.doctor_search))
            {
                where += @" AND (dm.name ILIKE @dsearch
                      OR CAST(lrm.dcode AS TEXT) ILIKE @dsearch) ";
                p.Add("dsearch", $"%{filter.doctor_search}%");
            }

            // ── MONTHLY ───────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.ismonthly))
            {
                if (bool.TryParse(filter.ismonthly, out bool monthlyVal))
                {
                    where += " AND COALESCE(rm.ismonthly, false) = @ismonthly ";
                    p.Add("ismonthly", monthlyVal);
                }
            }

            // ── SELECT REPORT (receipt type) ──────────────────────────────────
            if (!string.IsNullOrWhiteSpace(filter.report_type)
                && !filter.report_type.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                where += " AND rm.receipttype = @rtype ";
                p.Add("rtype", filter.report_type.ToUpper());
            }

            // ── LIGHT joins only — needed for filters (doctor_search/customer_search),
            //    but NO balancecollectionby here. This keeps count + pagination cheap.
            string lightJoins = @"
FROM receipt_master rm
LEFT JOIN receipt_details rd
       ON rd.receiptguid = rm.receiptguid
      AND rd.tenant_code  = rm.tenant_code
      AND COALESCE(rd.deleted, false) = false
LEFT JOIN lab_request_master lrm
       ON lrm.requestguid = rd.requestguid
      AND lrm.tenant_code  = rd.tenant_code
LEFT JOIN doctor_master dm
       ON dm.dcode       = lrm.dcode
      AND dm.tenant_code  = lrm.tenant_code";

            int total = await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) {lightJoins} {where}", p);

            if (total == 0)
                return (new List<HmsDueCollectionSummary>(), 0, new HmsPaidHistorySummary());

            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            // ── STEP 1: cheap paged ID fetch — light joins, no aggregation ────────────
            var pagedIds = (await db.QueryAsync<(string receiptguid, string? requestguid)>(
                $@"SELECT rm.receiptguid, rd.requestguid
           {lightJoins} {where}
           ORDER BY rm.receiptdate DESC
           LIMIT @limit OFFSET @offset", p)).ToList();

            if (!pagedIds.Any())
                return (new List<HmsDueCollectionSummary>(), total, new HmsPaidHistorySummary());

            var pageReceiptGuids = pagedIds.Select(x => x.receiptguid).Distinct().ToArray();
            var pageRequestGuids = pagedIds.Select(x => x.requestguid).Where(x => x != null).Distinct().ToArray();

            p.Add("rguids", pageReceiptGuids);
            p.Add("bguids", pageRequestGuids);

            // ── STEP 2: full enrichment — balancecollectionby aggregation scoped
            //    ONLY to this page's receipt/bill guids, same pattern as list/paid-history.
            var rows = (await db.QueryAsync<HmsDueCollectionSummary>($@"
    WITH adv_agg AS (
        SELECT bcb.request_guid,
               SUM(bcb.collectedamount) AS advance_used
          FROM balancecollectionby bcb
         WHERE bcb.tenant_code     = @t
           AND bcb.collection_type = 'ADVANCE'
           AND bcb.request_guid    = ANY(@bguids)
           AND COALESCE(bcb.deleted, false) = false
         GROUP BY bcb.request_guid
    ),
    col_type AS (
        SELECT DISTINCT ON (bcb.receipt_guid, bcb.request_guid)
               bcb.receipt_guid, bcb.request_guid, bcb.collection_type
          FROM balancecollectionby bcb
         WHERE bcb.tenant_code  = @t
           AND bcb.receipt_guid = ANY(@rguids)
           AND COALESCE(bcb.deleted, false) = false
         ORDER BY bcb.receipt_guid, bcb.request_guid, bcb.collection_type
    ),
    col_fallback AS (
        SELECT DISTINCT ON (bcb.receipt_guid)
               bcb.receipt_guid, bcb.collection_type
          FROM balancecollectionby bcb
         WHERE bcb.tenant_code  = @t
           AND bcb.receipt_guid = ANY(@rguids)
           AND COALESCE(bcb.deleted, false) = false
         ORDER BY bcb.receipt_guid, bcb.collection_type
    )
    SELECT
        rm.receiptguid                                               AS receipt_guid,
        rm.receiptsnoprint                                           AS receipt_no,
        rm.custid,
        COALESCE(crm.custcode, '')                                  AS patient_id,
        rd.requestguid                                              AS bill_guid,
        lrm.name                                                     AS patient_name,
        lrm.mobileno,
        lrm.requestsnoprint                                          AS bill_no,
        lrm.dcode                                                    AS dcode,
        dm.name                                                      AS doctor_name,

        CASE WHEN rm.receipttype = 'DUE'
             THEN COALESCE(rd.receiptamount, 0)
             ELSE COALESCE(rm.amountpaid, 0) END                     AS amount_paid,

        COALESCE(adv.advance_used, 0)                                AS advance_used,
        COALESCE(ct.collection_type, cf.collection_type)             AS collection_type,
        rm.receipttype                                               AS receipt_type,
        rm.receiptdate                                               AS receipt_date,
        COALESCE(lrm.totalamount, 0)                                 AS total_bill_amount,
        CASE WHEN lrm.requestguid IS NOT NULL
             THEN GREATEST(COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0), 0)
             ELSE 0 END                                              AS current_due,
        CASE WHEN lrm.requestguid IS NOT NULL
             THEN (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
             ELSE true END                                           AS is_fully_settled,
        bm.name                                                      AS branch_name
    FROM receipt_master rm
    LEFT JOIN receipt_details rd
           ON rd.receiptguid = rm.receiptguid AND rd.tenant_code = rm.tenant_code
          AND COALESCE(rd.deleted, false) = false
    LEFT JOIN lab_request_master lrm
           ON lrm.requestguid = rd.requestguid AND lrm.tenant_code = rd.tenant_code
    LEFT JOIN customerdb.customer_registration_master crm
           ON crm.custid = lrm.custid
          AND (crm.tenant_code = rm.tenant_code OR crm.tenant_code IS NULL)
    LEFT JOIN LATERAL (
        SELECT name FROM doctor_master
         WHERE dcode = lrm.dcode AND tenant_code = lrm.tenant_code
         LIMIT 1
    ) dm ON true
    LEFT JOIN LATERAL (
        SELECT name FROM mastertenant.branch_master
         WHERE bh_code = rm.enteredbhcode AND tenant_code = rm.tenant_code
         LIMIT 1
    ) bm ON true
    LEFT JOIN adv_agg adv ON adv.request_guid = rd.requestguid
    LEFT JOIN col_type ct ON ct.receipt_guid = rm.receiptguid AND ct.request_guid = rd.requestguid
    LEFT JOIN col_fallback cf ON cf.receipt_guid = rm.receiptguid
    WHERE rm.receiptguid = ANY(@rguids)
    ORDER BY rm.receiptdate DESC", p, commandTimeout: 30)).ToList();

            // ── SUMMARY — scoped to the entire FILTERED id set (not just this page,
            //    and NOT the whole tenant table). Same WHERE as count, just used to
            //    pull the matching ids first, then aggregate against only those.
            var filteredIds = (await db.QueryAsync<string>(
                $"SELECT rm.receiptguid {lightJoins} {where}", p)).Distinct().ToArray();

            var bcbAgg = await db.QueryFirstAsync<dynamic>(@"
        SELECT
            COALESCE(SUM(CASE WHEN bcb.collection_type <> 'ADVANCE'
                              THEN bcb.collectedamount ELSE 0 END), 0) AS total_due_collected
        FROM balancecollectionby bcb
        WHERE bcb.tenant_code  = @t
          AND bcb.receipt_guid = ANY(@ids)
          AND COALESCE(bcb.deleted, false) = false",
                new { t = tenantCode, ids = filteredIds });

            var rmAgg = await db.QueryFirstAsync<dynamic>(@"
        SELECT
            COALESCE(SUM(CASE WHEN receipttype = 'ADVANCE'        THEN amountpaid ELSE 0 END), 0) AS total_advance_deposited,
            COALESCE(SUM(CASE WHEN receipttype = 'ADVANCE_REFUND' THEN amountpaid ELSE 0 END), 0) AS total_advance_refunded
        FROM receipt_master
        WHERE tenant_code = @t AND receiptguid = ANY(@ids)",
                new { t = tenantCode, ids = filteredIds });

            var summary = new HmsPaidHistorySummary
            {
                total_receipts = total,
                total_due_collected = Math.Round((double)bcbAgg.total_due_collected, 2),
                total_advance_deposited = Math.Round((double)rmAgg.total_advance_deposited, 2),
                total_advance_refunded = Math.Round((double)rmAgg.total_advance_refunded, 2),
                net_collected = Math.Round(
                    (double)bcbAgg.total_due_collected
                  + (double)rmAgg.total_advance_deposited
                  - (double)rmAgg.total_advance_refunded, 2)
            };

            return (rows, total, summary);
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  4b. USE ADVANCE FOR CUSTOMER
        //
        //  Applies existing advance credit against outstanding due bill(s) for a
        //  patient — no counter shift required since no new money changes hands.
        //    - requestguid provided → settle only that bill
        //    - requestguid omitted  → FIFO across all due bills (oldest first)
        //      until amount_to_use or due bills are exhausted
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsAdvanceUseResponse? data)> UseAdvance(
            HmsAdvanceUseRequest req, string tenantCode)
        {
            if (req.amount_to_use <= 0)
                return ("Amount to use must be greater than zero.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                double advanceBalanceBefore = await GetPatientAdvanceBalance(db, req.custid, tenantCode, tx);

                if (advanceBalanceBefore <= 0.01)
                    return ("No advance balance available for this patient.", null);

                double amountToUse = Math.Round(Math.Min(req.amount_to_use, advanceBalanceBefore), 2);

                List<HmsLabRequestMaster> bills;

                if (!string.IsNullOrWhiteSpace(req.requestguid))
                {
                    var single = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                        @"SELECT * FROM lab_request_master
                   WHERE requestguid = @rg AND tenant_code = @t
                   FOR UPDATE",
                        new { rg = req.requestguid, t = tenantCode }, tx);

                    if (single == null)
                        return ("Bill not found.", null);
                    if (single.isdeleted == true || single.deleted == true)
                        return ("Cannot use advance on a cancelled bill.", null);
                    if (single.custid != req.custid)
                        return ("Bill does not belong to this patient.", null);

                    bills = new List<HmsLabRequestMaster> { single };
                }
                else
                {
                    bills = (await db.QueryAsync<HmsLabRequestMaster>(
                        @"SELECT * FROM lab_request_master
                   WHERE custid = @c AND tenant_code = @t
                     AND COALESCE(isdeleted, false) = false
                     AND COALESCE(deleted,   false) = false
                     AND (COALESCE(totalamount, 0) - COALESCE(paidamount, 0)) > 0.05
                   ORDER BY requestdatetime ASC
                   FOR UPDATE",
                        new { c = (int)req.custid, t = tenantCode }, tx)).ToList();
                }

                if (!bills.Any())
                    return ("No outstanding due bills found for this patient.", null);

                var patientName = bills.First().name;
                var itemResults = new List<HmsAdvanceUseItemResult>();
                double remaining = amountToUse;
                double totalUsed = 0;

                foreach (var bill in bills)
                {
                    if (remaining <= 0.01) break;

                    double totalBill = bill.totalamount ?? 0;
                    double previousPaid = bill.paidamount ?? 0;
                    double currentDue = Math.Round(Math.Max(totalBill - previousPaid, 0), 2);

                    if (currentDue <= 0.01) continue;

                    double useOnThisBill = Math.Round(Math.Min(currentDue, remaining), 2);

                    // FIFO deposit consumption for this bill's portion
                    var deposits = (await db.QueryAsync<dynamic>(
                        @"SELECT ra.receiptadvanceid, ra.receiptguid, ra.receiptamount,
                         COALESCE(
                             (SELECT SUM(u.receiptamount)
                                FROM receipt_advances u
                               WHERE u.receiptguid  = ra.receiptguid
                                 AND u.requestguid IS NOT NULL
                                 AND COALESCE(u.deleted, false) = false),
                         0) AS used_amount
                    FROM receipt_advances ra
                   WHERE ra.requestguid IS NULL
                     AND COALESCE(ra.deleted, false) = false
                     AND ra.receiptguid IN (
                         SELECT rm.receiptguid FROM receipt_master rm
                          WHERE rm.custid      = @custid
                            AND rm.tenant_code = @t
                            AND rm.receipttype = 'ADVANCE'
                            AND COALESCE(rm.isdeleted, false) = false
                     )
                   ORDER BY ra.receiptadvanceid ASC",
                        new { custid = (int?)req.custid, t = tenantCode }, tx)).ToList();

                    double billRemaining = useOnThisBill;

                    foreach (var dep in deposits)
                    {
                        if (billRemaining <= 0.01) break;

                        double available = Math.Round((double)dep.receiptamount - (double)dep.used_amount, 2);
                        if (available <= 0.01) continue;

                        double debit = Math.Round(Math.Min(available, billRemaining), 2);

                        await db.ExecuteAsync(
                            @"INSERT INTO receipt_advances
                        (receiptadvanceid, receiptguid, requestguid, receiptamount,
                         deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                      VALUES
                        (@id, @rg, @billGuid, @amt, false, @t, @uc, @cc, @dt, @dt)",
                            new
                            {
                                id = Guid.NewGuid().ToString(),
                                rg = (string)dep.receiptguid,
                                billGuid = bill.requestguid,
                                amt = debit,
                                t = tenantCode,
                                uc = req.usercode,
                                cc = req.computercode,
                                dt = DateTime.UtcNow
                            }, tx);

                        await db.ExecuteAsync(
                            @"INSERT INTO balancecollectionby (
                          balancecollectionbyid, bhcode,
                          collecteddate, collectiontype, receiptguid, requestguid,
                          collected_date, collection_type, receipt_guid, request_guid,
                          collectedamount, deleted, usercode, computercode,
                          entereddate, ibsdate, tmcode, cntcode, cnttid, pmcode, ctcode,
                          tenant_code)
                      VALUES (
                          @id, @bh,
                          @dt, 'ADVANCE', @rg, @req,
                          @dt, 'ADVANCE', @rg, @req,
                          @amt, false, @uc, @cc,
                          @dt, @dt, @tm, @cnt, NULL, @pm, @cct,
                          @t)",
                            new
                            {
                                id = Guid.NewGuid().ToString(),
                                bh = req.enteredbhcode,
                                dt = DateTime.UtcNow,
                                rg = (string)dep.receiptguid,
                                req = bill.requestguid,
                                amt = debit,
                                uc = req.usercode,
                                cc = req.computercode,
                                tm = bill.tmcode,
                                cnt = req.cntcode,
                                pm = bill.pmcode,
                                cct = bill.ctcode,
                                t = tenantCode
                            }, tx);

                        billRemaining = Math.Round(billRemaining - debit, 2);
                    }

                    double actuallyUsed = Math.Round(useOnThisBill - billRemaining, 2);
                    if (actuallyUsed <= 0.01) continue;

                    await db.ExecuteAsync(
                        @"UPDATE lab_request_master
                     SET paidamount = COALESCE(paidamount, 0) + @amt
                   WHERE requestguid = @rg AND tenant_code = @t",
                        new { amt = actuallyUsed, rg = bill.requestguid, t = tenantCode }, tx);

                    double dueAfter = Math.Round(Math.Max(currentDue - actuallyUsed, 0), 2);

                    if (dueAfter <= 0.05)
                    {
                        await db.ExecuteAsync(
                            @"UPDATE balancecollectionbytest
                         SET requeststatus = true
                       WHERE balancecollectionbyid IN (
                           SELECT balancecollectionbyid
                             FROM balancecollectionby
                            WHERE request_guid = @rg AND tenant_code = @t
                              AND COALESCE(deleted, false) = false
                       )",
                            new { rg = bill.requestguid, t = tenantCode }, tx);
                    }

                    itemResults.Add(new HmsAdvanceUseItemResult
                    {
                        requestguid = bill.requestguid ?? string.Empty,
                        bill_no = bill.requestsnoprint,
                        due_before = currentDue,
                        advance_used = actuallyUsed,
                        due_after = dueAfter,
                        is_fully_settled = dueAfter <= 0.05
                    });

                    totalUsed = Math.Round(totalUsed + actuallyUsed, 2);
                    remaining = Math.Round(remaining - actuallyUsed, 2);
                }

                if (totalUsed <= 0.01)
                {
                    tx.Rollback();
                    return ("Unable to apply advance — no eligible due amount on the target bill(s).", null);
                }

                tx.Commit();

                return ("SUCCESS", new HmsAdvanceUseResponse
                {
                    custid = req.custid,
                    patient_name = patientName,
                    advance_balance_before = advanceBalanceBefore,
                    total_advance_used = totalUsed,
                    advance_balance_after = Math.Round(advanceBalanceBefore - totalUsed, 2),
                    bills_settled = itemResults.Count,
                    items = itemResults
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "UseAdvance failed for custid={c}", req.custid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  4c. CANCEL ADVANCE DEPOSIT
        //
        //  Reverses an ADVANCE receipt (deposit) if, and only if, none of it has
        //  been consumed (usage rows) or refunded (refund rows).
        //    - soft-deletes receipt_master (ADVANCE)
        //    - soft-deletes the deposit row in receipt_advances
        //    - soft-deletes the deposit's balancecollectionby row
        //  Balance is derived from these tables, so it updates automatically.
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsAdvanceCancelResponse? data)> CancelAdvance(
            HmsAdvanceCancelRequest req, string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(req.receiptguid))
                return ("Advance receipt reference (receiptguid) is required.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Lock the receipt row so two cancels can't run concurrently
                var receipt = await db.QueryFirstOrDefaultAsync<HmsDueReceiptMaster>(
                    @"SELECT * FROM receipt_master
               WHERE receiptguid = @rg AND tenant_code = @t
               FOR UPDATE",
                    new { rg = req.receiptguid, t = tenantCode }, tx);

                if (receipt == null)
                    return ("Advance receipt not found.", null);
                if (receipt.isdeleted == true || receipt.deleted == true)
                    return ("Advance receipt is already cancelled.", null);
                if (receipt.receipttype != "ADVANCE")
                    return ("Only ADVANCE deposit receipts can be cancelled through this endpoint. " +
                            "Use the due-collection cancel for DUE receipts.", null);

                // Deposited amount (requestguid IS NULL)
                double deposited = await db.ExecuteScalarAsync<double>(
                    @"SELECT COALESCE(SUM(receiptamount), 0)
                FROM receipt_advances
               WHERE receiptguid = @rg
                 AND tenant_code = @t
                 AND requestguid IS NULL
                 AND COALESCE(deleted, false) = false",
                    new { rg = req.receiptguid, t = tenantCode }, tx);

                // Consumed amount (usage rows → bill guid, refund rows → refund receipt guid)
                double consumed = await db.ExecuteScalarAsync<double>(
                    @"SELECT COALESCE(SUM(receiptamount), 0)
                FROM receipt_advances
               WHERE receiptguid = @rg
                 AND tenant_code = @t
                 AND requestguid IS NOT NULL
                 AND COALESCE(deleted, false) = false",
                    new { rg = req.receiptguid, t = tenantCode }, tx);

                if (consumed > 0.01)
                    return ($"Cannot cancel: {consumed:F2} of this advance ({deposited:F2}) has already " +
                            "been used against bills or refunded. Cancel those collections/refunds first.", null);

                double balanceBefore = await GetPatientAdvanceBalance(db, receipt.custid, tenantCode, tx);

                // Soft-delete deposit rows
                await db.ExecuteAsync(
                    @"UPDATE receipt_advances
                 SET deleted = true
               WHERE receiptguid = @rg
                 AND tenant_code = @t
                 AND requestguid IS NULL
                 AND COALESCE(deleted, false) = false",
                    new { rg = req.receiptguid, t = tenantCode }, tx);

                // Soft-delete the deposit's cash-flow ledger row(s)
                await db.ExecuteAsync(
                    @"UPDATE balancecollectionby
                 SET deleted = true
               WHERE receipt_guid = @rg
                 AND tenant_code  = @t
                 AND request_guid IS NULL
                 AND COALESCE(deleted, false) = false",
                    new { rg = req.receiptguid, t = tenantCode }, tx);

                // Soft-delete the receipt itself
                await db.ExecuteAsync(
                    @"UPDATE receipt_master
                 SET isdeleted = true, deleted = true
               WHERE receiptguid = @rg AND tenant_code = @t",
                    new { rg = req.receiptguid, t = tenantCode }, tx);

                double balanceAfter = await GetPatientAdvanceBalance(db, receipt.custid, tenantCode, tx);

                tx.Commit();

                _logger.LogInformation(
                    "Advance receipt {rg} cancelled by user={u}, amount={a}, reason={r}",
                    req.receiptguid, req.usercode, deposited, req.reason);

                return ("SUCCESS", new HmsAdvanceCancelResponse
                {
                    receipt_guid = req.receiptguid,
                    receipt_no = receipt.receiptsnoprint,
                    custid = receipt.custid,
                    cancelled_amount = Math.Round(deposited, 2),
                    advance_balance_before = balanceBefore,
                    advance_balance_after = balanceAfter,
                    cancelled_date = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CancelAdvance failed for receiptguid={rg}", req.receiptguid);
                return ($"Cancellation error: {ex.Message}", null);
            }
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  ADVANCE REQUEST & CASH COUNTER DEPOSIT / REFUND EXTENSIONS
        // ═══════════════════════════════════════════════════════════════════════

        private static bool _advanceRequestTableRan = false;
        private static readonly object _advanceRequestTableLock = new object();

        private void EnsureAdvanceRequestTable(IDbConnection db)
        {
            if (_advanceRequestTableRan) return;
            lock (_advanceRequestTableLock)
            {
                if (_advanceRequestTableRan) return;
                try
                {
                    db.Execute(@"
                        CREATE TABLE IF NOT EXISTS hms_advance_request (
                            request_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                            request_no VARCHAR(50),
                            request_date TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                            custid NUMERIC(18,2) NOT NULL,
                            patient_name VARCHAR(200),
                            mobile_no VARCHAR(50),
                            opvisitid VARCHAR(100),
                            ip_id UUID,
                            amount NUMERIC(18,2) NOT NULL,
                            purpose VARCHAR(250),
                            remarks TEXT,
                            status VARCHAR(50) DEFAULT 'PENDING',
                            requested_by INT,
                            requested_by_name VARCHAR(150),
                            target_cntcode INT,
                            target_bhcode INT,
                            collected_by INT,
                            collected_by_name VARCHAR(150),
                            collected_date TIMESTAMP WITH TIME ZONE,
                            receiptguid VARCHAR(100),
                            receiptsnoprint VARCHAR(100),
                            cancel_reason TEXT,
                            cancelled_by INT,
                            cancelled_date TIMESTAMP WITH TIME ZONE,
                            tenant_code VARCHAR(50) NOT NULL,
                            created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                            updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                        );
                        CREATE INDEX IF NOT EXISTS idx_adv_req_tenant_status ON hms_advance_request (tenant_code, status);
                        CREATE INDEX IF NOT EXISTS idx_adv_req_custid ON hms_advance_request (custid, tenant_code);
                    ");
                    _advanceRequestTableRan = true;
                }
                catch { }
            }
        }

        // ── 1. Create advance request (sent to cash counter) ──────────────────
        public async Task<(string status, HmsAdvanceRequestRow? data)> CreateAdvanceRequest(
            HmsCreateAdvanceRequestDto req, string tenantCode)
        {
            if (req.custid <= 0)
                return ("Patient custid is required.", null);
            if (req.amount <= 0)
                return ("Advance amount must be greater than zero.", null);

            using var db = GetConnection();
            db.Open();
            EnsureAdvanceRequestTable(db);

            using var tx = db.BeginTransaction();
            try
            {
                // Resolve patient name / mobile if not provided
                string patientName = req.patient_name ?? string.Empty;
                string mobileNo = req.mobile_no ?? string.Empty;
                if (string.IsNullOrWhiteSpace(patientName) || string.IsNullOrWhiteSpace(mobileNo))
                {
                    var pat = await db.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT COALESCE(NULLIF(TRIM(CONCAT(initial, ' ', name)), ''), name) AS pname,
                                 COALESCE(mobileno, phoneno, '') AS mob
                          FROM customerdb.customer_master
                          WHERE custid = @c LIMIT 1",
                        new { c = req.custid }, tx);

                    if (pat != null)
                    {
                        if (string.IsNullOrWhiteSpace(patientName)) patientName = pat.pname ?? "";
                        if (string.IsNullOrWhiteSpace(mobileNo)) mobileNo = pat.mob ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(patientName))
                    {
                        var lrmPat = await db.QueryFirstOrDefaultAsync<dynamic>(
                            @"SELECT name, mobileno FROM lab_request_master
                              WHERE custid = @c AND tenant_code = @t
                              ORDER BY requestdatetime DESC LIMIT 1",
                            new { c = req.custid, t = tenantCode }, tx);
                        if (lrmPat != null)
                        {
                            if (string.IsNullOrWhiteSpace(patientName)) patientName = lrmPat.name ?? "";
                            if (string.IsNullOrWhiteSpace(mobileNo)) mobileNo = lrmPat.mobileno ?? "";
                        }
                    }
                }

                // Resolve requested by user name
                string requestedByName = req.user_name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(requestedByName) && req.usercode.HasValue)
                {
                    requestedByName = await db.QueryFirstOrDefaultAsync<string>(
                        @"SELECT name FROM mastertenant.user_master WHERE user_code = @uc LIMIT 1",
                        new { uc = req.usercode.Value }, tx) ?? $"User #{req.usercode}";
                }

                // Generate request number: ADVR-yyyyMMdd-0001
                string todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
                int countToday = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM hms_advance_request
                      WHERE tenant_code = @t AND request_date >= @dStart AND request_date < @dEnd",
                    new { t = tenantCode, dStart = DateTime.UtcNow.Date, dEnd = DateTime.UtcNow.Date.AddDays(1) }, tx);

                string requestNo = $"ADVR-{todayStr}-{(countToday + 1):D4}";

                var row = new HmsAdvanceRequestRow
                {
                    request_id = Guid.NewGuid(),
                    request_no = requestNo,
                    request_date = DateTime.UtcNow,
                    custid = req.custid,
                    patient_name = patientName,
                    mobile_no = mobileNo,
                    opvisitid = req.opvisitid,
                    ip_id = req.ip_id,
                    amount = Math.Round(req.amount, 2),
                    purpose = string.IsNullOrWhiteSpace(req.purpose) ? "Advance Deposit" : req.purpose.Trim(),
                    remarks = req.remarks,
                    status = "PENDING",
                    requested_by = req.usercode,
                    requested_by_name = requestedByName,
                    target_cntcode = req.target_cntcode,
                    target_bhcode = req.target_bhcode,
                    tenant_code = tenantCode,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                await db.ExecuteAsync(
                    @"INSERT INTO hms_advance_request
                        (request_id, request_no, request_date, custid, patient_name, mobile_no,
                         opvisitid, ip_id, amount, purpose, remarks, status,
                         requested_by, requested_by_name, target_cntcode, target_bhcode,
                         tenant_code, created_at, updated_at)
                      VALUES
                        (@request_id, @request_no, @request_date, @custid, @patient_name, @mobile_no,
                         @opvisitid, @ip_id, @amount, @purpose, @remarks, @status,
                         @requested_by, @requested_by_name, @target_cntcode, @target_bhcode,
                         @tenant_code, @created_at, @updated_at)",
                    row, tx);

                tx.Commit();
                return ("SUCCESS", row);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CreateAdvanceRequest failed for custid={c}", req.custid);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ── 2. List advance requests ──────────────────────────────────────────
        public async Task<(List<HmsAdvanceRequestRow> data, int totalCount, HmsAdvanceRequestListSummary summary)>
            GetAdvanceRequests(HmsAdvanceRequestFilterDto filter, string tenantCode)
        {
            using var db = GetConnection();
            EnsureAdvanceRequestTable(db);

            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = " WHERE tenant_code = @t ";

            if (!string.IsNullOrWhiteSpace(filter.status))
            {
                where += " AND UPPER(status) = UPPER(@st) ";
                p.Add("st", filter.status.Trim());
            }
            if (filter.custid.HasValue && filter.custid.Value > 0)
            {
                where += " AND custid = @custid ";
                p.Add("custid", filter.custid.Value);
            }
            if (filter.cntcode.HasValue)
            {
                where += " AND (target_cntcode = @cnt OR target_cntcode IS NULL) ";
                p.Add("cnt", filter.cntcode.Value);
            }
            if (filter.bhcode.HasValue)
            {
                where += " AND (target_bhcode = @bh OR target_bhcode IS NULL) ";
                p.Add("bh", filter.bhcode.Value);
            }
            if (filter.fromdate.HasValue)
            {
                where += " AND request_date >= @from ";
                p.Add("from", filter.fromdate.Value.Date);
            }
            if (filter.todate.HasValue)
            {
                where += " AND request_date <= @to ";
                p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (!string.IsNullOrWhiteSpace(filter.search))
            {
                where += @" AND (patient_name ILIKE @s
                             OR mobile_no ILIKE @s
                             OR request_no ILIKE @s
                             OR receiptsnoprint ILIKE @s
                             OR custid::text ILIKE @s) ";
                p.Add("s", $"%{filter.search.Trim()}%");
            }

            var summary = await db.QueryFirstOrDefaultAsync<HmsAdvanceRequestListSummary>(
                $@"SELECT 
                     COUNT(1) AS total_requests,
                     COALESCE(COUNT(CASE WHEN status = 'PENDING' THEN 1 END), 0) AS pending_count,
                     COALESCE(SUM(CASE WHEN status = 'PENDING' THEN amount ELSE 0 END), 0) AS pending_amount,
                     COALESCE(COUNT(CASE WHEN status = 'COLLECTED' THEN 1 END), 0) AS collected_count,
                     COALESCE(SUM(CASE WHEN status = 'COLLECTED' THEN amount ELSE 0 END), 0) AS collected_amount,
                     COALESCE(COUNT(CASE WHEN status = 'CANCELLED' THEN 1 END), 0) AS cancelled_count,
                     COALESCE(SUM(CASE WHEN status = 'CANCELLED' THEN amount ELSE 0 END), 0) AS cancelled_amount
                   FROM hms_advance_request
                   {where}", p) ?? new HmsAdvanceRequestListSummary();

            int totalCount = summary.total_requests;
            int offset = Math.Max((filter.page - 1) * filter.pagesize, 0);
            p.Add("lim", filter.pagesize);
            p.Add("off", offset);

            var rows = (await db.QueryAsync<HmsAdvanceRequestRow>(
                $@"SELECT * FROM hms_advance_request
                   {where}
                   ORDER BY request_date DESC
                   LIMIT @lim OFFSET @off", p)).ToList();

            return (rows, totalCount, summary);
        }

        // ── 3. Receive / Collect advance request at Cash Counter ──────────────
        public async Task<(string status, HmsAdvanceReceiptResponse? data)> ReceiveAdvanceRequest(
            HmsCollectAdvanceRequestDto req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            EnsureAdvanceRequestTable(db);

            using var tx = db.BeginTransaction();
            try
            {
                var advReq = await db.QueryFirstOrDefaultAsync<HmsAdvanceRequestRow>(
                    @"SELECT * FROM hms_advance_request
                      WHERE request_id = @id AND tenant_code = @t
                      FOR UPDATE",
                    new { id = req.request_id, t = tenantCode }, tx);

                if (advReq == null)
                    return ("Advance request not found.", null);

                if (!string.Equals(advReq.status, "PENDING", StringComparison.OrdinalIgnoreCase))
                    return ($"Cannot collect this request; it is already {advReq.status}.", null);

                double effectiveAmount = req.amount.HasValue && req.amount.Value > 0
                    ? Math.Round(req.amount.Value, 2)
                    : advReq.amount;

                if (effectiveAmount <= 0)
                    return ("Advance amount must be greater than zero.", null);

                int bh = req.enteredbhcode ?? advReq.target_bhcode ?? 0;
                int cnt = req.cntcode ?? advReq.target_cntcode ?? 0;

                var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE bhcode = @bh AND cntcode = @cnt
                         AND todate IS NULL AND tenant_code = @t LIMIT 1",
                    new { bh, cnt, t = tenantCode }, tx);

                if (shift == null)
                    return ("No open counter shift found. Please open a counter shift first.", null);

                var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
                       WHERE isreceiptno = true AND deleted = false AND tenant_code = @t LIMIT 1",
                    new { t = tenantCode }, tx);

                if (receiptCfg == null)
                    return ("Receipt number sequence configuration not found.", null);

                var seqInfo = await GetNextReceiptSequence(
                    db, tx, receiptCfg, bh, cnt, tenantCode);

                string receiptGuid = Guid.NewGuid().ToString();

                var receipt = new HmsDueReceiptMaster
                {
                    receiptguid = receiptGuid,
                    receiptdate = DateTime.UtcNow,
                    receiptsno = seqInfo.sno,
                    receiptsnoprint = seqInfo.snoprint,
                    receiptbarcode = seqInfo.barcode,
                    receiptcovertedbarcode = seqInfo.barcode,
                    cntcode = cnt,
                    cnttid = shift.cnttid,
                    tmcode = req.tmcode,
                    pmcode = req.pmcode,
                    ctcode = req.ctcode,
                    bankname = req.bank_name,
                    paymentreference = string.IsNullOrWhiteSpace(req.reference_no) ? $"Req:{advReq.request_no}" : req.reference_no,
                    cardno = req.card_no,
                    chequedate = req.cheque_date,
                    amountpaid = effectiveAmount,
                    amountadjusted = effectiveAmount,
                    amounttotal = effectiveAmount,
                    deleted = false,
                    isdeleted = false,
                    isbill = false,
                    ispatient = true,
                    isrefund = false,
                    receipttype = "ADVANCE",
                    custid = (int)advReq.custid,
                    opvisitid = advReq.opvisitid,
                    enteredbhcode = bh,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(receipt, tx);

                // Deposit row in receipt_advances
                await db.ExecuteAsync(
                    @"INSERT INTO receipt_advances
                        (receiptadvanceid, receiptguid, requestguid, receiptamount,
                         deleted, tenant_code, usercode, computercode, entereddate, ibsdate)
                      VALUES
                        (@id, @rg, NULL, @amt, false, @t, @uc, @cc, @dt, @dt)",
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        rg = receiptGuid,
                        amt = effectiveAmount,
                        t = tenantCode,
                        uc = req.usercode,
                        cc = req.computercode,
                        dt = DateTime.UtcNow
                    }, tx);

                // balancecollectionby
                await db.ExecuteAsync(
                    @"INSERT INTO balancecollectionby (
                          balancecollectionbyid,
                          bhcode, collecteddate, collectiontype, receiptguid, requestguid,
                          collected_date, collection_type, receipt_guid, request_guid,
                          collectedamount, deleted, usercode, computercode,
                          entereddate, ibsdate, tmcode, cntcode, cnttid, pmcode, ctcode,
                          tenant_code)
                      VALUES (
                          @id,
                          @bh, @dt, @ct, @rg, NULL,
                          @dt, @ct, @rg, NULL,
                          @amt, false, @uc, @cc,
                          @dt, @dt, @tm, @cnt, @cnttid, @pm, @cct,
                          @t)",
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        bh = bh,
                        dt = DateTime.UtcNow,
                        ct = (req.collection_type ?? "CASH").ToUpper(),
                        rg = receiptGuid,
                        amt = effectiveAmount,
                        uc = req.usercode,
                        cc = req.computercode,
                        tm = req.tmcode,
                        cnt = cnt,
                        cnttid = shift.cnttid,
                        pm = req.pmcode,
                        cct = req.ctcode,
                        t = tenantCode
                    }, tx);

                // Resolve collector name
                string collectorName = req.user_name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(collectorName) && req.usercode.HasValue)
                {
                    collectorName = await db.QueryFirstOrDefaultAsync<string>(
                        @"SELECT name FROM mastertenant.user_master WHERE user_code = @uc LIMIT 1",
                        new { uc = req.usercode.Value }, tx) ?? $"User #{req.usercode}";
                }

                // Update request record
                await db.ExecuteAsync(
                    @"UPDATE hms_advance_request
                      SET status = 'COLLECTED',
                          amount = @amt,
                          collected_by = @uc,
                          collected_by_name = @cname,
                          collected_date = now(),
                          receiptguid = @rg,
                          receiptsnoprint = @sno,
                          remarks = COALESCE(NULLIF(@rem, ''), remarks),
                          updated_at = now()
                      WHERE request_id = @id AND tenant_code = @t",
                    new
                    {
                        amt = effectiveAmount,
                        uc = req.usercode,
                        cname = collectorName,
                        rg = receiptGuid,
                        sno = seqInfo.snoprint,
                        rem = req.remarks,
                        id = advReq.request_id,
                        t = tenantCode
                    }, tx);

                tx.Commit();

                return ("SUCCESS", new HmsAdvanceReceiptResponse
                {
                    receipt_guid = receiptGuid,
                    receipt_no = seqInfo.snoprint,
                    receipt_barcode = seqInfo.barcode,
                    receipt_type = "ADVANCE",
                    custid = advReq.custid,
                    patient_name = advReq.patient_name,
                    amount = effectiveAmount,
                    collection_type = (req.collection_type ?? "CASH").ToUpper(),
                    receipt_date = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "ReceiveAdvanceRequest failed for request_id={id}", req.request_id);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        // ── 4. Cancel advance request ─────────────────────────────────────────
        public async Task<(string status, string? message)> CancelAdvanceRequest(
            HmsCancelAdvanceRequestDto req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            EnsureAdvanceRequestTable(db);

            var advReq = await db.QueryFirstOrDefaultAsync<HmsAdvanceRequestRow>(
                @"SELECT * FROM hms_advance_request
                  WHERE request_id = @id AND tenant_code = @t",
                new { id = req.request_id, t = tenantCode });

            if (advReq == null)
                return ("NOT_FOUND", "Advance request not found.");

            if (!string.Equals(advReq.status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return ("INVALID_STATE", $"Cannot cancel advance request with status '{advReq.status}'.");

            await db.ExecuteAsync(
                @"UPDATE hms_advance_request
                  SET status = 'CANCELLED',
                      cancel_reason = @reason,
                      cancelled_by = @uc,
                      cancelled_date = now(),
                      updated_at = now()
                  WHERE request_id = @id AND tenant_code = @t",
                new
                {
                    reason = req.cancel_reason,
                    uc = req.usercode,
                    id = req.request_id,
                    t = tenantCode
                });

            return ("SUCCESS", "Advance request cancelled successfully.");
        }

        // ── 5. List of patients who have deposited advance ────────────────────
        public async Task<(List<HmsPatientAdvanceDepositRow> data, int totalCount, HmsPatientAdvanceDepositSummary summary)>
            GetPatientAdvanceDeposits(HmsPatientAdvanceDepositFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string dateFilter = "";
            if (filter.fromdate.HasValue)
            {
                dateFilter += " AND rm.receiptdate >= @from ";
                p.Add("from", filter.fromdate.Value.Date);
            }
            if (filter.todate.HasValue)
            {
                dateFilter += " AND rm.receiptdate <= @to ";
                p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (filter.bhcode.HasValue)
            {
                dateFilter += " AND rm.enteredbhcode = @bh ";
                p.Add("bh", filter.bhcode.Value);
            }
            if (filter.cntcode.HasValue)
            {
                dateFilter += " AND rm.cntcode = @cnt ";
                p.Add("cnt", filter.cntcode.Value);
            }

            string outerWhere = " WHERE 1=1 ";
            string status = (filter.balance_status ?? "ALL").ToUpperInvariant();
            if (status == "HAS_BALANCE" || status == "REFUNDABLE")
            {
                outerWhere += " AND available_balance > 0.05 ";
            }
            else if (status == "ZERO_BALANCE" || status == "EXHAUSTED")
            {
                outerWhere += " AND available_balance <= 0.05 ";
            }

            if (!string.IsNullOrWhiteSpace(filter.search))
            {
                outerWhere += @" AND (patient_name ILIKE @s 
                                 OR custcode ILIKE @s 
                                 OR mobile_no ILIKE @s 
                                 OR custid::text ILIKE @s
                                 OR last_receipt_no ILIKE @s) ";
                p.Add("s", $"%{filter.search.Trim()}%");
            }

            string sqlCte = $@"
                WITH base_adv AS (
                    SELECT 
                        rm.custid,
                        MIN(rm.receiptdate) AS first_deposit_date,
                        MAX(rm.receiptdate) AS last_deposit_date,
                        COUNT(DISTINCT rm.receiptguid) AS deposit_count,
                        SUM(CASE WHEN ra.requestguid IS NULL THEN ra.receiptamount ELSE 0 END) AS total_deposited,
                        SUM(CASE WHEN ra.requestguid IS NOT NULL AND (rref.receipttype IS NULL OR rref.receipttype != 'ADVANCE_REFUND') THEN ra.receiptamount ELSE 0 END) AS total_used,
                        SUM(CASE WHEN rref.receipttype = 'ADVANCE_REFUND' THEN ra.receiptamount ELSE 0 END) AS total_refunded
                    FROM receipt_master rm
                    INNER JOIN receipt_advances ra 
                            ON ra.receiptguid = rm.receiptguid 
                           AND ra.tenant_code = rm.tenant_code 
                           AND COALESCE(ra.deleted, false) = false
                    LEFT JOIN receipt_master rref 
                           ON rref.receiptguid = ra.requestguid 
                          AND rref.tenant_code = rm.tenant_code 
                          AND rref.receipttype = 'ADVANCE_REFUND'
                    WHERE rm.tenant_code = @t
                      AND rm.receipttype = 'ADVANCE'
                      AND COALESCE(rm.isdeleted, false) = false
                      AND COALESCE(rm.deleted, false) = false
                      {dateFilter}
                    GROUP BY rm.custid
                ),
                pat_agg AS (
                    SELECT
                        b.custid,
                        COALESCE(cm.custcode, '') AS custcode,
                        COALESCE(NULLIF(TRIM(CONCAT(cm.initial, ' ', cm.name)), ''), NULLIF(cm.name, ''), lrm.name, 'Patient #' || b.custid::text) AS patient_name,
                        COALESCE(NULLIF(cm.mobileno, ''), NULLIF(cm.phoneno, ''), lrm.mobileno, '') AS mobile_no,
                        COALESCE(cm.gender, lrm.gender, '') AS gender,
                        COALESCE(cm.ageyears, 0) AS age,
                        ROUND(b.total_deposited::numeric, 2) AS total_deposited,
                        ROUND(b.total_used::numeric, 2) AS total_used,
                        ROUND(b.total_refunded::numeric, 2) AS total_refunded,
                        ROUND(GREATEST(b.total_deposited - b.total_used - b.total_refunded, 0)::numeric, 2) AS available_balance,
                        b.first_deposit_date,
                        b.last_deposit_date,
                        b.deposit_count,
                        last_rcp.receiptsnoprint AS last_receipt_no,
                        CASE WHEN (b.total_deposited - b.total_used - b.total_refunded) > 0.05 THEN 'HAS_BALANCE' ELSE 'EXHAUSTED' END AS status
                    FROM base_adv b
                    LEFT JOIN customerdb.customer_master cm 
                           ON cm.custid = b.custid
                    LEFT JOIN LATERAL (
                        SELECT l.name, l.mobileno, l.gender
                          FROM lab_request_master l
                         WHERE l.custid = b.custid AND l.tenant_code = @t
                           AND COALESCE(l.isdeleted, false) = false
                         ORDER BY l.requestdatetime DESC
                         LIMIT 1
                    ) lrm ON true
                    LEFT JOIN LATERAL (
                        SELECT rm_last.receiptsnoprint
                          FROM receipt_master rm_last
                         WHERE rm_last.custid = b.custid
                           AND rm_last.tenant_code = @t
                           AND rm_last.receipttype = 'ADVANCE'
                           AND COALESCE(rm_last.isdeleted, false) = false
                         ORDER BY rm_last.receiptdate DESC NULLS LAST
                         LIMIT 1
                    ) last_rcp ON true
                )";

            var summary = await db.QueryFirstOrDefaultAsync<HmsPatientAdvanceDepositSummary>(
                $@"{sqlCte}
                   SELECT 
                     COUNT(1) AS total_patients,
                     COALESCE(SUM(total_deposited), 0) AS total_deposited,
                     COALESCE(SUM(total_used), 0) AS total_used,
                     COALESCE(SUM(total_refunded), 0) AS total_refunded,
                     COALESCE(SUM(available_balance), 0) AS total_available_balance
                   FROM pat_agg
                   {outerWhere}", p) ?? new HmsPatientAdvanceDepositSummary();

            int totalCount = summary.total_patients;
            int offset = Math.Max((filter.page - 1) * filter.pagesize, 0);
            p.Add("lim", filter.pagesize);
            p.Add("off", offset);

            var rows = (await db.QueryAsync<HmsPatientAdvanceDepositRow>(
                $@"{sqlCte}
                   SELECT * FROM pat_agg
                   {outerWhere}
                   ORDER BY last_deposit_date DESC
                   LIMIT @lim OFFSET @off", p)).ToList();

            return (rows, totalCount, summary);
        }

        // ── 6. List of advance refunds history ────────────────────────────────
        public async Task<(List<HmsAdvanceRefundHistoryRow> data, int totalCount, HmsAdvanceRefundSummary summary)>
            GetAdvanceRefundHistory(HmsAdvanceRefundFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = @" WHERE rm.tenant_code = @t
                               AND rm.receipttype = 'ADVANCE_REFUND'
                               AND COALESCE(rm.isdeleted, false) = false
                               AND COALESCE(rm.deleted, false) = false ";

            if (filter.custid.HasValue && filter.custid.Value > 0)
            {
                where += " AND rm.custid = @custid ";
                p.Add("custid", (int)filter.custid.Value);
            }
            if (filter.bhcode.HasValue)
            {
                where += " AND rm.enteredbhcode = @bh ";
                p.Add("bh", filter.bhcode.Value);
            }
            if (filter.cntcode.HasValue)
            {
                where += " AND rm.cntcode = @cnt ";
                p.Add("cnt", filter.cntcode.Value);
            }
            if (filter.fromdate.HasValue)
            {
                where += " AND rm.receiptdate >= @from ";
                p.Add("from", filter.fromdate.Value.Date);
            }
            if (filter.todate.HasValue)
            {
                where += " AND rm.receiptdate <= @to ";
                p.Add("to", filter.todate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (!string.IsNullOrWhiteSpace(filter.search))
            {
                where += @" AND (rm.receiptsnoprint ILIKE @s
                             OR cm.name ILIKE @s
                             OR cm.custcode ILIKE @s
                             OR lrm.name ILIKE @s
                             OR lrm.mobileno ILIKE @s
                             OR rm.custid::text ILIKE @s) ";
                p.Add("s", $"%{filter.search.Trim()}%");
            }

            string sqlJoins = @"
                FROM receipt_master rm
                LEFT JOIN customerdb.customer_master cm 
                       ON cm.custid = rm.custid
                LEFT JOIN LATERAL (
                    SELECT l.name, l.mobileno
                      FROM lab_request_master l
                     WHERE l.custid = rm.custid AND l.tenant_code = rm.tenant_code
                       AND COALESCE(l.isdeleted, false) = false
                     ORDER BY l.requestdatetime DESC
                     LIMIT 1
                ) lrm ON true
                LEFT JOIN mastertenant.user_master um 
                       ON um.user_code = rm.usercode
                LEFT JOIN counter_master cnt 
                       ON cnt.cntcode = rm.cntcode AND cnt.tenant_code = rm.tenant_code
                LEFT JOIN mastertenant.branch_master bm 
                       ON bm.bh_code = rm.enteredbhcode AND bm.tenant_code = rm.tenant_code";

            var summary = await db.QueryFirstOrDefaultAsync<HmsAdvanceRefundSummary>(
                $@"SELECT 
                     COUNT(1) AS total_refund_count,
                     COALESCE(SUM(rm.amountpaid), 0) AS total_refund_amount
                   {sqlJoins}
                   {where}", p) ?? new HmsAdvanceRefundSummary();

            int totalCount = summary.total_refund_count;
            int offset = Math.Max((filter.page - 1) * filter.pagesize, 0);
            p.Add("lim", filter.pagesize);
            p.Add("off", offset);

            var rows = (await db.QueryAsync<HmsAdvanceRefundHistoryRow>(
                $@"SELECT
                     rm.receiptguid,
                     rm.receiptsnoprint AS receipt_no,
                     rm.receiptbarcode,
                     rm.receiptdate,
                     rm.custid,
                     COALESCE(cm.custcode, '') AS custcode,
                     COALESCE(NULLIF(TRIM(CONCAT(cm.initial, ' ', cm.name)), ''), NULLIF(cm.name, ''), lrm.name, 'Patient #' || rm.custid::text) AS patient_name,
                     COALESCE(NULLIF(cm.mobileno, ''), NULLIF(cm.phoneno, ''), lrm.mobileno, '') AS mobile_no,
                     rm.amountpaid AS refund_amount,
                     rm.usercode,
                     COALESCE(um.name, 'User #' || rm.usercode::text) AS entered_by,
                     rm.cntcode::int AS cntcode,
                     COALESCE(cnt.name, '') AS counter_name,
                     rm.enteredbhcode,
                     COALESCE(bm.name, '') AS branch_name,
                     rm.paymentreference AS remarks
                   {sqlJoins}
                   {where}
                   ORDER BY rm.receiptdate DESC
                   LIMIT @lim OFFSET @off", p)).ToList();

            return (rows, totalCount, summary);
        }
}
}
