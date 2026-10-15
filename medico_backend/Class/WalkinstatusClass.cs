using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class OpStatusMasterClass
    {
        private readonly string _db_conn;

        public OpStatusMasterClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        public async Task<string> Create(OpStatusMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                if (string.IsNullOrWhiteSpace(data.status_name))
                    return "status_name is required";

                string name = data.status_name.Trim();

                bool exists = await db.ExecuteScalarAsync<bool>(
                    @"SELECT EXISTS(SELECT 1 FROM op_status_master
                      WHERE tenant_code = @tenant_code AND deleted = false
                      AND LOWER(status_name) = LOWER(@status_name))",
                    new { data.tenant_code, status_name = name });

                if (exists) return "Status name already exists";

                int id = await db.ExecuteScalarAsync<int>(
                    @"INSERT INTO op_status_master (status_name, tenant_code, is_active, usercode)
                      VALUES (@status_name, @tenant_code, @is_active, @usercode)
                      RETURNING op_status_id",
                    new { status_name = name, data.tenant_code, data.is_active, data.usercode });

                return $"Success|Id:{id}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<string> Update(OpStatusMasterModel data)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                if (string.IsNullOrWhiteSpace(data.status_name))
                    return "status_name is required";

                string name = data.status_name.Trim();

                bool dup = await db.ExecuteScalarAsync<bool>(
                    @"SELECT EXISTS(SELECT 1 FROM op_status_master
                      WHERE tenant_code = @tenant_code AND deleted = false
                      AND op_status_id <> @op_status_id
                      AND LOWER(status_name) = LOWER(@status_name))",
                    new { data.tenant_code, data.op_status_id, status_name = name });

                if (dup) return "Status name already exists";

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_status_master
                      SET status_name = @status_name,
                          is_active   = @is_active,
                          updated_at  = now()
                      WHERE op_status_id = @op_status_id
                      AND   tenant_code  = @tenant_code
                      AND   deleted      = false",
                    new { status_name = name, data.is_active, data.op_status_id, data.tenant_code });

                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<string> Delete(int op_status_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_status_master
                      SET deleted = true, updated_at = now()
                      WHERE op_status_id = @op_status_id
                      AND   tenant_code  = @tenant_code
                      AND   deleted      = false",
                    new { op_status_id, tenant_code });

                return rows > 0 ? "Success" : "Record not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<List<OpStatusMasterModel>> GetAll(string tenant_code, bool activeOnly = false)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var res = await db.QueryAsync<OpStatusMasterModel>(
                @"SELECT * FROM op_status_master
                  WHERE tenant_code = @tenant_code
                  AND   deleted     = false
                  AND   (@activeOnly = false OR is_active = true)
                  ORDER BY status_name",
                new { tenant_code, activeOnly });

            return res.ToList();
        }
    }
}