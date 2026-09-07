using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    // ─────────────────────────────────────────────────────────────
    // OT (OPERATION THEATER) CONTROLLER
    // Pages: OT Master | Surgery Request | OT Schedule | OT Team |
    // Pre-Op Assessment | Consent | Checklist | Anesthesia |
    // Intra-Op | Consumables | Implants | Recovery/PACU |
    // Post-Op Orders | Operation Note | OT Summary
    //
    // BILLING: there is no bill/create endpoint. The procedure fee
    // (surgery-request/add) and every consumable/implant push straight
    // into unbilledcharges the moment they're saved, so they simply
    // appear on your existing common OP/IP billing screen — the same
    // way investigation tests do today.
    // ─────────────────────────────────────────────────────────────
    [Route("api/[controller]")]
    [ApiController]
    public class OTController : ControllerBase
    {
        private readonly OTClass cls;

        public OTController(OTClass _cls)
        {
            cls = _cls;
        }

        private string GetTenantCode() =>
            Request.Headers["tenant_code"].FirstOrDefault() ?? string.Empty;

        // ═══════════════════════════════════════════════════════
        // 1. OT MASTER
        // ═══════════════════════════════════════════════════════

        [HttpPost("master/add")]
        public async Task<IActionResult> AddOtMaster([FromBody] AddOtMasterRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddOtMaster(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpPost("master/update")]
        public async Task<IActionResult> UpdateOtMaster([FromBody] UpdateOtMasterRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateOtMaster(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        [HttpGet("master/list")]
        public async Task<IActionResult> GetOtMasterList([FromQuery] bool activeOnly = false)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOtMasterList(tenant, activeOnly));
        }

        [HttpGet("master/{ot_id}")]
        public async Task<IActionResult> GetOtMasterById(Guid ot_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtMasterById(ot_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "OT not found" });
        }

        [HttpGet("master/delete")]
        public async Task<IActionResult> DeleteOtMaster([FromQuery] Guid ot_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteOtMaster(ot_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 2. SURGERY REQUEST
        // ═══════════════════════════════════════════════════════

        // POST api/OT/surgery-request/add
        // "procedure_fee": 5000.00  -> automatically pushed to unbilledcharges
        [HttpPost("surgery-request/add")]
        public async Task<IActionResult> AddSurgeryRequest([FromBody] AddSurgeryRequestRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddSurgeryRequest(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpPost("surgery-request/update")]
        public async Task<IActionResult> UpdateSurgeryRequest([FromBody] UpdateSurgeryRequestRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateSurgeryRequest(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        [HttpPost("surgery-request/status")]
        public async Task<IActionResult> UpdateSurgeryStatus([FromBody] UpdateSurgeryStatusRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateSurgeryStatus(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        [HttpGet("surgery-request/{request_id}")]
        public async Task<IActionResult> GetSurgeryRequestById(Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetSurgeryRequestById(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Surgery request not found" });
        }

        [HttpGet("surgery-request/list")]
        public async Task<IActionResult> GetSurgeryRequestList(
            [FromQuery] string? status, [FromQuery] decimal? custid)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetSurgeryRequestList(tenant, status, custid));
        }

        [HttpGet("surgery-request/cancel")]
        public async Task<IActionResult> CancelSurgeryRequest([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.CancelSurgeryRequest(request_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 3. OT SCHEDULE
        // ═══════════════════════════════════════════════════════

        [HttpPost("schedule/add")]
        public async Task<IActionResult> ScheduleSurgery([FromBody] AddOtScheduleRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.ScheduleSurgery(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpPost("schedule/update")]
        public async Task<IActionResult> UpdateOtSchedule([FromBody] UpdateOtScheduleRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateOtSchedule(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("schedule/calendar")]
        public async Task<IActionResult> GetOtCalendar(
            [FromQuery] Guid? ot_id, [FromQuery] DateOnly? from_date, [FromQuery] DateOnly? to_date)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOtCalendar(ot_id, from_date, to_date, tenant));
        }

        [HttpGet("schedule/by-request")]
        public async Task<IActionResult> GetScheduleByRequest([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetScheduleByRequest(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Schedule not found" });
        }

        [HttpGet("schedule/cancel")]
        public async Task<IActionResult> CancelOtSchedule([FromQuery] Guid schedule_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.CancelOtSchedule(schedule_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 4. OT TEAM
        // ═══════════════════════════════════════════════════════

        [HttpPost("team/add")]
        public async Task<IActionResult> AddOtTeamMember([FromBody] AddOtTeamMemberRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddOtTeamMember(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("team/by-request")]
        public async Task<IActionResult> GetOtTeam([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOtTeam(request_id, tenant));
        }

        [HttpGet("team/remove")]
        public async Task<IActionResult> RemoveOtTeamMember([FromQuery] Guid team_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.RemoveOtTeamMember(team_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 5. PRE-OP ASSESSMENT
        // ═══════════════════════════════════════════════════════

        [HttpPost("preop/add")]
        public async Task<IActionResult> AddPreOpAssessment([FromBody] AddPreOpAssessmentRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddPreOpAssessment(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpPost("preop/update")]
        public async Task<IActionResult> UpdatePreOpAssessment([FromBody] UpdatePreOpAssessmentRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdatePreOpAssessment(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        [HttpGet("preop/by-request")]
        public async Task<IActionResult> GetPreOpAssessment([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetPreOpAssessment(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Pre-op assessment not found" });
        }

        // ═══════════════════════════════════════════════════════
        // 6. CONSENT
        // ═══════════════════════════════════════════════════════

        [HttpPost("consent/add")]
        public async Task<IActionResult> AddOtConsent([FromBody] AddOtConsentRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddOtConsent(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("consent/by-request")]
        public async Task<IActionResult> GetOtConsents([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOtConsents(request_id, tenant));
        }

        // ═══════════════════════════════════════════════════════
        // 7. OT CHECKLIST
        // ═══════════════════════════════════════════════════════

        [HttpPost("checklist/save")]
        public async Task<IActionResult> SaveOtChecklist([FromBody] SaveOtChecklistRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.SaveOtChecklist(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("checklist/by-request")]
        public async Task<IActionResult> GetOtChecklist([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtChecklist(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Checklist not found" });
        }

        // ═══════════════════════════════════════════════════════
        // 8. ANESTHESIA
        // ═══════════════════════════════════════════════════════

        [HttpPost("anesthesia/save")]
        public async Task<IActionResult> SaveOtAnesthesia([FromBody] SaveOtAnesthesiaRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.SaveOtAnesthesia(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("anesthesia/by-request")]
        public async Task<IActionResult> GetOtAnesthesia([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtAnesthesia(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Anesthesia record not found" });
        }

        // ═══════════════════════════════════════════════════════
        // 9. INTRA-OP / SURGERY RECORD
        // ═══════════════════════════════════════════════════════

        [HttpPost("intraop/save")]
        public async Task<IActionResult> SaveOtIntraOp([FromBody] SaveOtIntraOpRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.SaveOtIntraOp(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("intraop/by-request")]
        public async Task<IActionResult> GetOtIntraOp([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtIntraOp(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Intra-op record not found" });
        }

        // ═══════════════════════════════════════════════════════
        // 10. CONSUMABLES
        // POST body pushes an OT_CONSUMABLE row into unbilledcharges.
        // ═══════════════════════════════════════════════════════

        [HttpPost("consumable/add")]
        public async Task<IActionResult> AddOtConsumable([FromBody] AddOtConsumableRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddOtConsumable(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("consumable/by-request")]
        public async Task<IActionResult> GetOtConsumables([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOtConsumables(request_id, tenant));
        }

        [HttpGet("consumable/delete")]
        public async Task<IActionResult> DeleteOtConsumable([FromQuery] Guid consumable_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteOtConsumable(consumable_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 11. IMPLANTS
        // POST body pushes an OT_IMPLANT row into unbilledcharges.
        // ═══════════════════════════════════════════════════════

        [HttpPost("implant/add")]
        public async Task<IActionResult> AddOtImplant([FromBody] AddOtImplantRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddOtImplant(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("implant/by-request")]
        public async Task<IActionResult> GetOtImplants([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOtImplants(request_id, tenant));
        }

        [HttpGet("implant/delete")]
        public async Task<IActionResult> DeleteOtImplant([FromQuery] Guid implant_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteOtImplant(implant_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 12. RECOVERY / PACU
        // ═══════════════════════════════════════════════════════

        [HttpPost("recovery/save")]
        public async Task<IActionResult> SaveOtRecovery([FromBody] SaveOtRecoveryRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.SaveOtRecovery(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("recovery/by-request")]
        public async Task<IActionResult> GetOtRecovery([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtRecovery(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Recovery record not found" });
        }

        // ═══════════════════════════════════════════════════════
        // 13. POST-OP ORDERS
        // ═══════════════════════════════════════════════════════

        [HttpPost("postop-order/add")]
        public async Task<IActionResult> AddOtPostOpOrder([FromBody] AddOtPostOpOrderRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddOtPostOpOrder(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpPost("postop-order/status")]
        public async Task<IActionResult> UpdateOtPostOpOrderStatus([FromBody] UpdateOtPostOpOrderStatusRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateOtPostOpOrderStatus(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        [HttpGet("postop-order/by-request")]
        public async Task<IActionResult> GetOtPostOpOrders([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOtPostOpOrders(request_id, tenant));
        }

        [HttpGet("postop-order/delete")]
        public async Task<IActionResult> DeleteOtPostOpOrder([FromQuery] Guid order_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteOtPostOpOrder(order_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 14. OPERATION NOTE
        // ═══════════════════════════════════════════════════════

        [HttpPost("operation-note/save")]
        public async Task<IActionResult> SaveOtOperationNote([FromBody] SaveOtOperationNoteRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.SaveOtOperationNote(req, tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        [HttpGet("operation-note/by-request")]
        public async Task<IActionResult> GetOtOperationNote([FromQuery] Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtOperationNote(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Operation note not found" });
        }

        // ═══════════════════════════════════════════════════════
        // 15. OT SUMMARY
        // total_charge_count / pending_charge_count / is_fully_billed
        // are computed live from unbilledcharges — no separate bill step.
        // ═══════════════════════════════════════════════════════

        [HttpGet("summary/{request_id}")]
        public async Task<IActionResult> GetOtSummary(Guid request_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtSummary(request_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Surgery request not found" });
        }
    }
}