using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public static class OpStatusKeys
    {
        public const string Waiting = "WAITING";
        public const string InConsultation = "IN_CONSULTATION";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
        public const string Rescheduled = "RESCHEDULED";
    }

    public class OpStatusMasterClass
    {
        private readonly string _db_conn;
        private const int MaxNameLength = 50;

        // Fixed system statuses. Codes never change; names are the defaults.
        private static readonly (string code, string name, int order)[] DefaultStatuses =
        {
            (OpStatusKeys.Waiting,        "Waiting",         1),
            (OpStatusKeys.InConsultation, "In Consultation", 2),
            (OpStatusKeys.Completed,      "Completed",       3),
            (OpStatusKeys.Cancelled,      "Cancelled",       4),
            (OpStatusKeys.Rescheduled,    "Rescheduled",     5),
        };

        public OpStatusMasterClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ---------- helpers ----------

        private static string AllowedCodes =>
            string.Join(", ", DefaultStatuses.Select(s => s.code));

        private static string? NormalizeCode(string? code)
        {
            var c = code?.Trim().ToUpperInvariant();
            return DefaultStatuses.Any(s => s.code == c) ? c : null;
        }

        private static OpStatusApiResponse<List<OpStatusResponse>> Err(int http_code, string message) =>
            OpStatusApiResponse<List<OpStatusResponse>>.Error(http_code, message);

        // Tenant's saved display names: code -> name
        private static async Task<Dictionary<string, string>> LoadOverrides(
            IDbConnection db, string tenant_code, IDbTransaction? tx = null)
        {
            var rows = await db.QueryAsync<OpStatusMasterModel>(
                @"SELECT status_key, status_name FROM op_status_master
                  WHERE tenant_code = @tenant_code
                  AND   is_fixed    = true
                  AND   deleted     = false
                  AND   status_key IS NOT NULL",
                new { tenant_code }, tx);

            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.status_name))
                .ToDictionary(r => r.status_key!, r => r.status_name.Trim());
        }

        // All 5 statuses: tenant name if saved, otherwise default name
        private static List<OpStatusResponse> Build(Dictionary<string, string> overrides)
        {
            return DefaultStatuses.OrderBy(s => s.order).Select(s =>
            {
                string display = overrides.TryGetValue(s.code, out var n) ? n : s.name;
                return new OpStatusResponse
                {
                    status_code = s.code,
                    default_status_name = s.name,
                    display_status_name = display,
                    is_customized = !string.Equals(display, s.name, StringComparison.Ordinal)
                };
            }).ToList();
        }

        private static string? FindDuplicateName(IEnumerable<OpStatusResponse> list)
        {
            return list
                .GroupBy(s => s.display_status_name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1)?.Key;
        }

        // ---------- GET all ----------

        public async Task<OpStatusApiResponse<List<OpStatusResponse>>> GetAll(string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);
                var overrides = await LoadOverrides(db, tenant_code);
                return OpStatusApiResponse<List<OpStatusResponse>>.Success(
                    Build(overrides), "Status configurations retrieved successfully");
            }
            catch (Exception ex) { return Err(500, ex.Message); }
        }

        // ---------- GET by code ----------

        public async Task<OpStatusApiResponse<OpStatusResponse>> GetByCode(string tenant_code, string status_code)
        {
            try
            {
                string? code = NormalizeCode(status_code);
                if (code == null)
                    return OpStatusApiResponse<OpStatusResponse>.Error(404,
                        $"Invalid status_code. Allowed: {AllowedCodes}");

                using IDbConnection db = new NpgsqlConnection(_db_conn);
                var overrides = await LoadOverrides(db, tenant_code);

                return OpStatusApiResponse<OpStatusResponse>.Success(
                    Build(overrides).First(s => s.status_code == code),
                    "Status configuration retrieved successfully");
            }
            catch (Exception ex) { return OpStatusApiResponse<OpStatusResponse>.Error(500, ex.Message); }
        }

        // ---------- SAVE / BULK SAVE (shared, one transaction, UPSERT) ----------

        private async Task<OpStatusApiResponse<List<OpStatusResponse>>> SaveMany(
            string tenant_code, List<OpStatusSaveRequest>? items, int? usercode, string okMessage)
        {
            try
            {
                if (items == null || items.Count == 0)
                    return Err(400, "At least one status is required");

                var clean = new Dictionary<string, string>();
                foreach (var item in items)
                {
                    string? code = NormalizeCode(item?.status_code);
                    if (code == null)
                        return Err(400, $"Invalid status_code '{item?.status_code}'. Allowed: {AllowedCodes}");

                    string name = item!.display_status_name?.Trim() ?? string.Empty;
                    if (name.Length == 0)
                        return Err(400, $"display_status_name is required for {code}");
                    if (name.Length > MaxNameLength)
                        return Err(400, $"display_status_name for {code} must be {MaxNameLength} characters or less");
                    if (!clean.TryAdd(code, name))
                        return Err(400, $"Duplicate status_code {code} in request");
                }

                using IDbConnection db = new NpgsqlConnection(_db_conn);
                db.Open();
                using var tx = db.BeginTransaction();

                var merged = await LoadOverrides(db, tenant_code, tx);
                foreach (var kv in clean) merged[kv.Key] = kv.Value;

                var result = Build(merged);
                var dup = FindDuplicateName(result);
                if (dup != null)
                    return Err(400, $"Display name '{dup}' is used by more than one status");

                var rows = clean.Select(kv => new
                {
                    tenant_code,
                    status_key = kv.Key,
                    status_name = kv.Value,
                    sort_order = DefaultStatuses.First(s => s.code == kv.Key).order,
                    usercode
                });

                await db.ExecuteAsync(
                    @"INSERT INTO op_status_master
                          (status_name, tenant_code, is_active, is_fixed, status_key, sort_order, usercode)
                      VALUES (@status_name, @tenant_code, true, true, @status_key, @sort_order, @usercode)
                      ON CONFLICT (tenant_code, status_key)
                      DO UPDATE SET status_name = EXCLUDED.status_name,
                                    is_active   = true,
                                    is_fixed    = true,
                                    deleted     = false,
                                    usercode    = COALESCE(EXCLUDED.usercode, op_status_master.usercode),
                                    updated_at  = now()",
                    rows, tx);

                tx.Commit();
                return OpStatusApiResponse<List<OpStatusResponse>>.Success(result, okMessage);
            }
            catch (Exception ex) { return Err(500, ex.Message); }
        }

        public Task<OpStatusApiResponse<List<OpStatusResponse>>> Save(string tenant_code, OpStatusSaveRequest? data) =>
            SaveMany(tenant_code,
                     data == null ? null : new List<OpStatusSaveRequest> { data },
                     data?.usercode,
                     "Status configuration saved successfully");

        public Task<OpStatusApiResponse<List<OpStatusResponse>>> BulkSave(string tenant_code, OpStatusBulkSaveRequest? data) =>
            SaveMany(tenant_code, data?.statuses, data?.usercode,
                     "Status configurations saved successfully");

        // ---------- RESET one ----------

        public async Task<OpStatusApiResponse<OpStatusResponse>> Reset(string tenant_code, string status_code)
        {
            try
            {
                string? code = NormalizeCode(status_code);
                if (code == null)
                    return OpStatusApiResponse<OpStatusResponse>.Error(404,
                        $"Invalid status_code. Allowed: {AllowedCodes}");

                using IDbConnection db = new NpgsqlConnection(_db_conn);

                var overrides = await LoadOverrides(db, tenant_code);
                overrides.Remove(code);

                var preview = Build(overrides);
                var dup = FindDuplicateName(preview);
                if (dup != null)
                    return OpStatusApiResponse<OpStatusResponse>.Error(400,
                        $"Cannot reset: '{dup}' is already used by another status");

                await db.ExecuteAsync(
                    @"DELETE FROM op_status_master
                      WHERE tenant_code = @tenant_code
                      AND   status_key  = @status_key
                      AND   is_fixed    = true",
                    new { tenant_code, status_key = code });

                return OpStatusApiResponse<OpStatusResponse>.Success(
                    preview.First(s => s.status_code == code),
                    "Status reset to default successfully");
            }
            catch (Exception ex) { return OpStatusApiResponse<OpStatusResponse>.Error(500, ex.Message); }
        }

        // ---------- RESET all ----------

        public async Task<OpStatusApiResponse<List<OpStatusResponse>>> ResetAll(string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                await db.ExecuteAsync(
                    @"DELETE FROM op_status_master
                      WHERE tenant_code = @tenant_code
                      AND   is_fixed    = true
                      AND   status_key  = ANY(@codes)",
                    new { tenant_code, codes = DefaultStatuses.Select(s => s.code).ToArray() });

                return OpStatusApiResponse<List<OpStatusResponse>>.Success(
                    Build(new Dictionary<string, string>()),
                    "All statuses reset to default successfully");
            }
            catch (Exception ex) { return Err(500, ex.Message); }
        }

        // ---------- For OP Registration: code -> display name ----------

        public async Task<Dictionary<string, string>> GetDisplayMap(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var overrides = await LoadOverrides(db, tenant_code);

            return Build(overrides).ToDictionary(
                s => s.status_code,
                s => s.display_status_name,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}