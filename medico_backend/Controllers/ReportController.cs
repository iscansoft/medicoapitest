using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly ReportClass _reportClass;
        private readonly DischargeSummaryClass _dsClass;

        public ReportController(ReportClass reportClass, DischargeSummaryClass dsClass)
        {
            _reportClass = reportClass;
            _dsClass = dsClass;
        }

        private string T => !string.IsNullOrWhiteSpace(Request.Headers["tenant_code"]) 
            ? Request.Headers["tenant_code"].ToString() 
            : (!string.IsNullOrWhiteSpace(Request.Query["tenant_code"]) 
                ? Request.Query["tenant_code"].ToString() 
                : (!string.IsNullOrWhiteSpace(Request.Query["tenantcode"]) 
                    ? Request.Query["tenantcode"].ToString() 
                    : ""));
        private int Me => int.Parse(User.FindFirst("user_code")?.Value ?? "1");

        [HttpGet("getstatement")]
        public async Task<IActionResult> Statement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.StatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getduestatement")]
        public async Task<IActionResult> DueStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.DueStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getdiscountstatement")]
        public async Task<IActionResult> DiscountStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.DiscountStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getreferralstatement")]
        public async Task<IActionResult> ReferralStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.ReferralStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getdiscountsummary")]
        public async Task<IActionResult> DiscountSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.DiscountSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getduesummary")]
        public async Task<IActionResult> DueSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.DueSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getgroupstatement")]
        public async Task<IActionResult> GroupStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.GroupStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getgroupsummary")]
        public async Task<IActionResult> GroupSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string periodtype)
        {
            var res = await _reportClass.GroupSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getteststatement")]
        public async Task<IActionResult> TestStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.TestStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("gettestsummary")]
        public async Task<IActionResult> TestSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string periodtype)
        {
            var res = await _reportClass.TestSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getreferralsummary")]
        public async Task<IActionResult> ReferralSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string periodtype)
        {
            var res = await _reportClass.ReferralSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getsummary")]
        public async Task<IActionResult> Summary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.SummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [HttpGet("getdailybillreport")]
        public async Task<IActionResult> DailyBillReport([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.DailyBillReportPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getreferralreceipt")]
        public async Task<IActionResult> ReferralReceipt([FromQuery] Guid receiptguid, [FromQuery] bool? isletterhead = false)
        {
            var res = await _reportClass.ReferralReceiptPDF(receiptguid, T, isletterhead);
            return Ok(res);
        }

        [HttpGet("getpatientreceipt")]
        public async Task<IActionResult> PatientReceipt(
            [FromQuery] Guid receiptguid,
            [FromQuery] Guid? requestguid = null,
            [FromQuery] string? billno = null,
            [FromQuery] int? custid = null,
            [FromQuery] bool? isletterhead = false)
        {
            var res = await _reportClass.PatientReceiptPDF(receiptguid, T, isletterhead, requestguid, billno, custid);
            return Ok(res);
        }


                [AllowAnonymous]
        [HttpGet("getadvancereceipt")]
        public async Task<IActionResult> GetAdvanceReceipt(
            [FromQuery] string? receiptguid = null, 
            [FromQuery] bool? isletterhead = false,
            [FromQuery] string? tenantcode = null,
            [FromQuery] string? tenant_code = null,
            [FromQuery] string? format = null,
            [FromQuery] int? custid = null,
            [FromQuery] string? admissionno = null,
            [FromQuery] string? pageformat = "A4")
        {
            string t = !string.IsNullOrEmpty(tenantcode) ? tenantcode : (!string.IsNullOrEmpty(tenant_code) ? tenant_code : T);
            var res = await _reportClass.GetAdvanceReceiptPDF(receiptguid, t, isletterhead, custid, admissionno, pageformat);
            if (res == null) return NotFound("Advance receipt not found.");

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("getrefundreceipt")]
        public async Task<IActionResult> GetRefundReceipt(
            [FromQuery] string? receiptguid = null, 
            [FromQuery] bool? isletterhead = false,
            [FromQuery] string? tenantcode = null,
            [FromQuery] string? tenant_code = null,
            [FromQuery] string? format = null,
            [FromQuery] int? custid = null,
            [FromQuery] string? admissionno = null,
            [FromQuery] string? pageformat = "A4")
        {
            string t = !string.IsNullOrEmpty(tenantcode) ? tenantcode : (!string.IsNullOrEmpty(tenant_code) ? tenant_code : T);
            var res = await _reportClass.GetRefundReceiptPDF(receiptguid, t, isletterhead, custid, admissionno, pageformat);
            if (res == null) return NotFound("Refund receipt not found.");

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        

        [HttpGet("getbill")]
        public async Task<IActionResult> Bill([FromQuery] Guid requestguid, [FromQuery] bool? isletterhead = false)
        {
            var res = await _reportClass.BillPDF(requestguid, T, isletterhead);
            return Ok(res);
        }

        [HttpGet("getworklist")]
        public async Task<IActionResult> Worklist(
            [FromQuery] Guid? requestguid,
            [FromQuery] DateTime? fromdate,
            [FromQuery] DateTime? todate,
            [FromQuery] string? gcode)
        {
            var res = await _reportClass.WorklistPDF(requestguid, fromdate, todate, gcode, T);
            return Ok(res);
        }

        [HttpGet("culture-report")]
        public async Task<IActionResult> GetCultureReport([FromQuery] Guid requestguid, [FromQuery] bool? isletterhead = false)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var result = await _reportClass.GetCultureReportAsync(requestguid, T, isletterhead);

            return Ok(result);
        }

        [HttpGet("labreport")]
        public async Task<IActionResult> GetLabReport(
            [FromQuery] Guid requestguid,
            [FromQuery] bool? isletterhead,
            [FromQuery] string? timezone = null,
            [FromQuery] List<int>? tcodes = null,
            [FromQuery] string? tcode = null,
            [FromQuery] string? paper_size = null,
            [FromQuery] string? paperSize = null,
            [FromQuery] string? orientation = null)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            IList<int>? finalTCodes = tcodes;
            if ((finalTCodes == null || finalTCodes.Count == 0) && !string.IsNullOrWhiteSpace(tcode))
            {
                finalTCodes = tcode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .Select(x => int.TryParse(x, out int c) ? c : 0)
                                   .Where(c => c > 0)
                                   .ToList();
            }

            string effectivePaperSize = !string.IsNullOrWhiteSpace(paper_size) ? paper_size : paperSize;
            var result = await _reportClass.GetLabReportAsync(requestguid, T, isletterhead, timezone, finalTCodes, effectivePaperSize, orientation);

            return Ok(result);
        }

        [HttpGet("getpaymodestatement")]
        public async Task<IActionResult> PayModeStatement([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            var res = await _reportClass.PayModeStatementPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getpaymodesummary")]
        public async Task<IActionResult> PayModeSummary([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? periodtype)
        {
            var res = await _reportClass.PayModeSummaryPDF(fromdate, todate, T, periodtype);
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("getopcasesheet")]
        public async Task<IActionResult> GetOPCasesheet([FromQuery] Guid sheet_id, [FromQuery] bool? isletterhead = false, [FromQuery] string? format = null)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.OPCasesheetPDF(sheet_id, T, isletterhead);
            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) || 
                (Request.Headers.Accept.ToString().Contains("text/html") && !Request.Headers.ContainsKey("tenantcode")))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("getprescription")]
        [HttpGet("getdoctorprescription")]
        public async Task<IActionResult> GetDoctorPrescription(
            [FromQuery] Guid? pr_id = null,
            [FromQuery] string? pr_code = null,
            [FromQuery] Guid? sheet_id = null,
            [FromQuery] Guid? op_id = null,
            [FromQuery] Guid? ip_id = null,
            [FromQuery] decimal? custid = null,
            [FromQuery] bool? isletterhead = false,
            [FromQuery] string? format = null,
            [FromQuery] string? tenantcode = null,
            [FromQuery] string? tenant_code = null)
        {
            string t = !string.IsNullOrEmpty(tenantcode) ? tenantcode : (!string.IsNullOrEmpty(tenant_code) ? tenant_code : T);
            if (string.IsNullOrWhiteSpace(t))
                return BadRequest(new { success = false, message = "tenant_code header or query parameter is required." });

            var res = await _reportClass.DoctorPrescriptionPDF(pr_id, pr_code, sheet_id, op_id, ip_id, custid, t, isletterhead);
            if (res == null) return NotFound("Prescription report not found.");

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) || 
                (Request.Headers.Accept.ToString().Contains("text/html") && !Request.Headers.ContainsKey("tenantcode")))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpPost("getprescription")]
        [HttpPost("getdoctorprescription")]
        public async Task<IActionResult> PostDoctorPrescription([FromBody] DoctorPrescriptionReportApiRequest req)
        {
            string t = !string.IsNullOrEmpty(req.tenantcode) ? req.tenantcode : (!string.IsNullOrEmpty(req.tenant_code) ? req.tenant_code : T);
            if (string.IsNullOrWhiteSpace(t))
                t = !string.IsNullOrWhiteSpace(req.TenantId) ? req.TenantId : "0036";

            // If caller provided full CasesheetData directly in request body, forward directly to ReportingServer
            if (req.CasesheetData != null)
            {
                var resDirect = await _reportClass.DoctorPrescriptionDirectPDF(req);
                if (string.Equals(req.format, "pdf", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var clean = resDirect.Trim('"', ' ', '\r', '\n');
                        var bytes = Convert.FromBase64String(clean);
                        return File(bytes, "application/pdf");
                    }
                    catch { }
                }
                return Ok(resDirect);
            }

            var res = await _reportClass.DoctorPrescriptionPDF(req.pr_id, req.pr_code, req.sheet_id, req.op_id, req.ip_id, req.custid, t, req.IsLetterhead);
            if (res == null) return NotFound("Prescription report not found.");

            if (string.Equals(req.format, "pdf", StringComparison.OrdinalIgnoreCase) || 
                (Request.Headers.Accept.ToString().Contains("text/html") && !Request.Headers.ContainsKey("tenantcode")))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("getipcasesheet")]
        public async Task<IActionResult> GetIPCasesheet([FromQuery] Guid sheet_id, [FromQuery] bool? isletterhead = false, [FromQuery] string? format = null)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.IPCasesheetPDF(sheet_id, T, isletterhead);
            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) || 
                (Request.Headers.Accept.ToString().Contains("text/html") && !Request.Headers.ContainsKey("tenantcode")))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("getdischargesummary")]
        public async Task<IActionResult> GetDischargeSummary([FromQuery] Guid pds_id, [FromQuery] bool? isletterhead = false, [FromQuery] string? format = null)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _dsClass.GetDischargeSummaryReportPdfAsync(pds_id, T, isletterhead);
            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) || 
                (Request.Headers.Accept.ToString().Contains("text/html") && !Request.Headers.ContainsKey("tenantcode")))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("getconsolidatedbill")]
        public async Task<IActionResult> GetConsolidatedBill(
            [FromQuery] Guid requestguid, 
            [FromQuery] bool includeMedicines = true, 
            [FromQuery] bool? isletterhead = false,
            [FromQuery] Guid? op_id = null,
            [FromQuery] string? format = null,
            [FromQuery] string? viewtype = null)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.ConsolidatedBillPDF(requestguid, includeMedicines, T, isletterhead, op_id, viewtype);
            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) || 
                (Request.Headers.Accept.ToString().Contains("text/html") && !Request.Headers.ContainsKey("tenantcode")))
            {
                try
                {
                    var clean = res.Trim('"', ' ', '\r', '\n');
                    var bytes = Convert.FromBase64String(clean);
                    return File(bytes, "application/pdf");
                }
                catch { }
            }
            return Ok(res);
        }

        [HttpGet("stock-report")]
        public async Task<IActionResult> GetStockReport([FromQuery] string? warehousecode, [FromQuery] DateTime? date)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.StockReportPDF(T, warehousecode, date);
            return Ok(res);
        }

        [HttpGet("sales-report")]
        public async Task<IActionResult> GetSalesReport([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? warehousecode)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.SalesReportPDF(fromdate, todate, T, warehousecode);
            return Ok(res);
        }

        [HttpGet("getdoctorwiseop")]
        public async Task<IActionResult> GetDoctorwiseOpReport([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.GetDoctorwiseOpReportPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getdoctorwiseip")]
        public async Task<IActionResult> GetDoctorwiseIpReport([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.GetDoctorwiseIpReportPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getop")]
        public async Task<IActionResult> GetOpReport([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.GetOpReportPDF(fromdate, todate, T);
            return Ok(res);
        }

        [HttpGet("getip")]
        public async Task<IActionResult> GetIpReport([FromQuery] DateTime fromdate, [FromQuery] DateTime todate)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.GetIpReportPDF(fromdate, todate, T);
            return Ok(res);
        }
        [HttpGet("getpharmacybill")]
        public async Task<IActionResult> GetPharmacyBill([FromQuery] string billNo, [FromQuery] string? orientation = "portrait")
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            if (string.IsNullOrEmpty(billNo))
                return BadRequest(new { success = false, message = "BillNo is required." });

            var res = await _reportClass.GetPharmacyBillAsync(billNo, T, orientation ?? "portrait");
            
            if (res == null)
                return NotFound(new { success = false, message = "Pharmacy bill not found." });
                
            return Ok(res);
        }

        [HttpGet("getgstbill")]
        public async Task<IActionResult> GetGstBill([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] string? billtype = "all")
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.GetGstBillPDF(fromdate, todate, T, billtype);
            return Ok(res);
        }

        [HttpGet("getsalesregister")]
        public async Task<IActionResult> GetSalesRegister([FromQuery] DateTime fromdate, [FromQuery] DateTime todate, [FromQuery] long? warehousecode = null)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.GetSalesRegisterPDF(fromdate, todate, T, warehousecode);
            return Ok(res);
        }

        [HttpGet("getconsolidatedlists")]
        public async Task<IActionResult> GetConsolidatedLists(
            [FromQuery] DateTime fromdate, 
            [FromQuery] DateTime todate, 
            [FromQuery] string? type = "ALL", 
            [FromQuery] string? search = null, 
            [FromQuery] bool? isletterhead = false)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.ConsolidatedListsReportPDF(fromdate, todate, T, type, search, isletterhead);
            return Ok(res);
        }

        [HttpGet("getconsolidatedlistsdata")]
        public async Task<IActionResult> GetConsolidatedListsData(
            [FromQuery] DateTime fromdate, 
            [FromQuery] DateTime todate, 
            [FromQuery] string? type = "ALL", 
            [FromQuery] string? search = null)
        {
            if (string.IsNullOrWhiteSpace(T))
                return BadRequest(new { success = false, message = "tenant_code header required" });

            var res = await _reportClass.GetConsolidatedListsDataAsync(T, fromdate, todate, type, search);
            return Ok(res);
        }
    
        // ─── ITEM-WISE PATIENT SALES / MOVEMENT REPORT ────────────────────────────
        [AllowAnonymous]
        [HttpGet("getitemsforsalesreport")]
        [HttpGet("item-sales-report/items")]
        public async Task<IActionResult> GetItemsForSalesReport(
            [FromQuery] string? search = null,
            [FromQuery] string? tenantcode = null,
            [FromQuery] string? tenant_code = null)
        {
            try
            {
                string t = !string.IsNullOrWhiteSpace(tenantcode) ? tenantcode : (!string.IsNullOrWhiteSpace(tenant_code) ? tenant_code : T);
                if (string.IsNullOrWhiteSpace(t))
                    return BadRequest(new { Status = "Failed", Message = "Header or query parameter 'tenant_code' or 'tenantcode' is required." });

                var items = await _reportClass.GetItemsForSalesReport(t, search);
                return Ok(new { Status = "Success", Data = items });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("getitempatientsalesreport")]
        [HttpGet("item-sales-report/details")]
        public async Task<IActionResult> GetItemPatientSalesReport(
            [FromQuery] long itemcode,
            [FromQuery] DateTime? fromdate = null,
            [FromQuery] DateTime? todate = null,
            [FromQuery] string? batchno = null,
            [FromQuery] string? salestype = null,
            [FromQuery] long? warehousecode = null,
            [FromQuery] string? tenantcode = null,
            [FromQuery] string? tenant_code = null)
        {
            try
            {
                string t = !string.IsNullOrWhiteSpace(tenantcode) ? tenantcode : (!string.IsNullOrWhiteSpace(tenant_code) ? tenant_code : T);
                if (string.IsNullOrWhiteSpace(t))
                    return BadRequest(new { Status = "Failed", Message = "Header or query parameter 'tenant_code' or 'tenantcode' is required." });

                if (itemcode <= 0)
                    return BadRequest(new { Status = "Failed", Message = "Valid 'itemcode' is required." });

                var report = await _reportClass.GetItemPatientSalesReport(itemcode, fromdate, todate, batchno, salestype, warehousecode, t);
                if (report == null)
                    return NotFound(new { Status = "Failed", Message = "Item not found." });

                return Ok(new { Status = "Success", Data = report });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("getitempatientsalesreportpdf")]
        public async Task<IActionResult> GetItemPatientSalesReportPdf(
            [FromQuery] long itemcode,
            [FromQuery] DateTime? fromdate = null,
            [FromQuery] DateTime? todate = null,
            [FromQuery] string? batchno = null,
            [FromQuery] string? salestype = null,
            [FromQuery] long? warehousecode = null,
            [FromQuery] string? format = null,
            [FromQuery] string? tenantcode = null,
            [FromQuery] string? tenant_code = null)
        {
            try
            {
                string t = !string.IsNullOrWhiteSpace(tenantcode) ? tenantcode : (!string.IsNullOrWhiteSpace(tenant_code) ? tenant_code : T);
                if (string.IsNullOrWhiteSpace(t))
                    return BadRequest(new { Status = "Failed", Message = "Header or query parameter 'tenant_code' or 'tenantcode' is required." });

                if (itemcode <= 0)
                    return BadRequest(new { Status = "Failed", Message = "Valid 'itemcode' is required." });

                var pdf = await _reportClass.GetItemPatientSalesReportPDF(itemcode, fromdate, todate, batchno, salestype, warehousecode, t);
                if (string.IsNullOrWhiteSpace(pdf))
                    return NotFound(new { Status = "Failed", Message = "Could not generate report PDF." });

                if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = Convert.FromBase64String(pdf);
                    return File(bytes, "application/pdf", $"ItemSalesReport_{itemcode}.pdf");
                }

                return Ok(new { Status = "Success", Data = pdf });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
    }
}    }
}
