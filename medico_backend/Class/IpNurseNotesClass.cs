using Dapper;
using medico_backend.Model;
using Npgsql;
using System.Data;
using static medico_backend.Model.IpBedsideChartModel;
using medico_backend.InventoryClass;
using medico_backend.InventoryModel;   // ReceivePrescriptionRequest, PrescriptionQueueItem

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
        private readonly UnbilledChargesClass _unbilledCls;   
        private readonly ItemMasterClass _itemMasterCls;   

        public IpBedsideChartClass(IConfiguration configuration,
                                   UnbilledChargesClass unbilledCls,
                                   ItemMasterClass itemMasterCls)      
        {
            _db_conn = configuration.GetConnectionString("conn")!;
            _unbilledCls = unbilledCls;
            _itemMasterCls = itemMasterCls;                            
        }

        private async Task<bool> IpExists(IDbConnection db, Guid ip_id, string tenant_code)
        {
            // Step 1 — does this ip_id exist at all, regardless of tenant_code?
            var row = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT tenant_code FROM ip_registration WHERE ip_id = @ip_id",
                new { ip_id });

            if (row == null)
                return false;   // ip_id genuinely doesn't exist

            // Step 2 — tenant match, trim-tolerant (matches the rest of the codebase's pattern)
            string? dbTenant = (string?)row.tenant_code;
            return string.Equals(dbTenant?.Trim(), tenant_code?.Trim(), StringComparison.OrdinalIgnoreCase);
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
        // ═══════════════════════════════════════════════════════
        // 4. SERVICE
        // Pushes an 'IP_SERVICE' row into unbilledcharges the moment
        // it's saved — rate resolved from test_master via tcode using
        // the shared UnbilledChargesClass.GetTestMasterRate lookup.
        // ═══════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════
        // 4. SERVICE
        // ip_nurse_note_service stores only the clinical note
        // (service_name/action/remarks) — no pricing columns, same
        // as op_registration carries no fee fields. Pricing lives
        // solely in unbilledcharges, resolved from test_master via
        // tcode, pushed as an 'IP_SERVICE' row keyed on service_id —
        // exactly the AddConsultationCharge pattern for OP.
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddService(AddServiceRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();

            if (!await IpExists(db, req.ip_id, tenant_code))
                return "IP Registration not found";

            if (string.IsNullOrWhiteSpace(req.service_name))
                return "service_name is required";

            // Resolve tcode + rate from test_master by name — same helper OP uses
            // for Dressing/Service registration, now shared via UnbilledChargesClass.
            var (tcode, rate) = await _unbilledCls.GetTestMasterRateByName(db, null, req.service_name, tenant_code);
            if (tcode == null)
                return $"Service '{req.service_name}' not configured in test master for this tenant";

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

            // Push into unbilledcharges — only place pricing lives.
            decimal amount = rate * req.quantity;
            await _unbilledCls.AddOtChargeRow(
                db, null, op_id: null, ip_id: req.ip_id, custid: req.custid, tenant_code: tenant_code,
                entryType: "IP_SERVICE",
                entryId: data.service_id.ToString(),
                itemName: data.service_name,
                quantity: req.quantity,
                rate: rate,
                amount: amount);

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

            if (rows == 0) return "Record not found";

            await db.ExecuteAsync(
                @"DELETE FROM unbilledcharges
          WHERE entrytype = 'IP_SERVICE' AND entryid = @entryid AND tenant_code = @tenant_code
          AND (billedstatus = false OR billedstatus IS NULL)",
                new { entryid = service_id.ToString(), tenant_code });

            return "Success";
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

            // ... existing INSERT INTO ip_nurse_note_medicine stays exactly as is ...

            // ── NEW: push to pharmacy queue (best-effort, same as FinalizeCaseSheet) ──
            string pharmacyTag;
            try
            {
                var pharmacyReq = new ReceivePrescriptionRequest
                {
                    custid = req.custid,
                    op_id = null,
                    ip_id = req.ip_id.ToString(),
                    sheet_id = null,
                    // one group per patient per day, so the pharmacist sees one list
                    pr_code = $"IPMED/{DateTime.UtcNow:yyyyMMdd}/{req.ip_id.ToString()[..8]}",
                    items = new List<PrescriptionQueueItem>
            {
                new PrescriptionQueueItem
                {
                    pr_det_id = data.med_id.ToString(),   // links queue row back to the nurse note
                    drug_name = data.medicine_name,
                    qty       = 1,
                    route     = data.route,
                    notes     = string.IsNullOrWhiteSpace(data.remarks)
                                    ? (data.dose.HasValue ? $"Dose: {data.dose} {data.unit}" : null)
                                    : data.remarks
                }
            }
                };

                var pharmRes = await _itemMasterCls.ReceivePrescription(pharmacyReq, tenant_code);
                pharmacyTag = pharmRes.StartsWith("Success", StringComparison.OrdinalIgnoreCase)
                    ? "|Pharmacy:Sent"
                    : $"|Pharmacy:Failed({pharmRes})";
            }
            catch (Exception ex)
            {
                // Nurse note is already saved. Don't block the nurse on a pharmacy hiccup.
                pharmacyTag = $"|Pharmacy:Failed({ex.Message})";
            }

            return $"Success|Id:{data.med_id}{pharmacyTag}";   // was: return $"Success|Id:{data.med_id}";
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
        // ═══════════════════════════════════════════════════════
        // 7. VISITING DOCTOR LIST
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddVisitingDoctor(AddVisitingDoctorRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseVisitingDoctorModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                shift = req.shift,
                doctor_name = req.doctor_name,
                specialization = req.specialization,
                visit_time = req.visit_time,
                diagnosis_notes = req.diagnosis_notes,
                advice = req.advice,
                remarks = req.remarks,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
        INSERT INTO ip_nurse_note_visiting_doctor
        (visit_id, ip_id, custid, entry_date, entry_time, shift, doctor_name, specialization,
         visit_time, diagnosis_notes, advice, remarks, usercode, tenant_code, isdeleted, created_at, updated_at)
        VALUES
        (@visit_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @doctor_name, @specialization,
         @visit_time, @diagnosis_notes, @advice, @remarks, @usercode, @tenant_code, @isdeleted, @created_at, @updated_at)",
                new
                {
                    data.visit_id,
                    data.ip_id,
                    data.custid,
                    entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
                    entry_time = data.entry_time.ToTimeSpan(),
                    data.shift,
                    data.doctor_name,
                    data.specialization,
                    visit_time = data.visit_time.HasValue ? data.visit_time.Value.ToTimeSpan() : (TimeSpan?)null,
                    data.diagnosis_notes,
                    data.advice,
                    data.remarks,
                    data.usercode,
                    data.tenant_code,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

            return $"Success|Id:{data.visit_id}";
        }

        public async Task<string> UpdateVisitingDoctor(UpdateVisitingDoctorRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
        UPDATE ip_nurse_note_visiting_doctor
        SET shift = @shift, doctor_name = @doctor_name, specialization = @specialization,
            visit_time = @visit_time, diagnosis_notes = @diagnosis_notes, advice = @advice,
            remarks = @remarks, updated_at = now()
        WHERE visit_id = @visit_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.visit_id,
                    req.shift,
                    req.doctor_name,
                    req.specialization,
                    visit_time = req.visit_time.HasValue ? req.visit_time.Value.ToTimeSpan() : (TimeSpan?)null,
                    req.diagnosis_notes,
                    req.advice,
                    req.remarks,
                    tenant_code
                });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseVisitingDoctorModel>> GetVisitingDoctor(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseVisitingDoctorModel>(
                @"SELECT * FROM ip_nurse_note_visiting_doctor
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
          ORDER BY entry_date DESC, entry_time DESC",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<NurseVisitingDoctorModel?> GetVisitingDoctorById(Guid ip_id, Guid visit_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseVisitingDoctorModel>(
                @"SELECT * FROM ip_nurse_note_visiting_doctor
          WHERE ip_id = @ip_id AND visit_id = @visit_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, visit_id, tenant_code });
        }

        public async Task<string> DeleteVisitingDoctor(Guid visit_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_visiting_doctor SET isdeleted = true, updated_at = now()
          WHERE visit_id = @visit_id AND tenant_code = @tenant_code",
                new { visit_id, tenant_code });
            return rows > 0 ? "Success" : "Record not found";
        }

        // ═══════════════════════════════════════════════════════
        // 8. SCHEDULE
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddSchedule(AddScheduleRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseScheduleModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                shift = req.shift,
                scheduled_date = req.scheduled_date,
                scheduled_time = req.scheduled_time,
                item_type = req.item_type.ToUpper(),
                item_name = req.item_name,
                instructions = req.instructions,
                status = string.IsNullOrWhiteSpace(req.status) ? "PENDING" : req.status.ToUpper(),
                remarks = req.remarks,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
        INSERT INTO ip_nurse_note_schedule
        (schedule_id, ip_id, custid, entry_date, entry_time, shift, scheduled_date, scheduled_time,
         item_type, item_name, instructions, status, remarks, usercode, tenant_code, isdeleted, created_at, updated_at)
        VALUES
        (@schedule_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @scheduled_date, @scheduled_time,
         @item_type, @item_name, @instructions, @status, @remarks, @usercode, @tenant_code, @isdeleted, @created_at, @updated_at)",
                new
                {
                    data.schedule_id,
                    data.ip_id,
                    data.custid,
                    entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
                    entry_time = data.entry_time.ToTimeSpan(),
                    data.shift,
                    scheduled_date = data.scheduled_date.ToDateTime(TimeOnly.MinValue),
                    scheduled_time = data.scheduled_time.ToTimeSpan(),
                    data.item_type,
                    data.item_name,
                    data.instructions,
                    data.status,
                    data.remarks,
                    data.usercode,
                    data.tenant_code,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

            return $"Success|Id:{data.schedule_id}";
        }

        public async Task<string> UpdateSchedule(UpdateScheduleRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
        UPDATE ip_nurse_note_schedule
        SET shift = @shift, scheduled_date = @scheduled_date, scheduled_time = @scheduled_time,
            item_type = @item_type, item_name = @item_name, instructions = @instructions,
            status = @status, remarks = @remarks, updated_at = now()
        WHERE schedule_id = @schedule_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.schedule_id,
                    req.shift,
                    scheduled_date = req.scheduled_date.ToDateTime(TimeOnly.MinValue),
                    scheduled_time = req.scheduled_time.ToTimeSpan(),
                    item_type = req.item_type.ToUpper(),
                    req.item_name,
                    req.instructions,
                    status = string.IsNullOrWhiteSpace(req.status) ? "PENDING" : req.status.ToUpper(),
                    req.remarks,
                    tenant_code
                });

            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseScheduleModel>> GetSchedule(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseScheduleModel>(
                @"SELECT * FROM ip_nurse_note_schedule
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
          ORDER BY scheduled_date, scheduled_time",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<NurseScheduleModel?> GetScheduleById(Guid ip_id, Guid schedule_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseScheduleModel>(
                @"SELECT * FROM ip_nurse_note_schedule
          WHERE ip_id = @ip_id AND schedule_id = @schedule_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, schedule_id, tenant_code });
        }

        public async Task<string> DeleteSchedule(Guid schedule_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_schedule SET isdeleted = true, updated_at = now()
          WHERE schedule_id = @schedule_id AND tenant_code = @tenant_code",
                new { schedule_id, tenant_code });
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
                other = await GetOther(ip_id, tenant_code),
                visiting_doctor = await GetVisitingDoctor(ip_id, tenant_code),   // NEW
                schedule = await GetSchedule(ip_id, tenant_code)                // NEW
            };
        }
    }
}