using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [Route("api/OpStatusConfiguration")]   // alias: both URLs work
    [ApiController]
    public class OpStatusMasterController : ControllerBase
    {
        private readonly OpStatusMasterClass cls;

        public OpStatusMasterController(OpStatusMasterClass _cls)
        {
            cls = _cls;
        }

        private string Tenant => Request.Headers["tenant_code"].ToString().Trim();

        private IActionResult NoTenant() =>
            Unauthorized(new { status = "Error", message = "tenant_code header is required", data = (object?)null });

        // GET api/OpStatusMaster/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            if (string.IsNullOrWhiteSpace(Tenant)) return NoTenant();
            var res = await cls.GetAll(Tenant);
            return StatusCode(res.http_code, res);
        }

        // GET api/OpStatusMaster/WAITING
        [HttpGet("{statusCode}")]
        public async Task<IActionResult> GetByCode(string statusCode)
        {
            if (string.IsNullOrWhiteSpace(Tenant)) return NoTenant();
            var res = await cls.GetByCode(Tenant, statusCode);
            return StatusCode(res.http_code, res);
        }

        // POST api/OpStatusMaster/save
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] OpStatusSaveRequest data)
        {
            if (string.IsNullOrWhiteSpace(Tenant)) return NoTenant();
            var res = await cls.Save(Tenant, data);
            return StatusCode(res.http_code, res);
        }

        // POST api/OpStatusMaster/bulk-save
        [HttpPost("bulk-save")]
        public async Task<IActionResult> BulkSave([FromBody] OpStatusBulkSaveRequest data)
        {
            if (string.IsNullOrWhiteSpace(Tenant)) return NoTenant();
            var res = await cls.BulkSave(Tenant, data);
            return StatusCode(res.http_code, res);
        }

        // POST api/OpStatusMaster/reset/WAITING
        [HttpPost("reset/{statusCode}")]
        public async Task<IActionResult> Reset(string statusCode)
        {
            if (string.IsNullOrWhiteSpace(Tenant)) return NoTenant();
            var res = await cls.Reset(Tenant, statusCode);
            return StatusCode(res.http_code, res);
        }

        // POST api/OpStatusMaster/reset-all
        [HttpPost("reset-all")]
        public async Task<IActionResult> ResetAll()
        {
            if (string.IsNullOrWhiteSpace(Tenant)) return NoTenant();
            var res = await cls.ResetAll(Tenant);
            return StatusCode(res.http_code, res);
        }
    }
}