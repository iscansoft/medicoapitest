using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Medico_Backend.Model;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Medico_Backend.Class
{
    public class RoleMasterClass
    {
        private readonly string _masterConnStr;

        public RoleMasterClass(IConfiguration config)
        {
            var tenConn = config.GetConnectionString("ten_conn");
            var masterConn = config.GetConnectionString("master_conn");

            if (!string.IsNullOrWhiteSpace(tenConn))
            {
                _masterConnStr = tenConn;
            }
            else if (!string.IsNullOrWhiteSpace(masterConn))
            {
                _masterConnStr = masterConn;
            }
            else
            {
                var conn = config.GetConnectionString("conn") ?? string.Empty;
                var builder = new NpgsqlConnectionStringBuilder(conn) { Database = "MasterTenantDB" };
                _masterConnStr = builder.ConnectionString;
            }
        }

        // 1. Get all static catalog permissions for MEDICO_APP (364 items)
        public async Task<List<RolePermissionItem>> GetMasterPermissionsAsync(string productId = "MEDICO_APP")
        {
            using IDbConnection db = new NpgsqlConnection(_masterConnStr);
            string sql = @"
                SELECT ""Role_id"", 
                       COALESCE(""Main_module"", 'General') AS ""Main_module"", 
                       COALESCE(""Sub_module"", 'General') AS ""Sub_module"", 
                       ""Module"", 
                       ""Product_id""
                FROM public.""ROLE_MASTER""
                WHERE ""Product_id"" = @productId
                ORDER BY ""Role_id"";";

            var res = await db.QueryAsync<RolePermissionItem>(sql, new { productId });
            return res.ToList();
        }

        // 2. Get saved roles for a tenant
        public async Task<List<RoleSummaryDto>> GetRolesByTenantAsync(string tenantCode)
        {
            using IDbConnection db = new NpgsqlConnection(_masterConnStr);
            string sql = @"
                SELECT r.""RoleNameGUID"", r.""Role_Name"", r.tenant_code, r.""Role_id"", 
                       COALESCE(m.""Main_module"", 'General') AS ""Main_module""
                FROM public.""ROLE_NAME_MASTER"" r
                LEFT JOIN public.""ROLE_MASTER"" m ON r.""Role_id"" = m.""Role_id""
                WHERE r.tenant_code = @tenantCode
                ORDER BY r.""Role_Name"", r.""Role_id"";";

            var records = (await db.QueryAsync<dynamic>(sql, new { tenantCode })).ToList();
            var result = new List<RoleSummaryDto>();

            var grouped = records.GroupBy(x => (Guid)x.RoleNameGUID);
            foreach (var g in grouped)
            {
                var first = g.First();
                var assignedIds = g.Select(x => (long)x.Role_id).Distinct().ToList();
                var mainModules = g.Select(x => (string)x.Main_module)
                                   .Where(m => !string.IsNullOrEmpty(m))
                                   .Distinct()
                                   .ToList();

                result.Add(new RoleSummaryDto
                {
                    RoleNameGUID = g.Key,
                    Role_Name = (string)first.Role_Name,
                    Tenant_code = (string)first.tenant_code,
                    TotalPermissionsAssigned = assignedIds.Count,
                    AssignedRoleIds = assignedIds,
                    MainModulesCovered = mainModules
                });
            }
            return result;
        }

        // 3. Get single role details
        public async Task<RoleSummaryDto?> GetRoleByGuidAsync(Guid roleGuid, string tenantCode)
        {
            using IDbConnection db = new NpgsqlConnection(_masterConnStr);
            string sql = @"
                SELECT r.""RoleNameGUID"", r.""Role_Name"", r.tenant_code, r.""Role_id"", 
                       COALESCE(m.""Main_module"", 'General') AS ""Main_module""
                FROM public.""ROLE_NAME_MASTER"" r
                LEFT JOIN public.""ROLE_MASTER"" m ON r.""Role_id"" = m.""Role_id""
                WHERE r.""RoleNameGUID"" = @roleGuid AND r.tenant_code = @tenantCode;";

            var records = (await db.QueryAsync<dynamic>(sql, new { roleGuid, tenantCode })).ToList();
            if (!records.Any()) return null;

            var first = records.First();
            return new RoleSummaryDto
            {
                RoleNameGUID = roleGuid,
                Role_Name = (string)first.Role_Name,
                Tenant_code = (string)first.tenant_code,
                TotalPermissionsAssigned = records.Count,
                AssignedRoleIds = records.Select(x => (long)x.Role_id).Distinct().ToList(),
                MainModulesCovered = records.Select(x => (string)x.Main_module).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList()
            };
        }

        // 4. Save/Update role with transaction
        public async Task<Guid> SaveRoleAsync(RoleSaveRequest req, string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(req.Role_Name))
                throw new ArgumentException("Role name cannot be empty.");

            var roleGuid = req.RoleNameGUID.HasValue && req.RoleNameGUID.Value != Guid.Empty
                ? req.RoleNameGUID.Value
                : Guid.NewGuid();

            using var conn = new NpgsqlConnection(_masterConnStr);
            await conn.OpenAsync();
            using var trans = await conn.BeginTransactionAsync();

            try
            {
                // Delete existing records for this role GUID & tenant
                string deleteSql = @"DELETE FROM public.""ROLE_NAME_MASTER"" WHERE ""RoleNameGUID"" = @roleGuid AND tenant_code = @tenantCode;";
                await conn.ExecuteAsync(deleteSql, new { roleGuid, tenantCode }, trans);

                if (req.Role_ids != null && req.Role_ids.Any())
                {
                    string maxIdSql = @"SELECT COALESCE(MAX(""RoleNameId""), 0) FROM public.""ROLE_NAME_MASTER"";";
                    long currentMaxId = await conn.ExecuteScalarAsync<long>(maxIdSql, null, trans);

                    var distinctRoleIds = req.Role_ids.Distinct().ToList();
                    var entities = new List<object>();

                    for (int i = 0; i < distinctRoleIds.Count; i++)
                    {
                        currentMaxId++;
                        entities.Add(new
                        {
                            RoleNameId = currentMaxId,
                            RoleNameGUID = roleGuid,
                            Role_Name = req.Role_Name.Trim(),
                            Role_id = distinctRoleIds[i],
                            tenant_code = tenantCode
                        });
                    }

                    string insertSql = @"
                        INSERT INTO public.""ROLE_NAME_MASTER"" (""RoleNameId"", ""RoleNameGUID"", ""Role_Name"", ""Role_id"", tenant_code)
                        VALUES (@RoleNameId, @RoleNameGUID, @Role_Name, @Role_id, @tenant_code);";

                    await conn.ExecuteAsync(insertSql, entities, trans);
                }

                await trans.CommitAsync();
                return roleGuid;
            }
            catch
            {
                await trans.RollbackAsync();
                throw;
            }
        }

        // 5. Delete role
        public async Task<bool> DeleteRoleAsync(Guid roleGuid, string tenantCode)
        {
            using IDbConnection db = new NpgsqlConnection(_masterConnStr);
            string sql = @"DELETE FROM public.""ROLE_NAME_MASTER"" WHERE ""RoleNameGUID"" = @roleGuid AND tenant_code = @tenantCode;";
            var rows = await db.ExecuteAsync(sql, new { roleGuid, tenantCode });
            return rows > 0;
        }
    }
}
