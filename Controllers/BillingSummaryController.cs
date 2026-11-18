using medico_backend.Class;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BillingSummaryController : ControllerBase
    {
        private readonly BillingSummaryClass cls;

        public BillingSummaryController(BillingSummaryClass _cls)
        {
            cls = _cls;
        }

        // GET api/BillingSummary/op?op_no=OPD/2026/09/0004&custid=10234
        [HttpGet("op")]
        public async Task<IActionResult> GetOpBillingSummary(string op_no, decimal custid)
        {
            if (string.IsNullOrWhiteSpace(op_no))
                return BadRequest(new { message = "op_no is required" });
            if (custid <= 0)
                return BadRequest(new { message = "custid is required" });

            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.GetOpBillingSummary(op_no, custid, tenant);

            if (res is string errMsg)
                return BadRequest(new { message = errMsg });

            return Ok(res);
        }

        // GET api/BillingSummary/ip?ip_no=IPD/2026/09/0002&custid=20456
        [HttpGet("ip")]
        public async Task<IActionResult> GetIpBillingSummary(string ip_no, decimal custid)
        {
            if (string.IsNullOrWhiteSpace(ip_no))
                return BadRequest(new { message = "ip_no is required" });
            if (custid <= 0)
                return BadRequest(new { message = "custid is required" });

            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.GetIpBillingSummary(ip_no, custid, tenant);

            if (res is string errMsg)
                return BadRequest(new { message = errMsg });

            return Ok(res);
        }
        // GET api/BillingSummary/combined?op_no=OPD/2026/09/0004&ip_no=IPD/2026/09/0002&custid=10234
        // GET api/BillingSummary/combined?op_no=OPD/2026/09/0004&custid=10234          (OP only)
        // GET api/BillingSummary/combined?ip_no=IPD/2026/09/0002&custid=10234          (IP only)
        [HttpGet("combined")]
        public async Task<IActionResult> GetCombinedBillingSummary(string? op_no, string? ip_no, decimal custid)
        {
            if (string.IsNullOrWhiteSpace(op_no) && string.IsNullOrWhiteSpace(ip_no))
                return BadRequest(new { message = "Provide at least one of op_no or ip_no" });
            if (custid <= 0)
                return BadRequest(new { message = "custid is required" });

            var tenant = Request.Headers["tenant_code"].ToString();
            var res = await cls.GetCombinedBillingSummary(op_no, ip_no, custid, tenant);

            if (res is string errMsg)
                return BadRequest(new { message = errMsg });

            return Ok(res);
        }
    }
}