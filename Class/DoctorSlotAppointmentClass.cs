using Dapper;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class DoctorAppointmentSlotClass
    {
        private readonly string _db_conn;

        public DoctorAppointmentSlotClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        // ═══════════════════════════════════════════
        // MASTER - GET ALL
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotMasterModel>>
            GetAllMaster(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_master_id,
                slotnum,
                dcode,
                tenant_code,
                day_of_week,
                slot_start_time,
                slot_end_time,
                typeofslot,
                max_patients,
                max_walkin,
                max_online,
                avgtime,
                slot_date,
                is_active,
                isdeleted,
                is_cancel,
                cancel_reason,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_master
            WHERE isdeleted   = false
            AND   is_cancel   = false
            AND   tenant_code = @tenant_code
            ORDER BY slot_date, slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotMasterModel>(
                sql, new { tenant_code });

            return res.ToList();
        }

        // ═══════════════════════════════════════════
        // MASTER - GET BY DOCTOR
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotMasterModel>>
            GetMasterByDoctor(int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_master_id,
                slotnum,
                dcode,
                tenant_code,
                day_of_week,
                slot_start_time,
                slot_end_time,
                typeofslot,
                max_patients,
                max_walkin,
                max_online,
                avgtime,
                slot_date,
                is_active,
                isdeleted,
                is_cancel,
                cancel_reason,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_master
            WHERE isdeleted   = false
            AND   is_cancel   = false
            AND   dcode       = @dcode
            AND   tenant_code = @tenant_code
            ORDER BY slot_date, slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotMasterModel>(
                sql, new { dcode, tenant_code });

            return res.ToList();
        }

        // ═══════════════════════════════════════════
        // GET NEXT SLOT NUMBER
        // ═══════════════════════════════════════════
        public async Task<int> GetNextSlotNum(int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT COALESCE(MAX(slotnum), 0) + 1
            FROM doctor_appointment_slot_master
            WHERE dcode       = @dcode
            AND   tenant_code = @tenant_code";

            return await db.ExecuteScalarAsync<int>(sql, new { dcode, tenant_code });
        }

        // ═══════════════════════════════════════════
        // MASTER - BULK INSERT
        // ═══════════════════════════════════════════
        public async Task<object> BulkInsertMaster(
            List<DoctorAppointmentSlotMasterModel> slots,
            string tenant_code)
        {
            var inserted = new List<string>();
            var skipped = new List<string>();
            var failed = new List<string>();

            using IDbConnection db = new NpgsqlConnection(_db_conn);

            foreach (var data in slots)
            {
                try
                {
                    // ✅ VALIDATE DATE
                    if (!data.slot_date.HasValue)
                    {
                        failed.Add("slot_date is required");
                        continue;
                    }

                    // ✅ VALIDATE TIME
                    if (data.slot_start_time >= data.slot_end_time)
                    {
                        failed.Add(
                            $"Invalid time range : " +
                            $"{data.slot_start_time} - {data.slot_end_time}");
                        continue;
                    }

                    // ✅ OVERLAP CHECK
                    string overlapSql = @"
                    SELECT COUNT(1)
                    FROM doctor_appointment_slot_master
                    WHERE isdeleted   = false
                    AND   tenant_code = @tenant_code
                    AND   dcode       = @dcode
                    AND   slot_date   = @slot_date
                    AND   @new_start  < slot_end_time
                    AND   @new_end    > slot_start_time";

                    int overlapExists = await db.ExecuteScalarAsync<int>(overlapSql, new
                    {
                        tenant_code,
                        data.dcode,
                        slot_date = data.slot_date.Value.ToDateTime(TimeOnly.MinValue),
                        new_start = data.slot_start_time.ToTimeSpan(),
                        new_end = data.slot_end_time.ToTimeSpan()
                    });

                    if (overlapExists > 0)
                    {
                        skipped.Add(
                            $"Slot already exists or overlaps : " +
                            $"{data.slot_date:dd-MM-yyyy} " +
                            $"{data.slot_start_time} - {data.slot_end_time}");
                        continue;
                    }

                    // ✅ GENERATE IDS AND METADATA
                    data.slot_master_id = Guid.NewGuid();
                    data.slotnum = await GetNextSlotNum(data.dcode, tenant_code);
                    data.tenant_code = tenant_code;
                    data.created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                    data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                    // ✅ INSERT MASTER
                    string masterSql = @"
                    INSERT INTO doctor_appointment_slot_master
                    (
                        slot_master_id, slotnum, dcode, tenant_code,
                        day_of_week, slot_start_time, slot_end_time,
                        typeofslot, max_patients, max_walkin, max_online,
                        avgtime, slot_date, is_active, isdeleted,
                        created_at, updated_at
                    )
                    VALUES
                    (
                        @slot_master_id, @slotnum, @dcode, @tenant_code,
                        @day_of_week, @slot_start_time, @slot_end_time,
                        @typeofslot, @max_patients, @max_walkin, @max_online,
                        @avgtime, @slot_date, @is_active, @isdeleted,
                        @created_at, @updated_at
                    )";

                    await db.ExecuteAsync(masterSql, new
                    {
                        data.slot_master_id,
                        data.slotnum,
                        data.dcode,
                        data.tenant_code,
                        data.day_of_week,
                        slot_start_time = data.slot_start_time.ToTimeSpan(),
                        slot_end_time = data.slot_end_time.ToTimeSpan(),
                        data.typeofslot,
                        data.max_patients,
                        data.max_walkin,
                        data.max_online,
                        data.avgtime,
                        slot_date = data.slot_date.Value.ToDateTime(TimeOnly.MinValue),
                        data.is_active,
                        data.isdeleted,
                        data.created_at,
                        data.updated_at
                    });

                    string rangeLockKey = $"SLOTRANGE:{tenant_code}:{data.dcode}:{data.slot_date:yyyyMMdd}";
                    await db.ExecuteAsync("SELECT pg_advisory_lock(hashtext(@rangeLockKey))",
                        new { rangeLockKey });

                    int lastToken;
                    try
                    {
                        lastToken = await db.ExecuteScalarAsync<int>(@"
        SELECT COALESCE(MAX(GREATEST(
                   COALESCE(walkin_token_end, 0),
                   COALESCE(online_token_end, 0))), 0)
        FROM   doctor_appointment_slot_details
        WHERE  dcode            = @dcode
        AND    tenant_code      = @tenant_code
        AND    appointment_date = @appointment_date
        AND    isdeleted        = false",
                            new
                            {
                                data.dcode,
                                tenant_code,
                                appointment_date = data.slot_date.Value.ToDateTime(TimeOnly.MinValue)
                            });
                    }
                    finally
                    {
                        await db.ExecuteAsync("SELECT pg_advisory_unlock(hashtext(@rangeLockKey))",
                            new { rangeLockKey });
                    }

                    int onlineStart = lastToken + 1;
                    int onlineEnd = onlineStart + data.max_online - 1;
                    int walkinStart = onlineEnd + 1;
                    int walkinEnd = walkinStart + data.max_walkin - 1;

                    // ✅ AUTO INSERT DETAILS — includes typeofslot and avgtime
                    string detailSql = @"
                    INSERT INTO doctor_appointment_slot_details
                    (
                        slot_detail_id, slot_master_id, dcode, tenant_code,
                        appointment_date, slot_start_time, slot_end_time,
                        max_patients, max_walkin, max_online,
                        booked_count, walkin_count, online_count,
                        typeofslot, avgtime,
                        online_token_start, online_token_end,
                        walkin_token_start, walkin_token_end,
                        slot_status, is_active, isdeleted,
                        created_at, updated_at
                    )
                    VALUES
                    (
                        @slot_detail_id, @slot_master_id, @dcode, @tenant_code,
                        @appointment_date, @slot_start_time, @slot_end_time,
                        @max_patients, @max_walkin, @max_online,
                        0, 0, 0,
                        @typeofslot, @avgtime,
                        @online_token_start, @online_token_end,
                        @walkin_token_start, @walkin_token_end,
                        'OPEN', @is_active, false,
                        @created_at, @updated_at
                    )";

                    await db.ExecuteAsync(detailSql, new
                    {
                        slot_detail_id = Guid.NewGuid(),
                        slot_master_id = data.slot_master_id,
                        data.dcode,
                        tenant_code,
                        appointment_date = data.slot_date.Value.ToDateTime(TimeOnly.MinValue),
                        slot_start_time = data.slot_start_time.ToTimeSpan(),
                        slot_end_time = data.slot_end_time.ToTimeSpan(),
                        data.max_patients,
                        data.max_walkin,
                        data.max_online,
                        data.typeofslot,
                        data.avgtime,
                        online_token_start = onlineStart,
                        online_token_end = onlineEnd,
                        walkin_token_start = walkinStart,
                        walkin_token_end = walkinEnd,
                        data.is_active,
                        created_at = data.created_at,
                        updated_at = data.updated_at
                    });

                    inserted.Add(
                        $"Inserted Successfully : " +
                        $"{data.slot_date:dd-MM-yyyy} " +
                        $"{data.slot_start_time} - {data.slot_end_time}");
                }
                catch (Exception ex)
                {
                    failed.Add(ex.Message);
                }
            }

            return new { inserted, skipped, failed };
        }

        // ═══════════════════════════════════════════
        // MASTER - BULK UPDATE
        // ═══════════════════════════════════════════
        // ═══════════════════════════════════════════
        // MASTER - BULK UPDATE
        // ═══════════════════════════════════════════
        private class OldSlotRow
        {
            public int dcode { get; set; }
            public DateOnly? slot_date { get; set; }
        }

        private class UsedSeatsRow
        {
            public int walkin_count { get; set; }
            public int online_count { get; set; }
        }
        public async Task<object> BulkUpdateMaster(
            List<DoctorAppointmentSlotMasterModel> slots,
            string tenant_code)
        {
            var updated = new List<string>();
            var failed = new List<string>();

            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();

            foreach (var data in slots)
            {
                using var tx = db.BeginTransaction();
                try
                {
                    data.updated_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                    // remember the old doctor/date so its ranges can be re-flowed too
                    var old = await db.QueryFirstOrDefaultAsync<OldSlotRow>(
                        @"SELECT dcode, slot_date FROM doctor_appointment_slot_master
      WHERE slot_master_id = @slot_master_id AND tenant_code = @tenant_code",
                        new { data.slot_master_id, tenant_code }, tx);

                    // never shrink below seats already used
                    var used = await db.QueryFirstOrDefaultAsync<UsedSeatsRow>(
                        @"SELECT walkin_count, online_count FROM doctor_appointment_slot_details
      WHERE slot_master_id = @slot_master_id AND tenant_code = @tenant_code AND isdeleted = false",
                        new { data.slot_master_id, tenant_code }, tx);

                    if (used != null && (data.max_walkin < used.walkin_count || data.max_online < used.online_count))
                    {
                        tx.Rollback();
                        failed.Add($"Cannot reduce below used seats (walkin used {used.walkin_count}, online used {used.online_count}): {data.slot_master_id}");
                        continue;
                    }

                    int rows = await db.ExecuteAsync(@"
                UPDATE doctor_appointment_slot_master
                SET dcode = @dcode, day_of_week = @day_of_week,
                    slot_start_time = @slot_start_time, slot_end_time = @slot_end_time,
                    typeofslot = @typeofslot, max_patients = @max_patients,
                    max_walkin = @max_walkin, max_online = @max_online,
                    avgtime = @avgtime, slot_date = @slot_date,
                    is_active = @is_active, updated_at = @updated_at
                WHERE slot_master_id = @slot_master_id AND tenant_code = @tenant_code",
                        new
                        {
                            data.dcode,
                            data.day_of_week,
                            slot_start_time = data.slot_start_time.ToTimeSpan(),
                            slot_end_time = data.slot_end_time.ToTimeSpan(),
                            data.typeofslot,
                            data.max_patients,
                            data.max_walkin,
                            data.max_online,
                            data.avgtime,
                            slot_date = data.slot_date.HasValue
                                ? data.slot_date.Value.ToDateTime(TimeOnly.MinValue)
                                : (DateTime?)null,
                            data.is_active,
                            data.updated_at,
                            data.slot_master_id,
                            tenant_code
                        }, tx);

                    if (rows == 0)
                    {
                        tx.Rollback();
                        failed.Add($"Not Found: {data.slot_master_id}");
                        continue;
                    }

                    // sync details (capacity + slot info + date/time)
                    await db.ExecuteAsync(@"
                UPDATE doctor_appointment_slot_details
                SET dcode = @dcode,
                    appointment_date = COALESCE(@slot_date, appointment_date),
                    slot_start_time = @slot_start_time,
                    slot_end_time = @slot_end_time,
                    max_patients = @max_patients,
                    max_walkin = @max_walkin,
                    max_online = @max_online,
                    typeofslot = @typeofslot,
                    avgtime = @avgtime,
                    updated_at = now()
                WHERE slot_master_id = @slot_master_id
                AND   tenant_code = @tenant_code
                AND   isdeleted = false",
                        new
                        {
                            data.dcode,
                            slot_date = data.slot_date.HasValue
                                ? data.slot_date.Value.ToDateTime(TimeOnly.MinValue)
                                : (DateTime?)null,
                            slot_start_time = data.slot_start_time.ToTimeSpan(),
                            slot_end_time = data.slot_end_time.ToTimeSpan(),
                            data.max_patients,
                            data.max_walkin,
                            data.max_online,
                            data.typeofslot,
                            data.avgtime,
                            data.slot_master_id,
                            tenant_code
                        }, tx);

                    // re-flow token ranges for the new doctor/date, and the old one if it moved
                    // re-flow token ranges for the new doctor/date, and the old one if it moved
                    string? err = null;
                    if (data.slot_date.HasValue)
                        err = await RecalculateTokenRanges(db, tx, data.dcode,
                            data.slot_date.Value.ToDateTime(TimeOnly.MinValue), tenant_code);

                    if (err == null && old != null && old.slot_date.HasValue &&
                        (old.dcode != data.dcode || old.slot_date != data.slot_date))
                        err = await RecalculateTokenRanges(db, tx, old.dcode,
                            old.slot_date.Value.ToDateTime(TimeOnly.MinValue), tenant_code);

                    if (err != null)
                    {
                        tx.Rollback();
                        failed.Add($"{err}: {data.slot_master_id}");
                        continue;
                    }

                    tx.Commit();
                    updated.Add($"Updated: {data.slot_master_id}");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    failed.Add(ex.Message);
                }
            }

            return new { updated, failed };
        }

        // Re-flows online/walk-in token ranges for one doctor + date, in slot time order,
        // using the same rule as insert: online block first, then walk-in block, next slot continues.
        // Returns an error text if a slot that already has bookings would have its range moved.
        private async Task<string?> RecalculateTokenRanges(
            IDbConnection db, IDbTransaction tx, int dcode, DateTime date, string tenant_code)
        {
            string lockKey = $"SLOTRANGE:{tenant_code}:{dcode}:{date:yyyyMMdd}";
            await db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@lockKey))", new { lockKey }, tx);

            var rows = (await db.QueryAsync<SlotRangeRow>(@"
        SELECT slot_detail_id, max_online, max_walkin, booked_count,
               online_token_start, online_token_end, walkin_token_start, walkin_token_end
        FROM   doctor_appointment_slot_details
        WHERE  dcode = @dcode AND tenant_code = @tenant_code
        AND    appointment_date = @date AND isdeleted = false
        ORDER  BY slot_start_time",
                new { dcode, tenant_code, date }, tx)).ToList();

            int cursor = 0;
            foreach (var r in rows)
            {
                int os = cursor + 1;
                int oe = os + r.max_online - 1;
                int ws = oe + 1;
                int we = ws + r.max_walkin - 1;
                cursor = we;

                bool changed = r.online_token_start != os || r.online_token_end != oe ||
                               r.walkin_token_start != ws || r.walkin_token_end != we;
                if (!changed) continue;

                if (r.booked_count > 0)
                    return "Token ranges of a slot that already has bookings would change. Cancel/complete those bookings or keep the online/walk-in counts the same";

                await db.ExecuteAsync(@"
            UPDATE doctor_appointment_slot_details
            SET online_token_start = @os, online_token_end = @oe,
                walkin_token_start = @ws, walkin_token_end = @we,
                updated_at = now()
            WHERE slot_detail_id = @id",
                    new { os, oe, ws, we, id = r.slot_detail_id }, tx);
            }
            return null;
        }

        private class SlotRangeRow
        {
            public Guid slot_detail_id { get; set; }
            public int max_online { get; set; }
            public int max_walkin { get; set; }
            public int booked_count { get; set; }
            public int? online_token_start { get; set; }
            public int? online_token_end { get; set; }
            public int? walkin_token_start { get; set; }
            public int? walkin_token_end { get; set; }
        }
        // ═══════════════════════════════════════════
        // MASTER - DELETE (soft delete)
        // ═══════════════════════════════════════════
        public async Task<string> DeleteMaster(Guid slot_master_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                string masterSql = @"
                UPDATE doctor_appointment_slot_master
                SET
                    isdeleted  = true,
                    is_active  = false,
                    updated_at = now()
                WHERE slot_master_id = @slot_master_id
                AND   tenant_code    = @tenant_code";

                await db.ExecuteAsync(masterSql, new { slot_master_id, tenant_code });

                string detailSql = @"
                UPDATE doctor_appointment_slot_details
                SET
                    isdeleted  = true,
                    is_active  = false,
                    updated_at = now()
                WHERE slot_master_id = @slot_master_id
                AND   tenant_code    = @tenant_code";

                await db.ExecuteAsync(detailSql, new { slot_master_id, tenant_code });

                return "Success";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════════
        // MASTER - CANCEL
        // ═══════════════════════════════════════════
        public async Task<string> CancelSlot(
            Guid slot_master_id, string tenant_code, string cancel_reason)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            UPDATE doctor_appointment_slot_master
            SET
                is_cancel     = true,
                cancel_reason = @cancel_reason,
                is_active     = false,
                updated_at    = now()
            WHERE slot_master_id = @slot_master_id
            AND   tenant_code    = @tenant_code
            AND   isdeleted      = false";

            int rows = await db.ExecuteAsync(sql, new { slot_master_id, tenant_code, cancel_reason });

            if (rows == 0)
                return "No record found";

            string detailSql = @"
            UPDATE doctor_appointment_slot_details
            SET
                slot_status   = 'CANCELLED',
                is_cancel     = true,
                cancel_reason = @cancel_reason,
                cancelled_at  = now(),
                is_active     = false,
                updated_at    = now()
            WHERE slot_master_id = @slot_master_id
            AND   tenant_code    = @tenant_code";

            await db.ExecuteAsync(detailSql, new { slot_master_id, tenant_code, cancel_reason });

            return "Cancelled Successfully";
        }

        // ═══════════════════════════════════════════
        // DETAILS - GET ALL
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotDetailsModel>>
            GetAllDetails(string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_detail_id,
                slot_master_id,
                dcode,
                tenant_code,
                typeofslot,
                avgtime,
                appointment_date,
                slot_start_time,
                slot_end_time,
                max_patients,
                max_walkin,
                max_online,
                booked_count,
                walkin_count,
                online_count,
                (max_patients - booked_count) AS remaining_seats,
                (max_walkin  - walkin_count)  AS remaining_walkin,
                (max_online  - online_count)  AS remaining_online,
                slot_status,
                is_active,
                isdeleted,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_details
            WHERE isdeleted   = false
            AND   tenant_code = @tenant_code
            ORDER BY appointment_date, slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotDetailsModel>(
                sql, new { tenant_code });

            return res.ToList();
        }

        // ═══════════════════════════════════════════
        // DETAILS - GET BY DATE
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotDetailsModel>>
            GetDetailsByDate(int dcode, DateOnly appointment_date, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_detail_id,
                slot_master_id,
                dcode,
                tenant_code,
                typeofslot,
                avgtime,
                appointment_date,
                slot_start_time,
                slot_end_time,
                max_patients,
                max_walkin,
                max_online,
                booked_count,
                walkin_count,
                online_count,
                (max_patients - booked_count) AS remaining_seats,
                (max_walkin  - walkin_count)  AS remaining_walkin,
                (max_online  - online_count)  AS remaining_online,
                slot_status,
                is_active,
                isdeleted,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_details
            WHERE isdeleted        = false
            AND   dcode            = @dcode
            AND   appointment_date = @appointment_date
            AND   tenant_code      = @tenant_code
            ORDER BY slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotDetailsModel>(sql, new
            {
                dcode,
                appointment_date = appointment_date.ToDateTime(TimeOnly.MinValue),
                tenant_code
            });

            return res.ToList();
        }

        // ═══════════════════════════════════════════
        // DETAILS - GET BY ID
        // ═══════════════════════════════════════════
        public async Task<DoctorAppointmentSlotDetailsModel?> GetDetailById(
            Guid slot_detail_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_detail_id,
                slot_master_id,
                dcode,
                tenant_code,
                typeofslot,
                avgtime,
                appointment_date,
                slot_start_time,
                slot_end_time,
                max_patients,
                max_walkin,
                max_online,
                booked_count,
                walkin_count,
                online_count,
                (max_patients - booked_count) AS remaining_seats,
                (max_walkin  - walkin_count)  AS remaining_walkin,
                (max_online  - online_count)  AS remaining_online,
                slot_status,
                is_active,
                isdeleted,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_details
            WHERE slot_detail_id = @slot_detail_id
            AND   tenant_code    = @tenant_code
            AND   isdeleted      = false";

            return await db.QueryFirstOrDefaultAsync<DoctorAppointmentSlotDetailsModel>(
                sql, new { slot_detail_id, tenant_code });
        }

        // ═══════════════════════════════════════════
        // DETAILS - GET BY MASTER
        // ═══════════════════════════════════════════
        public async Task<List<DoctorAppointmentSlotDetailsModel>> GetDetailsByMaster(
            Guid slot_master_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sql = @"
            SELECT
                slot_detail_id,
                slot_master_id,
                dcode,
                tenant_code,
                typeofslot,
                avgtime,
                appointment_date,
                slot_start_time,
                slot_end_time,
                max_patients,
                max_walkin,
                max_online,
                booked_count,
                walkin_count,
                online_count,
                (max_patients - booked_count) AS remaining_seats,
                (max_walkin  - walkin_count)  AS remaining_walkin,
                (max_online  - online_count)  AS remaining_online,
                slot_status,
                is_active,
                isdeleted,
                created_at AT TIME ZONE 'UTC' AS created_at,
                updated_at AT TIME ZONE 'UTC' AS updated_at
            FROM doctor_appointment_slot_details
            WHERE slot_master_id = @slot_master_id
            AND   tenant_code    = @tenant_code
            AND   isdeleted      = false
            ORDER BY appointment_date, slot_start_time";

            var res = await db.QueryAsync<DoctorAppointmentSlotDetailsModel>(
                sql, new { slot_master_id, tenant_code });

            return res.ToList();
        }
    }
}