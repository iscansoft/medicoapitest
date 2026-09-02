using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;
using static medico_backend.Model.IpBedsideChartModel;

namespace medico_backend.Class
{
    // ─────────────────────────────────────────────────────────
    // IP NURSE NOTES — each section (Temperature, Vitals,
    // Input/Output, Service, Medicine, Other) has its own table
    // and its own Add/Get/Update/Delete, so a nurse's "Save"
    // button on one tab only touches that tab's table.
    // Every entry requires ip_id — this module is IP-only.
    // ─────────────────────────────────────────────────────────
    public class IpBedsideChartClass
    {
        private readonly string _db_conn;

        public IpBedsideChartClass(IConfiguration configuration)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
        }

        private async Task<bool> IpExists(IDbConnection db, Guid ip_id, string tenant_code)
        {
            int count = await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM ip_registration
                  WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                new { ip_id, tenant_code });
            return count > 0;
        }

        // ═══════════════════════════════════════════════════════
        // 1. TEMPERATURE
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddTemperature(AddTemperatureRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseTemperatureModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                shift = req.shift,
                temperature = req.temperature,
                unit = req.unit,
                remarks = req.remarks,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ip_nurse_note_temperature
                (temp_id, ip_id, custid, entry_date, entry_time, shift, temperature, unit,
                 remarks, usercode, tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@temp_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @temperature, @unit,
                 @remarks, @usercode, @tenant_code, @isdeleted, @created_at, @updated_at)",
    new
    {
        data.temp_id,
        data.ip_id,
        data.custid,
        entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
        entry_time = data.entry_time.ToTimeSpan(),
        data.shift,
        data.temperature,
        data.unit,
        data.remarks,
        data.usercode,
        data.tenant_code,
        data.isdeleted,
        data.created_at,
        data.updated_at
    });

            return $"Success|Id:{data.temp_id}";
        }

        public async Task<string> UpdateTemperature(UpdateTemperatureRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
                UPDATE ip_nurse_note_temperature
                SET shift = @shift, temperature = @temperature, unit = @unit,
                    remarks = @remarks, updated_at = now()
                WHERE temp_id = @temp_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.temp_id, req.shift, req.temperature, req.unit, req.remarks, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseTemperatureModel>> GetTemperature(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseTemperatureModel>(
                @"SELECT * FROM ip_nurse_note_temperature
                  WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY entry_date DESC, entry_time DESC",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteTemperature(Guid temp_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_temperature SET isdeleted = true, updated_at = now()
                  WHERE temp_id = @temp_id AND tenant_code = @tenant_code",
                new { temp_id, tenant_code });
            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════════════════════
        // 2. VITALS — intentionally not implemented here.
        // Use the existing vitals module, passing ip_id the same
        // way it already accepts op_id:
        //   POST api/OpRegistration/save-vitals
        //   POST api/OpRegistration/update-vitals
        //   GET  api/OpRegistration/vitals/all?ip_id=...
        //   GET  api/OpRegistration/vitals/detail?ip_id=...
        // ═══════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════
        // 3. INPUT / OUTPUT
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddInputOutput(AddInputOutputRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseInputOutputModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                shift = req.shift,
                io_type = req.io_type.ToUpper(),
                particulars = req.particulars,
                quantity = req.quantity,
                unit = req.unit,
                remarks = req.remarks,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ip_nurse_note_input_output
                (io_id, ip_id, custid, entry_date, entry_time, shift, io_type, particulars,
                 quantity, unit, remarks, usercode, tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@io_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @io_type, @particulars,
                 @quantity, @unit, @remarks, @usercode, @tenant_code, @isdeleted, @created_at, @updated_at)",
     new
     {
         data.io_id,
         data.ip_id,
         data.custid,
         entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
         entry_time = data.entry_time.ToTimeSpan(),
         data.shift,
         data.io_type,
         data.particulars,
         data.quantity,
         data.unit,
         data.remarks,
         data.usercode,
         data.tenant_code,
         data.isdeleted,
         data.created_at,
         data.updated_at
     });

            return $"Success|Id:{data.io_id}";
        }

        public async Task<string> UpdateInputOutput(UpdateInputOutputRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
                UPDATE ip_nurse_note_input_output
                SET shift = @shift, io_type = @io_type, particulars = @particulars,
                    quantity = @quantity, unit = @unit, remarks = @remarks, updated_at = now()
                WHERE io_id = @io_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.io_id,
                    req.shift,
                    io_type = req.io_type.ToUpper(),
                    req.particulars,
                    req.quantity,
                    req.unit,
                    req.remarks,
                    tenant_code
                });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseInputOutputModel>> GetInputOutput(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseInputOutputModel>(
                @"SELECT * FROM ip_nurse_note_input_output
                  WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY entry_date DESC, entry_time DESC",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteInputOutput(Guid io_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_input_output SET isdeleted = true, updated_at = now()
                  WHERE io_id = @io_id AND tenant_code = @tenant_code",
                new { io_id, tenant_code });
            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════════════════════
        // 4. SERVICE
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddService(AddServiceRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseServiceModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                shift = req.shift,
                service_name = req.service_name,
                action = req.action,
                remarks = req.remarks,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ip_nurse_note_service
                (service_id, ip_id, custid, entry_date, entry_time, shift, service_name, action,
                 remarks, usercode, tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@service_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @service_name, @action,
                 @remarks, @usercode, @tenant_code, @isdeleted, @created_at, @updated_at)",
    new
    {
        data.service_id,
        data.ip_id,
        data.custid,
        entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
        entry_time = data.entry_time.ToTimeSpan(),
        data.shift,
        data.service_name,
        data.action,
        data.remarks,
        data.usercode,
        data.tenant_code,
        data.isdeleted,
        data.created_at,
        data.updated_at
    });

            return $"Success|Id:{data.service_id}";
        }

        public async Task<string> UpdateService(UpdateServiceRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
                UPDATE ip_nurse_note_service
                SET shift = @shift, service_name = @service_name, action = @action,
                    remarks = @remarks, updated_at = now()
                WHERE service_id = @service_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.service_id, req.shift, req.service_name, req.action, req.remarks, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseServiceModel>> GetService(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseServiceModel>(
                @"SELECT * FROM ip_nurse_note_service
                  WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY entry_date DESC, entry_time DESC",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteService(Guid service_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_service SET isdeleted = true, updated_at = now()
                  WHERE service_id = @service_id AND tenant_code = @tenant_code",
                new { service_id, tenant_code });
            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════════════════════
        // 5. MEDICINE
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddMedicine(AddMedicineRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseMedicineModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                shift = req.shift,
                medicine_name = req.medicine_name,
                dose = req.dose,
                unit = req.unit,
                route = req.route,
                status = string.IsNullOrWhiteSpace(req.status) ? "GIVEN" : req.status.ToUpper(),
                remarks = req.remarks,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ip_nurse_note_medicine
                (med_id, ip_id, custid, entry_date, entry_time, shift, medicine_name, dose, unit,
                 route, status, remarks, usercode, tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@med_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @medicine_name, @dose, @unit,
                 @route, @status, @remarks, @usercode, @tenant_code, @isdeleted, @created_at, @updated_at)",
     new
     {
         data.med_id,
         data.ip_id,
         data.custid,
         entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
         entry_time = data.entry_time.ToTimeSpan(),
         data.shift,
         data.medicine_name,
         data.dose,
         data.unit,
         data.route,
         data.status,
         data.remarks,
         data.usercode,
         data.tenant_code,
         data.isdeleted,
         data.created_at,
         data.updated_at
     });

            return $"Success|Id:{data.med_id}";
        }

        public async Task<string> UpdateMedicine(UpdateMedicineRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
                UPDATE ip_nurse_note_medicine
                SET shift = @shift, medicine_name = @medicine_name, dose = @dose, unit = @unit,
                    route = @route, status = @status, remarks = @remarks, updated_at = now()
                WHERE med_id = @med_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.med_id,
                    req.shift,
                    req.medicine_name,
                    req.dose,
                    req.unit,
                    req.route,
                    status = string.IsNullOrWhiteSpace(req.status) ? "GIVEN" : req.status.ToUpper(),
                    req.remarks,
                    tenant_code
                });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseMedicineModel>> GetMedicine(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseMedicineModel>(
                @"SELECT * FROM ip_nurse_note_medicine
                  WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY entry_date DESC, entry_time DESC",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteMedicine(Guid med_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_medicine SET isdeleted = true, updated_at = now()
                  WHERE med_id = @med_id AND tenant_code = @tenant_code",
                new { med_id, tenant_code });
            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════════════════════
        // 6. OTHER
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddOther(AddOtherRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseOtherModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                shift = req.shift,
                note = req.note,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
                INSERT INTO ip_nurse_note_other
                (other_id, ip_id, custid, entry_date, entry_time, shift, note,
                 usercode, tenant_code, isdeleted, created_at, updated_at)
                VALUES
                (@other_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @note,
                 @usercode, @tenant_code, @isdeleted, @created_at, @updated_at)",
    new
    {
        data.other_id,
        data.ip_id,
        data.custid,
        entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
        entry_time = data.entry_time.ToTimeSpan(),
        data.shift,
        data.note,
        data.usercode,
        data.tenant_code,
        data.isdeleted,
        data.created_at,
        data.updated_at
    });

            return $"Success|Id:{data.other_id}";
        }

        public async Task<string> UpdateOther(UpdateOtherRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
                UPDATE ip_nurse_note_other
                SET shift = @shift, note = @note, updated_at = now()
                WHERE other_id = @other_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.other_id, req.shift, req.note, tenant_code });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseOtherModel>> GetOther(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseOtherModel>(
                @"SELECT * FROM ip_nurse_note_other
                  WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
                  ORDER BY entry_date DESC, entry_time DESC",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<string> DeleteOther(Guid other_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_other SET isdeleted = true, updated_at = now()
                  WHERE other_id = @other_id AND tenant_code = @tenant_code",
                new { other_id, tenant_code });
            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════════════════════
        // COMBINED — one call to populate the tabs this module owns
        // (Temperature / Input-Output / Service / Medicine / Other).
        // For the Vitals tab, call the existing vitals endpoint with
        // ip_id. For the OP Case Sheet tab, call
        // GET api/CaseSheet/by-visit?ip_id=... (read-only there).
        // ═══════════════════════════════════════════════════════
        public async Task<NurseNotesSummaryModel> GetAllNurseNotes(Guid ip_id, string tenant_code)
        {
            return new NurseNotesSummaryModel
            {
                temperature = await GetTemperature(ip_id, tenant_code),
                input_output = await GetInputOutput(ip_id, tenant_code),
                service = await GetService(ip_id, tenant_code),
                medicine = await GetMedicine(ip_id, tenant_code),
                other = await GetOther(ip_id, tenant_code)
            };
        }
        // GET single Temperature record by ip_id + temp_id
        public async Task<NurseTemperatureModel?> GetTemperatureById(Guid ip_id, Guid temp_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseTemperatureModel>(
                @"SELECT * FROM ip_nurse_note_temperature
                  WHERE ip_id = @ip_id AND temp_id = @temp_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, temp_id, tenant_code });
        }
        // GET single Input/Output record by ip_id + io_id
        public async Task<NurseInputOutputModel?> GetInputOutputById(Guid ip_id, Guid io_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseInputOutputModel>(
                @"SELECT * FROM ip_nurse_note_input_output
                  WHERE ip_id = @ip_id AND io_id = @io_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, io_id, tenant_code });
        }
        // GET single Service record by ip_id + service_id
        public async Task<NurseServiceModel?> GetServiceById(Guid ip_id, Guid service_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseServiceModel>(
                @"SELECT * FROM ip_nurse_note_service
                  WHERE ip_id = @ip_id AND service_id = @service_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, service_id, tenant_code });
        }
        // GET single Medicine record by ip_id + med_id
        public async Task<NurseMedicineModel?> GetMedicineById(Guid ip_id, Guid med_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseMedicineModel>(
                @"SELECT * FROM ip_nurse_note_medicine
                  WHERE ip_id = @ip_id AND med_id = @med_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, med_id, tenant_code });
        }
        // GET single Other record by ip_id + other_id
        public async Task<NurseOtherModel?> GetOtherById(Guid ip_id, Guid other_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseOtherModel>(
                @"SELECT * FROM ip_nurse_note_other
                  WHERE ip_id = @ip_id AND other_id = @other_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, other_id, tenant_code });
        }
    }
}