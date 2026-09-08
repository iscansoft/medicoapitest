using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    // ─────────────────────────────────────────────────────────
    // OT (OPERATION THEATER) MODULE
    // Covers: OT Master, Surgery Request, OT Schedule, OT Team,
    // Pre-Op Assessment, Consent, Checklist, Anesthesia, Intra-Op,
    // Consumables, Implants, Recovery/PACU, Post-Op Orders,
    // Operation Note, OT Summary.
    //
    // BILLING: there is no separate "create OT bill" step. The
    // procedure fee (on request creation) and every consumable /
    // implant are pushed into `unbilledcharges` the moment they are
    // saved — exactly the way NewOPCaseSheetClass.SaveInvestigation
    // pushes each test into unbilledcharges. They then simply show up
    // on your existing common OP/IP billing screen alongside
    // CONSULTATION and INVESTIGATION rows, with entrytype:
    //     'OT_PROCEDURE'   entryid = request_id
    //     'OT_CONSUMABLE'  entryid = consumable_id
    //     'OT_IMPLANT'     entryid = implant_id
    // Every table/query is scoped by tenant_code.
    //
    // DEPENDENCY: UnbilledChargesClass must expose AddOtChargeRow(...)
    // (added alongside AddInvestigationChargeRow) or this class will
    // not compile. See UnbilledChargesClass_AddOtChargeRow.cs.
    // ─────────────────────────────────────────────────────────
    public class OTClass
    {
        private readonly string _db_conn;
        private readonly UnbilledChargesClass _unbilledCls;

        public OTClass(IConfiguration configuration, UnbilledChargesClass unbilledCls)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
            _unbilledCls = unbilledCls;
        }

        private IDbConnection GetConnection() => new NpgsqlConnection(_db_conn);

        // ═══════════════════════════════════════════════════════
        // 1. OT MASTER
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddOtMaster(AddOtMasterRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var data = new OtMasterModel
            {
                ot_name = req.ot_name,
                ot_type = req.ot_type,
                location = req.location,
                specialty = req.specialty,
                timing_from = req.timing_from,
                timing_to = req.timing_to,
                equipment = req.equipment,
                is_active = req.is_active,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ot_master
                (ot_id, ot_name, ot_type, location, specialty, timing_from, timing_to,
                 equipment, is_active, tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@ot_id, @ot_name, @ot_type, @location, @specialty, @timing_from, @timing_to,
                 @equipment, @is_active, @tenant_code, @isdeleted, @created_at, @updated_at)",
                new
                {
                    data.ot_id,
                    data.ot_name,
                    data.ot_type,
                    data.location,
                    data.specialty,
                    timing_from = data.timing_from.HasValue ? data.timing_from.Value.ToTimeSpan() : (TimeSpan?)null,
                    timing_to = data.timing_to.HasValue ? data.timing_to.Value.ToTimeSpan() : (TimeSpan?)null,
                    data.equipment,
                    data.is_active,
                    data.tenant_code,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

            return $"Success|Id:{data.ot_id}";
        }

        public async Task<string> UpdateOtMaster(UpdateOtMasterRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(@"
                UPDATE ot_master
                SET ot_name = @ot_name, ot_type = @ot_type, location = @location, specialty = @specialty,
                    timing_from = @timing_from, timing_to = @timing_to, equipment = @equipment,
                    is_active = @is_active, updated_at = now()
                WHERE ot_id = @ot_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.ot_id,
                    req.ot_name,
                    req.ot_type,
                    req.location,
                    req.specialty,
                    timing_from = req.timing_from.HasValue ? req.timing_from.Value.ToTimeSpan() : (TimeSpan?)null,
                    timing_to = req.timing_to.HasValue ? req.timing_to.Value.ToTimeSpan() : (TimeSpan?)null,
                    req.equipment,
                    req.is_active,
                    tenant_code
                });

            return rows > 0 ? "Success" : "OT not found";
        }

        public async Task<List<OtMasterModel>> GetOtMasterList(string tenant_code, bool activeOnly = false)
        {
            using IDbConnection db = GetConnection();
            string sql = @"SELECT * FROM ot_master WHERE tenant_code = @tenant_code AND isdeleted = false";
            if (activeOnly) sql += " AND is_active = true";
            sql += " ORDER BY ot_name";

            var res = await db.QueryAsync<OtMasterModel>(sql, new { tenant_code });
            return res.ToList();
        }

        public async Task<OtMasterModel?> GetOtMasterById(Guid ot_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtMasterModel>(
                @"SELECT * FROM ot_master WHERE ot_id = @ot_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ot_id, tenant_code });
        }

        public async Task<string> DeleteOtMaster(Guid ot_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(
                @"UPDATE ot_master SET isdeleted = true, updated_at = now()
                  WHERE ot_id = @ot_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ot_id, tenant_code });
            return rows > 0 ? "Success" : "OT not found";
        }

        // ═══════════════════════════════════════════════════════
        // 2. SURGERY REQUEST
        // Pushes an 'OT_PROCEDURE' unbilled charge if procedure_fee > 0.
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddSurgeryRequest(AddSurgeryRequestRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var data = new OtSurgeryRequestModel
                {
                    op_id = string.IsNullOrWhiteSpace(req.op_id) ? null : req.op_id,
                    ip_id = req.ip_id,
                    custid = req.custid,
                    dcode = req.dcode,
                    diagnosis = req.diagnosis,
                    procedure_name = req.procedure_name,
                    procedure_code = req.procedure_code,
                    priority = string.IsNullOrWhiteSpace(req.priority) ? "ELECTIVE" : req.priority.ToUpper(),
                    expected_duration_minutes = req.expected_duration_minutes,
                    anesthesia_type = req.anesthesia_type,
                    procedure_fee = req.procedure_fee,
                    notes = req.notes,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_surgery_request
                    (request_id, op_id, ip_id, custid, dcode, diagnosis, procedure_name, procedure_code,
                     priority, expected_duration_minutes, anesthesia_type, procedure_fee, notes, status,
                     tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@request_id, CAST(@op_id AS uuid), CAST(@ip_id AS uuid), @custid, @dcode, @diagnosis, @procedure_name, @procedure_code,
                     @priority, @expected_duration_minutes, @anesthesia_type, @procedure_fee, @notes, @status,
                     @tenant_code, @isdeleted, @created_at, @updated_at)",
                    new
                    {
                        data.request_id,
                        data.op_id,
                        ip_id = data.ip_id?.ToString(),
                        data.custid,
                        data.dcode,
                        data.diagnosis,
                        data.procedure_name,
                        data.procedure_code,
                        data.priority,
                        data.expected_duration_minutes,
                        data.anesthesia_type,
                        data.procedure_fee,
                        data.notes,
                        data.status,
                        data.tenant_code,
                        data.isdeleted,
                        data.created_at,
                        data.updated_at
                    }, tx);

                if (req.procedure_fee.HasValue && req.procedure_fee.Value > 0)
                {
                    await _unbilledCls.AddOtChargeRow(
                        db, tx, data.op_id, data.ip_id, data.custid, tenant_code,
                        entryType: "OT_PROCEDURE",
                        entryId: data.request_id.ToString(),
                        itemName: data.procedure_name,
                        quantity: 1,
                        rate: req.procedure_fee,
                        amount: req.procedure_fee);
                }

                tx.Commit();
                return $"Success|Id:{data.request_id}";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        public async Task<string> UpdateSurgeryRequest(UpdateSurgeryRequestRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(@"
                UPDATE ot_surgery_request
                SET diagnosis = @diagnosis, procedure_name = @procedure_name, procedure_code = @procedure_code,
                    priority = @priority, expected_duration_minutes = @expected_duration_minutes,
                    anesthesia_type = @anesthesia_type, notes = @notes, updated_at = now()
                WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.request_id,
                    req.diagnosis,
                    req.procedure_name,
                    req.procedure_code,
                    priority = string.IsNullOrWhiteSpace(req.priority) ? "ELECTIVE" : req.priority.ToUpper(),
                    req.expected_duration_minutes,
                    req.anesthesia_type,
                    req.notes,
                    tenant_code
                });

            return rows > 0 ? "Success" : "Surgery request not found";
        }

        public async Task<string> UpdateSurgeryStatus(UpdateSurgeryStatusRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(@"
                UPDATE ot_surgery_request
                SET status = @status, updated_at = now()
                WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.request_id, status = req.status.ToUpper(), tenant_code });

            return rows > 0 ? "Success" : "Surgery request not found";
        }

        public async Task<OtSurgeryRequestModel?> GetSurgeryRequestById(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtSurgeryRequestModel>(
                @"SELECT * FROM ot_surgery_request WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { request_id, tenant_code });
        }

        public async Task<List<OtSurgeryRequestModel>> GetSurgeryRequestList(
            string tenant_code, string? status = null, decimal? custid = null)
        {
            using IDbConnection db = GetConnection();
            string sql = @"SELECT * FROM ot_surgery_request WHERE tenant_code = @tenant_code AND isdeleted = false";
            if (!string.IsNullOrWhiteSpace(status)) sql += " AND status = @status";
            if (custid.HasValue) sql += " AND custid = @custid";
            sql += " ORDER BY created_at DESC";

            var res = await db.QueryAsync<OtSurgeryRequestModel>(sql,
                new { tenant_code, status = status?.ToUpper(), custid });
            return res.ToList();
        }

        // FIXED: now also clears any pending OT_PROCEDURE / OT_CONSUMABLE / OT_IMPLANT
        // unbilledcharges rows tied to this request, so a cancelled surgery can't still
        // get billed. Already-billed rows are left untouched (refund concern, not this method's job).
        public async Task<string> CancelSurgeryRequest(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                int rows = await db.ExecuteAsync(
                    @"UPDATE ot_surgery_request SET status = 'CANCELLED', updated_at = now()
                      WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { request_id, tenant_code }, tx);

                if (rows == 0)
                {
                    tx.Rollback();
                    return "Surgery request not found";
                }

                var chargeIds = new List<string> { request_id.ToString() };
                chargeIds.AddRange((await db.QueryAsync<Guid>(
                    "SELECT consumable_id FROM ot_consumable WHERE request_id = @request_id AND tenant_code = @tenant_code",
                    new { request_id, tenant_code }, tx)).Select(g => g.ToString()));
                chargeIds.AddRange((await db.QueryAsync<Guid>(
                    "SELECT implant_id FROM ot_implant WHERE request_id = @request_id AND tenant_code = @tenant_code",
                    new { request_id, tenant_code }, tx)).Select(g => g.ToString()));

                await db.ExecuteAsync(
     @"DELETE FROM unbilledcharges
      WHERE entrytype IN ('OT_PROCEDURE','OT_CONSUMABLE','OT_IMPLANT')
      AND entryid = ANY(@chargeIds) AND tenant_code = @tenant_code
      AND (billedstatus = false OR billedstatus IS NULL)",
     new { chargeIds, tenant_code }, tx);

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════════════════════
        // 3. OT SCHEDULE  (double-booking guarded)
        // ═══════════════════════════════════════════════════════
        public async Task<string> ScheduleSurgery(AddOtScheduleRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                await db.ExecuteAsync(
                    "SELECT pg_advisory_xact_lock(hashtext(@lockKey))",
                    new { lockKey = $"otsched:{tenant_code}:{req.ot_id}:{req.scheduled_date:yyyyMMdd}" }, tx);

                int overlapping = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM ot_schedule
                      WHERE ot_id = @ot_id AND tenant_code = @tenant_code AND isdeleted = false
                      AND scheduled_date = @scheduled_date
                      AND status NOT IN ('CANCELLED')
                      AND scheduled_time < @estimated_end_time
                      AND estimated_end_time > @scheduled_time",
                    new
                    {
                        req.ot_id,
                        tenant_code,
                        scheduled_date = req.scheduled_date.ToDateTime(TimeOnly.MinValue),
                        scheduled_time = req.scheduled_time.ToTimeSpan(),
                        estimated_end_time = req.estimated_end_time.ToTimeSpan()
                    }, tx);

                if (overlapping > 0)
                {
                    tx.Rollback();
                    return "This OT already has an overlapping booking for the selected date/time.";
                }

                var data = new OtScheduleModel
                {
                    request_id = req.request_id,
                    ot_id = req.ot_id,
                    custid = req.custid,
                    scheduled_date = req.scheduled_date,
                    scheduled_time = req.scheduled_time,
                    estimated_end_time = req.estimated_end_time,
                    notes = req.notes,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_schedule
                    (schedule_id, request_id, ot_id, custid, scheduled_date, scheduled_time, estimated_end_time,
                     status, notes, tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@schedule_id, @request_id, @ot_id, @custid, @scheduled_date, @scheduled_time, @estimated_end_time,
                     @status, @notes, @tenant_code, @isdeleted, @created_at, @updated_at)",
                    new
                    {
                        data.schedule_id,
                        data.request_id,
                        data.ot_id,
                        data.custid,
                        scheduled_date = data.scheduled_date.ToDateTime(TimeOnly.MinValue),
                        scheduled_time = data.scheduled_time.ToTimeSpan(),
                        estimated_end_time = data.estimated_end_time.ToTimeSpan(),
                        data.status,
                        data.notes,
                        data.tenant_code,
                        data.isdeleted,
                        data.created_at,
                        data.updated_at
                    }, tx);

                await db.ExecuteAsync(
                    @"UPDATE ot_surgery_request SET status = 'SCHEDULED', updated_at = now()
                      WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { req.request_id, tenant_code }, tx);

                tx.Commit();
                return $"Success|Id:{data.schedule_id}";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        // FIXED: now runs inside a transaction and, when the caller sets status to
        // CANCELLED, syncs ot_surgery_request back to 'REQUESTED' (only if it was
        // still 'SCHEDULED', so IN_PROGRESS/COMPLETED requests are never clobbered).
        public async Task<string> UpdateOtSchedule(UpdateOtScheduleRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                int overlapping = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM ot_schedule
                      WHERE ot_id = @ot_id AND tenant_code = @tenant_code AND isdeleted = false
                      AND schedule_id <> @schedule_id
                      AND scheduled_date = @scheduled_date
                      AND status NOT IN ('CANCELLED')
                      AND scheduled_time < @estimated_end_time
                      AND estimated_end_time > @scheduled_time",
                    new
                    {
                        req.schedule_id,
                        req.ot_id,
                        tenant_code,
                        scheduled_date = req.scheduled_date.ToDateTime(TimeOnly.MinValue),
                        scheduled_time = req.scheduled_time.ToTimeSpan(),
                        estimated_end_time = req.estimated_end_time.ToTimeSpan()
                    }, tx);

                if (overlapping > 0)
                {
                    tx.Rollback();
                    return "This OT already has an overlapping booking for the selected date/time.";
                }

                var existing = await db.QueryFirstOrDefaultAsync<OtScheduleModel>(
                    @"SELECT * FROM ot_schedule WHERE schedule_id = @schedule_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { req.schedule_id, tenant_code }, tx);

                if (existing == null)
                {
                    tx.Rollback();
                    return "Schedule not found";
                }

                int rows = await db.ExecuteAsync(@"
                    UPDATE ot_schedule
                    SET ot_id = @ot_id, scheduled_date = @scheduled_date, scheduled_time = @scheduled_time,
                        estimated_end_time = @estimated_end_time, status = @status, notes = @notes, updated_at = now()
                    WHERE schedule_id = @schedule_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new
                    {
                        req.schedule_id,
                        req.ot_id,
                        scheduled_date = req.scheduled_date.ToDateTime(TimeOnly.MinValue),
                        scheduled_time = req.scheduled_time.ToTimeSpan(),
                        estimated_end_time = req.estimated_end_time.ToTimeSpan(),
                        req.status,
                        req.notes,
                        tenant_code
                    }, tx);

                if (string.Equals(req.status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    await db.ExecuteAsync(
                        @"UPDATE ot_surgery_request SET status = 'REQUESTED', updated_at = now()
                          WHERE request_id = @request_id AND tenant_code = @tenant_code AND status = 'SCHEDULED'",
                        new { existing.request_id, tenant_code }, tx);
                }

                tx.Commit();
                return rows > 0 ? "Success" : "Schedule not found";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        public async Task<List<OtScheduleModel>> GetOtCalendar(
            Guid? ot_id, DateOnly? from_date, DateOnly? to_date, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            string sql = @"SELECT * FROM ot_schedule WHERE tenant_code = @tenant_code AND isdeleted = false";
            if (ot_id.HasValue) sql += " AND ot_id = @ot_id";
            if (from_date.HasValue) sql += " AND scheduled_date >= @from_date";
            if (to_date.HasValue) sql += " AND scheduled_date <= @to_date";
            sql += " ORDER BY scheduled_date, scheduled_time";

            var res = await db.QueryAsync<OtScheduleModel>(sql,
                new
                {
                    tenant_code,
                    ot_id,
                    from_date = from_date?.ToDateTime(TimeOnly.MinValue),
                    to_date = to_date?.ToDateTime(TimeOnly.MinValue)
                });
            return res.ToList();
        }

        public async Task<OtScheduleModel?> GetScheduleByRequest(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtScheduleModel>(
                @"SELECT * FROM ot_schedule WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY created_at DESC LIMIT 1",
                new { request_id, tenant_code });
        }

        // FIXED: now runs inside a transaction and syncs ot_surgery_request back to
        // 'REQUESTED' (only if it was still 'SCHEDULED') so a cancelled slot doesn't
        // leave the request permanently stuck at SCHEDULED.
        public async Task<string> CancelOtSchedule(Guid schedule_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var schedule = await db.QueryFirstOrDefaultAsync<OtScheduleModel>(
                    @"SELECT * FROM ot_schedule WHERE schedule_id = @schedule_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { schedule_id, tenant_code }, tx);

                if (schedule == null)
                {
                    tx.Rollback();
                    return "Schedule not found";
                }

                await db.ExecuteAsync(
                    @"UPDATE ot_schedule SET status = 'CANCELLED', updated_at = now()
                      WHERE schedule_id = @schedule_id AND tenant_code = @tenant_code",
                    new { schedule_id, tenant_code }, tx);

                await db.ExecuteAsync(
                    @"UPDATE ot_surgery_request SET status = 'REQUESTED', updated_at = now()
                      WHERE request_id = @request_id AND tenant_code = @tenant_code AND status = 'SCHEDULED'",
                    new { schedule.request_id, tenant_code }, tx);

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════════════════════
        // 4. OT TEAM
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddOtTeamMember(AddOtTeamMemberRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var data = new OtTeamMemberModel
            {
                request_id = req.request_id,
                role = req.role.ToUpper(),
                dcode = req.dcode,
                staff_name = req.staff_name,
                staff_code = req.staff_code,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ot_team
                (team_id, request_id, role, dcode, staff_name, staff_code, tenant_code, isdeleted, created_at)
                VALUES
                (@team_id, @request_id, @role, @dcode, @staff_name, @staff_code, @tenant_code, @isdeleted, @created_at)",
                data);

            return $"Success|Id:{data.team_id}";
        }

        public async Task<List<OtTeamMemberModel>> GetOtTeam(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var res = await db.QueryAsync<OtTeamMemberModel>(
                @"SELECT * FROM ot_team WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY created_at",
                new { request_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> RemoveOtTeamMember(Guid team_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(
                @"UPDATE ot_team SET isdeleted = true WHERE team_id = @team_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { team_id, tenant_code });
            return rows > 0 ? "Success" : "Team member not found";
        }

        // ═══════════════════════════════════════════════════════
        // 5. PRE-OP ASSESSMENT
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddPreOpAssessment(AddPreOpAssessmentRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var data = new OtPreOpAssessmentModel
            {
                request_id = req.request_id,
                custid = req.custid,
                assessed_by_dcode = req.assessed_by_dcode,
                fitness_status = req.fitness_status.ToUpper(),
                medical_history = req.medical_history,
                allergies = req.allergies,
                investigations_summary = req.investigations_summary,
                vitals_summary = req.vitals_summary,
                clearance_notes = req.clearance_notes,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ot_preop_assessment
                (preop_id, request_id, custid, assessed_by_dcode, fitness_status, medical_history, allergies,
                 investigations_summary, vitals_summary, clearance_notes, assessment_date,
                 tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@preop_id, @request_id, @custid, @assessed_by_dcode, @fitness_status, @medical_history, @allergies,
                 @investigations_summary, @vitals_summary, @clearance_notes, @assessment_date,
                 @tenant_code, @isdeleted, @created_at, @updated_at)",
                data);

            return $"Success|Id:{data.preop_id}";
        }

        public async Task<string> UpdatePreOpAssessment(UpdatePreOpAssessmentRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(@"
                UPDATE ot_preop_assessment
                SET fitness_status = @fitness_status, medical_history = @medical_history, allergies = @allergies,
                    investigations_summary = @investigations_summary, vitals_summary = @vitals_summary,
                    clearance_notes = @clearance_notes, updated_at = now()
                WHERE preop_id = @preop_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.preop_id,
                    fitness_status = req.fitness_status.ToUpper(),
                    req.medical_history,
                    req.allergies,
                    req.investigations_summary,
                    req.vitals_summary,
                    req.clearance_notes,
                    tenant_code
                });

            return rows > 0 ? "Success" : "Pre-op assessment not found";
        }

        public async Task<OtPreOpAssessmentModel?> GetPreOpAssessment(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtPreOpAssessmentModel>(
                @"SELECT * FROM ot_preop_assessment WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY created_at DESC LIMIT 1",
                new { request_id, tenant_code });
        }

        // ═══════════════════════════════════════════════════════
        // 6. CONSENT
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddOtConsent(AddOtConsentRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var data = new OtConsentModel
            {
                request_id = req.request_id,
                custid = req.custid,
                consent_type = req.consent_type.ToUpper(),
                consent_given = req.consent_given,
                consent_by = req.consent_by,
                relation = req.relation,
                witness_name = req.witness_name,
                remarks = req.remarks,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ot_consent
                (consent_id, request_id, custid, consent_type, consent_given, consent_by, relation,
                 witness_name, consent_date, remarks, tenant_code, isdeleted, created_at)
                VALUES
                (@consent_id, @request_id, @custid, @consent_type, @consent_given, @consent_by, @relation,
                 @witness_name, @consent_date, @remarks, @tenant_code, @isdeleted, @created_at)",
                data);

            return $"Success|Id:{data.consent_id}";
        }

        public async Task<List<OtConsentModel>> GetOtConsents(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var res = await db.QueryAsync<OtConsentModel>(
                @"SELECT * FROM ot_consent WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY created_at",
                new { request_id, tenant_code });
            return res.ToList();
        }

        // ═══════════════════════════════════════════════════════
        // 7. OT CHECKLIST  (upsert)
        // ═══════════════════════════════════════════════════════
        public async Task<string> SaveOtChecklist(SaveOtChecklistRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<OtChecklistModel>(
                @"SELECT * FROM ot_checklist WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.request_id, tenant_code });

            if (existing == null)
            {
                var data = new OtChecklistModel
                {
                    request_id = req.request_id,
                    patient_confirmed = req.patient_confirmed,
                    procedure_confirmed = req.procedure_confirmed,
                    site_marked = req.site_marked,
                    consent_confirmed = req.consent_confirmed,
                    investigations_confirmed = req.investigations_confirmed,
                    blood_arranged = req.blood_arranged,
                    instruments_confirmed = req.instruments_confirmed,
                    implants_confirmed = req.implants_confirmed,
                    antibiotic_given = req.antibiotic_given,
                    checked_by = req.checked_by,
                    checked_at = DateTime.UtcNow,
                    remarks = req.remarks,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_checklist
                    (checklist_id, request_id, patient_confirmed, procedure_confirmed, site_marked, consent_confirmed,
                     investigations_confirmed, blood_arranged, instruments_confirmed, implants_confirmed, antibiotic_given,
                     checked_by, checked_at, remarks, tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@checklist_id, @request_id, @patient_confirmed, @procedure_confirmed, @site_marked, @consent_confirmed,
                     @investigations_confirmed, @blood_arranged, @instruments_confirmed, @implants_confirmed, @antibiotic_given,
                     @checked_by, @checked_at, @remarks, @tenant_code, @isdeleted, @created_at, @updated_at)",
                    data);

                return $"Success|Id:{data.checklist_id}";
            }
            else
            {
                await db.ExecuteAsync(@"
                    UPDATE ot_checklist
                    SET patient_confirmed = @patient_confirmed, procedure_confirmed = @procedure_confirmed,
                        site_marked = @site_marked, consent_confirmed = @consent_confirmed,
                        investigations_confirmed = @investigations_confirmed, blood_arranged = @blood_arranged,
                        instruments_confirmed = @instruments_confirmed, implants_confirmed = @implants_confirmed,
                        antibiotic_given = @antibiotic_given, checked_by = @checked_by, checked_at = now(),
                        remarks = @remarks, updated_at = now()
                    WHERE checklist_id = @checklist_id AND tenant_code = @tenant_code",
                    new
                    {
                        existing.checklist_id,
                        req.patient_confirmed,
                        req.procedure_confirmed,
                        req.site_marked,
                        req.consent_confirmed,
                        req.investigations_confirmed,
                        req.blood_arranged,
                        req.instruments_confirmed,
                        req.implants_confirmed,
                        req.antibiotic_given,
                        req.checked_by,
                        req.remarks,
                        tenant_code
                    });

                return $"Success|Id:{existing.checklist_id}";
            }
        }

        public async Task<OtChecklistModel?> GetOtChecklist(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtChecklistModel>(
                @"SELECT * FROM ot_checklist WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { request_id, tenant_code });
        }

        // ═══════════════════════════════════════════════════════
        // 8. ANESTHESIA  (upsert)
        // ═══════════════════════════════════════════════════════
        public async Task<string> SaveOtAnesthesia(SaveOtAnesthesiaRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<OtAnesthesiaModel>(
                @"SELECT * FROM ot_anesthesia WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.request_id, tenant_code });

            if (existing == null)
            {
                var data = new OtAnesthesiaModel
                {
                    request_id = req.request_id,
                    custid = req.custid,
                    anesthesia_type = req.anesthesia_type,
                    anesthetist_dcode = req.anesthetist_dcode,
                    asa_grade = req.asa_grade,
                    airway_assessment = req.airway_assessment,
                    start_time = req.start_time,
                    end_time = req.end_time,
                    drugs_used = req.drugs_used,
                    fluids_given = req.fluids_given,
                    complications = req.complications,
                    remarks = req.remarks,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_anesthesia
                    (anesthesia_id, request_id, custid, anesthesia_type, anesthetist_dcode, asa_grade, airway_assessment,
                     start_time, end_time, drugs_used, fluids_given, complications, remarks,
                     tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@anesthesia_id, @request_id, @custid, @anesthesia_type, @anesthetist_dcode, @asa_grade, @airway_assessment,
                     @start_time, @end_time, @drugs_used, @fluids_given, @complications, @remarks,
                     @tenant_code, @isdeleted, @created_at, @updated_at)",
                    data);

                return $"Success|Id:{data.anesthesia_id}";
            }
            else
            {
                await db.ExecuteAsync(@"
                    UPDATE ot_anesthesia
                    SET anesthesia_type = @anesthesia_type, anesthetist_dcode = @anesthetist_dcode, asa_grade = @asa_grade,
                        airway_assessment = @airway_assessment, start_time = @start_time, end_time = @end_time,
                        drugs_used = @drugs_used, fluids_given = @fluids_given, complications = @complications,
                        remarks = @remarks, updated_at = now()
                    WHERE anesthesia_id = @anesthesia_id AND tenant_code = @tenant_code",
                    new
                    {
                        existing.anesthesia_id,
                        req.anesthesia_type,
                        req.anesthetist_dcode,
                        req.asa_grade,
                        req.airway_assessment,
                        req.start_time,
                        req.end_time,
                        req.drugs_used,
                        req.fluids_given,
                        req.complications,
                        req.remarks,
                        tenant_code
                    });

                return $"Success|Id:{existing.anesthesia_id}";
            }
        }

        public async Task<OtAnesthesiaModel?> GetOtAnesthesia(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtAnesthesiaModel>(
                @"SELECT * FROM ot_anesthesia WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { request_id, tenant_code });
        }

        // ═══════════════════════════════════════════════════════
        // 9. INTRA-OP / SURGERY RECORD  (upsert)
        // ═══════════════════════════════════════════════════════
        public async Task<string> SaveOtIntraOp(SaveOtIntraOpRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<OtIntraOpModel>(
                @"SELECT * FROM ot_intraop WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.request_id, tenant_code });

            if (existing == null)
            {
                var data = new OtIntraOpModel
                {
                    request_id = req.request_id,
                    custid = req.custid,
                    procedure_performed = req.procedure_performed,
                    diagnosis_pre = req.diagnosis_pre,
                    diagnosis_post = req.diagnosis_post,
                    findings = req.findings,
                    surgery_start_time = req.surgery_start_time,
                    surgery_end_time = req.surgery_end_time,
                    blood_loss_ml = req.blood_loss_ml,
                    complications = req.complications,
                    remarks = req.remarks,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_intraop
                    (intraop_id, request_id, custid, procedure_performed, diagnosis_pre, diagnosis_post, findings,
                     surgery_start_time, surgery_end_time, blood_loss_ml, complications, remarks,
                     tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@intraop_id, @request_id, @custid, @procedure_performed, @diagnosis_pre, @diagnosis_post, @findings,
                     @surgery_start_time, @surgery_end_time, @blood_loss_ml, @complications, @remarks,
                     @tenant_code, @isdeleted, @created_at, @updated_at)",
                    data);

                await db.ExecuteAsync(
                    @"UPDATE ot_surgery_request SET status = 'IN_PROGRESS', updated_at = now()
                      WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { req.request_id, tenant_code });

                return $"Success|Id:{data.intraop_id}";
            }
            else
            {
                await db.ExecuteAsync(@"
                    UPDATE ot_intraop
                    SET procedure_performed = @procedure_performed, diagnosis_pre = @diagnosis_pre,
                        diagnosis_post = @diagnosis_post, findings = @findings,
                        surgery_start_time = @surgery_start_time, surgery_end_time = @surgery_end_time,
                        blood_loss_ml = @blood_loss_ml, complications = @complications, remarks = @remarks,
                        updated_at = now()
                    WHERE intraop_id = @intraop_id AND tenant_code = @tenant_code",
                    new
                    {
                        existing.intraop_id,
                        req.procedure_performed,
                        req.diagnosis_pre,
                        req.diagnosis_post,
                        req.findings,
                        req.surgery_start_time,
                        req.surgery_end_time,
                        req.blood_loss_ml,
                        req.complications,
                        req.remarks,
                        tenant_code
                    });

                if (req.surgery_end_time.HasValue)
                {
                    await db.ExecuteAsync(
                        @"UPDATE ot_surgery_request SET status = 'COMPLETED', updated_at = now()
                          WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                        new { req.request_id, tenant_code });
                }

                return $"Success|Id:{existing.intraop_id}";
            }
        }

        public async Task<OtIntraOpModel?> GetOtIntraOp(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtIntraOpModel>(
                @"SELECT * FROM ot_intraop WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { request_id, tenant_code });
        }

        // ═══════════════════════════════════════════════════════
        // 10. CONSUMABLES
        // Pushes an 'OT_CONSUMABLE' row into unbilledcharges — same
        // moment/pattern as an investigation test row being pushed
        // in NewOPCaseSheetClass.SaveInvestigation.
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddOtConsumable(AddOtConsumableRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var surgeryReq = await db.QueryFirstOrDefaultAsync<OtSurgeryRequestModel>(
                    @"SELECT * FROM ot_surgery_request WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { req.request_id, tenant_code }, tx);

                if (surgeryReq == null)
                {
                    tx.Rollback();
                    return "Surgery request not found";
                }

                decimal? amount = req.amount ?? (req.rate.HasValue ? req.rate.Value * req.quantity : null);

                var data = new OtConsumableModel
                {
                    request_id = req.request_id,
                    item_name = req.item_name,
                    item_code = req.item_code,
                    batch_no = req.batch_no,
                    quantity = req.quantity,
                    unit = req.unit,
                    rate = req.rate,
                    amount = amount,
                    remarks = req.remarks,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_consumable
                    (consumable_id, request_id, item_name, item_code, batch_no, quantity, unit, rate, amount,
                     is_billed, remarks, tenant_code, isdeleted, created_at)
                    VALUES
                    (@consumable_id, @request_id, @item_name, @item_code, @batch_no, @quantity, @unit, @rate, @amount,
                     false, @remarks, @tenant_code, @isdeleted, @created_at)",
                    data, tx);

                await _unbilledCls.AddOtChargeRow(
                    db, tx, surgeryReq.op_id, surgeryReq.ip_id, surgeryReq.custid, tenant_code,
                    entryType: "OT_CONSUMABLE",
                    entryId: data.consumable_id.ToString(),
                    itemName: data.item_name,
                    quantity: data.quantity,
                    rate: data.rate,
                    amount: data.amount);

                tx.Commit();
                return $"Success|Id:{data.consumable_id}";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        public async Task<List<OtConsumableModel>> GetOtConsumables(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var res = await db.QueryAsync<OtConsumableModel>(
                @"SELECT * FROM ot_consumable WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY created_at",
                new { request_id, tenant_code });
            return res.ToList();
        }

        // Soft-deletes the consumable AND clears its pending (not-yet-billed)
        // unbilledcharges row. A charge that's already been billed is left
        // alone — same rule SaveInvestigation follows for pending vs. billed rows.
        public async Task<string> DeleteOtConsumable(Guid consumable_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                int rows = await db.ExecuteAsync(
                    @"UPDATE ot_consumable SET isdeleted = true WHERE consumable_id = @consumable_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { consumable_id, tenant_code }, tx);

                if (rows == 0)
                {
                    tx.Rollback();
                    return "Consumable not found";
                }

                await db.ExecuteAsync(
                    @"DELETE FROM unbilledcharges
                      WHERE entrytype = 'OT_CONSUMABLE' AND entryid = @entryid AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                    new { entryid = consumable_id.ToString(), tenant_code }, tx);

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════════════════════
        // 11. IMPLANTS
        // Pushes an 'OT_IMPLANT' row into unbilledcharges, same pattern.
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddOtImplant(AddOtImplantRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                var surgeryReq = await db.QueryFirstOrDefaultAsync<OtSurgeryRequestModel>(
                    @"SELECT * FROM ot_surgery_request WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { req.request_id, tenant_code }, tx);

                if (surgeryReq == null)
                {
                    tx.Rollback();
                    return "Surgery request not found";
                }

                decimal? amount = req.amount ?? (req.rate.HasValue ? req.rate.Value * req.quantity : null);

                var data = new OtImplantModel
                {
                    request_id = req.request_id,
                    custid = req.custid,
                    item_name = req.item_name,
                    batch_no = req.batch_no,
                    serial_no = req.serial_no,
                    manufacturer = req.manufacturer,
                    quantity = req.quantity,
                    rate = req.rate,
                    amount = amount,
                    remarks = req.remarks,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_implant
                    (implant_id, request_id, custid, item_name, batch_no, serial_no, manufacturer, quantity, rate, amount,
                     is_billed, remarks, tenant_code, isdeleted, created_at)
                    VALUES
                    (@implant_id, @request_id, @custid, @item_name, @batch_no, @serial_no, @manufacturer, @quantity, @rate, @amount,
                     false, @remarks, @tenant_code, @isdeleted, @created_at)",
                    data, tx);

                await _unbilledCls.AddOtChargeRow(
                    db, tx, surgeryReq.op_id, surgeryReq.ip_id, surgeryReq.custid, tenant_code,
                    entryType: "OT_IMPLANT",
                    entryId: data.implant_id.ToString(),
                    itemName: data.item_name,
                    quantity: data.quantity,
                    rate: data.rate,
                    amount: data.amount);

                tx.Commit();
                return $"Success|Id:{data.implant_id}";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        public async Task<List<OtImplantModel>> GetOtImplants(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var res = await db.QueryAsync<OtImplantModel>(
                @"SELECT * FROM ot_implant WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY created_at",
                new { request_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteOtImplant(Guid implant_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                int rows = await db.ExecuteAsync(
                    @"UPDATE ot_implant SET isdeleted = true WHERE implant_id = @implant_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new { implant_id, tenant_code }, tx);

                if (rows == 0)
                {
                    tx.Rollback();
                    return "Implant not found";
                }

                await db.ExecuteAsync(
                    @"DELETE FROM unbilledcharges
                      WHERE entrytype = 'OT_IMPLANT' AND entryid = @entryid AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                    new { entryid = implant_id.ToString(), tenant_code }, tx);

                tx.Commit();
                return "Success";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        // ═══════════════════════════════════════════════════════
        // 12. RECOVERY / PACU  (upsert)
        // ═══════════════════════════════════════════════════════
        public async Task<string> SaveOtRecovery(SaveOtRecoveryRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<OtRecoveryModel>(
                @"SELECT * FROM ot_recovery WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.request_id, tenant_code });

            if (existing == null)
            {
                var data = new OtRecoveryModel
                {
                    request_id = req.request_id,
                    custid = req.custid,
                    admission_time = req.admission_time,
                    discharge_time = req.discharge_time,
                    vitals_summary = req.vitals_summary,
                    pain_score = req.pain_score,
                    oxygen_support = req.oxygen_support,
                    recovery_status = req.recovery_status.ToUpper(),
                    transferred_to = req.transferred_to,
                    remarks = req.remarks,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_recovery
                    (recovery_id, request_id, custid, admission_time, discharge_time, vitals_summary, pain_score,
                     oxygen_support, recovery_status, transferred_to, remarks, tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@recovery_id, @request_id, @custid, @admission_time, @discharge_time, @vitals_summary, @pain_score,
                     @oxygen_support, @recovery_status, @transferred_to, @remarks, @tenant_code, @isdeleted, @created_at, @updated_at)",
                    data);

                return $"Success|Id:{data.recovery_id}";
            }
            else
            {
                await db.ExecuteAsync(@"
                    UPDATE ot_recovery
                    SET admission_time = @admission_time, discharge_time = @discharge_time,
                        vitals_summary = @vitals_summary, pain_score = @pain_score, oxygen_support = @oxygen_support,
                        recovery_status = @recovery_status, transferred_to = @transferred_to, remarks = @remarks,
                        updated_at = now()
                    WHERE recovery_id = @recovery_id AND tenant_code = @tenant_code",
                    new
                    {
                        existing.recovery_id,
                        req.admission_time,
                        req.discharge_time,
                        req.vitals_summary,
                        req.pain_score,
                        req.oxygen_support,
                        recovery_status = req.recovery_status.ToUpper(),
                        req.transferred_to,
                        req.remarks,
                        tenant_code
                    });

                return $"Success|Id:{existing.recovery_id}";
            }
        }

        public async Task<OtRecoveryModel?> GetOtRecovery(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtRecoveryModel>(
                @"SELECT * FROM ot_recovery WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { request_id, tenant_code });
        }

        // ═══════════════════════════════════════════════════════
        // 13. POST-OP ORDERS
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddOtPostOpOrder(AddOtPostOpOrderRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var data = new OtPostOpOrderModel
            {
                request_id = req.request_id,
                custid = req.custid,
                order_type = req.order_type.ToUpper(),
                order_text = req.order_text,
                instructions = req.instructions,
                ordered_by_dcode = req.ordered_by_dcode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ot_postop_order
                (order_id, request_id, custid, order_type, order_text, instructions, ordered_by_dcode, status,
                 order_date, tenant_code, isdeleted, created_at)
                VALUES
                (@order_id, @request_id, @custid, @order_type, @order_text, @instructions, @ordered_by_dcode, @status,
                 @order_date, @tenant_code, @isdeleted, @created_at)",
                data);

            return $"Success|Id:{data.order_id}";
        }

        public async Task<List<OtPostOpOrderModel>> GetOtPostOpOrders(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            var res = await db.QueryAsync<OtPostOpOrderModel>(
                @"SELECT * FROM ot_postop_order WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY order_date DESC",
                new { request_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> UpdateOtPostOpOrderStatus(UpdateOtPostOpOrderStatusRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(
                @"UPDATE ot_postop_order SET status = @status
                  WHERE order_id = @order_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.order_id, status = req.status.ToUpper(), tenant_code });
            return rows > 0 ? "Success" : "Order not found";
        }

        public async Task<string> DeleteOtPostOpOrder(Guid order_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            int rows = await db.ExecuteAsync(
                @"UPDATE ot_postop_order SET isdeleted = true WHERE order_id = @order_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { order_id, tenant_code });
            return rows > 0 ? "Success" : "Order not found";
        }

        // ═══════════════════════════════════════════════════════
        // 14. OPERATION NOTE  (upsert)
        // ═══════════════════════════════════════════════════════
        public async Task<string> SaveOtOperationNote(SaveOtOperationNoteRequest req, string tenant_code)
        {
            using IDbConnection db = GetConnection();

            var existing = await db.QueryFirstOrDefaultAsync<OtOperationNoteModel>(
                @"SELECT * FROM ot_operation_note WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.request_id, tenant_code });

            if (existing == null)
            {
                var data = new OtOperationNoteModel
                {
                    request_id = req.request_id,
                    custid = req.custid,
                    surgeon_dcode = req.surgeon_dcode,
                    pre_op_diagnosis = req.pre_op_diagnosis,
                    post_op_diagnosis = req.post_op_diagnosis,
                    procedure_performed = req.procedure_performed,
                    findings = req.findings,
                    blood_loss_ml = req.blood_loss_ml,
                    specimen_sent = req.specimen_sent,
                    specimen_details = req.specimen_details,
                    complications = req.complications,
                    instructions = req.instructions,
                    tenant_code = tenant_code
                };

                await db.ExecuteAsync(@"
                    INSERT INTO ot_operation_note
                    (note_id, request_id, custid, surgeon_dcode, pre_op_diagnosis, post_op_diagnosis, procedure_performed,
                     findings, blood_loss_ml, specimen_sent, specimen_details, complications, instructions, note_date,
                     tenant_code, isdeleted, created_at, updated_at)
                    VALUES
                    (@note_id, @request_id, @custid, @surgeon_dcode, @pre_op_diagnosis, @post_op_diagnosis, @procedure_performed,
                     @findings, @blood_loss_ml, @specimen_sent, @specimen_details, @complications, @instructions, @note_date,
                     @tenant_code, @isdeleted, @created_at, @updated_at)",
                    data);

                return $"Success|Id:{data.note_id}";
            }
            else
            {
                await db.ExecuteAsync(@"
                    UPDATE ot_operation_note
                    SET surgeon_dcode = @surgeon_dcode, pre_op_diagnosis = @pre_op_diagnosis,
                        post_op_diagnosis = @post_op_diagnosis, procedure_performed = @procedure_performed,
                        findings = @findings, blood_loss_ml = @blood_loss_ml, specimen_sent = @specimen_sent,
                        specimen_details = @specimen_details, complications = @complications,
                        instructions = @instructions, updated_at = now()
                    WHERE note_id = @note_id AND tenant_code = @tenant_code",
                    new
                    {
                        existing.note_id,
                        req.surgeon_dcode,
                        req.pre_op_diagnosis,
                        req.post_op_diagnosis,
                        req.procedure_performed,
                        req.findings,
                        req.blood_loss_ml,
                        req.specimen_sent,
                        req.specimen_details,
                        req.complications,
                        req.instructions,
                        tenant_code
                    });

                return $"Success|Id:{existing.note_id}";
            }
        }

        public async Task<OtOperationNoteModel?> GetOtOperationNote(Guid request_id, string tenant_code)
        {
            using IDbConnection db = GetConnection();
            return await db.QueryFirstOrDefaultAsync<OtOperationNoteModel>(
                @"SELECT * FROM ot_operation_note WHERE request_id = @request_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { request_id, tenant_code });
        }

        // ═══════════════════════════════════════════════════════
        // 15. OT SUMMARY — one call, everything for a request.
        // Billed/pending state is read live from unbilledcharges
        // instead of a separate bill record.
        // FIXED: switched from QueryFirstOrDefaultAsync<(int,int)> (Dapper does
        // not map named ValueTuple fields — this silently returned (0,0)) to
        // a dynamic row with Convert.ToInt32 (Postgres COUNT returns bigint).
        // ═══════════════════════════════════════════════════════
        public async Task<OtSummaryModel?> GetOtSummary(Guid request_id, string tenant_code)
        {
            var request = await GetSurgeryRequestById(request_id, tenant_code);
            if (request == null) return null;

            var consumables = await GetOtConsumables(request_id, tenant_code);
            var implants = await GetOtImplants(request_id, tenant_code);

            var chargeIds = new List<string> { request_id.ToString() };
            chargeIds.AddRange(consumables.Select(c => c.consumable_id.ToString()));
            chargeIds.AddRange(implants.Select(i => i.implant_id.ToString()));

            int totalCharges = 0, pendingCharges = 0;
            using (IDbConnection db = GetConnection())
            {
                var counts = await db.QueryFirstOrDefaultAsync<dynamic>(
    @"SELECT COUNT(*) AS total,
             COUNT(*) FILTER (WHERE billedstatus = false OR billedstatus IS NULL) AS pending
      FROM   unbilledcharges
      WHERE  tenant_code = @tenant_code
      AND    entrytype IN ('OT_PROCEDURE', 'OT_CONSUMABLE', 'OT_IMPLANT')
      AND    entryid = ANY(@chargeIds)",
    new { tenant_code, chargeIds });

                totalCharges = counts != null ? Convert.ToInt32(counts.total) : 0;
                pendingCharges = counts != null ? Convert.ToInt32(counts.pending) : 0;
            }

            return new OtSummaryModel
            {
                request = request,
                schedule = await GetScheduleByRequest(request_id, tenant_code),
                team = await GetOtTeam(request_id, tenant_code),
                preop = await GetPreOpAssessment(request_id, tenant_code),
                consents = await GetOtConsents(request_id, tenant_code),
                checklist = await GetOtChecklist(request_id, tenant_code),
                anesthesia = await GetOtAnesthesia(request_id, tenant_code),
                intraop = await GetOtIntraOp(request_id, tenant_code),
                consumables = consumables,
                implants = implants,
                recovery = await GetOtRecovery(request_id, tenant_code),
                postop_orders = await GetOtPostOpOrders(request_id, tenant_code),
                operation_note = await GetOtOperationNote(request_id, tenant_code),
                total_charge_count = totalCharges,
                pending_charge_count = pendingCharges,
                is_fully_billed = totalCharges > 0 && pendingCharges == 0
            };
        }
    }
}