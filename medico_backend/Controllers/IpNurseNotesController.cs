using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;
using static medico_backend.Model.IpBedsideChartModel;

namespace medico_backend.Controllers
{
    // ─────────────────────────────────────────────────────────────
    // IP BEDSIDE CHART CONTROLLER  (a.k.a. "IP Nurse Notes")
    // Named separately from NurseNotesController/NurseNotesClass
    // to avoid any collision with the existing module.
    //
    // Tabs: Temperature | Vitals | Input/Output | Service |
    //       Medicine | Other
    // Each tab has its own Save button -> its own add/get/delete
    // endpoints below, all scoped to a single ip_id.
    //
    // OP Case Sheet is intentionally NOT here — reuse:
    //   GET api/CaseSheet/by-visit?ip_id=...
    // ─────────────────────────────────────────────────────────────
    [Route("api/[controller]")]
    [ApiController]
    public class IpBedsideChartController : ControllerBase
    {
        private readonly IpBedsideChartClass cls;

        public IpBedsideChartController(IpBedsideChartClass _cls)
        {
            cls = _cls;
        }

        private string GetTenantCode() =>
            Request.Headers["tenant_code"].FirstOrDefault() ?? string.Empty;

        // Logged-in nurse's usercode. Replace this with however the rest
        // of the app resolves the current user (claims/session/header) —
        // kept as a header read here so it matches the tenant_code pattern.
        private int GetUserCode() =>
            int.TryParse(Request.Headers["usercode"].FirstOrDefault(), out int uc) ? uc : 1;

        // ═══════════════════════════════════════════════════════
        // 1. TEMPERATURE
        // ═══════════════════════════════════════════════════════

        // POST api/IpBedsideChart/temperature/add
        // { "ip_id": "...", "custid": 1001, "shift": "MORNING",
        //   "temperature": 98.4, "unit": "F", "remarks": "Normal" }
        [HttpPost("temperature/add")]
        public async Task<IActionResult> AddTemperature([FromBody] AddTemperatureRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddTemperature(req, GetUserCode(), tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        // POST api/IpBedsideChart/temperature/update
        // { "temp_id": "...", "shift": "MORNING", "temperature": 99.1, "unit": "F", "remarks": "" }
        [HttpPost("temperature/update")]
        public async Task<IActionResult> UpdateTemperature([FromBody] UpdateTemperatureRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateTemperature(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/IpBedsideChart/temperature/{ip_id}
        [HttpGet("temperature/{ip_id}")]
        public async Task<IActionResult> GetTemperature(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetTemperature(ip_id, tenant));
        }

        // GET api/IpBedsideChart/temperature/delete?temp_id=...
        [HttpGet("temperature/delete")]
        public async Task<IActionResult> DeleteTemperature([FromQuery] Guid temp_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteTemperature(temp_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 2. VITALS — no endpoints here on purpose.
        // The "Vitals" tab in the UI should call the module that
        // already owns vitals, passing ip_id the same way it
        // already accepts op_id:
        //   POST api/OpRegistration/save-vitals
        //   POST api/OpRegistration/update-vitals
        //   GET  api/OpRegistration/vitals/all?ip_id=...
        //   GET  api/OpRegistration/vitals/detail?ip_id=...
        // ═══════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════
        // 3. INPUT / OUTPUT
        // ═══════════════════════════════════════════════════════

        // POST api/IpBedsideChart/input-output/add
        [HttpPost("input-output/add")]
        public async Task<IActionResult> AddInputOutput([FromBody] AddInputOutputRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddInputOutput(req, GetUserCode(), tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        // POST api/IpBedsideChart/input-output/update
        [HttpPost("input-output/update")]
        public async Task<IActionResult> UpdateInputOutput([FromBody] UpdateInputOutputRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateInputOutput(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/IpBedsideChart/input-output/{ip_id}
        [HttpGet("input-output/{ip_id}")]
        public async Task<IActionResult> GetInputOutput(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetInputOutput(ip_id, tenant));
        }

        // GET api/IpBedsideChart/input-output/delete?io_id=...
        [HttpGet("input-output/delete")]
        public async Task<IActionResult> DeleteInputOutput([FromQuery] Guid io_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteInputOutput(io_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 4. SERVICE
        // ═══════════════════════════════════════════════════════

        // POST api/IpBedsideChart/service/add
        [HttpPost("service/add")]
        public async Task<IActionResult> AddService([FromBody] AddServiceRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddService(req, GetUserCode(), tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        // POST api/IpBedsideChart/service/update
        [HttpPost("service/update")]
        public async Task<IActionResult> UpdateService([FromBody] UpdateServiceRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateService(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/IpBedsideChart/service/{ip_id}
        [HttpGet("service/{ip_id}")]
        public async Task<IActionResult> GetService(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetService(ip_id, tenant));
        }

        // GET api/IpBedsideChart/service/delete?service_id=...
        [HttpGet("service/delete")]
        public async Task<IActionResult> DeleteService([FromQuery] Guid service_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteService(service_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 5. MEDICINE
        // ═══════════════════════════════════════════════════════

        // POST api/IpBedsideChart/medicine/add
        [HttpPost("medicine/add")]
        public async Task<IActionResult> AddMedicine([FromBody] AddMedicineRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddMedicine(req, GetUserCode(), tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        // POST api/IpBedsideChart/medicine/update
        [HttpPost("medicine/update")]
        public async Task<IActionResult> UpdateMedicine([FromBody] UpdateMedicineRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateMedicine(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/IpBedsideChart/medicine/{ip_id}
        [HttpGet("medicine/{ip_id}")]
        public async Task<IActionResult> GetMedicine(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetMedicine(ip_id, tenant));
        }

        // GET api/IpBedsideChart/medicine/delete?med_id=...
        [HttpGet("medicine/delete")]
        public async Task<IActionResult> DeleteMedicine([FromQuery] Guid med_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteMedicine(med_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 6. OTHER
        // ═══════════════════════════════════════════════════════

        // POST api/IpBedsideChart/other/add
        [HttpPost("other/add")]
        public async Task<IActionResult> AddOther([FromBody] AddOtherRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddOther(req, GetUserCode(), tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        // POST api/IpBedsideChart/other/update
        [HttpPost("other/update")]
        public async Task<IActionResult> UpdateOther([FromBody] UpdateOtherRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateOther(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/IpBedsideChart/other/{ip_id}
        [HttpGet("other/{ip_id}")]
        public async Task<IActionResult> GetOther(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetOther(ip_id, tenant));
        }

        // GET api/IpBedsideChart/other/delete?other_id=...
        [HttpGet("other/delete")]
        public async Task<IActionResult> DeleteOther([FromQuery] Guid other_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteOther(other_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // COMBINED — Temperature + Input/Output + Service + Medicine
        // + Other for a patient in one call. Vitals and OP Case
        // Sheet are NOT included (fetch those separately from
        // api/OpRegistration/vitals/all?ip_id=... and
        // api/CaseSheet/by-visit?ip_id=... — see notes above).
        // ═══════════════════════════════════════════════════════

        // GET api/IpBedsideChart/summary/{ip_id}
        [HttpGet("summary/{ip_id}")]
        public async Task<IActionResult> GetSummary(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetAllNurseNotes(ip_id, tenant));
        }
        // GET api/IpBedsideChart/temperature/{ip_id}/detail?temp_id=...
        [HttpGet("temperature/{ip_id}/detail")]
        public async Task<IActionResult> GetTemperatureById(Guid ip_id, [FromQuery] Guid temp_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetTemperatureById(ip_id, temp_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Record not found" });
        }
        // GET api/IpBedsideChart/input-output/{ip_id}/detail?io_id=...
        [HttpGet("input-output/{ip_id}/detail")]
        public async Task<IActionResult> GetInputOutputById(Guid ip_id, [FromQuery] Guid io_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetInputOutputById(ip_id, io_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Record not found" });
        }
        // GET api/IpBedsideChart/service/{ip_id}/detail?service_id=...
        [HttpGet("service/{ip_id}/detail")]
        public async Task<IActionResult> GetServiceById(Guid ip_id, [FromQuery] Guid service_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetServiceById(ip_id, service_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Record not found" });
        }
        // GET api/IpBedsideChart/medicine/{ip_id}/detail?med_id=...
        [HttpGet("medicine/{ip_id}/detail")]
        public async Task<IActionResult> GetMedicineById(Guid ip_id, [FromQuery] Guid med_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetMedicineById(ip_id, med_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Record not found" });
        }
        // GET api/IpBedsideChart/other/{ip_id}/detail?other_id=...
        [HttpGet("other/{ip_id}/detail")]
        public async Task<IActionResult> GetOtherById(Guid ip_id, [FromQuery] Guid other_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetOtherById(ip_id, other_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Record not found" });
        }
        // ═══════════════════════════════════════════════════════
        // 7. VISITING DOCTOR LIST
        // ═══════════════════════════════════════════════════════

        // POST api/IpBedsideChart/visiting-doctor/add
        // { "ip_id": "...", "custid": 1001, "shift": "MORNING",
        //   "doctor_name": "Dr. Karthik Raja", "specialization": "Cardiology",
        //   "visit_time": "10:30:00", "diagnosis_notes": "Stable, mild tachycardia",
        //   "advice": "Continue current medication, review tomorrow", "remarks": "" }
        [HttpPost("visiting-doctor/add")]
        public async Task<IActionResult> AddVisitingDoctor([FromBody] AddVisitingDoctorRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddVisitingDoctor(req, GetUserCode(), tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        // POST api/IpBedsideChart/visiting-doctor/update
        [HttpPost("visiting-doctor/update")]
        public async Task<IActionResult> UpdateVisitingDoctor([FromBody] UpdateVisitingDoctorRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateVisitingDoctor(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/IpBedsideChart/visiting-doctor/{ip_id}
        [HttpGet("visiting-doctor/{ip_id}")]
        public async Task<IActionResult> GetVisitingDoctor(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetVisitingDoctor(ip_id, tenant));
        }

        // GET api/IpBedsideChart/visiting-doctor/{ip_id}/detail?visit_id=...
        [HttpGet("visiting-doctor/{ip_id}/detail")]
        public async Task<IActionResult> GetVisitingDoctorById(Guid ip_id, [FromQuery] Guid visit_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetVisitingDoctorById(ip_id, visit_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Record not found" });
        }

        // GET api/IpBedsideChart/visiting-doctor/delete?visit_id=...
        [HttpGet("visiting-doctor/delete")]
        public async Task<IActionResult> DeleteVisitingDoctor([FromQuery] Guid visit_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteVisitingDoctor(visit_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════════════════════
        // 8. SCHEDULE
        // ═══════════════════════════════════════════════════════

        // POST api/IpBedsideChart/schedule/add
        // { "ip_id": "...", "custid": 1001, "shift": "MORNING",
        //   "scheduled_date": "2026-09-04", "scheduled_time": "08:00:00",
        //   "item_type": "MEDICINE", "item_name": "Insulin 10 units",
        //   "instructions": "Give before breakfast", "status": "PENDING" }
        [HttpPost("schedule/add")]
        public async Task<IActionResult> AddSchedule([FromBody] AddScheduleRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddSchedule(req, GetUserCode(), tenant);
            return res.StartsWith("Success") ? Ok(new { message = res }) : BadRequest(new { message = res });
        }

        // POST api/IpBedsideChart/schedule/update
        [HttpPost("schedule/update")]
        public async Task<IActionResult> UpdateSchedule([FromBody] UpdateScheduleRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateSchedule(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/IpBedsideChart/schedule/{ip_id}
        [HttpGet("schedule/{ip_id}")]
        public async Task<IActionResult> GetSchedule(Guid ip_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetSchedule(ip_id, tenant));
        }

        // GET api/IpBedsideChart/schedule/{ip_id}/detail?schedule_id=...
        [HttpGet("schedule/{ip_id}/detail")]
        public async Task<IActionResult> GetScheduleById(Guid ip_id, [FromQuery] Guid schedule_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.GetScheduleById(ip_id, schedule_id, tenant);
            return res != null ? Ok(res) : NotFound(new { message = "Record not found" });
        }

        // GET api/IpBedsideChart/schedule/delete?schedule_id=...
        [HttpGet("schedule/delete")]
        public async Task<IActionResult> DeleteSchedule([FromQuery] Guid schedule_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteSchedule(schedule_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }
    }
}