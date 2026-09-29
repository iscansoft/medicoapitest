using System;
using System.Linq;
using System.Threading.Tasks;
using Medico_Backend.Class;
using Medico_Backend.Model;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoleMasterController : ControllerBase
    {
        private readonly RoleMasterClass _roleMasterClass;

        public RoleMasterController(RoleMasterClass roleMasterClass)
        {
            _roleMasterClass = roleMasterClass;
        }

        private string? GetTenantCode()
        {
            var code = Request.Headers["tenant_code"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(code))
                code = User.FindFirst("tenant_code")?.Value;
            return code;
        }

        [HttpGet("master-roles")]
        public async Task<IActionResult> GetMasterRoles([FromQuery] string productId = "MEDICO_APP")
        {
            var result = await _roleMasterClass.GetMasterPermissionsAsync(productId);
            return Ok(result);
        }

        [HttpGet("get-roles")]
        public async Task<IActionResult> GetRoles()
        {
            var tenantCode = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenantCode))
                return BadRequest("tenant_code header is required.");

            var roles = await _roleMasterClass.GetRolesByTenantAsync(tenantCode);
            return Ok(roles);
        }

        [HttpGet("get-role")]
        public async Task<IActionResult> GetRole([FromQuery] Guid roleGuid)
        {
            var tenantCode = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenantCode))
                return BadRequest("tenant_code header is required.");

            var role = await _roleMasterClass.GetRoleByGuidAsync(roleGuid, tenantCode);
            return role != null ? Ok(role) : NotFound("Role template not found.");
        }

        [HttpPost("save-role")]
        public async Task<IActionResult> SaveRole([FromBody] RoleSaveRequest req)
        {
            var tenantCode = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenantCode))
                return BadRequest("tenant_code header is required.");

            if (req == null || string.IsNullOrWhiteSpace(req.Role_Name))
                return BadRequest("Role Name is required.");

            var roleGuid = await _roleMasterClass.SaveRoleAsync(req, tenantCode);
            return Ok(new { success = true, roleGuid, message = "Role template saved successfully." });
        }

        [HttpDelete("delete-role")]
        public async Task<IActionResult> DeleteRole([FromQuery] Guid roleGuid)
        {
            var tenantCode = GetTenantCode();
            if (string.IsNullOrWhiteSpace(tenantCode))
                return BadRequest("tenant_code header is required.");

            var deleted = await _roleMasterClass.DeleteRoleAsync(roleGuid, tenantCode);
            return Ok(new { success = true, deleted, message = "Role template deleted successfully." });
        }

        [HttpPost("delete-role")]
        public async Task<IActionResult> DeleteRolePost([FromQuery] Guid roleGuid)
        {
            return await DeleteRole(roleGuid);
        }
    }
}
