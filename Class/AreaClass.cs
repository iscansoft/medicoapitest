using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class AreaMasterClass
    {
        private readonly string db_conn;

        public AreaMasterClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // GET NEXT AREACODE
        // ─────────────────────────────────────────
        public async Task<int> GetNextAreaCode(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(areacode), 0) + 1
                           FROM area_master
                           WHERE tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT
        // ─────────────────────────────────────────
        public async Task<string> Insert(AreaMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.areacode = await GetNextAreaCode(tenant_code);
                data.entereddate = DateTime.UtcNow;
                data.ibsdate = DateTime.UtcNow;
                data.deleted = false;

                string sql = @"
                    INSERT INTO area_master
                    (
                        areacode,
                        tenant_code,
                        orderno,
                        shortname,
                        areaname,
                        citycode,
                        areapincode,
                        statecode,
                        countrycode,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate
                    )
                    VALUES
                    (
                        @areacode,
                        @tenant_code,
                        @orderno,
                        @shortname,
                        @areaname,
                        @citycode,
                        @areapincode,
                        @statecode,
                        @countrycode,
                        @description,
                        @deleted,
                        @usercode,
                        @computercode,
                        @entereddate,
                        @ibsdate
                    )";

                await db.ExecuteAsync(sql, data);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────
        public async Task<string> Update(AreaMasterModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                data.tenant_code = tenant_code;
                data.ibsdate = DateTime.UtcNow;

                string sql = @"
                    UPDATE area_master
                    SET
                        orderno      = @orderno,
                        shortname    = @shortname,
                        areaname     = @areaname,
                        citycode     = @citycode,
                        areapincode  = @areapincode,
                        statecode    = @statecode,
                        countrycode  = @countrycode,
                        description  = @description,
                        deleted      = @deleted,
                        usercode     = @usercode,
                        computercode = @computercode,
                        ibsdate      = @ibsdate,
                        tenant_code  = @tenant_code
                    WHERE areacode = @areacode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, data);
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(int areacode, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                string sql = @"
                    UPDATE area_master
                    SET deleted = true,
                        ibsdate = now()
                    WHERE areacode = @areacode
                    AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { areacode, tenant_code });
                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL (tenant + global)
        // ─────────────────────────────────────────
        public async Task<List<AreaMasterModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM area_master
                WHERE deleted = false
                AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                ORDER BY areacode";

            var result = await db.QueryAsync<AreaMasterModel>(sql, new { tenant_code });
            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY AREACODE (tenant + global)
        // ─────────────────────────────────────────
        public async Task<AreaMasterModel?> GetByAreaCode(int areacode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM area_master
                WHERE deleted = false
                AND areacode = @areacode
                AND (tenant_code = @tenant_code OR tenant_code IS NULL)";

            return await db.QueryFirstOrDefaultAsync<AreaMasterModel>(
                sql, new { areacode, tenant_code });
        }

        // ─────────────────────────────────────────
        // SEARCH BY AREA NAME (tenant + global)
        // ─────────────────────────────────────────
        public async Task<List<AreaMasterModel>> SearchByAreaName(string areaname, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM area_master
                WHERE deleted = false
                AND (tenant_code = @tenant_code OR tenant_code IS NULL)
                AND LOWER(areaname) LIKE LOWER(@areaname)
                ORDER BY areaname";

            var result = await db.QueryAsync<AreaMasterModel>(
                sql, new { areaname = $"%{areaname}%", tenant_code });
            return result.ToList();
        }
    }
}