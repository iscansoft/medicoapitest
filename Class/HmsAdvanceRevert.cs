using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;

namespace medico_backend.Class
{
    /// <summary>
    /// Reverts advance that was used against a bill and re-collects the same
    /// amount through a real paymode. Runs inside the CALLER's transaction —
    /// caller commits / rolls back.
    /// </summary>
    public static class HmsAdvanceRevertHelper
    {
        public static async Task<(string? error, HmsAdvanceRevertResult? result)> RevertUsedAdvanceToPaymode(
            IDbConnection db, IDbTransaction tx,
            string requestGuid, string tenantCode,
            HmsAdvanceRevertInput input, bool updatePmcOnBill)
        {
            if (string.IsNullOrWhiteSpace(requestGuid)) return ("requestguid is required.", null);
            if (input.pmcode <= 0) return ("Paymode (pmcode) is required to revert advance.", null);
            if (input.enteredbhcode == null || input.cntcode == null)
                return ("Branch code (enteredbhcode) and counter code (cntcode) are required to revert advance.", null);
            if (input.usercode == null) return ("usercode is required.", null);

            // ── Lock bill ─────────────────────────────────────────────────────
            var bill = await db.QueryFirstOrDefaultAsync<HmsLabRequestMaster>(
                @"SELECT * FROM lab_request_master
                   WHERE requestguid = @rg AND tenant_code = @t
                   FOR UPDATE",
                new { rg = requestGuid, t = tenantCode }, tx);

            if (bill == null) return ("Bill not found.", null);
            if (bill.isdeleted == true || bill.deleted == true)
                return ("Cannot revert advance on a cancelled bill.", null);

            // ── Paymode from paymode_master ───────────────────────────────────
            var pm = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT pmcode, name FROM paymode_master
                   WHERE pmcode = @pm AND tenant_code = @t
                     AND COALESCE(deleted, false) = false
                   LIMIT 1",
                new { pm = input.pmcode, t = tenantCode }, tx);

            if (pm == null) return ("Selected paymode not found in paymode master.", null);

            decimal pmCode = Convert.ToDecimal(pm.pmcode);
            string collectionType = (((string?)pm.name) ?? "").Trim().ToUpperInvariant();
            if (collectionType == "") return ("Selected paymode has no name.", null);
            if (collectionType == "ADVANCE")
                return ("Cannot revert advance into the ADVANCE paymode. Choose a real paymode.", null);

            bool isCheque = collectionType == "CHEQUE" || input.cheque_date != null
                            || !string.IsNullOrWhiteSpace(input.bank_name);
            var refRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CARD", "UPI", "CHEQUE", "BANK" };
            //if (refRequired.Contains(collectionType) && string.IsNullOrWhiteSpace(input.reference_no))
            //    return ($"Transaction reference number is required for payment mode '{collectionType}'.", null);

            // ── How much advance is currently used on this bill ───────────────
            double totalUsed = await db.ExecuteScalarAsync<double>(
                @"SELECT COALESCE(SUM(receiptamount), 0)::float8
                    FROM receipt_advances
                   WHERE requestguid = @rg AND tenant_code = @t
                     AND COALESCE(deleted, false) = false",
                new { rg = requestGuid, t = tenantCode }, tx);

            if (totalUsed <= 0.01)
                return ("No advance has been used against this bill.", null);

            double amount = (input.amount ?? 0) > 0.01 ? Math.Round(input.amount!.Value, 2) : Math.Round(totalUsed, 2);
            if (amount > totalUsed + 0.01)
                return ($"Revert amount ({amount:F2}) exceeds advance used on this bill ({totalUsed:F2}).", null);
            amount = Math.Min(amount, Math.Round(totalUsed, 2));

            double balanceBefore = await GetAdvanceBalance(db, tx, bill.custid, tenantCode);

            // ── Open counter shift ────────────────────────────────────────────
            var shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                @"SELECT * FROM counter_timing
                   WHERE bhcode = @bh AND cntcode = @cnt
                     AND todate IS NULL
                     AND (planned_to IS NULL OR planned_to > @now)
                     AND tenant_code = @t
                   LIMIT 1",
                new { bh = input.enteredbhcode, cnt = input.cntcode, t = tenantCode, now = DateTime.UtcNow }, tx);

            if (shift == null)
            {
                shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE bhcode = @bh AND cntcode = @cnt
                         AND todate IS NULL AND tenant_code = @t
                       ORDER BY cnttid DESC LIMIT 1",
                    new { bh = input.enteredbhcode, cnt = input.cntcode, t = tenantCode }, tx);
            }

            if (shift == null)
            {
                shift = await db.QueryFirstOrDefaultAsync<HmsCounterTiming>(
                    @"SELECT * FROM counter_timing
                       WHERE todate IS NULL AND tenant_code = @t
                       ORDER BY cnttid DESC LIMIT 1",
                    new { t = tenantCode }, tx);
            }

            if (shift == null)
                return ("No open counter shift found for this branch/counter. Please open a shift first.", null);

            var receiptCfg = await db.QueryFirstOrDefaultAsync<HmsBillNoMaster>(
                @"SELECT * FROM billno_master
                   WHERE isreceiptno = true AND deleted = false AND tenant_code = @t
                   LIMIT 1",
                new { t = tenantCode }, tx);
            if (receiptCfg == null)
                return ("Receipt number sequence configuration not found in billno_master.", null);

            // ── 1. Release the advance (restores balance automatically) ───────
            await ConsumeLedger(db, tx,
                @"SELECT receiptadvanceid AS id, receiptamount::float8 AS amt
                    FROM receipt_advances
                   WHERE requestguid = @rg AND tenant_code = @t
                     AND COALESCE(deleted, false) = false
                   ORDER BY entereddate DESC, receiptadvanceid DESC",
                requestGuid, tenantCode,
                "receipt_advances", "receiptadvanceid", "receiptamount", amount);

            await ConsumeLedger(db, tx,
                @"SELECT balancecollectionbyid AS id, collectedamount::float8 AS amt
                    FROM balancecollectionby
                   WHERE request_guid = @rg AND tenant_code = @t
                     AND collection_type = 'ADVANCE'
                     AND COALESCE(deleted, false) = false
                   ORDER BY entereddate DESC, balancecollectionbyid DESC",
                requestGuid, tenantCode,
                "balancecollectionby", "balancecollectionbyid", "collectedamount", amount);

            // ── 2. New receipt through the chosen paymode ─────────────────────
            var seq = await GetNextReceiptSequence(db, tx, receiptCfg,
                input.enteredbhcode ?? 0, input.cntcode ?? 0, tenantCode);

            string receiptGuid = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            await db.InsertAsync(new HmsDueReceiptMaster
            {
                receiptguid = receiptGuid,
                receiptdate = now,
                receiptsno = seq.sno,
                receiptsnoprint = seq.snoprint,
                receiptbarcode = seq.barcode,
                receiptcovertedbarcode = seq.barcode,
                cntcode = input.cntcode,
                cnttid = shift.cnttid,
                tmcode = bill.tmcode,
                pmcode = pmCode,
                ctcode = bill.ctcode,
                bankname = isCheque ? input.bank_name : null,
                paymentreference = string.IsNullOrWhiteSpace(input.reference_no)
                                    ? $"AdvRevert:{bill.requestsnoprint}" : input.reference_no,
                cardno = input.card_no,
                chequedate = isCheque ? input.cheque_date : null,
                amountpaid = amount,
                amountadjusted = amount,
                amounttotal = amount,
                deleted = false,
                isdeleted = false,
                isbill = true,
                ispatient = true,
                isrefund = false,
                isrefferal = false,
                ismonthly = false,
                receipttype = "ADVANCE_REVERT",
                custid = bill.custid.HasValue ? (int)bill.custid.Value : null,
                opvisitid = bill.opvisitid,
                enteredbhcode = input.enteredbhcode,
                usercode = input.usercode,
                computercode = input.computercode,
                entereddate = now,
                ibsdate = now,
                tenant_code = tenantCode
            }, tx);

            await db.InsertAsync(new HmsDueReceiptDetail
            {
                receiptdetailsid = Guid.NewGuid().ToString(),
                receiptguid = receiptGuid,
                requestguid = requestGuid,
                receiptamount = amount,
                discount_amount = 0,
                refund_amount = 0,
                deleted = false,
                usercode = input.usercode,
                computercode = input.computercode,
                entereddate = now,
                ibsdate = now,
                tenant_code = tenantCode,
                custid = bill.custid.HasValue ? (int)bill.custid.Value : null,
                opvisitid = bill.opvisitid,
                enteredbhcode = input.enteredbhcode,
                isbill = true,
                ispatient = true,
                isrefund = false
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
                      @dt, @ct, @rg, @req,
                      @dt, @ct, @rg, @req,
                      @amt, false, @uc, @cc,
                      @dt, @dt, @tm, @cnt, @cnttid, @pm, @cct,
                      @t)",
                new
                {
                    id = Guid.NewGuid().ToString(),
                    bh = input.enteredbhcode,
                    dt = now,
                    ct = collectionType,
                    rg = receiptGuid,
                    req = requestGuid,
                    amt = amount,
                    uc = input.usercode,
                    cc = input.computercode,
                    tm = bill.tmcode,
                    cnt = input.cntcode,
                    cnttid = shift.cnttid,
                    pm = pmCode,
                    cct = bill.ctcode,
                    t = tenantCode
                }, tx);

            // ── 3. Bill master: paidamount unchanged (advance → paymode swap) ─
            int f1 = 0, f2 = 0, f3 = 0; double a1 = 0, a2 = 0, a3 = 0;
            switch (collectionType)
            {
                case "CARD": f2 = 1; a2 = amount; break;
                case "UPI": f3 = 1; a3 = amount; break;
                default: f1 = 1; a1 = amount; break;   // CASH + others (same as AddPayment)
            }

            string pmcSet = updatePmcOnBill
                ? @", pmc1 = COALESCE(pmc1,0) + @f1, pmc2 = COALESCE(pmc2,0) + @f2, pmc3 = COALESCE(pmc3,0) + @f3,
                      pmc1_amount = COALESCE(pmc1_amount,0) + @a1,
                      pmc2_amount = COALESCE(pmc2_amount,0) + @a2,
                      pmc3_amount = COALESCE(pmc3_amount,0) + @a3"
                : "";

            await db.ExecuteAsync(
                $@"UPDATE lab_request_master
                      SET paidviareceipt = COALESCE(paidviareceipt, 0) + @amt
                          {pmcSet}
                    WHERE requestguid = @rg AND tenant_code = @t",
                new { amt = amount, f1, f2, f3, a1, a2, a3, rg = requestGuid, t = tenantCode }, tx);

            double balanceAfter = await GetAdvanceBalance(db, tx, bill.custid, tenantCode);

            return (null, new HmsAdvanceRevertResult
            {
                requestguid = requestGuid,
                bill_no = bill.requestsnoprint,
                custid = bill.custid,
                reverted_amount = amount,
                pmcode = pmCode,
                paymode = collectionType,
                receipt_guid = receiptGuid,
                receipt_no = seq.snoprint,
                receipt_barcode = seq.barcode,
                advance_balance_before = balanceBefore,
                advance_balance_after = balanceAfter,
                bill_paid_amount = bill.paidamount ?? 0,
                reverted_date = now
            });
        }

        // Newest rows first: fully-consumed rows are soft-deleted, the last one is reduced.
        private static async Task ConsumeLedger(
            IDbConnection db, IDbTransaction tx, string selectSql,
            string requestGuid, string tenantCode,
            string table, string idCol, string amtCol, double amount)
        {
            var rows = (await db.QueryAsync<(string id, double amt)>(
                selectSql, new { rg = requestGuid, t = tenantCode }, tx)).ToList();

            double remaining = amount;
            foreach (var row in rows)
            {
                if (remaining <= 0.004) break;
                double take = Math.Round(Math.Min(row.amt, remaining), 2);
                if (take <= 0.004) continue;

                if (row.amt - take <= 0.005)
                {
                    await db.ExecuteAsync(
                        $"UPDATE {table} SET deleted = true WHERE {idCol} = @id AND tenant_code = @t",
                        new { id = row.id, t = tenantCode }, tx);
                }
                else
                {
                    await db.ExecuteAsync(
                        $"UPDATE {table} SET {amtCol} = {amtCol} - @take WHERE {idCol} = @id AND tenant_code = @t",
                        new { take, id = row.id, t = tenantCode }, tx);
                }
                remaining = Math.Round(remaining - take, 2);
            }
        }

        private static async Task<double> GetAdvanceBalance(
            IDbConnection db, IDbTransaction tx, decimal? custid, string tenantCode)
        {
            if (custid == null) return 0;
            var r = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT
                      COALESCE(SUM(CASE WHEN ra.requestguid IS NULL THEN ra.receiptamount ELSE 0 END), 0) AS deposited,
                      COALESCE(SUM(CASE WHEN ra.requestguid IS NOT NULL THEN ra.receiptamount ELSE 0 END), 0) AS used
                    FROM receipt_advances ra
                   INNER JOIN receipt_master rm ON ra.receiptguid = rm.receiptguid
                   WHERE rm.custid = @c AND rm.tenant_code = @t
                     AND rm.receipttype = 'ADVANCE'
                     AND COALESCE(rm.isdeleted, false) = false
                     AND COALESCE(ra.deleted, false) = false",
                new { c = (int)custid.Value, t = tenantCode }, tx);
            if (r == null) return 0;
            return Math.Round(Math.Max(Convert.ToDouble(r.deposited) - Convert.ToDouble(r.used), 0), 2);
        }

        // Same logic as HmsDueCollectionClass.GetNextReceiptSequence
        private static async Task<HmsNumberResult> GetNextReceiptSequence(
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
            return new HmsNumberResult
            {
                sno = nextOrder,
                snoprint = $"{prefix}-{DateTime.UtcNow:yyMM}-{nextOrder:D5}",
                barcode = $"{cfg.bncode}{bhcode}{cntcode}{nextOrder}",
                used_bncode = cfg.bncode
            };
        }
    }
}

