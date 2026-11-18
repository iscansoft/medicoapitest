using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    // ─────────────────────────────────────────────────────────────
    // MASTER LIST CONTROLLER
    // Simple lookup masters (integer PK) used as dropdown sources:
    //   IO Particulars  -> used in Input/Output tab (particulars field)
    //   Service Names   -> used in Service tab (service_name field)
    //   Schedule Types  -> used in Schedule tab (item_type field)
    // ─────────────────────────────────────────────────────────────
    [Route("api/[controller]")]
    [ApiController]
    public class MasterListController : ControllerBase
    {
        private readonly MasterListClass cls;

        public MasterListController(MasterListClass _cls)
        {
            cls = _cls;
        }

        private string GetTenantCode() =>
            Request.Headers["tenant_code"].FirstOrDefault() ?? string.Empty;

        // ═══════════════════════════════════════
        // 1. IO PARTICULARS MASTER
        // ═══════════════════════════════════════

        // POST api/MasterList/io-particulars/add
        // { "particular_name": "Oral Fluids", "io_type": "INPUT" }
        [HttpPost("io-particulars/add")]
        public async Task<IActionResult> AddIoParticular([FromBody] AddIoParticularRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddIoParticular(req, tenant);
            return Ok(new { message = res });
        }

        // POST api/MasterList/io-particulars/update
        [HttpPost("io-particulars/update")]
        public async Task<IActionResult> UpdateIoParticular([FromBody] UpdateIoParticularRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateIoParticular(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/MasterList/io-particulars?activeOnly=true
        [HttpGet("io-particulars")]
        public async Task<IActionResult> GetIoParticulars([FromQuery] bool activeOnly = true)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetIoParticulars(tenant, activeOnly));
        }

        // GET api/MasterList/io-particulars/delete?particular_id=1
        [HttpGet("io-particulars/delete")]
        public async Task<IActionResult> DeleteIoParticular([FromQuery] int particular_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteIoParticular(particular_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════
        // 2. SERVICE NAME MASTER
        // ═══════════════════════════════════════

        // POST api/MasterList/service-name/add
        // { "service_name": "Nebulization" }
        [HttpPost("service-name/add")]
        public async Task<IActionResult> AddServiceName([FromBody] AddServiceNameRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddServiceName(req, tenant);
            return Ok(new { message = res });
        }

        // POST api/MasterList/service-name/update
        [HttpPost("service-name/update")]
        public async Task<IActionResult> UpdateServiceName([FromBody] UpdateServiceNameRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateServiceName(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/MasterList/service-name?activeOnly=true
        [HttpGet("service-name")]
        public async Task<IActionResult> GetServiceNames([FromQuery] bool activeOnly = true)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetServiceNames(tenant, activeOnly));
        }

        // GET api/MasterList/service-name/delete?service_id=1
        [HttpGet("service-name/delete")]
        public async Task<IActionResult> DeleteServiceName([FromQuery] int service_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteServiceName(service_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // ═══════════════════════════════════════
        // 3. SCHEDULE TYPE MASTER
        // ═══════════════════════════════════════

        // POST api/MasterList/schedule-type/add
        // { "type_name": "PROCEDURE" }
        [HttpPost("schedule-type/add")]
        public async Task<IActionResult> AddScheduleType([FromBody] AddScheduleTypeRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.AddScheduleType(req, tenant);
            return Ok(new { message = res });
        }

        // POST api/MasterList/schedule-type/update
        [HttpPost("schedule-type/update")]
        public async Task<IActionResult> UpdateScheduleType([FromBody] UpdateScheduleTypeRequest req)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.UpdateScheduleType(req, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }

        // GET api/MasterList/schedule-type?activeOnly=true
        [HttpGet("schedule-type")]
        public async Task<IActionResult> GetScheduleTypes([FromQuery] bool activeOnly = true)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            return Ok(await cls.GetScheduleTypes(tenant, activeOnly));
        }

        // GET api/MasterList/schedule-type/delete?type_id=1
        [HttpGet("schedule-type/delete")]
        public async Task<IActionResult> DeleteScheduleType([FromQuery] int type_id)
        {
            var tenant = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenant)) return BadRequest("tenant_code header is required");

            var res = await cls.DeleteScheduleType(type_id, tenant);
            return res == "Success" ? Ok(new { message = res }) : NotFound(new { message = res });
        }
    }
}