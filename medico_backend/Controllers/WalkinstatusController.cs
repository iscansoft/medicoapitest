using medico_backend.Class;
using medico_backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OpStatusMasterController : ControllerBase
    {
        private readonly OpStatusMasterClass cls;

        public OpStatusMasterController(OpStatusMasterClass _cls)
        {
            cls = _cls;
        }

        // POST api/OpStatusMaster/create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] OpStatusMasterModel data)
        {
            data.tenant_code = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.Create(data));
        }

        // POST api/OpStatusMaster/update
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] OpStatusMasterModel data)
        {
            data.tenant_code = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.Update(data));
        }

        // POST api/OpStatusMaster/delete?op_status_id=1
        [HttpPost("delete")]
        public async Task<IActionResult> Delete(int op_status_id)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.Delete(op_status_id, tenant));
        }

        // GET api/OpStatusMaster/all?activeOnly=true   (use this for the dropdown)
        [HttpGet("all")]
        public async Task<IActionResult> GetAll(bool activeOnly = false)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await cls.GetAll(tenant, activeOnly));
        }
    }
}