using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class CounterTimingClass
    {
        private readonly string db_conn;

        public CounterTimingClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }
        private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        private static DateTime ComputePlannedEndUtc(DateTime startUtc, string? mode, string? endTime)
        {
            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), Tz);
            var midnight = startLocal.Date.AddDays(1);
            var endLocal = midnight;

            if (string.Equals(mode, "FIXED", StringComparison.OrdinalIgnoreCase)
                && TimeSpan.TryParse(endTime, out var t))
            {
                var configured = startLocal.Date + t;
                if (configured > startLocal && configured < midnight) endLocal = configured;
            }
            return TimeZoneInfo.ConvertTimeToUtc(endLocal, Tz);
        }
        // ─────────────────────────────────────────
        // GET NEXT SHIFTSNO (per bhcode/cntcode/date/tenant)
        // ─────────────────────────────────────────
        public async Task<int> GetNextShiftNo(int bhcode, int cntcode, DateTime counterdate, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT COALESCE(MAX(shiftsno), 0) + 1
                           FROM counter_timing
                           WHERE bhcode = @bhcode
                           AND cntcode = @cntcode
                           AND counterdate = @counterdate::date
                           AND tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { bhcode, cntcode, counterdate, tenant_code });
        }

        // ─────────────────────────────────────────
        // INSERT (OPEN SHIFT)
        // ─────────────────────────────────────────
        public async Task<string> Insert(CounterTimingModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                if (data.bhcode == null || data.cntcode == null)
                    return "bhcode and cntcode are required.";

                var now = DateTime.UtcNow;
                data.tenant_code = tenant_code;

                // close an expired-but-unswept shift so it never blocks a new one
                await db.ExecuteAsync(
                    @"UPDATE counter_timing SET todate = planned_to, close_type = 'AUTO'
              WHERE bhcode=@bhcode AND cntcode=@cntcode AND tenant_code=@tenant_code
                AND todate IS NULL AND planned_to IS NOT NULL AND planned_to <= @now",
                    new { data.bhcode, data.cntcode, tenant_code, now });

                int openShifts = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(*) FROM counter_timing
              WHERE bhcode = @bhcode AND cntcode = @cntcode AND todate IS NULL
              AND tenant_code = @tenant_code",
                    new { data.bhcode, data.cntcode, tenant_code });

                if (openShifts > 0)
                    return "An open shift already exists for this branch/counter. Close it first.";

                var counterDate = data.counterdate?.Date ?? TimeZoneInfo.ConvertTimeFromUtc(now, Tz).Date;

                var setting = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT shift_timing_mode, shift_end_time FROM lab_settings
              WHERE tenant_code=@tenant_code AND deleted=false
                AND (bh_code=@bh OR bh_code=0 OR bh_code IS NULL)
              ORDER BY (bh_code=@bh) DESC NULLS LAST LIMIT 1",
                    new { bh = data.bhcode, tenant_code });

                string mode = ((string?)setting?.shift_timing_mode ?? "VARIABLE").ToUpper();

                data.cnttid = Guid.NewGuid().ToString();
                data.counterdate = counterDate;
                data.shiftsno = await GetNextShiftNo(data.bhcode.Value, data.cntcode.Value, counterDate, tenant_code);
                data.fromdate = now;
                data.todate = null;
                data.shift_mode = mode;
                data.planned_to = ComputePlannedEndUtc(now, mode, (string?)setting?.shift_end_time);
                data.close_type = null;
                data.entereddate = now;
                data.ibsdate = now;

                string sql = @"
            INSERT INTO counter_timing
            (cnttid, bhcode, cntcode, shiftsno, counterdate, fromdate, todate,
             shift_mode, planned_to, close_type,
             tenant_code, usercode, computercode, entereddate, ibsdate)
            VALUES
            (@cnttid, @bhcode, @cntcode, @shiftsno, @counterdate, @fromdate, @todate,
             @shift_mode, @planned_to, @close_type,
             @tenant_code, @usercode, @computercode, @entereddate, @ibsdate)";

                await db.ExecuteAsync(sql, data);
                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // UPDATE (e.g. CLOSE SHIFT)
        // ─────────────────────────────────────────
        public async Task<string> Update(CounterTimingModel data, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var existing = await db.QueryFirstOrDefaultAsync<CounterTimingModel>(
                    "SELECT * FROM counter_timing WHERE cnttid=@cnttid AND tenant_code=@tenant_code",
                    new { data.cnttid, tenant_code });
                if (existing == null) return "Data Not Found";

                bool closeRequested = data.todate != null;   // frontend sends todate to close
                var now = DateTime.UtcNow;

                if (existing.todate != null)
                    return closeRequested ? "Success" : "Shift is already closed.";

                if (closeRequested)
                {
                    DateTime closeAt; string closeType;

                    if (existing.planned_to != null && now >= existing.planned_to.Value.AddSeconds(-60))
                    { closeAt = existing.planned_to.Value; closeType = "AUTO"; }
                    else if (existing.shift_mode == "FIXED")
                        return "This is a fixed-timing shift. It will close automatically at the scheduled time.";
                    else
                    { closeAt = now; closeType = "MANUAL"; }

                    await db.ExecuteAsync(
                        @"UPDATE counter_timing
                  SET todate=@closeAt, close_type=@closeType,
                      usercode=@usercode, computercode=@computercode, ibsdate=@now
                  WHERE cnttid=@cnttid AND tenant_code=@tenant_code AND todate IS NULL",
                        new { closeAt, closeType, data.usercode, data.computercode, now, data.cnttid, tenant_code });
                    return "Success";
                }

                await db.ExecuteAsync(
                    @"UPDATE counter_timing
              SET usercode=@usercode, computercode=@computercode, ibsdate=@now
              WHERE cnttid=@cnttid AND tenant_code=@tenant_code",
                    new { data.usercode, data.computercode, now, data.cnttid, tenant_code });
                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────
        public async Task<string> Delete(string cnttid, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(db_conn);

                var existing = await db.QueryFirstOrDefaultAsync<CounterTimingModel>(
                    "SELECT * FROM counter_timing WHERE cnttid = @cnttid AND tenant_code = @tenant_code",
                    new { cnttid, tenant_code });

                if (existing == null)
                    return "Data Not Found";

                if (existing.todate == null)
                {
                    int linkedBills = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(*) FROM lab_request_master
                          WHERE cnttid = @cnttid AND isdeleted = false
                          AND tenant_code = @tenant_code",
                        new { cnttid, tenant_code });

                    if (linkedBills > 0)
                        return $"Cannot delete: {linkedBills} active bill(s) are linked to this shift. Close the shift first.";
                }

                string sql = @"DELETE FROM counter_timing WHERE cnttid = @cnttid AND tenant_code = @tenant_code";

                await db.ExecuteAsync(sql, new { cnttid, tenant_code });

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ─────────────────────────────────────────
        // GET ALL
        // ─────────────────────────────────────────
        public async Task<List<CounterTimingModel>> Get(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM counter_timing
                WHERE tenant_code = @tenant_code
                ORDER BY counterdate DESC, fromdate DESC";

            var result = await db.QueryAsync<CounterTimingModel>(sql, new { tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET BY CNTTID
        // ─────────────────────────────────────────
        public async Task<CounterTimingModel?> GetByCnttid(string cnttid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM counter_timing
                WHERE cnttid = @cnttid
                AND tenant_code = @tenant_code";

            return await db.QueryFirstOrDefaultAsync<CounterTimingModel>(
                sql,
                new { cnttid, tenant_code });
        }

        // ─────────────────────────────────────────
        // GET BY CNTCODE (history for a counter)
        // ─────────────────────────────────────────
        public async Task<List<CounterTimingModel>> GetByCntcode(int cntcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
                SELECT *
                FROM counter_timing
                WHERE cntcode = @cntcode
                AND tenant_code = @tenant_code
                ORDER BY counterdate DESC, fromdate DESC";

            var result = await db.QueryAsync<CounterTimingModel>(
                sql,
                new { cntcode, tenant_code });

            return result.ToList();
        }

        // ─────────────────────────────────────────
        // GET OPEN SHIFT (bhcode + cntcode)
        // ─────────────────────────────────────────
        public async Task<CounterTimingModel?> GetOpenShift(int bhcode, int cntcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"
        SELECT *
        FROM counter_timing
        WHERE bhcode = @bhcode
        AND cntcode = @cntcode
        AND todate IS NULL
        AND (planned_to IS NULL OR planned_to > @now)
        AND tenant_code = @tenant_code
        LIMIT 1";

            return await db.QueryFirstOrDefaultAsync<CounterTimingModel>(
                sql, new { bhcode, cntcode, tenant_code, now = DateTime.UtcNow });
        }
    }
}