using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class BillingSummaryClass
    {
        private readonly string _db_conn;
        private readonly UnbilledChargesClass _unbilledCls;

        public BillingSummaryClass(IConfiguration configuration, UnbilledChargesClass unbilledCls)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
            _unbilledCls = unbilledCls;
        }

        // ─────────────────────────────────────────
        // OP BILLING SUMMARY — by op_no + custid
        // Confirms the OP belongs to that patient (custid), pulls patient
        // name from customer_master, then returns every charge tied to
        // that op_id — unbilled (pending) and billed — regardless of
        // entrytype (CONSULTATION, INVESTIGATION/lab, scan, dressing, etc.),
        // so the caller can see exactly what was billed and what's still due.
        // ─────────────────────────────────────────
        public async Task<object> GetOpBillingSummary(string op_no, decimal custid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                db.Open();

                string sql = @"
                    SELECT o.op_id, o.op_no, o.custid, o.visit_status, o.dcode, o.visit_date,
                           cl.name AS patient_name, cl.mobile
                    FROM op_registration o
                    LEFT JOIN customerdb.customer_master cl
                           ON cl.custid = o.custid AND cl.deleted = false
                    WHERE o.isdeleted   = false
                      AND o.tenant_code = @tenant_code
                      AND o.op_no       = @op_no
                      AND o.custid      = @custid
                    ORDER BY o.created_at DESC
                    LIMIT 1";

                var op = await db.QueryFirstOrDefaultAsync(sql, new { tenant_code, op_no, custid });

                if (op == null)
                    return "No OP registration found for that OP number and patient";

                string opIdStr = ((Guid)op.op_id).ToString();

                var unbilledItems = await _unbilledCls.GetUnbilledByVisit(opIdStr, null, tenant_code);
                var billedItems = await _unbilledCls.GetBilledByVisit(opIdStr, null, tenant_code);

                return BuildSummary(
                    visitType: "OP",
                    id: op.op_id,
                    number: op.op_no,
                    custid: op.custid,
                    patient_name: op.patient_name,
                    mobile: op.mobile,
                    status: op.visit_status,
                    extra: new { dcode = op.dcode, visit_date = op.visit_date },
                    unbilledItems: unbilledItems,
                    billedItems: billedItems);
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // IP BILLING SUMMARY — by ip_no + custid
        // Same as above, for admissions. Refreshes ROOMRENT before listing
        // so pending room-rent is current, same as the billing screen does.
        // ─────────────────────────────────────────
        public async Task<object> GetIpBillingSummary(string ip_no, decimal custid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                db.Open();

                string sql = @"
                    SELECT i.ip_id, i.ip_no, i.custid, i.ip_status, i.dcode,
                           i.admitdate, i.dischargedate,
                           cl.name AS patient_name, cl.mobile
                    FROM ip_registration i
                    LEFT JOIN customerdb.customer_master cl
                           ON cl.custid = i.custid AND cl.deleted = false
                    WHERE i.isdeleted   = false
                      AND i.tenant_code = @tenant_code
                      AND i.ip_no       = @ip_no
                      AND i.custid      = @custid
                    ORDER BY i.created_at DESC
                    LIMIT 1";

                var ip = await db.QueryFirstOrDefaultAsync(sql, new { tenant_code, ip_no, custid });

                if (ip == null)
                    return "No IP registration found for that IP number and patient";

                Guid ipId = (Guid)ip.ip_id;

                // Keep ROOMRENT current before listing — same as UnbilledChargesController.GetByVisit
                await _unbilledCls.RecalculateRoomRent(ipId, tenant_code);

                var unbilledItems = await _unbilledCls.GetUnbilledByVisit(null, ipId.ToString(), tenant_code);
                var billedItems = await _unbilledCls.GetBilledByVisit(null, ipId.ToString(), tenant_code);

                return BuildSummary(
                    visitType: "IP",
                    id: ip.ip_id,
                    number: ip.ip_no,
                    custid: ip.custid,
                    patient_name: ip.patient_name,
                    mobile: ip.mobile,
                    status: ip.ip_status,
                    extra: new { dcode = ip.dcode, admitdate = ip.admitdate, dischargedate = ip.dischargedate },
                    unbilledItems: unbilledItems,
                    billedItems: billedItems);
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Shared response shape for both OP and IP summaries ──
        private static object BuildSummary(
            string visitType, object id, object number, object custid,
            object? patient_name, object? mobile, object? status, object extra,
            List<UnbilledChargeRow> unbilledItems, List<UnbilledChargeRow> billedItems)
        {
            return new
            {
                visit_type = visitType,          // "OP" or "IP"
                id,                               // op_id or ip_id
                number,                           // op_no or ip_no
                custid,
                patient_name,
                mobile,
                status,
                details = extra,
                unbilled_total = unbilledItems.Sum(x => x.amount ?? 0),
                billed_total = billedItems.Sum(x => x.billedamount ?? x.amount ?? 0),
                has_unbilled = unbilledItems.Any(),
                has_billed = billedItems.Any(),
                // entrytype + particulars on each row tell you WHAT was billed/pending —
                // CONSULTATION, INVESTIGATION (lab/scan), DRESSING, ROOMRENT, etc.
                unbilled_items = unbilledItems,
                billed_items = billedItems
            };
        }
        // ─────────────────────────────────────────
        // COMBINED BILLING SUMMARY — search by op_no and/or ip_no, with custid
        // Same patient can have an OP visit and an IP admission both tied to the
        // same custid — this returns whichever of the two is found (or both),
        // each with its own unbilled/billed breakdown.
        // ─────────────────────────────────────────
        public async Task<object> GetCombinedBillingSummary(
            string? op_no, string? ip_no, decimal custid, string tenant_code)
        {
            if (string.IsNullOrWhiteSpace(op_no) && string.IsNullOrWhiteSpace(ip_no))
                return "Provide at least one of op_no or ip_no";

            object? opResult = null;
            object? ipResult = null;
            string? opError = null;
            string? ipError = null;

            if (!string.IsNullOrWhiteSpace(op_no))
            {
                var res = await GetOpBillingSummary(op_no, custid, tenant_code);
                if (res is string err) opError = err;
                else opResult = res;
            }

            if (!string.IsNullOrWhiteSpace(ip_no))
            {
                var res = await GetIpBillingSummary(ip_no, custid, tenant_code);
                if (res is string err) ipError = err;
                else ipResult = res;
            }

            // Both requested but neither found → surface as a clean failure
            if (opResult == null && ipResult == null)
                return $"{opError ?? ""} {ipError ?? ""}".Trim();

            return new
            {
                custid,
                op = opResult,
                op_error = opError,     // null if op_no wasn't searched or was found fine
                ip = ipResult,
                ip_error = ipError,     // null if ip_no wasn't searched or was found fine
                has_op = opResult != null,
                has_ip = ipResult != null
            };
        }
    }
}