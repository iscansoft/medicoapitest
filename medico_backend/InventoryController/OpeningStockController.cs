using medico_backend.InventoryClass;
using medico_backend.InventoryModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.InventoryController
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class OpeningStockController : ControllerBase
    {
        private readonly OpeningStockUploadClass _svc;

        public OpeningStockController(OpeningStockUploadClass svc)
        {
            _svc = svc;
        }

        private string GetTenantCode() =>
            HttpContext.Items["TenantCode"]?.ToString()
            ?? Request.Headers["tenantcode"].FirstOrDefault()
            ?? string.Empty;

        // ─── UPLOAD ───────────────────────────────────────────────────────────
        // POST api/OpeningStock/upload-opening-stock   (multipart/form-data)
        [HttpPost("upload-opening-stock")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadOpeningStock([FromForm] opening_stock_upload_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return BadRequest(new { Status = "Failed", Message = "Header 'tenantcode' is required." });

                if (request == null || request.file == null || request.file.Length == 0)
                    return BadRequest(new { Status = "Failed", Message = "Excel file is required." });

                if (request.warehousecode <= 0)
                    return BadRequest(new { Status = "Failed", Message = "warehousecode is required." });

                var ext = Path.GetExtension(request.file.FileName).ToLowerInvariant();
                if (ext != ".xlsx")
                    return BadRequest(new { Status = "Failed", Message = "Only .xlsx files are supported." });

                if (request.file.Length > 10 * 1024 * 1024)
                    return BadRequest(new { Status = "Failed", Message = "File size must be 10 MB or less." });

                var result = await _svc.UploadOpeningStock(request, tenantcode);

                return result.success
                    ? Ok(new { Status = "Success", Message = result.message, Data = result })
                    : BadRequest(new { Status = "Failed", Message = result.message, Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── TEMPLATE DOWNLOAD ────────────────────────────────────────────────
        // GET api/OpeningStock/download-template
        [HttpGet("download-template")]
        public IActionResult DownloadTemplate()
        {
            try
            {
                var bytes = _svc.BuildTemplate();
                return File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "OpeningStockTemplate.xlsx");
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
    }
}