using System;
using System.Threading.Tasks;
using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace medico_backend.Controllers
{
    /// <summary>
    /// HMS Due Collection API
    ///
    /// Tenant resolved (priority order):
    ///   1. JWT claim "tenant_code" or "TenantCode"
    ///   2. Request header "tenant_code"
    ///   3. Fallback: "DEFAULT" (warning logged)
    ///
    /// Endpoints:
    ///   GET  /api/HmsDueCollection/preview/{requestguid}?advanceToUse=2000
    ///   POST /api/HmsDueCollection/collect
    ///   POST /api/HmsDueCollection/collect/bulk          ← NEW
    ///   GET  /api/HmsDueCollection/cancel/{receiptGuid}
    ///   POST /api/HmsDueCollection/list
    ///   GET  /api/HmsDueCollection/advance-summary/{custid}
    ///   POST /api/HmsDueCollection/advance/deposit
    ///   POST /api/HmsDueCollection/advance/refund
    ///   POST /api/HmsDueCollection/due-bills
    ///   POST /api/HmsDueCollection/paid-history
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HmsDueCollectionController : ControllerBase
    {
        private readonly HmsDueCollectionClass _service;
        private readonly ILogger<HmsDueCollectionController> _logger;

        public HmsDueCollectionController(
            HmsDueCollectionClass service,
            ILogger<HmsDueCollectionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private string ResolveTenantCode()
        {
            var fromClaim = User.FindFirst("tenant_code")?.Value
                         ?? User.FindFirst("TenantCode")?.Value;
            if (!string.IsNullOrWhiteSpace(fromClaim))
                return fromClaim.Trim();

            if (Request.Headers.TryGetValue("tenant_code", out var header)
                && !string.IsNullOrWhiteSpace(header))
                return header.ToString().Trim();

            _logger.LogWarning("Tenant code not found in JWT or headers — using DEFAULT.");
            return "DEFAULT";
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  1. DRY-RUN PREVIEW  (no DB writes)
        //
        //  GET /api/HmsDueCollection/preview/{requestguid}?advanceToUse=2000
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet("preview/{requestguid}")]
        public async Task<IActionResult> GetDuePreview(
            string requestguid,
            [FromQuery] double advanceToUse = 0)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.GetDuePreview(requestguid, advanceToUse, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in due preview for requestguid={rg}", requestguid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  2. COLLECT DUE  (single bill, atomic transaction)
        //
        //  Advance deducted from receipt_advances deposit rows (FIFO).
        //  Cancel restores advance by soft-deleting usage rows.
        //
        //  POST /api/HmsDueCollection/collect
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("collect")]
        public async Task<IActionResult> CollectDue([FromBody] HmsDueCollectionRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.CollectDue(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CollectDue for requestguid={rg}", req?.requestguid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  3. BULK COLLECT DUE  (multiple bills, one shared receipt)
        //
        //  Settles multiple bills in a single atomic transaction.
        //  One receipt_master row is shared across all bills.
        //  Each bill gets its own receipt_details and balancecollectionby row.
        //  Per-bill advance amounts can differ (patient wallet queried per bill).
        //  Bills that are already paid or not found are skipped (not errors).
        //
        //  POST /api/HmsDueCollection/collect/bulk
        //
        //  Request body:
        //  {
        //    "collection_type": "CASH",
        //    "enteredbhcode": 1,
        //    "cntcode": 1,
        //    "usercode": 10,
        //    "computercode": 1,
        //    "items": [
        //      { "requestguid": "abc-001", "advance_to_use": 500,  "cash_collected": 700  },
        //      { "requestguid": "abc-002", "advance_to_use": 0,    "cash_collected": 1200 },
        //      { "requestguid": "abc-003", "advance_to_use": 1000, "cash_collected": 0    }
        //    ]
        //  }
        //
        //  Response 200:
        //  {
        //    "success": true,
        //    "data": {
        //      "batch_receipt_guid"   : "rcp-guid",
        //      "receipt_no"           : "RCP-2606-00005",
        //      "total_advance_used"   : 1500.00,
        //      "total_cash_collected" : 1900.00,
        //      "total_settled"        : 3400.00,
        //      "items_processed"      : 3,
        //      "items_skipped"        : 0,
        //      "items": [
        //        { "requestguid": "abc-001", "due_before": 1200, "advance_used": 500,
        //          "cash_collected": 700, "due_after": 0, "is_fully_settled": true, "status": "collected" },
        //        ...
        //      ]
        //    }
        //  }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("collect/bulk")]
        public async Task<IActionResult> BulkCollectDue([FromBody] HmsBulkDueCollectionRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.BulkCollectDue(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BulkCollectDue");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  4. CANCEL DUE COLLECTION RECEIPT
        //
        //  Reverses a DUE receipt fully:
        //    - Soft-deletes receipt_master, receipt_details, balancecollectionby
        //    - Soft-deletes advance usage rows → advance balance auto-restored
        //    - Reverts lab_request_master.paidamount (e.g. 300 paid → reverted → 1200 due again)
        //    - Sets balancecollectionbytest.requeststatus = false
        //
        //  GET /api/HmsDueCollection/cancel/{receiptGuid}
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet("cancel/{receiptGuid}")]
        public async Task<IActionResult> CancelDueCollection(
            string receiptGuid,
            [FromQuery] int? usercode = null,
            [FromQuery] string? reason = null)
        {
            try
            {
                string tenant = ResolveTenantCode();
                string status = await _service.CancelDueCollection(receiptGuid, usercode, reason, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new
                {
                    success = true,
                    message = "Due collection receipt cancelled and all entries reversed successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling receipt={rg}", receiptGuid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  5. PAGINATED DUE COLLECTION LIST
        //
        //  POST /api/HmsDueCollection/list
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("list")]
        public async Task<IActionResult> GetDueCollectionList(
            [FromBody] HmsDueCollectionFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount) = await _service.GetDueCollectionList(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching due collection list.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  6. PATIENT ADVANCE SUMMARY
        //
        //  Shows total deposited, used, refunded, available balance + full ledger.
        //  Call before opening due collection screen so operator can see advance balance.
        //
        //  GET /api/HmsDueCollection/advance-summary/{custid}
        // ═══════════════════════════════════════════════════════════════════════

        [HttpGet("advance-summary/{custid}")]
        public async Task<IActionResult> GetPatientAdvanceSummary(decimal custid)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var data = await _service.GetPatientAdvanceSummary(custid, tenant);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching advance summary for custid={c}", custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  7. DEPOSIT ADVANCE
        //
        //  POST /api/HmsDueCollection/advance/deposit
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/deposit")]
        public async Task<IActionResult> DepositAdvance([FromBody] HmsAdvanceDepositRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.DepositAdvance(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error depositing advance for custid={c}", req?.custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  8. REFUND ADVANCE
        //
        //  POST /api/HmsDueCollection/advance/refund
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/refund")]
        public async Task<IActionResult> RefundAdvance([FromBody] HmsAdvanceRefundRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.RefundAdvance(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding advance for custid={c}", req?.custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  9. ALL DUE BILLS
        //
        //  POST /api/HmsDueCollection/due-bills
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("due-bills")]
        public async Task<IActionResult> GetAllDueBills([FromBody] HmsAllDueFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) = await _service.GetAllDueBills(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all due bills.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  10. PAID HISTORY
        //
        //  POST /api/HmsDueCollection/paid-history
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("paid-history")]
        public async Task<IActionResult> GetPaidHistory([FromBody] HmsPaidHistoryFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) = await _service.GetPaidHistory(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paid history.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  11. ADVANCED PAID HISTORY FILTER
        //
        //  POST /api/HmsDueCollection/paid-history/filter
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("save/filter")]
        public async Task<IActionResult> GetAdvancedPaidHistory(
            [FromBody] HmsPaidHistoryAdvancedFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) =
                    await _service.GetAdvancedPaidHistory(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAdvancedPaidHistory.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  11. DAILY COLLECTION REPORT
        //
        //  POST /api/HmsDueCollection/daily-collection-report
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("history/filter")]
        public async Task<IActionResult> GetDailyCollectionReport(
            [FromBody] HmsDailyCollectionReportFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) =
                    await _service.GetDailyCollectionReport(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDailyCollectionReport.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  8b. USE ADVANCE FOR CUSTOMER
        //
        //  POST /api/HmsDueCollection/advance/use
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/use")]
        public async Task<IActionResult> UseAdvance([FromBody] HmsAdvanceUseRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.UseAdvance(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error using advance for custid={c}", req?.custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  8c. CANCEL ADVANCE DEPOSIT
        //
        //  POST /api/HmsDueCollection/advance/cancel
        //  Body: { "receiptguid": "...", "usercode": 10, "reason": "Entered by mistake" }
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/cancel")]
        public async Task<IActionResult> CancelAdvance([FromBody] HmsAdvanceCancelRequest req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.CancelAdvance(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new
                {
                    success = true,
                    message = "Advance deposit cancelled successfully.",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling advance receipt={rg}", req?.receiptguid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // ═══════════════════════════════════════════════════════════════════════
        //  12. ADVANCE COLLECTION REQUEST TO CASH COUNTER
        //
        //  POST /api/HmsDueCollection/advance/request
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/request")]
        public async Task<IActionResult> CreateAdvanceRequest([FromBody] HmsCreateAdvanceRequestDto req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.CreateAdvanceRequest(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new
                {
                    success = true,
                    message = "Advance collection request sent to cash counter.",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating advance request for custid={c}", req?.custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  13. LIST ADVANCE COLLECTION REQUESTS
        //
        //  POST /api/HmsDueCollection/advance/requests
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/requests")]
        public async Task<IActionResult> GetAdvanceRequests([FromBody] HmsAdvanceRequestFilterDto filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) = await _service.GetAdvanceRequests(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching advance requests.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  14. RECEIVE / COLLECT ADVANCE REQUEST AT CASH COUNTER
        //
        //  POST /api/HmsDueCollection/advance/request/receive
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/request/receive")]
        public async Task<IActionResult> ReceiveAdvanceRequest([FromBody] HmsCollectAdvanceRequestDto req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, data) = await _service.ReceiveAdvanceRequest(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message = status });

                return Ok(new
                {
                    success = true,
                    message = "Advance received and deposited successfully.",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving advance request={id}", req?.request_id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  15. CANCEL ADVANCE COLLECTION REQUEST
        //
        //  POST /api/HmsDueCollection/advance/request/cancel
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/request/cancel")]
        public async Task<IActionResult> CancelAdvanceRequest([FromBody] HmsCancelAdvanceRequestDto req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, message) = await _service.CancelAdvanceRequest(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling advance request={id}", req?.request_id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpPost("advance/refund-request")]
        public async Task<IActionResult> CreateRefundRequest([FromBody] HmsCreateRefundRequestDto req)
        {
            try
            {
                var (status, data) = await _service.CreateRefundRequest(req, ResolveTenantCode());
                if (status != "SUCCESS") return BadRequest(new { success = false, message = status });
                return Ok(new { success = true, message = "Refund request sent to cash counter.", data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating refund request for custid={c}", req?.custid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        

        [HttpPost("advance/refund-requests")]
        public async Task<IActionResult> GetRefundRequests([FromBody] HmsAdvanceRequestFilterDto filter)
        {
            try
            {
                filter.request_type = "REFUND";
                var (data, totalCount, summary) = await _service.GetAdvanceRequests(filter, ResolveTenantCode());
                return Ok(new { success = true, totalCount, page = filter.page, pagesize = filter.pagesize, summary, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching refund requests.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

       

        [HttpPost("advance/refund-request/process")]
        public async Task<IActionResult> ProcessRefundRequest([FromBody] HmsProcessRefundRequestDto req)
        {
            try
            {
                var (status, data) = await _service.ProcessRefundRequest(req, ResolveTenantCode());
                if (status != "SUCCESS") return BadRequest(new { success = false, message = status });
                return Ok(new { success = true, message = "Advance refunded successfully.", data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund request={id}", req?.request_id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  16. PATIENTS WHO HAVE DEPOSITED ADVANCE (WITH AVAILABLE BALANCE / REFUND QUEUE)
        //
        //  POST /api/HmsDueCollection/advance/patient-deposits
        // ═══════════════════════════════════════════════════════════════════════

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  15d. CANCEL ADVANCE REFUND REQUEST
        //
        //  POST /api/HmsDueCollection/advance/refund-request/cancel
        //  POST /api/HmsDueCollection/advance/refund-requests/cancel
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [HttpPost("advance/refund-request/cancel")]
        [HttpPost("advance/refund-requests/cancel")]
        public async Task<IActionResult> CancelRefundRequest([FromBody] HmsCancelAdvanceRequestDto req)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (status, message) = await _service.CancelAdvanceRequest(req, tenant);

                if (status != "SUCCESS")
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message = "Refund request cancelled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling refund request={id}", req?.request_id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("advance/patient-deposits")]
        public async Task<IActionResult> GetPatientAdvanceDeposits([FromBody] HmsPatientAdvanceDepositFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) = await _service.GetPatientAdvanceDeposits(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching patient advance deposits.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  17. ADVANCE REFUND HISTORY
        //
        //  POST /api/HmsDueCollection/advance/refund-history
        // ═══════════════════════════════════════════════════════════════════════

        [HttpPost("advance/refund-history")]
        public async Task<IActionResult> GetAdvanceRefundHistory([FromBody] HmsAdvanceRefundFilterRequest filter)
        {
            try
            {
                string tenant = ResolveTenantCode();
                var (data, totalCount, summary) = await _service.GetAdvanceRefundHistory(filter, tenant);

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page = filter.page,
                    pagesize = filter.pagesize,
                    summary,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching advance refund history.");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST /api/HmsDueCollection/advance/revert-used
        [HttpPost("advance/revert-used")]
        public async Task<IActionResult> RevertUsedAdvance([FromBody] HmsAdvanceRevertRequest req)
        {
            try
            {
                var (status, data) = await _service.RevertUsedAdvance(req, ResolveTenantCode());
                if (status != "SUCCESS") return BadRequest(new { success = false, message = status });
                return Ok(new { success = true, message = "Used advance reverted to wallet and collected via selected paymode.", data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reverting used advance for bill={rg}", req?.requestguid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET /api/HmsDueCollection/advance/used/{requestguid}
        [HttpGet("advance/used/{requestguid}")]
        public async Task<IActionResult> GetBillAdvanceUsage(string requestguid)
        {
            try
            {
                var (status, data) = await _service.GetBillAdvanceUsage(requestguid, ResolveTenantCode());
                if (status != "SUCCESS") return BadRequest(new { success = false, message = status });
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching advance usage for bill={rg}", requestguid);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
