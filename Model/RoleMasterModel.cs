using System;
using System.Collections.Generic;

namespace Medico_Backend.Model
{
    public class RolePermissionItem
    {
        public long Role_id { get; set; }
        public string? Main_module { get; set; }
        public string? Sub_module { get; set; }
        public string? Module { get; set; }
        public string? Product_id { get; set; }
    }

    public class RoleSummaryDto
    {
        public Guid RoleNameGUID { get; set; }
        public string Role_Name { get; set; } = string.Empty;
        public string Tenant_code { get; set; } = string.Empty;
        public int TotalPermissionsAssigned { get; set; }
        public List<long> AssignedRoleIds { get; set; } = new();
        public List<string> MainModulesCovered { get; set; } = new();
    }

    public class RoleSaveRequest
    {
        public Guid? RoleNameGUID { get; set; }
        public string Role_Name { get; set; } = string.Empty;
        public List<long> Role_ids { get; set; } = new();
    }
}
