using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    // ─────────────────────────────────────────────────────────
    // MASTER LIST CLASS — simple lookup masters used across
    // Nurse Notes: IO Particulars, Service Names, Schedule Types.
    // Integer identity PK (1,2,3...). Soft delete via is_active.
    // ─────────────────────────────────────────────────────────
    public class MasterListClass
    {
        private readonly string _db_conn;

        public MasterListClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ═══════════════════════════════════════
        // 1. IO PARTICULARS MASTER
        // ═══════════════════════════════════════
        public async Task<string> AddIoParticular(AddIoParticularRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int id = await db.ExecuteScalarAsync<int>(
                @"INSERT INTO io_particulars_master
                  (particular_name, io_type, is_active, tenant_code, created_at, updated_at)
                  VALUES (@particular_name, @io_type, true, @tenant_code, NOW(), NOW())
                  RETURNING particular_id",
                new { req.particular_name, req.io_type, tenant_code });

            return $"Success|Id:{id}";
        }

        public async Task<string> UpdateIoParticular(UpdateIoParticularRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int rows = await db.ExecuteAsync(
                @"UPDATE io_particulars_master
                  SET particular_name = @particular_name, io_type = @io_type,
                      is_active = @is_active, updated_at = NOW()
                  WHERE particular_id = @particular_id AND tenant_code = @tenant_code",
                new { req.particular_id, req.particular_name, req.io_type, req.is_active, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<IoParticularsMasterModel>> GetIoParticulars(string tenant_code, bool activeOnly = true)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM io_particulars_master
                            WHERE tenant_code = @tenant_code
                            " + (activeOnly ? "AND is_active = true" : "") + @"
                            ORDER BY particular_name";

            var res = await db.QueryAsync<IoParticularsMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteIoParticular(int particular_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int rows = await db.ExecuteAsync(
                @"UPDATE io_particulars_master SET is_active = false, updated_at = NOW()
                  WHERE particular_id = @particular_id AND tenant_code = @tenant_code",
                new { particular_id, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════
        // 2. SERVICE NAME MASTER
        // ═══════════════════════════════════════
        public async Task<string> AddServiceName(AddServiceNameRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int id = await db.ExecuteScalarAsync<int>(
                @"INSERT INTO service_name_master
                  (service_name, is_active, tenant_code, created_at, updated_at)
                  VALUES (@service_name, true, @tenant_code, NOW(), NOW())
                  RETURNING service_id",
                new { req.service_name, tenant_code });

            return $"Success|Id:{id}";
        }

        public async Task<string> UpdateServiceName(UpdateServiceNameRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int rows = await db.ExecuteAsync(
                @"UPDATE service_name_master
                  SET service_name = @service_name, is_active = @is_active, updated_at = NOW()
                  WHERE service_id = @service_id AND tenant_code = @tenant_code",
                new { req.service_id, req.service_name, req.is_active, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<ServiceNameMasterModel>> GetServiceNames(string tenant_code, bool activeOnly = true)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM service_name_master
                            WHERE tenant_code = @tenant_code
                            " + (activeOnly ? "AND is_active = true" : "") + @"
                            ORDER BY service_name";

            var res = await db.QueryAsync<ServiceNameMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteServiceName(int service_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int rows = await db.ExecuteAsync(
                @"UPDATE service_name_master SET is_active = false, updated_at = NOW()
                  WHERE service_id = @service_id AND tenant_code = @tenant_code",
                new { service_id, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════
        // 3. SCHEDULE TYPE MASTER
        // ═══════════════════════════════════════
        public async Task<string> AddScheduleType(AddScheduleTypeRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int id = await db.ExecuteScalarAsync<int>(
                @"INSERT INTO schedule_type_master
                  (type_name, is_active, tenant_code, created_at, updated_at)
                  VALUES (@type_name, true, @tenant_code, NOW(), NOW())
                  RETURNING type_id",
                new { req.type_name, tenant_code });

            return $"Success|Id:{id}";
        }

        public async Task<string> UpdateScheduleType(UpdateScheduleTypeRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int rows = await db.ExecuteAsync(
                @"UPDATE schedule_type_master
                  SET type_name = @type_name, is_active = @is_active, updated_at = NOW()
                  WHERE type_id = @type_id AND tenant_code = @tenant_code",
                new { req.type_id, req.type_name, req.is_active, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<ScheduleTypeMasterModel>> GetScheduleTypes(string tenant_code, bool activeOnly = true)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"SELECT * FROM schedule_type_master
                            WHERE tenant_code = @tenant_code
                            " + (activeOnly ? "AND is_active = true" : "") + @"
                            ORDER BY type_name";

            var res = await db.QueryAsync<ScheduleTypeMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteScheduleType(int type_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            int rows = await db.ExecuteAsync(
                @"UPDATE schedule_type_master SET is_active = false, updated_at = NOW()
                  WHERE type_id = @type_id AND tenant_code = @tenant_code",
                new { type_id, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }
    }
}