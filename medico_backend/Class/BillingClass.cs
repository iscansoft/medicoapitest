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
    public class HmsBillingClass
    {
        private readonly string _conn;
        private readonly string? _inventoryConn;
        private readonly ILogger<HmsBillingClass> _logger;
        private readonly UnbilledChargesClass _unbilledCls;   // ADD

        public HmsBillingClass(IConfiguration cfg, ILogger<HmsBillingClass> logger, UnbilledChargesClass unbilledCls)
        {
            _conn = cfg.GetConnectionString("conn")
                ?? throw new InvalidOperationException("Database connection string 'conn' not found.");
            _logger = logger;
            _unbilledCls = unbilledCls;
            _inventoryConn = cfg.GetConnectionString("inventory_conn");
        }

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ════════════════════════════════════════════════════════════════════════
        //  1. SAVE / CREATE / UPDATE BILLS
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsBillResponse? data)> SaveBill(CreateHmsBillRequest req, string tenantCode)
        {
            var validationErr = ValidateBillRequest(req);
            if (validationErr != null) return (validationErr, null);

            return string.IsNullOrEmpty(req.requestguid)
                ? await CreateBill(req, tenantCode)
                : await UpdateBill(req.requestguid, req, tenantCode);
        }

        private async Task<(string status, HmsBillResponse? data)> CreateBill(CreateHmsBillRequest req, string tenantCode)
        {
            _logger.LogInformation(">>>>>> RESOLVED TENANT CODE: [{tenant}]", tenantCode);
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            string requestGuid;

            try
            {
                // Verify/Retrieve Active Counter Timing
                var currentShift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing 
              WHERE bhcode = @bhcode AND cntcode = @cntcode AND todate IS NULL
                AND (planned_to IS NULL OR planned_to > @now)
                AND tenant_code = @tenantCode 
              LIMIT 1", new { bhcode = req.enteredbhcode, cntcode = req.cntcode, tenantCode, now = DateTime.UtcNow }, tx);

                if (currentShift == null)
                    return ("Selected billing counter shift session is not open.", null);

                // Fetch Sequential Master Record Configurations 
                var masterBillConfig = await ResolveBillNoConfig(db, tx, tenantCode, req.enteredbhcode, req.cntcode, isReceipt: false);
                if (masterBillConfig == null)
                    return ("Bill Number sequential configuration master rule not found.", null);

                // Generate Bill sequence and barcode numbers
                var billNumInfo = await GetNextSequenceNumber(db, tx, masterBillConfig.bncode, req.enteredbhcode ?? 0, req.cntcode ?? 0, tenantCode);

                requestGuid = Guid.NewGuid().ToString();
                double lineGrossTotal = req.items.Sum(x => (x.amount ?? 0));
                double aggregateDiscount = (req.discountamount ?? 0) + (req.specialdiscount ?? 0) + (req.ourdiscount ?? 0);
                double calculativeNetAmount = lineGrossTotal - aggregateDiscount;
                if (calculativeNetAmount < 0) calculativeNetAmount = 0;

                var masterRecord = new HmsLabRequestMaster
                {
                    requestguid = requestGuid,
                    requestsno = billNumInfo.sno,
                    requestsnoprint = billNumInfo.snoprint,
                    requestbarcode = billNumInfo.barcode,
                    requestconvertedbarcode = billNumInfo.barcode,
                    requestdatetime = DateTime.UtcNow,
                    requesteddatetime = DateTime.UtcNow,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow,
                    bncode = billNumInfo.used_bncode,
                    cntcode = req.cntcode,
                    cnttid = currentShift.cnttid,
                    custid = req.custid,
                    name = req.patient_name,
                    gender = req.gender,
                    dateofbirth = req.dateofbirth,
                    ageyears = req.ageyears,
                    agemonths = req.agemonths,
                    agedays = req.agedays,
                    mobileno = req.mobileno,
                    address = req.address,
                    areacode = req.areacode,
                    dcode = req.dcode,
                    consultantdcode = req.consultantdcode,
                    ftcode = req.ftcode,
                    pmcode = req.pmcode,
                    ctcode = req.ctcode,
                    ricode = req.ricode,
                    requestamount = lineGrossTotal,
                    discountper = req.discountper,
                    discountamount = req.discountamount,
                    specialdiscount = req.specialdiscount,
                    ourdispercentage = req.ourdispercentage,
                    ourdiscount = req.ourdiscount,
                    totalamount = calculativeNetAmount,
                    paidamount = req.paidamount ?? 0,
                    paidviareceipt = 0,
                    pmc1 = req.pmc1 ?? 0,
                    pmc2 = req.pmc2 ?? 0,
                    pmc3 = req.pmc3 ?? 0,
                    pmc1_amount = req.pmc1_amount ?? 0,
                    pmc2_amount = req.pmc2_amount ?? 0,
                    pmc3_amount = req.pmc3_amount ?? 0,
                    iscashbill = req.iscashbill,
                    iscreditbill = req.iscreditbill,
                    isinvestigation = true,
                    requeststatus = true,
                    resultstatus = false,
                    deleted = false,
                    isdeleted = false,
                    isverified = false,
                    isinsurancepatient = req.isinsurancepatient,
                    policyno = req.policyno,
                    authorisationno = req.authorisationno,
                    concessionreason = req.concessionreason,
                    card_refno = req.card_refno,
                    bank_app = req.bank_app,
                    bill_category = "HMS",
                    sheet_id = req.sheet_id,
                    opvisitid = req.op_id,
                    ip_id = req.ip_id,
                    enteredbhcode = req.enteredbhcode,
                    usercode = req.usercode,
                    computercode = req.computercode,
                    tenant_code = tenantCode
                };

                await db.InsertAsync(masterRecord, tx);

                // Insert Items
                int itemIndex = 1;
                foreach (var line in req.items)
                {
                    var detailRow = new HmsLabRequestDetail
                    {
                        requestdetailsid = Guid.NewGuid().ToString(),
                        requestguid = requestGuid,
                        testsno = itemIndex++,
                        tcode = line.tcode,
                        chargetype = line.charge_type,
                        item_name = line.item_name,
                        item_ref_id = line.item_ref_id,
                        testrate = line.unit_rate,
                        standardprice = line.unit_rate,
                        testamount = line.amount,
                        discount = line.discount,
                        newamount = (line.amount ?? 0) - (line.discount ?? 0),
                        gstper = line.gst_per,
                        gstamount = ((line.amount ?? 0) - (line.discount ?? 0)) * ((line.gst_per ?? 0) / 100.0),
                        qty = line.qty,
                        ttid = line.ttid,
                        resultstatus = false,
                        requeststatus = true,
                        isdeleted = false,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(detailRow, tx);
                }

                // If immediate payment is recorded, log receipts
                if ((req.paidamount ?? 0) > 0)
                {
                    await GenerateReceiptLog(db, tx, masterRecord, req.paidamount ?? 0, req.collection_type, tenantCode);
                }

                // If billing from unbilled charges, mark them billed in the same tx
                if (req.unbilled_charge_ids != null && req.unbilled_charge_ids.Any())
                {
                    await db.ExecuteAsync(
                        @"UPDATE unbilledcharges
                  SET billedstatus   = true,
                      billno         = @billno,
                      billid         = @billid,
                      billeddate     = @now,
                      billedquantity = quantity,
                      billedamount   = amount
                  WHERE unbilledid = ANY(@ids) AND tenant_code = @tenantCode",
                        new
                        {
                            billno = billNumInfo.snoprint,
                            billid = requestGuid,
                            now = DateTime.UtcNow,
                            ids = req.unbilled_charge_ids.ToArray(),
                            tenantCode
                        }, tx);
                }

                // ── Everything above is validated and staged. Commit now. ──────────────
                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed to create HMS billing record transaction entry context.");
                return ($"Internal transaction error: {ex.Message}", null);
            }

            // ── Transaction has committed successfully at this point. The bill, its ──
            // items, its sequence number, and any receipt are durably saved no matter
            // what happens below. A failure here must NEVER be reported as a write
            // failure, or the caller will believe (and may retry) something that
            // already succeeded — which is what causes "skipped" sequence numbers.
            try
            {
                var result = await FetchBillRecordByGuid(requestGuid, tenantCode);
                return ("SUCCESS", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bill {guid} committed successfully but post-commit fetch failed.", requestGuid);
                return ("SUCCESS_FETCH_FAILED", null);
            }
        }

        private async Task<(string status, HmsBillResponse? data)> UpdateBill(string requestGuid, CreateHmsBillRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var existingMaster = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    "SELECT * FROM lab_request_master WHERE requestguid = @requestGuid AND tenant_code = @tenantCode",
                    new { requestGuid, tenantCode }, tx);

                if (existingMaster == null) return ("Target billing record profile update vector not located.", null);
                if (existingMaster.isdeleted == true || existingMaster.deleted == true) return ("Modification criteria locked against deleted profiles.", null);

                double lineGrossTotal = req.items.Sum(x => (x.amount ?? 0));
                double aggregateDiscount = (req.discountamount ?? 0) + (req.specialdiscount ?? 0) + (req.ourdiscount ?? 0);
                double calculativeNetAmount = lineGrossTotal - aggregateDiscount;
                if (calculativeNetAmount < 0) calculativeNetAmount = 0;

                existingMaster.custid = req.custid;
                existingMaster.opvisitid = req.op_id;
                existingMaster.ip_id = req.ip_id ?? existingMaster.ip_id;
                existingMaster.name = req.patient_name;
                existingMaster.gender = req.gender;
                existingMaster.dateofbirth = req.dateofbirth;
                existingMaster.ageyears = req.ageyears;
                existingMaster.agemonths = req.agemonths;
                existingMaster.agedays = req.agedays;
                existingMaster.mobileno = req.mobileno;
                existingMaster.address = req.address;
                existingMaster.areacode = req.areacode;
                existingMaster.dcode = req.dcode;
                existingMaster.consultantdcode = req.consultantdcode;
                existingMaster.ftcode = req.ftcode;
                existingMaster.pmcode = req.pmcode;
                existingMaster.ctcode = req.ctcode;
                existingMaster.ricode = req.ricode;
                existingMaster.requestamount = lineGrossTotal;
                existingMaster.discountper = req.discountper;
                existingMaster.discountamount = req.discountamount;
                existingMaster.specialdiscount = req.specialdiscount;
                existingMaster.ourdispercentage = req.ourdispercentage;
                existingMaster.ourdiscount = req.ourdiscount;
                existingMaster.totalamount = calculativeNetAmount;
                existingMaster.pmc1 = req.pmc1 ?? 0;
                existingMaster.pmc2 = req.pmc2 ?? 0;
                existingMaster.pmc3 = req.pmc3 ?? 0;
                existingMaster.pmc1_amount = req.pmc1_amount ?? 0;
                existingMaster.pmc2_amount = req.pmc2_amount ?? 0;
                existingMaster.pmc3_amount = req.pmc3_amount ?? 0;
                existingMaster.iscashbill = req.iscashbill;
                existingMaster.iscreditbill = req.iscreditbill;
                existingMaster.isinsurancepatient = req.isinsurancepatient;
                existingMaster.policyno = req.policyno;
                existingMaster.authorisationno = req.authorisationno;
                existingMaster.concessionreason = req.concessionreason;
                existingMaster.card_refno = req.card_refno;
                existingMaster.bank_app = req.bank_app;
                existingMaster.alteredbhcode = req.enteredbhcode;

                await db.UpdateAsync(existingMaster, tx);

                // Re-align detail lines
                await db.ExecuteAsync("DELETE FROM lab_request_details WHERE requestguid = @requestGuid AND tenant_code = @tenantCode", new { requestGuid, tenantCode }, tx);

                int itemIndex = 1;
                foreach (var line in req.items)
                {
                    var detailRow = new HmsLabRequestDetail
                    {
                        requestdetailsid = Guid.NewGuid().ToString(),
                        requestguid = requestGuid,
                        testsno = itemIndex++,
                        tcode = line.tcode,
                        chargetype = line.charge_type,
                        item_name = line.item_name,
                        item_ref_id = line.item_ref_id,
                        testrate = line.unit_rate,
                        standardprice = line.unit_rate,
                        testamount = line.amount,
                        discount = line.discount,
                        newamount = (line.amount ?? 0) - (line.discount ?? 0),
                        gstper = line.gst_per,
                        gstamount = ((line.amount ?? 0) - (line.discount ?? 0)) * ((line.gst_per ?? 0) / 100.0),
                        qty = line.qty,
                        ttid = line.ttid,
                        resultstatus = false,
                        requeststatus = true,
                        isdeleted = false,
                        tenant_code = tenantCode
                    };
                    await db.InsertAsync(detailRow, tx);
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed executing structural content update over hms billing profile.");
                return ($"Update action transaction failure: {ex.Message}", null);
            }

            try
            {
                var result = await FetchBillRecordByGuid(requestGuid, tenantCode);
                return ("SUCCESS", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bill {guid} update committed successfully but post-commit fetch failed.", requestGuid);
                return ("SUCCESS_FETCH_FAILED", null);
            }
        }

        private static async Task<HmsBillNoMaster?> ResolveBillNoConfig(
     IDbConnection db, IDbTransaction tx, string tenantCode,
     int? bhcode, int? cntcode, bool isReceipt)
        {
            string flagColumn = isReceipt ? "isreceiptno" : "issampleno";
            HmsBillNoMaster? cfg = null;

            var npgsqlTx = tx as Npgsql.NpgsqlTransaction;
            const string savepointName = "sp_billno_mapping_probe";

            if (npgsqlTx != null)
                await npgsqlTx.SaveAsync(savepointName);

            try
            {
                cfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>($@"
            SELECT bm.*
            FROM billno_master bm
            INNER JOIN billno_mapping map
                    ON map.bncode = bm.bncode AND map.tenant_code = bm.tenant_code
            WHERE bm.tenant_code = @t
              AND bm.{flagColumn} = true
              AND bm.deleted  = false
              AND map.deleted = false
              AND map.tenant_code = @t
              AND (map.bhcode  = @bh OR (map.bhcode  IS NULL AND @bh IS NULL))
              AND (map.cntcode = @cn OR (map.cntcode IS NULL AND @cn IS NULL))
            ORDER BY
                CASE
                    WHEN map.bhcode IS NOT NULL AND map.cntcode IS NOT NULL THEN 1
                    WHEN map.bhcode IS NOT NULL AND map.cntcode IS NULL     THEN 2
                    WHEN map.bhcode IS NULL     AND map.cntcode IS NULL     THEN 3
                    ELSE 4
                END, bm.orderno
            LIMIT 1 FOR UPDATE",
                    new { t = tenantCode, bh = bhcode, cn = cntcode }, tx);

                if (npgsqlTx != null)
                    await npgsqlTx.ReleaseAsync(savepointName);
            }
            catch
            {
                // Roll back ONLY to the savepoint — the outer transaction stays alive and usable.
                if (npgsqlTx != null)
                    await npgsqlTx.RollbackAsync(savepointName);
                cfg = null;
            }

            cfg ??= await ResolveBillNoConfigLegacy(db, tx, tenantCode, bhcode, cntcode, isReceipt);
            return cfg;
        }

        private static async Task<HmsBillNoMaster?> ResolveBillNoConfigLegacy(
            IDbConnection db, IDbTransaction tx, string tenantCode,
            int? bhcode, int? cntcode, bool isReceipt)
        {
            string flagColumn = isReceipt ? "isreceiptno" : "issampleno";
            HmsBillNoMaster? cfg = null;

            if (bhcode.HasValue && cntcode.HasValue)
                cfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    $@"SELECT * FROM billno_master
               WHERE tenant_code=@t AND {flagColumn} = true AND deleted=false
                 AND (allbranch IS NULL OR allbranch=false)
                 AND (allcounter IS NULL OR allcounter=false)
                 AND bhcode=@bh AND cntcode=@cn
               ORDER BY isdefault DESC NULLS LAST, entereddate DESC
               LIMIT 1 FOR UPDATE",
                    new { t = tenantCode, bh = bhcode.Value, cn = cntcode.Value }, tx);

            if (cfg == null && bhcode.HasValue)
                cfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    $@"SELECT * FROM billno_master
               WHERE tenant_code=@t AND {flagColumn} = true AND deleted=false
                 AND (allbranch IS NULL OR allbranch=false) AND allcounter=true AND bhcode=@bh
               ORDER BY isdefault DESC NULLS LAST, entereddate DESC
               LIMIT 1 FOR UPDATE",
                    new { t = tenantCode, bh = bhcode.Value }, tx);

            cfg ??= await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                $@"SELECT * FROM billno_master
           WHERE tenant_code=@t AND {flagColumn} = true AND deleted=false
             AND allbranch=true AND allcounter=true
           ORDER BY isdefault DESC NULLS LAST, entereddate DESC
           LIMIT 1 FOR UPDATE",
                new { t = tenantCode }, tx);

            return cfg;
        }

        // ────────────────────────────────────────────────────────────────────────
        // GetNextSequenceNumber — locks (or creates) the billno_sequence row and
        // advances/restarts the running number based on masterConfig's restart*
        // flag. Standard LIMS period formatting & barcode generation applied.
        // ────────────────────────────────────────────────────────────────────────
        private async Task<HmsNumberResult> GetNextSequenceNumber(
            IDbConnection db, IDbTransaction tx, decimal engineCode,
            int branchReference, int counterReference, string tenantCode)
        {
            var masterConfig = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @engineCode AND tenant_code = @tenantCode",
                new { engineCode, tenantCode }, tx);

            if (masterConfig == null)
                throw new InvalidOperationException($"Billno master configuration bncode={engineCode} not found.");

            int? seqBhKey = masterConfig.allbranch == true ? (int?)null : branchReference;
            decimal? seqCntKey = (masterConfig.allbranch == true || masterConfig.allcounter == true)
                ? (decimal?)null
                : counterReference;

            // ── Serialize concurrent callers for this EXACT sequence key ──────────
            string sequenceLockKey = $"seq|{tenantCode}|{engineCode}|{seqBhKey?.ToString() ?? "ALL"}|{seqCntKey?.ToString() ?? "ALL"}";
            await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@lockKey))", new { lockKey = sequenceLockKey }, tx);

            var sequentialRecord = await db.QueryFirstOrDefaultAsync<HmsBillNoSequence>(
                @"SELECT seq_id, bncode, bhcode, cntcode, orderno,
                 last_used_date::timestamp AS last_used_date,
                 tenant_code, snoprint
          FROM billno_sequence
          WHERE bncode = @engineCode AND tenant_code = @tenantCode
            AND (bhcode  = @seqBh  OR (bhcode  IS NULL AND @seqBh  IS NULL))
            AND (cntcode = @seqCnt OR (cntcode IS NULL AND @seqCnt IS NULL))
          FOR UPDATE",
                new { engineCode, tenantCode, seqBh = seqBhKey, seqCnt = seqCntKey }, tx);

            DateTime today = DateTime.UtcNow.Date;
            int targetedProgressiveOrder;

            if (sequentialRecord == null)
            {
                targetedProgressiveOrder = masterConfig.orderno;

                var initialRow = new HmsBillNoSequence
                {
                    bncode = engineCode,
                    bhcode = seqBhKey,
                    cntcode = seqCntKey,
                    orderno = targetedProgressiveOrder,
                    last_used_date = today,
                    tenant_code = tenantCode
                };
                await db.InsertAsync(initialRow, tx);
            }
            else
            {
                bool shouldReset = ShouldResetSequence(sequentialRecord.last_used_date, today, masterConfig);

                targetedProgressiveOrder = shouldReset
                    ? masterConfig.orderno          // restart the count
                    : sequentialRecord.orderno + 1; // continue as before

                sequentialRecord.orderno = targetedProgressiveOrder;
                sequentialRecord.last_used_date = today;
                await db.UpdateAsync(sequentialRecord, tx);
            }

            string sn = masterConfig.shortname ?? tenantCode[..Math.Min(3, tenantCode.Length)].ToUpper();
            string period = BuildPeriod(masterConfig, today);
            string prefix = masterConfig.allbranch == true ? sn
                           : masterConfig.allcounter == true ? $"{sn}{branchReference}"
                           : $"{sn}{branchReference}-{counterReference}";

            string printRepresentation = $"{prefix}/{targetedProgressiveOrder:D3}/{period}";
            string trackingBarcode = $"{sn}{branchReference}{counterReference}{today:yyyyMMdd}{targetedProgressiveOrder:D5}";

            return new HmsNumberResult
            {
                sno = targetedProgressiveOrder,
                snoprint = printRepresentation,
                barcode = trackingBarcode,
                used_bncode = engineCode
            };
        }

        // ────────────────────────────────────────────────────────────────────────
        // ────────────────────────────────────────────────────────────────────────
        // GetOrCreateYearlyReceiptConfig — resolves or creates a yearly restart
        // receipt configuration for receipt numbers when isreceiptno = false or
        // when no custom receipt configuration is found.
        // ────────────────────────────────────────────────────────────────────────
        private async Task<HmsBillNoMaster> GetOrCreateYearlyReceiptConfig(
            IDbConnection db, IDbTransaction tx, string tenantCode, int? bhcode, int? cntcode)
        {
            // 1. Check if an existing receipt config with yearly restart exists
            HmsBillNoMaster? cfg = null;

            if (bhcode.HasValue && cntcode.HasValue)
                cfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
               WHERE tenant_code=@t AND isreceiptno = true AND deleted=false
                 AND (allbranch IS NULL OR allbranch=false)
                 AND (allcounter IS NULL OR allcounter=false)
                 AND bhcode=@bh AND cntcode=@cn
                 AND (restartcalendaryear = true OR restartfinancialyear = true)
               ORDER BY isdefault DESC NULLS LAST, entereddate DESC
               LIMIT 1 FOR UPDATE",
                    new { t = tenantCode, bh = bhcode.Value, cn = cntcode.Value }, tx);

            if (cfg == null && bhcode.HasValue)
                cfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    @"SELECT * FROM billno_master
               WHERE tenant_code=@t AND isreceiptno = true AND deleted=false
                 AND (allbranch IS NULL OR allbranch=false) AND allcounter=true AND bhcode=@bh
                 AND (restartcalendaryear = true OR restartfinancialyear = true)
               ORDER BY isdefault DESC NULLS LAST, entereddate DESC
               LIMIT 1 FOR UPDATE",
                    new { t = tenantCode, bh = bhcode.Value }, tx);

            cfg ??= await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                @"SELECT * FROM billno_master
           WHERE tenant_code=@t AND isreceiptno = true AND deleted=false
             AND allbranch=true AND allcounter=true
             AND (restartcalendaryear = true OR restartfinancialyear = true)
           ORDER BY isdefault DESC NULLS LAST, entereddate DESC
           LIMIT 1 FOR UPDATE",
                new { t = tenantCode }, tx);

            if (cfg != null) return cfg;

            // 2. If no yearly receipt config exists, check if ANY global receipt config exists for this tenant
            cfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                @"SELECT * FROM billno_master
           WHERE tenant_code=@t AND isreceiptno = true AND deleted=false
             AND allbranch=true AND allcounter=true
           ORDER BY isdefault DESC NULLS LAST, entereddate DESC
           LIMIT 1 FOR UPDATE",
                new { t = tenantCode }, tx);

            if (cfg != null) return cfg;

            // 3. Otherwise, create a default global yearly restart receipt config for this tenant
            string lockKey = $"create_yearly_rcp_cfg|{tenantCode}";
            await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@lockKey))", new { lockKey }, tx);

            cfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                @"SELECT * FROM billno_master
           WHERE tenant_code=@t AND isreceiptno = true AND deleted=false
             AND allbranch=true AND allcounter=true
           LIMIT 1 FOR UPDATE",
                new { t = tenantCode }, tx);

            if (cfg != null) return cfg;

            decimal nextCode = await db.ExecuteScalarAsync<decimal>(
                @"SELECT COALESCE(MAX(bncode), 0) + 1 FROM billno_master WHERE tenant_code = @t",
                new { t = tenantCode }, tx);

            cfg = new HmsBillNoMaster
            {
                bncode = nextCode,
                orderno = 1,
                name = "Default Yearly Receipt Series",
                shortname = "RCP",
                isdefault = true,
                allbranch = true,
                allcounter = true,
                restartcalendaryear = true,
                restartfinancialyear = false,
                restartmonthly = false,
                restartdaily = false,
                issampleno = false,
                isreceiptno = true,
                deleted = false,
                tenant_code = tenantCode,
                usercode = 0,
                computercode = 0,
                entereddate = DateTime.UtcNow,
                ibsdate = DateTime.UtcNow
            };

            await db.InsertAsync(cfg, tx);
            return cfg;
        }

        // ────────────────────────────────────────────────────────────────────────
        // GenerateReceiptLog — creates the receipt_master / receipt_details /
        // balancecollectionby rows for a payment against a bill.
        //
        // Business rules:
        //   • Receipt Number and Sample Number follow SEPARATE series.
        //   • isreceiptno = true  → follows configuration (resolves custom receipt
        //     numbering config if available and advances its sequence).
        //   • isreceiptno = false → follows separate yearly restart series.
        //   • issampleno = true   → always follows configuration.
        // ────────────────────────────────────────────────────────────────────────
        private async Task<HmsReceiptInserted> GenerateReceiptLog(
            IDbConnection db, IDbTransaction tx,
            HmsLabRequestMaster master, double amount,
            string collectionType, string tenantCode)
        {
            var governingConfig = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @bncode AND tenant_code = @tenantCode",
                new { bncode = master.bncode, tenantCode }, tx);

            HmsNumberResult receiptNumInfo;

            if (governingConfig?.isreceiptno == true)
            {
                var receiptConfig = await ResolveBillNoConfig(
                    db, tx, tenantCode, master.enteredbhcode, (int?)master.cntcode, isReceipt: true);

                // ── CRITICAL GUARD ─────────────────────────────────────────────
                // If the resolved "receipt" config is the SAME physical row that
                // generated the bill/sample number (master.bncode), using it here
                // would make the receipt draw its next value from the exact same
                // billno_sequence row as the bill — i.e. bill=1 then receipt=2,
                // sharing one counter instead of running as two independent series.
                // This happens whenever a billno_master row has BOTH
                // issampleno=true AND isreceiptno=true set (bad config data),
                // and we must never let that leak into the numbering logic —
                // so we deliberately refuse to use it and fall back to the
                // separate yearly-restart receipt series instead.
                if (receiptConfig != null && receiptConfig.bncode != master.bncode)
                {
                    receiptNumInfo = await GetNextSequenceNumber(
                        db, tx, receiptConfig.bncode,
                        master.enteredbhcode ?? 0, (int)(master.cntcode ?? 0), tenantCode);
                }
                else
                {
                    if (receiptConfig != null && receiptConfig.bncode == master.bncode)
                    {
                        _logger.LogWarning(
                            "Receipt config resolved to the SAME bncode={bncode} as the bill's own config (tenant={tenant}). " +
                            "Refusing to reuse it for receipt numbering to avoid sharing one sequence between bill and receipt. " +
                            "Falling back to the separate yearly restart receipt series.",
                            master.bncode, tenantCode);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Receipt Number config (isreceiptno=true) not found for bncode={bncode}, tenant={tenant}. Using separate yearly restart receipt series.",
                            master.bncode, tenantCode);
                    }

                    var yearlyReceiptConfig = await GetOrCreateYearlyReceiptConfig(
                        db, tx, tenantCode, master.enteredbhcode, (int?)master.cntcode);

                    receiptNumInfo = await GetNextSequenceNumber(
                        db, tx, yearlyReceiptConfig.bncode,
                        master.enteredbhcode ?? 0, (int)(master.cntcode ?? 0), tenantCode);
                }
            }
            else
            {
                // isreceiptno = false -> Receipt number follows separate yearly restart series
                var yearlyReceiptConfig = await GetOrCreateYearlyReceiptConfig(
                    db, tx, tenantCode, master.enteredbhcode, (int?)master.cntcode);

                receiptNumInfo = await GetNextSequenceNumber(
                    db, tx, yearlyReceiptConfig.bncode,
                    master.enteredbhcode ?? 0, (int)(master.cntcode ?? 0), tenantCode);
            }

            string receiptGuid = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            var receiptMaster = new HmsReceiptMaster
            {
                receiptguid = receiptGuid,
                receiptdate = now,
                receiptsno = receiptNumInfo.sno,
                receiptsnoprint = receiptNumInfo.snoprint,
                receiptbarcode = receiptNumInfo.barcode,
                receiptcovertedbarcode = receiptNumInfo.barcode,
                cntcode = master.cntcode,
                cnttid = master.cnttid,
                tmcode = master.tmcode,
                pmcode = master.pmcode,
                ctcode = master.ctcode,
                amountpaid = amount,
                amountadjusted = 0,
                amounttotal = amount,
                deleted = false,
                isdeleted = false,
                isbill = true,
                ispatient = true,
                isrefund = false,
                isrefferal = false,
                ismonthly = false,
                receipttype = collectionType,
                custid = master.custid.HasValue ? (int?)master.custid.Value : null,
                opvisitid = master.opvisitid,
                enteredbhcode = master.enteredbhcode,
                usercode = master.usercode,
                computercode = master.computercode,
                entereddate = now,
                ibsdate = now,
                tenant_code = tenantCode
            };
            await db.InsertAsync(receiptMaster, tx);

            var receiptDetail = new HmsReceiptDetail
            {
                receiptdetailsid = Guid.NewGuid().ToString(),
                receiptguid = receiptGuid,
                requestguid = master.requestguid,
                receiptamount = amount,
                discount_amount = 0,
                refund_amount = 0,
                deleted = false,
                usercode = master.usercode,
                computercode = master.computercode,
                entereddate = now,
                ibsdate = now,
                tenant_code = tenantCode
            };
            await db.InsertAsync(receiptDetail, tx);

            var balanceCollection = new HmsBalanceCollectionBy
            {
                balancecollectionbyid = Guid.NewGuid().ToString(),
                bhcode = master.enteredbhcode,
                collected_date = now,
                collection_type = collectionType,
                receipt_guid = receiptGuid,
                request_guid = master.requestguid,
                collectedamount = amount,
                tmcode = master.tmcode,
                cntcode = master.cntcode,
                cnttid = master.cnttid,
                ctcode = master.ctcode,
                pmcode = master.pmcode,
                deleted = false,
                usercode = master.usercode,
                computercode = master.computercode,
                entereddate = now,
                ibsdate = now,
                tenant_code = tenantCode
            };
            await db.InsertAsync(balanceCollection, tx);

            return new HmsReceiptInserted
            {
                guid = receiptGuid,
                barcode = receiptNumInfo.barcode,
                snoprint = receiptNumInfo.snoprint
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. POST BALANCE PAYMENT SETTLEMENT
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsBillResponse? data)> AddPayment(AddHmsPaymentRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var masterBill = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    "SELECT * FROM lab_request_master WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                if (masterBill == null) return ("Target invoice reference for allocation entry context is missing.", null);

                double netLimit = masterBill.totalamount ?? 0;
                double existingSettled = masterBill.paidamount ?? 0;
                double pendingResidual = netLimit - existingSettled;

                if (req.amount <= 0 || req.amount > (pendingResidual + 0.01))
                    return ($"Payment amount conflict. Pending amount balance remaining: {pendingResidual}", null);

                // ── Resolve the cash/card/upi split ──────────────────────────────
                double pmc1Amt = req.pmc1_amount ?? 0;
                double pmc2Amt = req.pmc2_amount ?? 0;
                double pmc3Amt = req.pmc3_amount ?? 0;
                double splitTotal = pmc1Amt + pmc2Amt + pmc3Amt;

                string effectiveCollectionType;

                if (splitTotal > 0.01)
                {
                    if (Math.Abs(splitTotal - req.amount) > 0.01)
                        return ($"Payment split mismatch: pmc1+pmc2+pmc3 ({splitTotal}) does not equal amount ({req.amount}).", null);

                    int modesUsed = (pmc1Amt > 0.01 ? 1 : 0) + (pmc2Amt > 0.01 ? 1 : 0) + (pmc3Amt > 0.01 ? 1 : 0);
                    effectiveCollectionType = modesUsed > 1 ? "MIXED" : req.collection_type;
                }
                else
                {
                    effectiveCollectionType = req.collection_type;
                    switch (req.collection_type?.ToUpperInvariant())
                    {
                        case "CASH": pmc1Amt = req.amount; break;
                        case "CARD": pmc2Amt = req.amount; break;
                        case "UPI": pmc3Amt = req.amount; break;
                        default: pmc1Amt = req.amount; break;
                    }
                }

                int pmc1Flag = pmc1Amt > 0.01 ? 1 : 0;
                int pmc2Flag = pmc2Amt > 0.01 ? 1 : 0;
                int pmc3Flag = pmc3Amt > 0.01 ? 1 : 0;

                masterBill.paidamount = existingSettled + req.amount;
                masterBill.paidviareceipt = 0;
                masterBill.pmc1 = (masterBill.pmc1 ?? 0) + pmc1Flag;
                masterBill.pmc2 = (masterBill.pmc2 ?? 0) + pmc2Flag;
                masterBill.pmc3 = (masterBill.pmc3 ?? 0) + pmc3Flag;
                masterBill.pmc1_amount = (masterBill.pmc1_amount ?? 0) + pmc1Amt;
                masterBill.pmc2_amount = (masterBill.pmc2_amount ?? 0) + pmc2Amt;
                masterBill.pmc3_amount = (masterBill.pmc3_amount ?? 0) + pmc3Amt;

                await db.UpdateAsync(masterBill, tx);

                await GenerateReceiptLog(db, tx, masterBill, req.amount, effectiveCollectionType, tenantCode);

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Error processing incoming ledger collection context over allocation parameters.");
                return ($"Ledger posting error context: {ex.Message}", null);
            }

            try
            {
                var response = await FetchBillRecordByGuid(req.requestguid, tenantCode);
                return ("SUCCESS", response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment for bill {guid} committed successfully but post-commit fetch failed.", req.requestguid);
                return ("SUCCESS_FETCH_FAILED", null);
            }
        }
        // ════════════════════════════════════════════════════════════════════════
        //  4. BILL CANCELLATION
        // ════════════════════════════════════════════════════════════════════════

        public async Task<string> CancelBill(CancelHmsBillRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var masterRecord = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    "SELECT * FROM lab_request_master WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                if (masterRecord == null) return "Billing file master reference layout profile not found.";
                if (masterRecord.isdeleted == true) return "Target asset index context profile state already registers cancelled indicators.";

                await db.ExecuteAsync(
                    @"UPDATE lab_request_master 
                      SET isdeleted = true, deleted = true, concessionreason = CONCAT(@reason, ' | Cancelled Context') 
                      WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, reason = req.reason, tenantCode }, tx);

                await db.ExecuteAsync(
                    "UPDATE lab_request_details SET isdeleted = true WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                await db.ExecuteAsync(
                    @"UPDATE receipt_master SET isdeleted = true, deleted = true WHERE receiptguid IN (
                        SELECT receiptguid FROM receipt_details WHERE requestguid = @requestguid AND tenant_code = @tenantCode
                    )", new { requestguid = req.requestguid, tenantCode }, tx);

                await db.ExecuteAsync(
                    "UPDATE receipt_details SET deleted = true WHERE requestguid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                await db.ExecuteAsync(
                    "UPDATE balancecollectionby SET deleted = true WHERE request_guid = @requestguid AND tenant_code = @tenantCode",
                    new { requestguid = req.requestguid, tenantCode }, tx);

                tx.Commit();
                return "SUCCESS";
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Structural operational state execution context rollback occurred handling cancellations.");
                return $"Structural cancellation action framework context failure mapping: {ex.Message}";
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  5. COUNTER SEED SESSIONS MANAGEMENT
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, HmsCounterTimingDto? session)> OpenCounterShift(HmsCounterTiming req, string tenantCode)
        {
            using var db = GetConnection();
            var now = DateTime.UtcNow;

            // 1. Close any expired-but-unswept shift for this counter, so it never blocks a new one
            await db.ExecuteAsync(
                @"UPDATE counter_timing SET todate = planned_to, close_type = 'AUTO'
          WHERE bhcode=@bhcode AND cntcode=@cntcode AND tenant_code=@tenantCode
            AND todate IS NULL AND planned_to IS NOT NULL AND planned_to <= @now",
                new { bhcode = req.bhcode, cntcode = req.cntcode, tenantCode, now });

            // 2. Is a live shift still open?
            var running = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                @"SELECT * FROM counter_timing
          WHERE bhcode=@bhcode AND cntcode=@cntcode AND todate IS NULL AND tenant_code=@tenantCode LIMIT 1",
                new { bhcode = req.bhcode, cntcode = req.cntcode, tenantCode });
            if (running != null)
                return ("An active shift counter is currently operational for this target interface window.", null);

            // 3. Default counterdate BEFORE using it
            var counterDate = req.counterdate?.Date ?? TimeZoneInfo.ConvertTimeFromUtc(now, Tz).Date;

            int shiftNo = await db.ExecuteScalarAsync<int>(
                @"SELECT COALESCE(MAX(shiftsno), 0) + 1 FROM counter_timing
          WHERE bhcode=@bhcode AND cntcode=@cntcode AND counterdate=@counterDate::date AND tenant_code=@tenantCode",
                new { bhcode = req.bhcode, cntcode = req.cntcode, counterDate, tenantCode });

            // 4. Read the lab setting
            var setting = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT shift_timing_mode, shift_end_time FROM lab_settings
          WHERE tenant_code=@tenantCode AND deleted=false
            AND (bh_code=@bh OR bh_code=0 OR bh_code IS NULL)
          ORDER BY (bh_code=@bh) DESC NULLS LAST LIMIT 1",
                new { bh = req.bhcode, tenantCode });

            string mode = ((string?)setting?.shift_timing_mode ?? "VARIABLE").ToUpper();

            // 5. Fill everything, then insert ONCE
            req.cnttid = Guid.NewGuid().ToString();
            req.shiftsno = shiftNo;
            req.counterdate = counterDate;
            req.fromdate = now;
            req.todate = null;
            req.shift_mode = mode;
            req.planned_to = ComputePlannedEndUtc(now, mode, (string?)setting?.shift_end_time);
            req.close_type = null;
            req.tenant_code = tenantCode;

            await db.InsertAsync(req);

            var mapped = await EvaluateShiftProfiling(req.cnttid, tenantCode);
            return ("SUCCESS", mapped);
        }

        public async Task<string> CloseCounterShift(CloseCounterRequest req, string tenantCode)
        {
            using var db = GetConnection();
            var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                "SELECT * FROM counter_timing WHERE cnttid=@cnttid AND tenant_code=@tenantCode",
                new { cnttid = req.cnttid, tenantCode });

            if (shift == null) return "Shift not found.";
            if (shift.todate != null) return "SUCCESS";               // already closed (auto or manual): treat as done

            var now = DateTime.UtcNow;

            // Planned time reached (1-minute tolerance for client clock drift) -> close AT the planned time
            if (shift.planned_to != null && now >= shift.planned_to.Value.AddSeconds(-60))
            {
                await db.ExecuteAsync(
                    "UPDATE counter_timing SET todate=@t, close_type='AUTO' WHERE cnttid=@id AND tenant_code=@tenantCode AND todate IS NULL",
                    new { t = shift.planned_to, id = shift.cnttid, tenantCode });
                return "SUCCESS";
            }

            if (shift.shift_mode == "FIXED")
                return "This is a fixed-timing shift. It will close automatically at the scheduled time.";

            // VARIABLE, before midnight: take the moment they click End Shift
            await db.ExecuteAsync(
                "UPDATE counter_timing SET todate=@now, close_type='MANUAL' WHERE cnttid=@id AND tenant_code=@tenantCode AND todate IS NULL",
                new { now, id = shift.cnttid, tenantCode });
            return "SUCCESS";
        }

        // ════════════════════════════════════════════════════════════════════════
        //  6. READ DATA QUERIES & ANALYTICAL COMPILATIONS
        // ════════════════════════════════════════════════════════════════════════

        public async Task<HmsBillResponse?> FetchBillRecordByGuid(string requestGuid, string tenantCode)
        {
            using var db = GetConnection();
            var master = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT rm.*, bm.name as countername 
                  FROM lab_request_master rm 
                  LEFT JOIN billno_master bm ON rm.bncode = bm.bncode AND rm.tenant_code = bm.tenant_code
                  WHERE rm.requestguid = @requestGuid AND rm.tenant_code = @tenantCode", new { requestGuid, tenantCode });

            if (master == null) return null;

            var items = await db.QueryAsync<HmsBillLineResponse>(
                @"SELECT requestdetailsid, chargetype as charge_type, item_name, tcode, item_ref_id, 
                         testrate as unit_rate, testamount as amount, discount, newamount as final_amount, qty, gstper as gst_per, gstamount as gst_amount
                  FROM lab_request_details 
                  WHERE requestguid = @requestGuid AND tenant_code = @tenantCode ORDER BY testsno ASC", new { requestGuid, tenantCode });

            var receiptProfile = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT r.receiptguid, r.receiptsnoprint, r.receiptbarcode 
                  FROM receipt_details d
                  INNER JOIN receipt_master r ON d.receiptguid = r.receiptguid
                  WHERE d.requestguid = @requestGuid AND r.isdeleted = false AND r.tenant_code = @tenantCode LIMIT 1", new { requestGuid, tenantCode });

            double totalInvoiceLimit = (double)(master.totalamount ?? 0.0);
            double recognizedCollections = (double)(master.paidamount ?? 0.0);
            double computedDifference = totalInvoiceLimit - recognizedCollections;

            return new HmsBillResponse
            {
                requestguid = master.requestguid,
                op_id = master.opvisitid,
                ip_id = master.ip_id,
                bill_no = master.requestsnoprint,
                barcode = master.requestbarcode,
                bill_date = master.requestdatetime,
                custid = master.custid,
                patient_name = master.name,
                gender = master.gender,
                dateofbirth = master.dateofbirth,
                mobileno = master.mobileno,
                ageyears = master.ageyears,
                dcode = (int?)master.dcode,
                enteredbhcode = master.enteredbhcode,
                cntcode = (int?)master.cntcode,
                cnttid = master.cnttid,
                tmcode = master.tmcode,
                gross_amount = master.requestamount,
                discount_amount = (double)(master.discountamount ?? 0.0) + (double)(master.specialdiscount ?? 0.0) + (double)(master.ourdiscount ?? 0.0),
                general_concession_per = master.discountper,
                general_concession_amount = master.discountamount,
                referral_concession_per = master.ourdispercentage,
                referral_concession_amount = master.ourdiscount,
                tax_amount = master.taxamount,
                net_amount = totalInvoiceLimit,
                paid_amount = recognizedCollections,
                balance_amount = computedDifference < 0.01 ? 0.0 : computedDifference,
                is_settled = (computedDifference <= 0.05),
                pmc1 = master.pmc1,
                pmc2 = master.pmc2,
                pmc3 = master.pmc3,
                pmc1_amount = master.pmc1_amount,
                pmc2_amount = master.pmc2_amount,
                pmc3_amount = master.pmc3_amount,
                counter_name = master.countername,
                receiptguid = receiptProfile?.receiptguid,
                receipt_no = receiptProfile?.receiptsnoprint,
                receipt_barcode = receiptProfile?.receiptbarcode,
                items = items.ToList()
            };
        }

        public async Task<(List<HmsBillSummary> data, int totalCount)> QueryPaginatedBillsList(HmsBillFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var parameters = new DynamicParameters();
            parameters.Add("tenantCode", tenantCode);

            string queryConditions = "WHERE m.isdeleted = false AND m.bill_category = 'HMS' AND m.tenant_code = @tenantCode ";

            if (filter.bhcode.HasValue)
            {
                queryConditions += " AND m.enteredbhcode = @bhcode ";
                parameters.Add("bhcode", filter.bhcode);
            }
            if (filter.cntcode.HasValue)
            {
                queryConditions += " AND m.cntcode = @cntcode ";
                parameters.Add("cntcode", filter.cntcode);
            }
            if (filter.fromdate.HasValue)
            {
                queryConditions += " AND m.requestdatetime >= @fromdate ";
                parameters.Add("fromdate", filter.fromdate.Value.Date);
            }
            if (filter.todate.HasValue)
            {
                queryConditions += " AND m.requestdatetime <= @todate ";
                parameters.Add("todate", filter.todate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            if (filter.custid.HasValue)
            {
                queryConditions += " AND m.custid = @custid ";
                parameters.Add("custid", filter.custid);
            }
            if (filter.pendingonly == true)
            {
                queryConditions += " AND (m.totalamount - m.paidamount) > 0.05 ";
            }
            if (!string.IsNullOrEmpty(filter.search))
            {
                queryConditions += " AND (m.name ILIKE @searchTerm OR m.requestsnoprint ILIKE @searchTerm OR m.mobileno ILIKE @searchTerm) ";
                parameters.Add("searchTerm", $"%{filter.search}%");
            }
            if (filter.ip_id.HasValue)
            {
                queryConditions += " AND m.ip_id = @ip_id ";
                parameters.Add("ip_id", filter.ip_id);
            }
            string totalSumQuery = $"SELECT COUNT(*) FROM lab_request_master m {queryConditions}";
            int aggregatedCount = await db.ExecuteScalarAsync<int>(totalSumQuery, parameters);

            int rowsOffset = (filter.page - 1) * filter.pagesize;
            parameters.Add("limit", filter.pagesize);
            parameters.Add("offset", rowsOffset);

            string structuralFetchQuery = $@"
    SELECT m.requestguid, m.requestsnoprint as bill_no, m.name as patient_name, m.mobileno, 
           m.requestdatetime as bill_date, m.requestamount as gross_amount, 
           (COALESCE(m.discountamount,0) + COALESCE(m.specialdiscount,0)) as discount_amount,
           m.totalamount as net_amount, m.paidamount as paid_amount, 
           m.enteredbhcode, m.cntcode,
           m.opvisitid, m.dateofbirth, m.dcode, m.usercode,
           CASE 
             WHEN m.ip_id IS NOT NULL THEN 'IP' 
             WHEN m.opvisitid IS NOT NULL THEN 'OP' 
             ELSE 'LAB' 
           END as type
    FROM lab_request_master m
    {queryConditions}
    ORDER BY m.requestdatetime DESC 
    LIMIT @limit OFFSET @offset";

            var dataRows = await db.QueryAsync<HmsBillSummary>(structuralFetchQuery, parameters);
            var listings = dataRows.Select(x => {
                double computedDiff = (x.net_amount ?? 0) - (x.paid_amount ?? 0);
                x.balance_amount = computedDiff < 0.01 ? 0 : computedDiff;
                x.is_settled = (computedDiff <= 0.05);
                return x;
            }).ToList();

            return (listings, aggregatedCount);
        }

        public async Task<HmsCounterTimingDto?> EvaluateShiftProfiling(string sessionId, string tenantCode)
        {
            using var db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<HmsCounterTimingDto>(
                @"SELECT c.*, b.name as counter_name,
                         CASE WHEN c.todate IS NULL THEN true ELSE false END as is_open,
                         CASE WHEN c.todate IS NOT NULL THEN true ELSE false END as is_closed
                  FROM counter_timing c
                  LEFT JOIN billno_master b ON c.cntcode = b.cntcode AND c.tenant_code = b.tenant_code
                  WHERE c.cnttid = @sessionId AND c.tenant_code = @tenantCode LIMIT 1", new { sessionId, tenantCode });
        }

        public async Task<List<HmsDailyCollectionDto>> ExtractDailyCollectionSummaryReport(int branchCode, DateTime reportingDay, string tenantCode)
        {
            using var db = GetConnection();
            string compilationRawQuery = @"
                SELECT 
                    @reportingDay::DATE as date,
                    m.enteredbhcode as bhcode,
                    m.cntcode,
                    bm.name as counter_name,
                    COUNT(m.requestguid)::INT as total_bills,
                    SUM(COALESCE(m.requestamount, 0))::DOUBLE PRECISION as gross_amount,
                    SUM(COALESCE(m.discountamount, 0) + COALESCE(m.specialdiscount, 0))::DOUBLE PRECISION as discount_amount,
                    SUM(COALESCE(m.totalamount, 0))::DOUBLE PRECISION as net_amount,
                    SUM(COALESCE(m.pmc1, 0))::DOUBLE PRECISION as collected_cash,
                    SUM(COALESCE(m.pmc2, 0))::DOUBLE PRECISION as collected_card,
                    SUM(COALESCE(m.pmc3, 0))::DOUBLE PRECISION as collected_upi,
                    SUM(COALESCE(m.paidamount, 0))::DOUBLE PRECISION as total_collected
                FROM lab_request_master m
                LEFT JOIN billno_master bm ON m.cntcode = bm.cntcode AND m.tenant_code = bm.tenant_code
                WHERE m.enteredbhcode = @branchCode 
                  AND m.requestdatetime::DATE = @reportingDay::DATE 
                  AND m.isdeleted = false 
                  AND m.bill_category = 'HMS'
                  AND m.tenant_code = @tenantCode
                GROUP BY m.enteredbhcode, m.cntcode, bm.name";

            var summaryList = await db.QueryAsync<HmsDailyCollectionDto>(compilationRawQuery, new { branchCode, reportingDay = reportingDay.Date, tenantCode });
            return summaryList.Select(x => {
                double diff = x.net_amount - x.total_collected;
                x.pending_amount = diff < 0.01 ? 0 : diff;
                return x;
            }).ToList();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  7. RESTART-FLAG HELPERS
        // ════════════════════════════════════════════════════════════════════════

        // put this in HmsBillingClass, call it right before InsertAsync/UpdateAsync
        private static void EnforceSingleRestartMode(HmsBillNoMaster row)
        {
            // Precedence order mirrors ShouldResetSequence: daily > monthly > FY > CY
            if (row.restartdaily == true)
            {
                row.restartmonthly = false;
                row.restartfinancialyear = false;
                row.restartcalendaryear = false;
            }
            else if (row.restartmonthly == true)
            {
                row.restartdaily = false;
                row.restartfinancialyear = false;
                row.restartcalendaryear = false;
            }
            else if (row.restartfinancialyear == true)
            {
                row.restartdaily = false;
                row.restartmonthly = false;
                row.restartcalendaryear = false;
            }
            else if (row.restartcalendaryear == true)
            {
                row.restartdaily = false;
                row.restartmonthly = false;
                row.restartfinancialyear = false;
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // EnforceBillNoBusinessRules — normalizes a billno_master row before save.
        // Sample-number rows AND receipt-number rows are treated identically here:
        // the admin's chosen scope (branch/counter/global) and restart method
        // (daily/monthly/FY/CY) are both respected as configured — we only make
        // sure a single restart flag is active at a time.
        // ────────────────────────────────────────────────────────────────────────
        private static void EnforceBillNoBusinessRules(HmsBillNoMaster row)
        {
            EnforceSingleRestartMode(row);
        }

        /// <summary>
        /// Decides whether the running sequence should restart, based on which restart* flag
        /// is configured on billno_master and the gap between last_used_date and "today".
        /// Precedence when more than one flag is set: daily > monthly > financial year > calendar year.
        /// </summary>
        private static bool ShouldResetSequence(DateTime? lastUsedDate, DateTime today, HmsBillNoMaster master)
        {
            if (lastUsedDate == null) return false;
            var last = lastUsedDate.Value.Date;

            if (master.restartdaily == true)
                return last != today;

            if (master.restartmonthly == true)
                return last.Year != today.Year || last.Month != today.Month;

            if (master.restartfinancialyear == true)
                return FinancialYearOf(last) != FinancialYearOf(today);

            if (master.restartcalendaryear == true)
                return last.Year != today.Year;

            return false; // no restart policy set -> sequence runs indefinitely, as it does today
        }

        private const int FY_START_MONTH = 4; // assumes April–March FY (India); change if different

        private static int FinancialYearOf(DateTime date)
            => date.Month >= FY_START_MONTH ? date.Year : date.Year - 1;

        private static string BuildPeriod(HmsBillNoMaster cfg, DateTime today)
        {
            if (cfg.restartfinancialyear == true)
            { int fy = FinancialYearOf(today); return $"{fy % 100:D2}-{(fy + 1) % 100:D2}"; }
            if (cfg.restartdaily == true) return today.ToString("dd/MM/yyyy");
            if (cfg.restartmonthly == true) return today.ToString("MM/yyyy");
            return today.Year.ToString();
        }

        private string? ValidateBillRequest(CreateHmsBillRequest requestPayload)
        {
            if (requestPayload == null) return "Missing structural parameter body array context contents.";
            if (string.IsNullOrEmpty(requestPayload.patient_name)) return "Patient nomenclature descriptor context must contain value parameters.";
            if (requestPayload.items == null || !requestPayload.items.Any()) return "Invoice payload does not define child operational data line line-items.";

            foreach (var element in requestPayload.items)
            {
                if (string.IsNullOrEmpty(element.item_name) && !element.tcode.HasValue)
                    return "Item descriptions require alternate literal content names when code master markers are unavailable.";
                if ((element.amount ?? 0) < 0) return "Line calculation properties cannot process negative evaluation values.";
            }
            return null;
        }
        // ════════════════════════════════════════════════════════════════════════
        //  8. BILLNO MASTER CONFIGURATION (Bill / Receipt / Sample number setup)
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, BillNoMasterResponse? data)> CreateBillNoConfig(
            CreateBillNoMasterRequest req, string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(req.name))
                return ("Configuration name is required.", null);
            if (string.IsNullOrWhiteSpace(req.shortname))
                return ("Short code (prefix, e.g. 'BILL', 'RCP') is required.", null);
            if (req.shortname.Length > 10)
                return ("Short code must be 10 characters or fewer.", null);

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Guard against duplicate "type" configs for the same scope when isdefault=true.
                // Only one default bill-number config and one default receipt-number config
                // should exist per tenant (mirrors how CreateBill/GenerateReceiptLog pick "LIMIT 1").
                if (req.isdefault)
                {
                    var clashing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                        @"SELECT * FROM billno_master
                   WHERE tenant_code = @t AND deleted = false
                     AND isreceiptno = @isrcpt AND issampleno = @issample
                     AND isdefault = true
                   LIMIT 1",
                        new { t = tenantCode, isrcpt = req.isreceiptno, issample = req.issampleno }, tx);

                    if (clashing != null)
                        return ($"A default configuration of this type already exists: '{clashing.name}' " +
                                $"(bncode={clashing.bncode}). Update or delete it first, or set isdefault=false.", null);
                }

                await db.ExecuteAsync(
   "SELECT pg_advisory_xact_lock(hashtext(@t))",
   new { t = tenantCode }, tx);

                // Safe MAX after lock acquired
                decimal nextCode = await db.ExecuteScalarAsync<decimal>(
                    @"SELECT COALESCE(MAX(bncode), 0) + 1 FROM billno_master WHERE tenant_code = @t",
                    new { t = tenantCode }, tx);

                var row = new HmsBillNoMaster
                {
                    bncode = nextCode,
                    orderno = req.orderno,
                    name = req.name,
                    shortname = req.shortname.ToUpper(),
                    bhcode = req.bhcode,
                    cntcode = req.cntcode,
                    isdefault = req.isdefault,
                    allbranch = req.allbranch,
                    allcounter = req.allcounter,
                    restartfinancialyear = req.restartfinancialyear,
                    restartcalendaryear = req.restartcalendaryear,
                    restartmonthly = req.restartmonthly,
                    restartdaily = req.restartdaily,
                    issampleno = req.issampleno,
                    isreceiptno = req.isreceiptno,
                    deleted = false,
                    tenant_code = tenantCode,
                    usercode = req.usercode ?? 0,
                    computercode = req.computercode ?? 0,
                    entereddate = DateTime.UtcNow,
                    ibsdate = DateTime.UtcNow
                };

                // Enforces: receipt configs => allbranch+allcounter+restartcalendaryear;
                // bill/sample configs => single active restart flag as configured.
                EnforceBillNoBusinessRules(row);

                await db.InsertAsync(row, tx);
                tx.Commit();

                return ("SUCCESS", MapToResponse(row, 0));
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "CreateBillNoConfig failed.");
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        public async Task<(string status, BillNoMasterResponse? data)> UpdateBillNoConfig(
            UpdateBillNoMasterRequest req, string tenantCode)
        {
            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var existing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                    "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                    new { bn = req.bncode, t = tenantCode }, tx);

                if (existing == null) return ("Configuration not found.", null);
                if (existing.deleted) return ("Cannot update a deleted configuration. Restore it first.", null);

                if (req.name != null) existing.name = req.name;
                if (req.shortname != null) existing.shortname = req.shortname.ToUpper();
                if (req.orderno.HasValue) existing.orderno = req.orderno.Value;
                if (req.bhcode.HasValue) existing.bhcode = req.bhcode;
                if (req.cntcode.HasValue) existing.cntcode = req.cntcode;
                if (req.isdefault.HasValue) existing.isdefault = req.isdefault;
                if (req.allbranch.HasValue) existing.allbranch = req.allbranch;
                if (req.allcounter.HasValue) existing.allcounter = req.allcounter;
                if (req.restartfinancialyear.HasValue) existing.restartfinancialyear = req.restartfinancialyear;
                if (req.restartcalendaryear.HasValue) existing.restartcalendaryear = req.restartcalendaryear;
                if (req.restartmonthly.HasValue) existing.restartmonthly = req.restartmonthly;
                if (req.restartdaily.HasValue) existing.restartdaily = req.restartdaily;
                if (req.issampleno.HasValue) existing.issampleno = req.issampleno;
                if (req.isreceiptno.HasValue) existing.isreceiptno = req.isreceiptno;

                // Enforces: receipt configs => allbranch+allcounter+restartcalendaryear;
                // bill/sample configs => single active restart flag as configured.
                EnforceBillNoBusinessRules(existing);

                // Re-check default clash if isdefault is being turned on
                if (existing.isdefault == true)
                {
                    var clashing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                        @"SELECT * FROM billno_master
                   WHERE tenant_code = @t AND deleted = false AND bncode <> @bn
                     AND isreceiptno = @isrcpt AND issampleno = @issample
                     AND isdefault = true
                   LIMIT 1",
                        new { t = tenantCode, bn = existing.bncode, isrcpt = existing.isreceiptno, issample = existing.issampleno }, tx);

                    if (clashing != null)
                        return ($"Another default configuration of this type already exists: '{clashing.name}'.", null);
                }

                await db.UpdateAsync(existing, tx);
                tx.Commit();

                int inUse = await CountSequenceRows(existing.bncode, tenantCode);
                return ("SUCCESS", MapToResponse(existing, inUse));
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "UpdateBillNoConfig failed for bncode={bn}", req.bncode);
                return ($"Transaction error: {ex.Message}", null);
            }
        }

        public async Task<string> DeleteBillNoConfig(DeleteBillNoMasterRequest req, string tenantCode)
        {
            using var db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                new { bn = req.bncode, t = tenantCode });

            if (existing == null) return "Configuration not found.";
            if (existing.deleted) return "Configuration is already deleted.";

            // Warn rather than block — sequence rows just stop advancing for this config once it's soft-deleted.
            // Bills already created referencing this bncode are unaffected (they keep their own bncode value).
            await db.ExecuteAsync(
                "UPDATE billno_master SET deleted = true WHERE bncode = @bn AND tenant_code = @t",
                new { bn = req.bncode, t = tenantCode });

            return "SUCCESS";
        }

        public async Task<(string status, BillNoMasterResponse? data)> RestoreBillNoConfig(
            decimal bncode, string tenantCode)
        {
            using var db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });

            if (existing == null) return ("Configuration not found.", null);
            if (!existing.deleted) return ("Configuration is not deleted.", null);

            await db.ExecuteAsync(
                "UPDATE billno_master SET deleted = false WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });

            existing.deleted = false;
            int inUse = await CountSequenceRows(bncode, tenantCode);
            return ("SUCCESS", MapToResponse(existing, inUse));
        }

        public async Task<BillNoMasterResponse?> GetBillNoConfigByCode(decimal bncode, string tenantCode)
        {
            using var db = GetConnection();
            var row = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                "SELECT * FROM billno_master WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });

            if (row == null) return null;
            int inUse = await CountSequenceRows(bncode, tenantCode);
            return MapToResponse(row, inUse);
        }

        public async Task<(List<BillNoMasterResponse> data, int totalCount)> GetBillNoConfigList(
            BillNoMasterFilterRequest filter, string tenantCode)
        {
            using var db = GetConnection();
            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            string where = "WHERE tenant_code = @t ";

            if (filter.includeDeleted != true)
                where += " AND deleted = false ";
            if (filter.isreceiptno.HasValue)
            {
                where += " AND isreceiptno = @isrcpt ";
                p.Add("isrcpt", filter.isreceiptno);
            }
            if (filter.issampleno.HasValue)
            {
                where += " AND issampleno = @issample ";
                p.Add("issample", filter.issampleno);
            }
            if (!string.IsNullOrEmpty(filter.search))
            {
                where += " AND (name ILIKE @s OR shortname ILIKE @s) ";
                p.Add("s", $"%{filter.search}%");
            }

            int total = await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM billno_master {where}", p);

            int offset = (filter.page - 1) * filter.pagesize;
            p.Add("limit", filter.pagesize);
            p.Add("offset", offset);

            var rows = (await db.QueryAsync<HmsBillNoMaster>(
                $@"SELECT * FROM billno_master {where}
           ORDER BY isreceiptno ASC, issampleno ASC, bncode ASC
           LIMIT @limit OFFSET @offset", p)).ToList();

            var results = new List<BillNoMasterResponse>();
            foreach (var row in rows)
            {
                int inUse = await CountSequenceRows(row.bncode, tenantCode);
                results.Add(MapToResponse(row, inUse));
            }

            return (results, total);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<int> CountSequenceRows(decimal bncode, string tenantCode)
        {
            using var db = GetConnection();
            return await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM billno_sequence WHERE bncode = @bn AND tenant_code = @t",
                new { bn = bncode, t = tenantCode });
        }

        private BillNoMasterResponse MapToResponse(HmsBillNoMaster row, int inUse) => new()
        {
            bncode = row.bncode,
            name = row.name,
            shortname = row.shortname,
            orderno = row.orderno,
            bhcode = row.bhcode,
            cntcode = row.cntcode,
            isdefault = row.isdefault,
            allbranch = row.allbranch,
            allcounter = row.allcounter,
            restartfinancialyear = row.restartfinancialyear,
            restartcalendaryear = row.restartcalendaryear,
            restartmonthly = row.restartmonthly,
            restartdaily = row.restartdaily,
            issampleno = row.issampleno,
            isreceiptno = row.isreceiptno,
            deleted = row.deleted,
            tenant_code = row.tenant_code,
            entereddate = row.entereddate,
            sequence_rows_in_use = inUse
        };
        // ════════════════════════════════════════════════════════════════════════
        //  9. DEDICATED BILL UPDATE
        // ════════════════════════════════════════════════════════════════════════

        public async Task<(string status, UpdateHmsBillResponse? data)> UpdateBillDedicated(
    UpdateHmsBillRequest req, string tenantCode)
        {
            // ── Validations ───────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(req.requestguid))
                return ("requestguid is required for update.", null);
            if (string.IsNullOrWhiteSpace(req.patient_name))
                return ("Patient name is required.", null);
            if (req.items == null || !req.items.Any())
                return ("At least one line item is required.", null);

            foreach (var item in req.items)
            {
                if (string.IsNullOrWhiteSpace(item.item_name) && !item.tcode.HasValue)
                    return ("Each item must have either item_name or tcode.", null);
                if ((item.amount ?? 0) < 0)
                    return ("Item amount cannot be negative.", null);
            }

            bool wantsAdvanceRevert = req.revert_all_advance == true || (req.revert_advance_amount ?? 0) > 0.01;
            HmsAdvanceRevertResult? revertResult = null;

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // ── Fetch existing bill (lock row) ────────────────────────────
                var existing = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                    @"SELECT * FROM lab_request_master
               WHERE requestguid = @rg AND tenant_code = @t
               FOR UPDATE",
                    new { rg = req.requestguid, t = tenantCode }, tx);

                if (existing == null)
                    return ("Bill not found.", null);
                if (existing.isdeleted == true || existing.deleted == true)
                    return ("Cannot update a cancelled bill.", null);

                // ── Recalculate amounts ───────────────────────────────────────
                double lineGrossTotal = req.items.Sum(x => x.amount ?? 0);
                double aggregateDiscount = (req.discountamount ?? 0) + (req.specialdiscount ?? 0) + (req.ourdiscount ?? 0);
                double netAmount = Math.Max(lineGrossTotal - aggregateDiscount, 0);

                // ── Apply changes to master ───────────────────────────────────
                existing.custid = req.custid ?? existing.custid;
                existing.name = req.patient_name ?? existing.name;
                existing.gender = req.gender ?? existing.gender;
                existing.dateofbirth = req.dateofbirth ?? existing.dateofbirth;
                existing.ageyears = req.ageyears ?? existing.ageyears;
                existing.agemonths = req.agemonths ?? existing.agemonths;
                existing.agedays = req.agedays ?? existing.agedays;
                existing.mobileno = req.mobileno ?? existing.mobileno;
                existing.address = req.address ?? existing.address;
                existing.areacode = req.areacode ?? existing.areacode;
                existing.dcode = req.dcode ?? existing.dcode;
                existing.consultantdcode = req.consultantdcode ?? existing.consultantdcode;
                existing.ftcode = req.ftcode ?? existing.ftcode;
                existing.pmcode = req.pmcode ?? existing.pmcode;
                existing.ctcode = req.ctcode ?? existing.ctcode;
                existing.ricode = req.ricode ?? existing.ricode;
                existing.discountper = req.discountper ?? existing.discountper;
                existing.discountamount = req.discountamount ?? existing.discountamount;
                existing.specialdiscount = req.specialdiscount ?? existing.specialdiscount;
                existing.ourdispercentage = req.ourdispercentage ?? existing.ourdispercentage;
                existing.ourdiscount = req.ourdiscount ?? existing.ourdiscount;
                existing.pmc1 = req.pmc1 ?? existing.pmc1;
                existing.pmc2 = req.pmc2 ?? existing.pmc2;
                existing.pmc3 = req.pmc3 ?? existing.pmc3;
                existing.pmc1_amount = req.pmc1_amount ?? existing.pmc1_amount;
                existing.pmc2_amount = req.pmc2_amount ?? existing.pmc2_amount;
                existing.pmc3_amount = req.pmc3_amount ?? existing.pmc3_amount;
                existing.iscashbill = req.iscashbill ?? existing.iscashbill;
                existing.iscreditbill = req.iscreditbill ?? existing.iscreditbill;
                existing.isinsurancepatient = req.isinsurancepatient ?? existing.isinsurancepatient;
                existing.policyno = req.policyno ?? existing.policyno;
                existing.authorisationno = req.authorisationno ?? existing.authorisationno;
                existing.concessionreason = req.concessionreason ?? existing.concessionreason;
                existing.card_refno = req.card_refno ?? existing.card_refno;
                existing.bank_app = req.bank_app ?? existing.bank_app;
                existing.sheet_id = req.sheet_id ?? existing.sheet_id;
                existing.opvisitid = req.op_id ?? existing.opvisitid;
                existing.ip_id = req.ip_id ?? existing.ip_id;
                existing.alteredbhcode = req.enteredbhcode ?? existing.alteredbhcode;
                existing.requestamount = lineGrossTotal;
                existing.totalamount = netAmount;

                await db.UpdateAsync(existing, tx);

                // ── Delete old detail lines and re-insert ─────────────────────
                await db.ExecuteAsync(
                    @"DELETE FROM lab_request_details
               WHERE requestguid = @rg AND tenant_code = @t",
                    new { rg = req.requestguid, t = tenantCode }, tx);

                int sno = 1;
                foreach (var line in req.items)
                {
                    await db.InsertAsync(new HmsLabRequestDetail
                    {
                        requestdetailsid = Guid.NewGuid().ToString(),
                        requestguid = req.requestguid,
                        testsno = sno++,
                        tcode = line.tcode,
                        chargetype = line.charge_type,
                        item_name = line.item_name,
                        item_ref_id = line.item_ref_id,
                        testrate = line.unit_rate,
                        standardprice = line.unit_rate,
                        testamount = line.amount,
                        discount = line.discount,
                        newamount = (line.amount ?? 0) - (line.discount ?? 0),
                        gstper = line.gst_per,
                        gstamount = ((line.amount ?? 0) - (line.discount ?? 0))
                                           * ((line.gst_per ?? 0) / 100.0),
                        qty = line.qty,
                        ttid = line.ttid,
                        resultstatus = false,
                        requeststatus = true,
                        isdeleted = false,
                        tenant_code = tenantCode
                    }, tx);
                }

                // ── OPTIONAL: revert wrongly-used advance into a paymode ──────
                // Same transaction: bill edit + advance revert commit or fail together.
                // pmc1/2/3 on the bill are NOT touched here (the request controls them,
                // exactly as in a normal bill edit) — only paidviareceipt is bumped.
                if (wantsAdvanceRevert)
                {
                    var (revErr, revRes) = await HmsAdvanceRevertHelper.RevertUsedAdvanceToPaymode(
                        db, tx, req.requestguid, tenantCode,
                        new HmsAdvanceRevertInput
                        {
                            amount = req.revert_all_advance == true ? null : req.revert_advance_amount,
                            pmcode = req.revert_pmcode ?? 0,
                            reference_no = req.revert_reference_no,
                            bank_name = req.revert_bank_name,
                            card_no = req.revert_card_no,
                            cheque_date = req.revert_cheque_date,
                            enteredbhcode = req.enteredbhcode,
                            cntcode = req.cntcode,
                            usercode = req.usercode,
                            computercode = req.computercode,
                            reason = req.revert_reason
                        },
                        updatePmcOnBill: false);

                    if (revErr != null)
                        return (revErr, null);   // tx disposed un-committed => full rollback

                    revertResult = revRes;
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "UpdateBillDedicated failed for requestguid={rg}", req.requestguid);
                return ($"Transaction error: {ex.Message}", null);
            }

            // ── Fetch and return updated bill (commit already succeeded) ──────────
            try
            {
                var updated = await FetchBillRecordByGuid(req.requestguid, tenantCode);

                return ("SUCCESS", new UpdateHmsBillResponse
                {
                    requestguid = updated?.requestguid ?? req.requestguid,
                    op_id = updated?.op_id,
                    ip_id = updated?.ip_id,
                    bill_no = updated?.bill_no,
                    barcode = updated?.barcode,
                    bill_date = updated?.bill_date,
                    custid = updated?.custid,
                    patient_name = updated?.patient_name,
                    gender = updated?.gender,
                    mobileno = updated?.mobileno,
                    ageyears = updated?.ageyears,
                    enteredbhcode = updated?.enteredbhcode,
                    cntcode = updated?.cntcode,
                    gross_amount = updated?.gross_amount,
                    discount_amount = updated?.discount_amount,
                    general_concession_per = updated?.general_concession_per,
                    general_concession_amount = updated?.general_concession_amount,
                    referral_concession_per = updated?.referral_concession_per,
                    referral_concession_amount = updated?.referral_concession_amount,
                    net_amount = updated?.net_amount,
                    paid_amount = updated?.paid_amount,
                    balance_amount = updated?.balance_amount,
                    is_settled = updated?.is_settled ?? false,
                    message = revertResult != null
                        ? $"Bill updated. Advance {revertResult.reverted_amount:F2} reverted to wallet and collected via {revertResult.paymode}."
                        : "Bill updated successfully.",
                    advance_reverted_amount = revertResult?.reverted_amount,
                    revert_receipt_guid = revertResult?.receipt_guid,
                    revert_receipt_no = revertResult?.receipt_no,
                    advance_balance_after = revertResult?.advance_balance_after,
                    items = updated?.items ?? new()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateBillDedicated for {rg} committed successfully but post-commit fetch failed.", req.requestguid);
                return ("SUCCESS_FETCH_FAILED", new UpdateHmsBillResponse
                {
                    requestguid = req.requestguid,
                    advance_reverted_amount = revertResult?.reverted_amount,
                    revert_receipt_no = revertResult?.receipt_no,
                    message = "Bill updated successfully, but the response could not be reloaded. Please refresh."
                });
            }
        }
        public async Task<HmsCounterTimingDto?> GetActiveShiftByBranchCounter(int bhcode, int cntcode, string tenantCode)
        {
            using var db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<HmsCounterTimingDto>(
                @"SELECT c.*, b.name as counter_name,
                 CASE WHEN c.todate IS NULL THEN true ELSE false END as is_open,
                 CASE WHEN c.todate IS NOT NULL THEN true ELSE false END as is_closed
          FROM counter_timing c
          LEFT JOIN billno_master b ON c.cntcode = b.cntcode AND c.tenant_code = b.tenant_code
          WHERE c.bhcode = @bhcode AND c.cntcode = @cntcode AND c.todate IS NULL
            AND (c.planned_to IS NULL OR c.planned_to > @now)
            AND c.tenant_code = @tenantCode
          LIMIT 1", new { bhcode, cntcode, tenantCode, now = DateTime.UtcNow });
        }
        // ════════════════════════════════════════════════════════════════════════
        //  AUTO-BILL AN OP VISIT — pulls all pending unbilled charges for the
        //  visit (consultation + any additional SERVICE/DRESSING items) and
        //  raises a single bill from them via the normal SaveBill/CreateBill path.
        //  Used by ComboRegistration when auto_route_billing_after_register = true.
        // ════════════════════════════════════════════════════════════════════════
        public async Task<(string status, HmsBillResponse? data)> AutoBillOpVisit(
            string op_id, decimal custid, int dcode,
            int? enteredbhcode, int? cntcode, string tenantCode)
        {
            try
            {
                // 1. Pull everything still pending for this visit
                var pending = await _unbilledCls.GetUnbilledByVisit(op_id, null, tenantCode);

                if (pending == null || !pending.Any())
                    return ("No unbilled charges found for this OP visit.", null);

                using var db = GetConnection();

                // 2. Patient + doctor display info for the bill header
                var patient = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT name, mobile, gender, dateofbirth, ageyears, agemonths, agedays
              FROM customerdb.customer_master
              WHERE custid = @custid AND deleted = false",
                    new { custid });

                // 3. Map pending unbilled rows -> bill line items
                int sno = 1;
                var items = pending.Select(u => new HmsBillLineItemDto
                {
                    sno = sno++,
                    charge_type = string.IsNullOrWhiteSpace(u.entrytype) ? "CONSULTATION" : u.entrytype!,
                    item_name = u.particulars,
                    tcode = u.tcode.HasValue ? (decimal?)u.tcode.Value : null,
                    unit_rate = u.rate,
                    amount = u.amount,
                    qty = u.quantity ?? 1,
                    discount = u.discount,
                    gst_per = 0
                }).ToList();

                var billReq = new CreateHmsBillRequest
                {
                    op_id = op_id,
                    custid = custid,
                    patient_name = (string?)patient?.name ?? "Unknown",
                    gender = (string?)patient?.gender,
                    dateofbirth = patient?.dateofbirth?.ToString(),
                    ageyears = patient?.ageyears?.ToString(),
                    agemonths = patient?.agemonths?.ToString(),
                    agedays = patient?.agedays?.ToString(),
                    mobileno = (string?)patient?.mobile,
                    dcode = dcode,
                    enteredbhcode = enteredbhcode,
                    cntcode = cntcode,
                    iscashbill = true,
                    iscreditbill = false,
                    collection_type = "CASH",
                    paidamount = 0,
                    items = items,
                    unbilled_charge_ids = pending
                        .Where(u => !string.IsNullOrWhiteSpace(u.unbilledid))
                        .Select(u => u.unbilledid!)
                        .ToList()
                };

                // 4. Reuse the existing create-bill pipeline (numbering, receipt, tx, etc.)
                return await SaveBill(billReq, tenantCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoBillOpVisit failed for op_id={op_id}", op_id);
                return ($"AutoBillOpVisit error: {ex.Message}", null);
            }
        }
        private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        private static DateTime ComputePlannedEndUtc(DateTime startUtc, string? mode, string? endTime)
        {
            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), Tz);
            var midnight = startLocal.Date.AddDays(1);
            var endLocal = midnight;   // default: midnight (Variable, or Fixed with no valid time)

            if (string.Equals(mode, "FIXED", StringComparison.OrdinalIgnoreCase)
                && TimeSpan.TryParse(endTime, out var t))
            {
                var configured = startLocal.Date + t;
                if (configured > startLocal && configured < midnight) endLocal = configured;
            }
            return TimeZoneInfo.ConvertTimeToUtc(endLocal, Tz);
        }
        // ════════════════════════════════════════════════════════════════════════
        //  AUTO-BILL BY OP ID — standalone trigger. Looks up custid/dcode from
        //  op_registration, then reuses AutoBillOpVisit (same flow as
        //  ComboRegistration). Does NOT check any lab_settings flag.
        // ════════════════════════════════════════════════════════════════════════
        public async Task<(string status, HmsBillResponse? data)> AutoBillOpByOpId(
            AutoBillOpRequest req, string tenantCode)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.op_id) || !Guid.TryParse(req.op_id, out var opGuid))
                return ("A valid op_id is required.", null);

            if (!req.enteredbhcode.HasValue || !req.cntcode.HasValue)
                return ("enteredbhcode and cntcode are required to bill into a cash counter.", null);

            try
            {
                using var db = GetConnection();

                var op = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT op_id, custid, dcode, visit_status
              FROM op_registration
              WHERE op_id = @op_id AND tenant_code = @tenantCode AND isdeleted = false",
                    new { op_id = opGuid, tenantCode });

                if (op == null)
                    return ("OP Registration not found.", null);

                string visitStatus = (string?)op.visit_status ?? "";
                if (visitStatus == "CANCELLED" || visitStatus == "TRANSFERRED" || visitStatus == "RESCHEDULED")
                    return ($"Cannot bill a {visitStatus.ToLower()} visit.", null);

                // Same pipeline ComboRegistration uses (pending unbilled -> bill -> mark billed)
                return await AutoBillOpVisit(
                    opGuid.ToString(),
                    (decimal)op.custid,
                    (int)op.dcode,
                    req.enteredbhcode,
                    req.cntcode,
                    tenantCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoBillOpByOpId failed for op_id={op_id}", req.op_id);
                return ($"AutoBillOpByOpId error: {ex.Message}", null);
            }
        }
        public static class HmsOpCategories
        {
            public const string Consultation = "CONSULTATION";
            public const string Dressing = "DRESSING";
            public const string Observation = "OBSERVATION";
            public const string Service = "SERVICE";
            public const string Physio = "PHYSIO";
            public const string Lab = "LAB";
            public const string Scan = "SCAN";
            public const string Ecg = "ECG";
            public const string Echo = "ECHO";
            public const string Ip = "IP";
            public const string Pharmacy = "PHARMACY";

            // Display order in the report (same as before)
            public static readonly string[] Order =
            {
        Consultation, Dressing, Observation, Service, Physio, Lab, Scan, Ecg, Echo, Ip, Pharmacy
    };

            // Shown only when at least one item exists
            public static readonly HashSet<string> Optional = new() { Echo };
        }
        public async Task<HmsPayModeCollectionResponse> GetPayModeCollectionReport(
     DateTime fromdate, DateTime todate, int? bhcode, int? cntcode, string tenantCode)
        {
            using var db = GetConnection();

            var p = new DynamicParameters();
            p.Add("t", tenantCode);
            p.Add("from", fromdate.Date);
            p.Add("to", todate.Date.AddDays(1));   // exclusive upper bound

            string extra = "";
            string extraRm = "";
            if (bhcode.HasValue) { extra += " AND bcb.bhcode = @bh "; extraRm += " AND rm.enteredbhcode = @bh "; p.Add("bh", bhcode.Value); }
            if (cntcode.HasValue) { extra += " AND bcb.cntcode = @cnt "; extraRm += " AND rm.cntcode = @cnt "; p.Add("cnt", cntcode.Value); }

            // Money-in rows only. ADVANCE usage rows are excluded (already counted at deposit time).
            var details = (await db.QueryAsync<HmsPayModeSourceRow>($@"
        SELECT
            CASE WHEN rm.receipttype = 'DUE'     THEN 'DUE'
                 WHEN rm.receipttype = 'ADVANCE' THEN 'ADVANCE'
                 ELSE 'BILL' END                                              AS source,
            UPPER(COALESCE(NULLIF(bcb.collection_type, ''), 'UNSPECIFIED'))   AS paymode,
            COUNT(DISTINCT bcb.receipt_guid)::int                             AS receipt_count,
            COUNT(DISTINCT bcb.request_guid)::int                             AS bill_count,
            COALESCE(SUM(bcb.collectedamount), 0)::double precision           AS total_collected
        FROM balancecollectionby bcb
        INNER JOIN receipt_master rm
                ON rm.receiptguid = bcb.receipt_guid
               AND rm.tenant_code = bcb.tenant_code
        LEFT JOIN lab_request_master m
               ON m.requestguid = bcb.request_guid
              AND m.tenant_code = bcb.tenant_code
        WHERE bcb.tenant_code = @t
          AND COALESCE(bcb.deleted, false)  = false
          AND COALESCE(rm.isdeleted, false) = false
          AND COALESCE(rm.deleted, false)   = false
          AND UPPER(COALESCE(bcb.collection_type, '')) <> 'ADVANCE'
          AND (bcb.request_guid IS NULL
               OR (m.bill_category = 'HMS'
                   AND COALESCE(m.isdeleted, false) = false
                   AND COALESCE(m.deleted, false)   = false))
          AND COALESCE(bcb.collected_date, bcb.entereddate) >= @from
          AND COALESCE(bcb.collected_date, bcb.entereddate) <  @to
          {extra}
        GROUP BY 1, 2
        ORDER BY 2, 1", p)).ToList();

            // Advance refunds (no balancecollectionby row is written for refunds)
            double refunded = await db.ExecuteScalarAsync<double>($@"
        SELECT COALESCE(SUM(rm.amountpaid), 0)::double precision
        FROM receipt_master rm
        WHERE rm.tenant_code = @t
          AND rm.receipttype = 'ADVANCE_REFUND'
          AND COALESCE(rm.isdeleted, false) = false
          AND rm.receiptdate >= @from AND rm.receiptdate < @to
          {extraRm}", p);

            var byPaymode = details
                .GroupBy(d => d.paymode)
                .Select(g => new HmsPayModeTotalRow
                {
                    paymode = g.Key,
                    bill_amount = Math.Round(g.Where(x => x.source == "BILL").Sum(x => x.total_collected), 2),
                    due_amount = Math.Round(g.Where(x => x.source == "DUE").Sum(x => x.total_collected), 2),
                    advance_amount = Math.Round(g.Where(x => x.source == "ADVANCE").Sum(x => x.total_collected), 2),
                    total = Math.Round(g.Sum(x => x.total_collected), 2)
                })
                .OrderByDescending(x => x.total)
                .ToList();

            double bill = byPaymode.Sum(x => x.bill_amount);
            double due = byPaymode.Sum(x => x.due_amount);
            double adv = byPaymode.Sum(x => x.advance_amount);
            double grand = Math.Round(bill + due + adv, 2);

            return new HmsPayModeCollectionResponse
            {
                fromdate = fromdate.Date,
                todate = todate.Date,
                bhcode = bhcode,
                cntcode = cntcode,
                bill_total = Math.Round(bill, 2),
                due_total = Math.Round(due, 2),
                advance_total = Math.Round(adv, 2),
                grand_total = grand,
                advance_refunded = Math.Round(refunded, 2),
                net_total = Math.Round(grand - refunded, 2),
                by_paymode = byPaymode,
                details = details
            };
        }
        public async Task<HmsOpCategoryCollectionResponse> GetOpCategoryCollectionReport(
DateTime fromdate, DateTime todate, int? bhcode, int? cntcode, int? dcode, string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(tenantCode))
                throw new ArgumentException("tenant_code is required.", nameof(tenantCode));

            using var db = GetConnection();

            var p = new DynamicParameters();
            p.Add("t", tenantCode);

            // Bill-date window: IST day boundaries converted to UTC (bills are stored with DateTime.UtcNow)
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(fromdate.Date, DateTimeKind.Unspecified), Tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(todate.Date.AddDays(1), DateTimeKind.Unspecified), Tz);
            p.Add("fromUtc", fromUtc);
            p.Add("toUtc", toUtc);

            string extra = "";
            if (bhcode.HasValue) { extra += " AND lrm.enteredbhcode = @bh "; p.Add("bh", bhcode.Value); }
            if (cntcode.HasValue) { extra += " AND lrm.cntcode = @cnt "; p.Add("cnt", cntcode.Value); }
            if (dcode.HasValue) { extra += " AND op.dcode = @dcode "; p.Add("dcode", dcode.Value); }

            string sql = $@"
    WITH base AS (
        SELECT
            COALESCE(op.op_id::text, 'BILL-' || lrm.requestguid) AS op_id,
            COALESCE(op.op_no, 'NO VISIT')                   AS op_no,
            op.token_no,
            COALESCE(op.visit_date::timestamp, lrm.requestdatetime::timestamp) AS visit_date,
            lrm.name                                         AS patient_name,
            lrm.custid                                       AS custid,
            lrm.requestguid                                  AS requestguid,
            lrm.requestsnoprint                              AS bill_no,
            COALESCE(lrm.totalamount, 0)                     AS bill_total,
            COALESCE(lrm.paidamount, 0)                      AS bill_paid,
            UPPER(COALESCE(lrd.chargetype, ''))              AS chargetype,
            COALESCE(NULLIF(lrd.item_name, ''), tm.name, '') AS item_name,
            COALESCE(tm.name, '')                            AS test_name,
            COALESCE(lrm.requestamount, 0)                   AS bill_gross,
            (COALESCE(lrm.discountamount, 0) + COALESCE(lrm.specialdiscount, 0) + COALESCE(lrm.ourdiscount, 0)) AS bill_discount,
            COALESCE(lrd.discount, 0)                        AS line_discount,
            GREATEST(
                COALESCE(lrd.testamount, 0),
                COALESCE(lrd.newamount, 0) + COALESCE(lrd.discount, 0),
                (COALESCE(lrd.testrate, 0) * COALESCE(lrd.qty, 1))
            )                                                AS line_gross,
            COALESCE(lrd.newamount, lrd.testamount, 0)       AS line_net,
            COALESCE(gm.name, '')                            AS groupname,
            COALESCE(gm.isscan, false)                       AS isscan,
            COALESCE(gm.islab, false)                        AS islab,
            svc.service_id,
            svc.service_name,
            COALESCE(op.is_dressing, false)                  AS op_is_dressing,
            op.service_id                                    AS op_service_id,
            COALESCE(op.visit_type, '')                      AS op_visit_type,
            COALESCE(lrd.tcode, tm.tcode)                    AS tcode,
            SUM(COALESCE(lrd.newamount, lrd.testamount, 0))
                OVER (PARTITION BY lrm.requestguid)          AS bill_lines_total,
            SUM(COALESCE(lrd.discount, 0))
                OVER (PARTITION BY lrm.requestguid)          AS bill_lines_discount_sum,
            SUM(GREATEST(
                COALESCE(lrd.testamount, 0),
                COALESCE(lrd.newamount, 0) + COALESCE(lrd.discount, 0),
                (COALESCE(lrd.testrate, 0) * COALESCE(lrd.qty, 1))
            ))  OVER (PARTITION BY lrm.requestguid)          AS bill_lines_gross_sum
        FROM lab_request_master lrm
        LEFT JOIN op_registration op
                ON op.op_id::text   = COALESCE(NULLIF(lrm.opvisitid, ''), NULLIF(lrm.sheet_id, ''))
               AND op.tenant_code   = lrm.tenant_code
               AND COALESCE(op.isdeleted, false) = false
        INNER JOIN lab_request_details lrd
                ON lrd.requestguid  = lrm.requestguid
               AND COALESCE(lrd.isdeleted, false) = false
        LEFT JOIN test_master tm
                ON tm.tcode = lrd.tcode AND tm.tenant_code = lrm.tenant_code
        LEFT JOIN group_master gm
                ON gm.gcode = tm.gcode AND gm.tenant_code = lrm.tenant_code
        LEFT JOIN LATERAL (
            SELECT s.service_id, s.service_name
              FROM service_type_master s
             WHERE s.tenant_code = lrm.tenant_code
               AND COALESCE(s.deleted, false) = false
               AND LOWER(TRIM(s.service_name)) = LOWER(TRIM(COALESCE(NULLIF(lrd.item_name, ''), tm.name, '')))
             LIMIT 1
        ) svc ON true
        WHERE lrm.tenant_code = @t
          AND lrm.bill_category = 'HMS'
          AND COALESCE(lrm.isdeleted, false) = false
          AND COALESCE(lrm.deleted, false)   = false
          AND lrm.requestdatetime >= @fromUtc AND lrm.requestdatetime < @toUtc
          -- STRICTLY EXCLUDE IP BILLS SO THEY DON'T GET MIXED INTO OP
          AND lrm.ip_id IS NULL
          AND (lrm.sheet_id IS NULL OR (lrm.sheet_id NOT ILIKE 'IP%' AND NOT EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = lrm.tenant_code AND (ipr.ip_id::text = lrm.sheet_id OR ipr.ip_no = lrm.sheet_id))))
          AND (lrm.opvisitid IS NULL OR (lrm.opvisitid NOT ILIKE 'IP%' AND NOT EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = lrm.tenant_code AND (ipr.ip_id::text = lrm.opvisitid OR ipr.ip_no = lrm.opvisitid))))
          AND NOT EXISTS (SELECT 1 FROM unbilledcharges uc WHERE uc.tenant_code = lrm.tenant_code AND uc.billid = lrm.requestguid AND uc.ip_id IS NOT NULL)
          AND UPPER(COALESCE(lrm.billtype, '')) NOT LIKE '%IP%'
          {extra}
    ),
    cat AS (
        SELECT b.*,
            CASE
                -- OBSERVATION: chargetype OBS*, or item name that STARTS with observation
                WHEN b.chargetype LIKE 'OBS%'
                     OR LOWER(TRIM(b.item_name)) LIKE 'observation%'
                     OR LOWER(TRIM(b.item_name)) = 'obs'                                THEN 'OBSERVATION'

                -- SERVICE (by service master name, only when the line is not a test_master item)
                WHEN b.service_id IS NOT NULL AND b.tcode IS NULL                          THEN 'SERVICE'

                -- DRESSING: based on the item / test master name only (not the visit type)
                WHEN b.chargetype LIKE 'DRESS%'
                     OR LOWER(TRIM(b.item_name)) ~ '\mdress'
                     OR LOWER(TRIM(b.test_name)) ~ '\mdress'                            THEN 'DRESSING'

                -- PHYSIO / ECG / ECHO: chargetype, group, or item name that STARTS with the word
                WHEN b.chargetype IN ('PHYSIO', 'PHYSIOTHERAPY', 'FISHO')
                     OR LOWER(b.groupname) ~ 'physio|fisho'
                     OR LOWER(TRIM(b.item_name)) ~ '^(physio|fisho)'                    THEN 'PHYSIO'

                WHEN b.chargetype = 'ECG'
                     OR LOWER(b.groupname) ~ 'ecg|electrocardio'
                     OR LOWER(TRIM(b.item_name)) ~ '^(ecg|electrocardio)'               THEN 'ECG'

                WHEN b.chargetype = 'ECHO'
                     OR LOWER(b.groupname) ~ 'echo|doppler'
                     OR LOWER(TRIM(b.item_name)) ~ '^(echo|doppler)'                    THEN 'ECHO'

                -- SCAN: isscan flag, chargetype or group only
                WHEN b.isscan
                     OR b.chargetype = 'SCAN'
                     OR LOWER(b.groupname)
                        ~ 'x-?ray|radio|scan|usg|ultrasound|\m(ct|mri)\M'               THEN 'SCAN'

                -- SERVICE: matched in service_type_master OR chargetype SERVICE
                WHEN b.service_id IS NOT NULL
                     OR b.chargetype = 'SERVICE'
                     OR LOWER(TRIM(b.item_name)) LIKE 'service%'                        THEN 'SERVICE'

                -- CONSULTATION: doctor consultation fee (evaluated before Lab so consultation items with tcode/group are properly matched)
                WHEN b.chargetype = 'CONSULTATION'
                     OR b.chargetype LIKE '%CONSULT%'
                     OR LOWER(TRIM(b.item_name)) LIKE '%consultation%'
                     OR LOWER(TRIM(b.test_name)) LIKE '%consultation%'
                     OR LOWER(TRIM(b.item_name)) LIKE 'dr.%'
                     OR LOWER(TRIM(b.item_name)) LIKE 'dr %'
                     OR LOWER(TRIM(b.item_name)) LIKE '%doctor%'
                     OR (b.service_id IS NULL
                         AND NOT b.isscan
                         AND NOT b.islab
                         AND (b.chargetype IN ('CONSULTATION', 'OP', '') OR b.chargetype IS NULL)
                         AND (LOWER(b.op_visit_type) IN ('newvisit', 'consultation', 'followup', 'review', 'routine', 'waiting', 'in_consultation', 'completed') OR b.op_visit_type = ''))
                                                                                        THEN 'CONSULTATION'

                -- LAB: lab tests / investigations
                WHEN b.islab
                     OR b.chargetype IN ('LAB', 'INVESTIGATION', 'TEST')
                     OR (b.tcode IS NOT NULL AND b.groupname != '')
                     OR (b.groupname != '' AND NOT b.isscan)
                     OR LOWER(TRIM(b.item_name)) ~ 'test|profile|panel|culture|urine|blood|serum|stool|cbc|esr|smear|biopsy|widal|dengue'
                     OR b.tcode IS NOT NULL
                                                                                        THEN 'LAB'

                ELSE 'SERVICE'
            END AS category,
            CASE WHEN b.bill_lines_total > 0 THEN b.line_net / b.bill_lines_total
                 WHEN b.bill_lines_gross_sum > 0 THEN b.line_gross / b.bill_lines_gross_sum
                 ELSE 0 END AS ratio,
            GREATEST(
                b.bill_discount,
                b.bill_lines_discount_sum,
                b.bill_gross - b.bill_total,
                b.bill_lines_gross_sum - b.bill_total,
                0
            ) AS actual_bill_discount
        FROM base b
    )
    SELECT op_id, op_no, token_no, visit_date, patient_name, custid, requestguid, bill_no,
           item_name, service_id, service_name, category,
           GREATEST(
               line_gross,
               (bill_total * ratio) + GREATEST(line_discount, actual_bill_discount * ratio)
           )::float8                                                      AS gross,
           GREATEST(line_discount, (actual_bill_discount * ratio))::float8 AS discount,
           (bill_total * ratio)::float8  AS net,
           (bill_paid  * ratio)::float8  AS paid,
           ratio::float8                 AS ratio
    FROM cat
    ORDER BY visit_date, op_no";

            var lines = (await db.QueryAsync<HmsOpCategoryLine>(sql, p, commandTimeout: 60)).ToList();

            // â”€â”€ Payments per bill per paymode â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var billGuids = lines.Select(l => l.requestguid).Distinct().ToArray();
            var payRows = billGuids.Length == 0
                ? new List<HmsOpBillPayRow>()
                : (await db.QueryAsync<HmsOpBillPayRow>(@"
            SELECT
                bcb.request_guid AS requestguid,
                CASE WHEN UPPER(COALESCE(bcb.collection_type, '')) = 'ADVANCE'
                     THEN 'ADVANCE ADJUSTED'
                     ELSE UPPER(COALESCE(NULLIF(TRIM(pm.name), ''),
                                         NULLIF(TRIM(bcb.collection_type), ''),
                                         'UNSPECIFIED')) END AS paymode,
                MAX(CASE WHEN UPPER(COALESCE(bcb.collection_type, '')) = 'ADVANCE'
                         THEN NULL ELSE bcb.pmcode END)      AS pmcode,
                COALESCE(SUM(bcb.collectedamount), 0)::float8 AS amount
            FROM balancecollectionby bcb
            LEFT JOIN paymode_master pm
                   ON pm.pmcode = bcb.pmcode
                  AND pm.tenant_code = bcb.tenant_code
                  AND COALESCE(pm.deleted, false) = false
            WHERE bcb.tenant_code = @t
              AND COALESCE(bcb.deleted, false) = false
              AND bcb.request_guid = ANY(@guids)
            GROUP BY 1, 2",
                    new { t = tenantCode, guids = billGuids })).ToList();

            var payByBill = payRows.ToLookup(x => x.requestguid);
            var billPaid = lines.GroupBy(l => l.requestguid).ToDictionary(g => g.Key, g => g.Sum(x => x.paid));
            var billKnown = payRows.GroupBy(x => x.requestguid).ToDictionary(g => g.Key, g => g.Sum(x => x.amount));

            double R(double v) => Math.Round(v, 2);

            IEnumerable<(string paymode, decimal? pmcode, double amount)> Split(HmsOpCategoryLine l)
            {
                double paidTotal = billPaid.TryGetValue(l.requestguid, out var pt) ? pt : 0;
                double known = billKnown.TryGetValue(l.requestguid, out var k) ? k : 0;
                double scale = (known > paidTotal + 0.01 && known > 0) ? paidTotal / known : 1.0;

                foreach (var pr in payByBill[l.requestguid])
                    yield return (pr.paymode, pr.pmcode, pr.amount * scale * l.ratio);

                double unspecified = paidTotal - known * scale;
                if (unspecified > 0.01)
                    yield return ("UNSPECIFIED", null, unspecified * l.ratio);
            }

            List<HmsOpPaymodeAmount> Agg(IEnumerable<HmsOpCategoryLine> src) =>
                src.SelectMany(Split)
                   .GroupBy(x => x.paymode)
                   .Select(g => new HmsOpPaymodeAmount
                   {
                       paymode = g.Key,
                       pmcode = g.Select(x => x.pmcode).FirstOrDefault(x => x.HasValue),
                       amount = R(g.Sum(x => x.amount))
                   })
                   .Where(x => x.amount > 0.004)
                   .OrderByDescending(x => x.amount)
                   .ToList();

            // â”€â”€ IP Category Row (All IP bills with IP number / ID + Unbilled IP) â”€â”€â”€â”€
            var ipCategoryRow = new HmsOpCategoryAmountRow { category = HmsOpCategories.Ip };
            try
            {
                string ipExtra = "";
                var ipParams = new DynamicParameters();
                ipParams.Add("t", tenantCode);
                ipParams.Add("fromUtc", fromUtc);
                ipParams.Add("toUtc", toUtc);
                if (bhcode.HasValue) { ipExtra += " AND (lrm.enteredbhcode = @bh OR lrm.alteredbhcode = @bh) "; ipParams.Add("bh", bhcode.Value); }
                if (cntcode.HasValue) { ipExtra += " AND lrm.cntcode = @cnt "; ipParams.Add("cnt", cntcode.Value); }
                if (dcode.HasValue)
                {
                    ipExtra += @" AND (
                        lrm.dcode = @dcode 
                        OR lrm.consultantdcode = @dcode 
                        OR EXISTS (
                            SELECT 1 FROM ip_registration ipr 
                            WHERE ipr.tenant_code = @t 
                              AND (ipr.ip_id = lrm.ip_id OR ipr.ip_id::text = lrm.sheet_id OR ipr.ip_no = lrm.sheet_id OR ipr.ip_id::text = lrm.opvisitid OR ipr.ip_no = lrm.opvisitid) 
                              AND ipr.dcode = @dcode
                        )
                    ) ";
                    ipParams.Add("dcode", dcode.Value);
                }

                // 1. Billed IP: Matches ip_id, sheet_id with IP, opvisitid with IP, unbilledcharges link, or billtype IP
                string sqlIpBilled = $@"
                    SELECT 
                        COUNT(DISTINCT lrm.requestguid) AS bill_count,
                        COUNT(DISTINCT COALESCE(
                            lrm.ip_id::text, 
                            NULLIF(lrm.sheet_id, ''), 
                            NULLIF(lrm.opvisitid, ''), 
                            lrm.requestguid
                        )) AS visit_count,
                        COALESCE(SUM(COALESCE(lrm.requestamount, lrm.totalamount + ABS(COALESCE(lrm.discountamount, 0)) + ABS(COALESCE(lrm.specialdiscount, 0)) + ABS(COALESCE(lrm.ourdiscount, 0)))), 0)::float8 AS gross,
                        COALESCE(SUM(ABS(COALESCE(lrm.discountamount, 0)) + ABS(COALESCE(lrm.specialdiscount, 0)) + ABS(COALESCE(lrm.ourdiscount, 0))), 0)::float8 AS discount,
                        COALESCE(SUM(lrm.totalamount), 0)::float8 AS net,
                        COALESCE(SUM(lrm.paidamount), 0)::float8 AS paid
                    FROM lab_request_master lrm
                    WHERE lrm.tenant_code = @t
                      AND (
                          lrm.ip_id IS NOT NULL 
                          OR (lrm.sheet_id IS NOT NULL AND (
                              lrm.sheet_id ILIKE 'IP%' 
                              OR EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = @t AND (ipr.ip_id::text = lrm.sheet_id OR ipr.ip_no = lrm.sheet_id))
                          ))
                          OR (lrm.opvisitid IS NOT NULL AND (
                              lrm.opvisitid ILIKE 'IP%' 
                              OR EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = @t AND (ipr.ip_id::text = lrm.opvisitid OR ipr.ip_no = lrm.opvisitid))
                          ))
                          OR EXISTS (
                              SELECT 1 FROM unbilledcharges uc 
                              WHERE uc.tenant_code = @t AND uc.billid = lrm.requestguid AND uc.ip_id IS NOT NULL
                          )
                          OR UPPER(COALESCE(lrm.billtype, '')) LIKE '%IP%'
                      )
                      AND COALESCE(lrm.isdeleted, false) = false
                      AND COALESCE(lrm.deleted, false) = false
                      AND lrm.requestdatetime >= @fromUtc AND lrm.requestdatetime < @toUtc
                      {ipExtra}";

                var billedIp = await db.QueryFirstOrDefaultAsync<dynamic>(sqlIpBilled, ipParams);

                // Billed items count
                string sqlIpItems = $@"
                    SELECT COUNT(1)
                    FROM lab_request_details lrd
                    INNER JOIN lab_request_master lrm ON lrm.requestguid = lrd.requestguid
                    WHERE lrm.tenant_code = @t
                      AND (
                          lrm.ip_id IS NOT NULL 
                          OR (lrm.sheet_id IS NOT NULL AND (
                              lrm.sheet_id ILIKE 'IP%' 
                              OR EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = @t AND (ipr.ip_id::text = lrm.sheet_id OR ipr.ip_no = lrm.sheet_id))
                          ))
                          OR (lrm.opvisitid IS NOT NULL AND (
                              lrm.opvisitid ILIKE 'IP%' 
                              OR EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = @t AND (ipr.ip_id::text = lrm.opvisitid OR ipr.ip_no = lrm.opvisitid))
                          ))
                          OR EXISTS (
                              SELECT 1 FROM unbilledcharges uc 
                              WHERE uc.tenant_code = @t AND uc.billid = lrm.requestguid AND uc.ip_id IS NOT NULL
                          )
                          OR UPPER(COALESCE(lrm.billtype, '')) LIKE '%IP%'
                      )
                      AND COALESCE(lrm.isdeleted, false) = false
                      AND COALESCE(lrm.deleted, false) = false
                      AND COALESCE(lrd.isdeleted, false) = false
                      AND lrm.requestdatetime >= @fromUtc AND lrm.requestdatetime < @toUtc
                      {ipExtra}";

                int billedItemsCount = await db.ExecuteScalarAsync<int>(sqlIpItems, ipParams);

                // Billed IP Paymodes
                string sqlIpPay = $@"
                    SELECT 
                        CASE WHEN UPPER(COALESCE(bcb.collection_type, '')) = 'ADVANCE'
                             THEN 'ADVANCE ADJUSTED'
                             ELSE UPPER(COALESCE(NULLIF(TRIM(pm.name), ''),
                                                 NULLIF(TRIM(bcb.collection_type), ''),
                                                 'UNSPECIFIED')) END AS paymode,
                        MAX(CASE WHEN UPPER(COALESCE(bcb.collection_type, '')) = 'ADVANCE' THEN NULL ELSE bcb.pmcode END) AS pmcode,
                        COALESCE(SUM(bcb.collectedamount), 0)::float8 AS amount
                    FROM balancecollectionby bcb
                    INNER JOIN lab_request_master lrm ON lrm.requestguid = bcb.request_guid
                    LEFT JOIN paymode_master pm ON pm.pmcode = bcb.pmcode AND pm.tenant_code = bcb.tenant_code AND COALESCE(pm.deleted, false) = false
                    WHERE bcb.tenant_code = @t
                      AND COALESCE(bcb.deleted, false) = false
                      AND (
                          lrm.ip_id IS NOT NULL 
                          OR (lrm.sheet_id IS NOT NULL AND (
                              lrm.sheet_id ILIKE 'IP%' 
                              OR EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = @t AND (ipr.ip_id::text = lrm.sheet_id OR ipr.ip_no = lrm.sheet_id))
                          ))
                          OR (lrm.opvisitid IS NOT NULL AND (
                              lrm.opvisitid ILIKE 'IP%' 
                              OR EXISTS (SELECT 1 FROM ip_registration ipr WHERE ipr.tenant_code = @t AND (ipr.ip_id::text = lrm.opvisitid OR ipr.ip_no = lrm.opvisitid))
                          ))
                          OR EXISTS (
                              SELECT 1 FROM unbilledcharges uc 
                              WHERE uc.tenant_code = @t AND uc.billid = lrm.requestguid AND uc.ip_id IS NOT NULL
                          )
                          OR UPPER(COALESCE(lrm.billtype, '')) LIKE '%IP%'
                      )
                      AND COALESCE(lrm.isdeleted, false) = false
                      AND COALESCE(lrm.deleted, false) = false
                      AND lrm.requestdatetime >= @fromUtc AND lrm.requestdatetime < @toUtc
                      {ipExtra}
                    GROUP BY 1
                    HAVING COALESCE(SUM(bcb.collectedamount), 0) > 0.004";

                var ipPaymodes = (await db.QueryAsync<HmsOpPaymodeAmount>(sqlIpPay, ipParams)).ToList();

                // 2. Unbilled IP charges from unbilledcharges (when counter not filtered)
                double unbilledGross = 0, unbilledDiscount = 0, unbilledNet = 0;
                int unbilledItems = 0, unbilledVisits = 0;
                if (!cntcode.HasValue)
                {
                    string sqlIpUnbilled = @"
                        SELECT 
                            COUNT(DISTINCT uc.ip_id) AS visit_count,
                            COUNT(1) AS item_count,
                            COALESCE(SUM(CASE WHEN COALESCE(uc.amount, 0) = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END), 0)::float8 AS gross,
                            COALESCE(SUM(ABS(COALESCE(uc.discount, 0))), 0)::float8 AS discount,
                            COALESCE(SUM((CASE WHEN COALESCE(uc.amount, 0) = 0 THEN COALESCE(uc.quantity, 1) * COALESCE(uc.rate, 0) ELSE uc.amount END) - ABS(COALESCE(uc.discount, 0))), 0)::float8 AS net
                        FROM unbilledcharges uc
                        WHERE uc.tenant_code = @t
                          AND uc.ip_id IS NOT NULL
                          AND (COALESCE(uc.billedstatus, false) = false OR uc.billid IS NULL)
                          AND COALESCE(uc.chargedate, uc.billeddate) >= @fromUtc AND COALESCE(uc.chargedate, uc.billeddate) < @toUtc";

                    var unbilledRes = await db.QueryFirstOrDefaultAsync<dynamic>(sqlIpUnbilled, ipParams);
                    if (unbilledRes != null)
                    {
                        unbilledVisits = (int)(unbilledRes.visit_count ?? 0);
                        unbilledItems = (int)(unbilledRes.item_count ?? 0);
                        unbilledGross = (double)(unbilledRes.gross ?? 0);
                        unbilledDiscount = (double)(unbilledRes.discount ?? 0);
                        unbilledNet = (double)(unbilledRes.net ?? 0);
                    }
                }

                int billedVisits = (int)(billedIp?.visit_count ?? 0);
                int billedBills = (int)(billedIp?.bill_count ?? 0);
                double billedGross = (double)(billedIp?.gross ?? 0);
                double billedDiscount = (double)(billedIp?.discount ?? 0);
                double billedNet = (double)(billedIp?.net ?? 0);
                double billedPaid = (double)(billedIp?.paid ?? 0);

                double ipNet = billedNet + unbilledNet;
                double ipGross = billedGross + unbilledGross;
                double ipDiscount = Math.Abs(billedDiscount) + Math.Abs(unbilledDiscount);
                double ipCollected = billedPaid;
                double ipDue = Math.Max(ipNet - ipCollected, 0);

                double knownIpPay = ipPaymodes.Sum(x => x.amount);
                if (ipCollected - knownIpPay > 0.01)
                {
                    ipPaymodes.Add(new HmsOpPaymodeAmount
                    {
                        paymode = "UNSPECIFIED",
                        amount = R(ipCollected - knownIpPay)
                    });
                }

                ipCategoryRow.visit_count = Math.Max(billedVisits, unbilledVisits);
                ipCategoryRow.bill_count = billedBills;
                ipCategoryRow.item_count = billedItemsCount + unbilledItems;
                ipCategoryRow.gross_amount = R(ipGross);
                ipCategoryRow.gross = R(ipGross);
                ipCategoryRow.discount_amount = R(ipDiscount);
                ipCategoryRow.discount = R(ipDiscount);
                ipCategoryRow.net_amount = R(ipNet);
                ipCategoryRow.net = R(ipNet);
                ipCategoryRow.collected_amount = R(ipCollected);
                ipCategoryRow.collected = R(ipCollected);
                ipCategoryRow.due_amount = R(ipDue);
                ipCategoryRow.due = R(ipDue);
                ipCategoryRow.paymodes = ipPaymodes;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute IP category row for tenant {Tenant}", tenantCode);
            }

            // â”€â”€ Pharmacy Category Row â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var pharmCategoryRow = new HmsOpCategoryAmountRow { category = HmsOpCategories.Pharmacy };
            try
            {
                var connStringsToTry = new List<string>();
                if (!string.IsNullOrWhiteSpace(_inventoryConn)) connStringsToTry.Add(_inventoryConn);
                if (!string.IsNullOrWhiteSpace(_conn) && !connStringsToTry.Contains(_conn)) connStringsToTry.Add(_conn);

                foreach (var connStr in connStringsToTry)
                {
                    try
                    {
                        using var invDb = new NpgsqlConnection(connStr);
                        invDb.Open();

                        bool hasSalesMaster = await invDb.ExecuteScalarAsync<bool>(@"
                            SELECT EXISTS (
                                SELECT 1 FROM information_schema.tables 
                                WHERE table_schema = 'public' AND table_name = 'sales_master'
                            )");
                        if (!hasSalesMaster) continue;

                        var pharmParams = new DynamicParameters();
                        pharmParams.Add("t", tenantCode);
                        pharmParams.Add("fromdate", fromdate.Date);
                        pharmParams.Add("todate", todate.Date.AddDays(1));
                        pharmParams.Add("fromUtc", fromUtc);
                        pharmParams.Add("toUtc", toUtc);

                        string pharmExtra = "";
                        if (bhcode.HasValue)
                        {
                            pharmExtra += " AND (sm.bh_code = @bh OR sm.branchcode = @bh::text OR sm.branchcode = @bhStr) ";
                            pharmParams.Add("bh", bhcode.Value);
                            pharmParams.Add("bhStr", bhcode.Value.ToString());
                        }

                        // Use ABS on discountamount so discount is always strictly positive
                        string sqlPharmacy = $@"
                            SELECT 
                                COUNT(DISTINCT sm.salescode) AS bill_count,
                                COUNT(DISTINCT COALESCE(NULLIF(sm.patientid, ''), NULLIF(sm.customercode::text, ''), sm.salescode::text)) AS visit_count,
                                COALESCE(SUM(COALESCE(sm.grossamount, sm.netamount + ABS(COALESCE(sm.discountamount, 0)))), 0)::float8 AS gross,
                                COALESCE(SUM(ABS(COALESCE(sm.discountamount, 0))), 0)::float8 AS discount,
                                COALESCE(SUM(COALESCE(sm.netamount, 0)), 0)::float8 AS net,
                                COALESCE(SUM(CASE WHEN UPPER(COALESCE(sm.paymentstatus, 'PAID')) NOT IN ('CREDIT', 'DUE', 'PENDING', 'UNPAID') THEN COALESCE(sm.netamount, 0) ELSE 0 END), 0)::float8 AS paid,
                                COALESCE(SUM(CASE WHEN UPPER(COALESCE(sm.paymentstatus, 'PAID')) IN ('CREDIT', 'DUE', 'PENDING', 'UNPAID') THEN COALESCE(sm.netamount, 0) ELSE 0 END), 0)::float8 AS due
                            FROM public.sales_master sm
                            WHERE COALESCE(sm.deleted, false) = false
                              AND (
                                  sm.tenantcode = @t 
                                  OR TRIM(sm.tenantcode) = TRIM(@t) 
                                  OR TRIM(LEADING '0' FROM sm.tenantcode) = TRIM(LEADING '0' FROM @t)
                              )
                              AND (
                                  (sm.billdate >= @fromdate AND sm.billdate < @todate)
                                  OR (COALESCE(sm.billdate, sm.createddate) >= @fromUtc AND COALESCE(sm.billdate, sm.createddate) < @toUtc)
                              )
                              {pharmExtra}";

                        var pharmRes = await invDb.QueryFirstOrDefaultAsync<dynamic>(sqlPharmacy, pharmParams);

                        if (pharmRes != null && (pharmRes.bill_count > 0 || pharmRes.net > 0))
                        {
                            int pharmItemsCount = 0;
                            try
                            {
                                bool hasSalesDetail = await invDb.ExecuteScalarAsync<bool>(@"
                                    SELECT EXISTS (
                                        SELECT 1 FROM information_schema.tables 
                                        WHERE table_schema = 'public' AND table_name = 'sales_detail'
                                    )");
                                if (hasSalesDetail)
                                {
                                    string sqlPharmItems = $@"
                                        SELECT COUNT(1)
                                        FROM public.sales_detail sd
                                        INNER JOIN public.sales_master sm ON sm.salescode = sd.salescode AND sm.tenantcode = sd.tenantcode
                                        WHERE COALESCE(sm.deleted, false) = false
                                          AND (
                                              sm.tenantcode = @t 
                                              OR TRIM(sm.tenantcode) = TRIM(@t) 
                                              OR TRIM(LEADING '0' FROM sm.tenantcode) = TRIM(LEADING '0' FROM @t)
                                          )
                                          AND (
                                              (sm.billdate >= @fromdate AND sm.billdate < @todate)
                                              OR (COALESCE(sm.billdate, sm.createddate) >= @fromUtc AND COALESCE(sm.billdate, sm.createddate) < @toUtc)
                                          )
                                          {pharmExtra}";
                                    pharmItemsCount = await invDb.ExecuteScalarAsync<int>(sqlPharmItems, pharmParams);
                                }
                            }
                            catch { }

                            var pharmPaymodes = new List<HmsOpPaymodeAmount>();
                            try
                            {
                                string sqlPharmPay = $@"
                                    SELECT 
                                        UPPER(COALESCE(NULLIF(TRIM(sm.paymentmode), ''), 'CASH')) AS paymode,
                                        COALESCE(SUM(CASE WHEN UPPER(COALESCE(sm.paymentstatus, 'PAID')) NOT IN ('CREDIT', 'DUE', 'PENDING', 'UNPAID') THEN COALESCE(sm.netamount, 0) ELSE 0 END), 0)::float8 AS amount
                                    FROM public.sales_master sm
                                    WHERE COALESCE(sm.deleted, false) = false
                                      AND (
                                          sm.tenantcode = @t 
                                          OR TRIM(sm.tenantcode) = TRIM(@t) 
                                          OR TRIM(LEADING '0' FROM sm.tenantcode) = TRIM(LEADING '0' FROM @t)
                                      )
                                      AND (
                                          (sm.billdate >= @fromdate AND sm.billdate < @todate)
                                          OR (COALESCE(sm.billdate, sm.createddate) >= @fromUtc AND COALESCE(sm.billdate, sm.createddate) < @toUtc)
                                      )
                                      {pharmExtra}
                                    GROUP BY 1
                                    HAVING COALESCE(SUM(CASE WHEN UPPER(COALESCE(sm.paymentstatus, 'PAID')) NOT IN ('CREDIT', 'DUE', 'PENDING', 'UNPAID') THEN COALESCE(sm.netamount, 0) ELSE 0 END), 0) > 0.004";

                                pharmPaymodes = (await invDb.QueryAsync<HmsOpPaymodeAmount>(sqlPharmPay, pharmParams)).ToList();
                            }
                            catch { }

                            int bCount = (int)(pharmRes.bill_count ?? 0);
                            int vCount = (int)(pharmRes.visit_count ?? 0);
                            double gAmt = Math.Abs((double)(pharmRes.gross ?? 0));
                            double dAmt = Math.Abs((double)(pharmRes.discount ?? 0));
                            double nAmt = Math.Abs((double)(pharmRes.net ?? 0));
                            double pAmt = (double)(pharmRes.paid ?? 0);
                            double dueAmt = (double)(pharmRes.due ?? 0);

                            // Calculate discount if zero but gross > net, or align gross
                            if (dAmt == 0 && gAmt > nAmt) dAmt = gAmt - nAmt;
                            if (gAmt < nAmt + dAmt) gAmt = nAmt + dAmt;
                            dAmt = Math.Abs(dAmt);

                            if (nAmt > 0 && pAmt == 0 && dueAmt == 0) pAmt = nAmt;
                            dueAmt = Math.Max(nAmt - pAmt, 0);

                            pharmCategoryRow.visit_count = vCount > 0 ? vCount : bCount;
                            pharmCategoryRow.bill_count = bCount;
                            pharmCategoryRow.item_count = pharmItemsCount > 0 ? pharmItemsCount : bCount;
                            pharmCategoryRow.gross_amount = R(gAmt);
                            pharmCategoryRow.gross = R(gAmt);
                            pharmCategoryRow.discount_amount = R(dAmt);
                            pharmCategoryRow.discount = R(dAmt);
                            pharmCategoryRow.net_amount = R(nAmt);
                            pharmCategoryRow.net = R(nAmt);
                            pharmCategoryRow.collected_amount = R(pAmt);
                            pharmCategoryRow.collected = R(pAmt);
                            pharmCategoryRow.due_amount = R(dueAmt);
                            pharmCategoryRow.due = R(dueAmt);
                            pharmCategoryRow.paymodes = pharmPaymodes;

                            break;
                        }
                    }
                    catch (Exception exConn)
                    {
                        _logger.LogWarning(exConn, "Attempt to query pharmacy from connection failed: {Msg}", exConn.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute Pharmacy category row for tenant {Tenant}", tenantCode);
            }

            // â”€â”€ Category summary (All breakdown categories including IP & Pharmacy) 
            var order = HmsOpCategories.Order;
            var optional = HmsOpCategories.Optional;

            var categories = order
                .Select(c =>
                {
                    if (c == HmsOpCategories.Ip)
                        return ipCategoryRow;
                    if (c == HmsOpCategories.Pharmacy)
                        return pharmCategoryRow;

                    var g = lines.Where(l => l.category == c).ToList();
                    double net = g.Sum(x => x.net), paid = g.Sum(x => x.paid), discount = Math.Abs(g.Sum(x => x.discount));
                    double gross = net + discount;
                    return new HmsOpCategoryAmountRow
                    {
                        category = c,
                        visit_count = g.Select(x => x.op_id).Distinct().Count(),
                        bill_count = g.Select(x => x.requestguid).Distinct().Count(),
                        item_count = g.Count,
                        gross_amount = R(gross),
                        gross = R(gross),
                        discount_amount = R(discount),
                        discount = R(discount),
                        net_amount = R(net),
                        net = R(net),
                        collected_amount = R(paid),
                        collected = R(paid),
                        due_amount = R(Math.Max(net - paid, 0)),
                        due = R(Math.Max(net - paid, 0)),
                        paymodes = Agg(g)
                    };
                })
                .Where(r => !optional.Contains(r.category) || r.item_count > 0 || r.net_amount > 0)
                .ToList();

            // â”€â”€ Per-service breakdown â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var services = lines
                .Where(l => l.category == "SERVICE")
                .GroupBy(l => new { l.service_id, name = l.service_name ?? l.item_name ?? "Service" })
                .Select(g =>
                {
                    double net = g.Sum(x => x.net), paid = g.Sum(x => x.paid), discount = Math.Abs(g.Sum(x => x.discount));
                    double gross = net + discount;
                    return new HmsOpServiceAmountRow
                    {
                        service_id = g.Key.service_id ?? g.Select(x => x.service_id).FirstOrDefault(x => x.HasValue),
                        service_name = g.Key.name,
                        visit_count = g.Select(x => x.op_id).Distinct().Count(),
                        item_count = g.Count(),
                        tokens = string.Join(", ", g.Select(x => x.token_no)
                                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                                    .Distinct().Take(50)),
                        gross_amount = R(gross),
                        gross = R(gross),
                        discount_amount = R(discount),
                        discount = R(discount),
                        net_amount = R(net),
                        net = R(net),
                        collected_amount = R(paid),
                        collected = R(paid),
                        due_amount = R(Math.Max(net - paid, 0)),
                        due = R(Math.Max(net - paid, 0)),
                        paymodes = Agg(g)
                    };
                })
                .OrderByDescending(s => s.net_amount)
                .ToList();

            // â”€â”€ Per-visit rows â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var visits = lines
                .GroupBy(l => l.op_id)
                .Select(g =>
                {
                    var first = g.First();
                    var row = new HmsOpVisitCategoryRow
                    {
                        op_id = g.Key,
                        op_no = first.op_no,
                        token_no = first.token_no,
                        visit_date = first.visit_date,
                        custid = first.custid,
                        patient_name = first.patient_name,
                        bill_nos = string.Join(", ", g.Select(x => x.bill_no).Where(x => x != null).Distinct())
                    };
                    foreach (var c in order)
                    {
                        var cl = g.Where(x => x.category == c).ToList();
                        if (cl.Count == 0 && optional.Contains(c)) continue;
                        row.net_by_category[c] = R(cl.Sum(x => x.net));
                        row.collected_by_category[c] = R(cl.Sum(x => x.paid));
                    }
                    foreach (var pm in Agg(g)) row.collected_by_paymode[pm.paymode] = pm.amount;
                    row.discount_amount = R(Math.Abs(g.Sum(x => x.discount)));
                    row.discount = row.discount_amount;
                    row.net_amount = R(g.Sum(x => x.net));
                    row.net = row.net_amount;
                    row.gross_amount = R(row.net_amount + row.discount_amount);
                    row.gross = row.gross_amount;
                    row.collected_amount = R(g.Sum(x => x.paid));
                    row.collected = row.collected_amount;
                    row.due_amount = R(Math.Max(row.net_amount - row.collected_amount, 0));
                    row.due = row.due_amount;
                    return row;
                })
                .OrderBy(v => v.visit_date).ThenBy(v => v.op_no)
                .ToList();

            // â”€â”€ Combined paymodes across OP, IP, and Pharmacy â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var allPaymodes = Agg(lines);
            var combinedPaymodes = allPaymodes
                .Concat(ipCategoryRow.paymodes)
                .Concat(pharmCategoryRow.paymodes)
                .GroupBy(p => p.paymode, StringComparer.OrdinalIgnoreCase)
                .Select(g => new HmsOpPaymodeAmount
                {
                    paymode = g.Key,
                    pmcode = g.Select(x => x.pmcode).FirstOrDefault(x => x.HasValue),
                    amount = R(g.Sum(x => x.amount))
                })
                .Where(x => x.amount > 0.004)
                .OrderByDescending(x => x.amount)
                .ToList();

            // â”€â”€ Totals across all active categories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            double totalGross = categories.Sum(c => c.gross_amount);
            double totalDiscount = categories.Sum(c => c.discount_amount);
            double totalNet = categories.Sum(c => c.net_amount);
            double totalCollected = categories.Sum(c => c.collected_amount);
            double totalDue = categories.Sum(c => c.due_amount);

            return new HmsOpCategoryCollectionResponse
            {
                fromdate = fromdate.Date,
                todate = todate.Date,
                bhcode = bhcode,
                cntcode = cntcode,
                dcode = dcode,
                total_visits = visits.Count,
                total_gross = R(totalGross),
                total_discount = R(totalDiscount),
                total_net = R(totalNet),
                total_collected = R(totalCollected),
                total_due = R(totalDue),
                total_ip_cost = ipCategoryRow.net_amount,
                total_pharmacy_cost = pharmCategoryRow.net_amount,
                paymode_totals = combinedPaymodes,
                categories = categories,
                services = services,
                visits = visits
            };
        }
    }
}