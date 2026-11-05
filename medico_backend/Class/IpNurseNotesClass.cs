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
    //
    // entry_date / entry_time: taken from the request when sent,
    // otherwise server time (Add). On Update they only change
    // when sent.
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

        private static bool _nurseMedicineColumnsRan = false;
        private static readonly object _nurseMedicineColumnsLock = new object();

        private void EnsureNurseMedicineColumns()
        {
            if (_nurseMedicineColumnsRan) return;
            lock (_nurseMedicineColumnsLock)
            {
                if (_nurseMedicineColumnsRan) return;
                try
                {
                    using IDbConnection db = new NpgsqlConnection(_db_conn);
                    db.Execute(@"
                        ALTER TABLE ip_nurse_note_medicine
                            ADD COLUMN IF NOT EXISTS sent_to_pharmacy    boolean DEFAULT false,
                            ADD COLUMN IF NOT EXISTS sent_to_pharmacy_at timestamp with time zone,
                            ADD COLUMN IF NOT EXISTS morning            varchar(50) DEFAULT '0',
                            ADD COLUMN IF NOT EXISTS afternoon          varchar(50) DEFAULT '0',
                            ADD COLUMN IF NOT EXISTS evening            varchar(50) DEFAULT '0',
                            ADD COLUMN IF NOT EXISTS night              varchar(50) DEFAULT '0',
                            ADD COLUMN IF NOT EXISTS morning_dose       varchar(50),
                            ADD COLUMN IF NOT EXISTS afternoon_dose     varchar(50),
                            ADD COLUMN IF NOT EXISTS evening_dose       varchar(50),
                            ADD COLUMN IF NOT EXISTS night_dose         varchar(50),
                            ADD COLUMN IF NOT EXISTS dose_unit          varchar(50),
                            ADD COLUMN IF NOT EXISTS before_food        boolean DEFAULT false,
                            ADD COLUMN IF NOT EXISTS after_food         boolean DEFAULT false,
                            ADD COLUMN IF NOT EXISTS days               integer,
                            ADD COLUMN IF NOT EXISTS drug_code          numeric,
                            ADD COLUMN IF NOT EXISTS generic_name       varchar(255),
                            ADD COLUMN IF NOT EXISTS drug_category      varchar(100),
                            ADD COLUMN IF NOT EXISTS rate               numeric(12,2),
                            ADD COLUMN IF NOT EXISTS mrp                numeric(12,2),
                            ADD COLUMN IF NOT EXISTS batch_no           varchar(100),
                            ADD COLUMN IF NOT EXISTS batch_expiry_date  date,
                            ADD COLUMN IF NOT EXISTS stockcode          bigint,
                            ADD COLUMN IF NOT EXISTS warehousecode      bigint,
                            ADD COLUMN IF NOT EXISTS notes              text,
                            ADD COLUMN IF NOT EXISTS pr_code            varchar(100),
                            ADD COLUMN IF NOT EXISTS sno                integer;");
                    _nurseMedicineColumnsRan = true;
                }
                catch { }
            }
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
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),   // NEW
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),   // NEW
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
                SET entry_date = COALESCE(@entry_date::date, entry_date),
                    entry_time = COALESCE(@entry_time::time, entry_time),
                    shift = @shift, temperature = @temperature, unit = @unit,
                    remarks = @remarks, updated_at = now()
                WHERE temp_id = @temp_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.temp_id,
                    entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                    entry_time = req.entry_time?.ToTimeSpan(),
                    req.shift,
                    req.temperature,
                    req.unit,
                    req.remarks,
                    tenant_code
                });

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
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),   // NEW
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),   // NEW
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
                SET entry_date = COALESCE(@entry_date::date, entry_date),
                    entry_time = COALESCE(@entry_time::time, entry_time),
                    shift = @shift, io_type = @io_type, particulars = @particulars,
                    quantity = @quantity, unit = @unit, remarks = @remarks, updated_at = now()
                WHERE io_id = @io_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.io_id,
                    entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                    entry_time = req.entry_time?.ToTimeSpan(),
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

            if (req.custid <= 0)
            {
                var cid = await db.QueryFirstOrDefaultAsync<decimal?>(
                    "SELECT custid FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code LIMIT 1",
                    new { req.ip_id, tenant_code });
                if (cid.HasValue && cid.Value > 0)
                    req.custid = cid.Value;
            }

            // Resolve tcode + rate from test_master by name
            var (tcode, rate) = await _unbilledCls.GetTestMasterRateByName(db, null, req.service_name, tenant_code);
            decimal effectiveRate = (req.rate.HasValue && req.rate.Value > 0) ? req.rate.Value : rate;

            var data = new NurseServiceModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),   // NEW
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),   // NEW
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
            bool alreadyCharged = await db.ExecuteScalarAsync<bool>(
                @"SELECT EXISTS (SELECT 1 FROM unbilledcharges
                                 WHERE (entrytype = 'SERVICE' OR entrytype = 'IP_SERVICE') AND entryid = @entryid
                                   AND tenant_code = @tenant_code)",
                new { entryid = data.service_id.ToString(), tenant_code });

            if (!alreadyCharged)
            {
                decimal qty = req.quantity > 0 ? req.quantity : 1;
                decimal amount = effectiveRate * qty;
                await _unbilledCls.AddOtChargeRow(
                    db, null, op_id: null, ip_id: req.ip_id, custid: req.custid, tenant_code: tenant_code,
                    entryType: "SERVICE",
                    entryId: data.service_id.ToString(),
                    itemName: data.service_name,
                    quantity: qty,
                    rate: effectiveRate,
                    amount: amount,
                    tcode: tcode);
            }

            return $"Success|Id:{data.service_id}";
        }

        // Updates the note AND re-prices its pending IP_SERVICE charge row.
        // The charge row is removed and re-inserted through AddOtChargeRow
        // (same helper AddService uses), so no unbilledcharges column names
        // are written by hand. Already-billed charges are never touched.
        public async Task<string> UpdateService(UpdateServiceRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();

            if (string.IsNullOrWhiteSpace(req.service_name))
                return "service_name is required";

            // 1. Existing nurse note (also gives ip_id / custid for the charge row)
            var note = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT ip_id, custid FROM ip_nurse_note_service
                  WHERE service_id = @service_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { req.service_id, tenant_code });
            if (note == null) return "Record not found";

            // 2. Block if the charge is already billed
            bool isBilled = await db.ExecuteScalarAsync<bool>(
                @"SELECT EXISTS (SELECT 1 FROM unbilledcharges
                                 WHERE (entrytype = 'SERVICE' OR entrytype = 'IP_SERVICE') AND entryid = @entryid
                                 AND tenant_code = @tenant_code AND billedstatus = true)",
                new { entryid = req.service_id.ToString(), tenant_code });
            if (isBilled)
                return "Service is already billed; fee cannot be changed";

            // 3. Re-resolve rate from test_master for the (possibly new) service name
            var (tcode, rate) = await _unbilledCls.GetTestMasterRateByName(db, null, req.service_name, tenant_code);
            decimal qty = (req.quantity.HasValue && req.quantity.Value > 0) ? req.quantity.Value : 1;
            decimal effectiveRate = (req.rate.HasValue && req.rate.Value > 0) ? req.rate.Value : rate;

            using var tx = db.BeginTransaction();
            try
            {
                // 4. Update the clinical note
                await db.ExecuteAsync(@"
                    UPDATE ip_nurse_note_service
                    SET entry_date = COALESCE(@entry_date::date, entry_date),
                        entry_time = COALESCE(@entry_time::time, entry_time),
                        shift = @shift, service_name = @service_name, action = @action,
                        remarks = @remarks, updated_at = now()
                    WHERE service_id = @service_id AND tenant_code = @tenant_code AND isdeleted = false",
                    new
                    {
                        req.service_id,
                        entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                        entry_time = req.entry_time?.ToTimeSpan(),
                        req.shift,
                        req.service_name,
                        req.action,
                        req.remarks,
                        tenant_code
                    }, tx);

                // 5. Fee: remove pending charge row, re-insert via the same helper as AddService
                await db.ExecuteAsync(@"
                    DELETE FROM unbilledcharges
                    WHERE (entrytype = 'SERVICE' OR entrytype = 'IP_SERVICE') AND entryid = @entryid AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                    new { entryid = req.service_id.ToString(), tenant_code }, tx);

                await _unbilledCls.AddOtChargeRow(
                    db, tx, op_id: null, ip_id: (Guid)note.ip_id, custid: (decimal)note.custid, tenant_code: tenant_code,
                    entryType: "SERVICE",
                    entryId: req.service_id.ToString(),
                    itemName: req.service_name,
                    quantity: qty,
                    rate: effectiveRate,
                    amount: effectiveRate * qty,
                    tcode: tcode);

                tx.Commit();
                return "Success";
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        public async Task<List<NurseServiceModel>> GetService(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseServiceModel>(
                @"SELECT s.*,
                         uc.quantity, uc.rate, uc.amount, uc.billedstatus, uc.billno
                  FROM ip_nurse_note_service s
                  LEFT JOIN unbilledcharges uc
                    ON (uc.entrytype = 'SERVICE' OR uc.entrytype = 'IP_SERVICE')
                   AND uc.entryid = s.service_id::text
                   AND uc.tenant_code = s.tenant_code
                  WHERE s.ip_id = @ip_id AND s.tenant_code = @tenant_code AND s.isdeleted = false
                  ORDER BY s.entry_date DESC, s.entry_time DESC",
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
          WHERE (entrytype = 'SERVICE' OR entrytype = 'IP_SERVICE') AND entryid = @entryid AND tenant_code = @tenant_code
          AND (billedstatus = false OR billedstatus IS NULL)",
                new { entryid = service_id.ToString(), tenant_code });

            return "Success";
        }

        // ═══════════════════════════════════════════════════════
        // 5. MEDICINE
        // ═══════════════════════════════════════════════════════
        public async Task<string> AddMedicine(AddMedicineRequest req, int usercode, string tenant_code)
        {
            EnsureNurseMedicineColumns();
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();

            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            if (req.custid <= 0)
            {
                var cid = await db.QueryFirstOrDefaultAsync<decimal?>(
                    "SELECT custid FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code LIMIT 1",
                    new { req.ip_id, tenant_code });
                if (cid.HasValue && cid.Value > 0)
                    req.custid = cid.Value;
            }

            decimal effectiveQty = req.quantity > 0 ? req.quantity : (req.qty.HasValue && req.qty.Value > 0 ? req.qty.Value : 1);

            var data = new NurseMedicineModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),
                shift = string.IsNullOrWhiteSpace(req.shift) ? "MORNING" : req.shift.ToUpper(),
                medicine_name = req.medicine_name,
                dose = req.dose,
                unit = req.unit ?? req.dose_unit,
                route = req.route,
                quantity = effectiveQty,
                status = string.IsNullOrWhiteSpace(req.status) ? "GIVEN" : req.status.ToUpper(),
                remarks = req.remarks ?? req.notes,
                usercode = usercode,
                tenant_code = tenant_code,
                sent_to_pharmacy = false,

                // CaseSheet-style prescription fields
                sno = req.sno,
                pr_code = req.pr_code,
                drug_code = req.drug_code,
                generic_name = req.generic_name,
                drug_category = req.drug_category,
                morning = string.IsNullOrWhiteSpace(req.morning) ? "0" : req.morning.Trim(),
                afternoon = string.IsNullOrWhiteSpace(req.afternoon) ? "0" : req.afternoon.Trim(),
                evening = string.IsNullOrWhiteSpace(req.evening) ? "0" : req.evening.Trim(),
                night = string.IsNullOrWhiteSpace(req.night) ? "0" : req.night.Trim(),
                morning_dose = req.morning_dose,
                afternoon_dose = req.afternoon_dose,
                evening_dose = req.evening_dose,
                night_dose = req.night_dose,
                dose_unit = req.dose_unit ?? req.unit,
                before_food = req.before_food,
                after_food = req.after_food,
                days = req.days,
                rate = req.rate,
                mrp = req.mrp,
                batch_no = string.IsNullOrWhiteSpace(req.batch_no) ? null : req.batch_no.Trim(),
                batch_expiry_date = req.batch_expiry_date,
                stockcode = req.stockcode,
                warehousecode = req.warehousecode,
                notes = req.notes ?? req.remarks
            };

            await db.ExecuteAsync(@"
                INSERT INTO ip_nurse_note_medicine
                (med_id, ip_id, custid, entry_date, entry_time, shift, medicine_name, dose, unit,
                 route, quantity, status, remarks, usercode, tenant_code, isdeleted, sent_to_pharmacy, created_at, updated_at,
                 morning, afternoon, evening, night, morning_dose, afternoon_dose, evening_dose, night_dose,
                 dose_unit, before_food, after_food, days, drug_code, generic_name, drug_category,
                 rate, mrp, batch_no, batch_expiry_date, stockcode, warehousecode, notes, pr_code, sno)
                VALUES
                (@med_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @medicine_name, @dose, @unit,
                 @route, @quantity, @status, @remarks, @usercode, @tenant_code, @isdeleted, @sent_to_pharmacy, @created_at, @updated_at,
                 @morning, @afternoon, @evening, @night, @morning_dose, @afternoon_dose, @evening_dose, @night_dose,
                 @dose_unit, @before_food, @after_food, @days, @drug_code, @generic_name, @drug_category,
                 @rate, @mrp, @batch_no, @batch_expiry_date, @stockcode, @warehousecode, @notes, @pr_code, @sno)",
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
                    data.quantity,
                    data.status,
                    data.remarks,
                    data.usercode,
                    data.tenant_code,
                    data.isdeleted,
                    data.sent_to_pharmacy,
                    data.created_at,
                    data.updated_at,
                    data.morning,
                    data.afternoon,
                    data.evening,
                    data.night,
                    data.morning_dose,
                    data.afternoon_dose,
                    data.evening_dose,
                    data.night_dose,
                    data.dose_unit,
                    data.before_food,
                    data.after_food,
                    data.days,
                    data.drug_code,
                    data.generic_name,
                    data.drug_category,
                    data.rate,
                    data.mrp,
                    data.batch_no,
                    data.batch_expiry_date,
                    data.stockcode,
                    data.warehousecode,
                    data.notes,
                    data.pr_code,
                    data.sno
                });

            // If notes for this admission are already authorized, immediately push to pharmacy queue
            bool isAuthorized = await db.ExecuteScalarAsync<bool>(@"
                SELECT COALESCE(is_authorized, false)
                FROM ip_nurse_note_authorization
                WHERE ip_id = @ip_id AND tenant_code = @tenant_code
                LIMIT 1",
                new { req.ip_id, tenant_code });

            string pharmacyTag = "";
            if (isAuthorized)
            {
                pharmacyTag = await SendMedicinesToPharmacy(db, req.ip_id, tenant_code);
            }

            return $"Success|Id:{data.med_id}" + pharmacyTag;
        }

        // Save multiple prescription items at once in CaseSheet format
        public async Task<string> SaveNursePrescription(SaveNursePrescriptionRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";
            if (req.items == null || !req.items.Any()) return "At least one prescription medicine item is required";

            EnsureNurseMedicineColumns();

            if (req.custid <= 0)
            {
                var cid = await db.QueryFirstOrDefaultAsync<decimal?>(
                    "SELECT custid FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code LIMIT 1",
                    new { req.ip_id, tenant_code });
                if (cid.HasValue && cid.Value > 0)
                    req.custid = cid.Value;
            }

            string prCode = string.IsNullOrWhiteSpace(req.pr_code)
                ? $"IPMED/{DateTime.UtcNow:yyyyMMddHHmmss}/{req.ip_id.ToString()[..8]}"
                : req.pr_code.Trim();

            var entryDate = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var entryTime = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow);

            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                int itemIndex = 1;
                var insertedIds = new List<Guid>();

                foreach (var item in req.items)
                {
                    if (string.IsNullOrWhiteSpace(item.drug_name)) continue;

                    Guid medId = Guid.NewGuid();
                    insertedIds.Add(medId);

                    decimal qty = item.qty.HasValue && item.qty.Value > 0 ? item.qty.Value : 1;

                    await db.ExecuteAsync(@"
                        INSERT INTO ip_nurse_note_medicine
                        (med_id, ip_id, custid, entry_date, entry_time, shift, medicine_name, dose, unit,
                         route, quantity, status, remarks, usercode, tenant_code, isdeleted, sent_to_pharmacy, created_at, updated_at,
                         morning, afternoon, evening, night, morning_dose, afternoon_dose, evening_dose, night_dose,
                         dose_unit, before_food, after_food, days, drug_code, generic_name, drug_category,
                         rate, mrp, batch_no, batch_expiry_date, stockcode, warehousecode, notes, pr_code, sno)
                        VALUES
                        (@med_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @medicine_name, @dose, @unit,
                         @route, @quantity, @status, @remarks, @usercode, @tenant_code, false, false, now(), now(),
                         @morning, @afternoon, @evening, @night, @morning_dose, @afternoon_dose, @evening_dose, @night_dose,
                         @dose_unit, @before_food, @after_food, @days, @drug_code, @generic_name, @drug_category,
                         @rate, @mrp, @batch_no, @batch_expiry_date, @stockcode, @warehousecode, @notes, @pr_code, @sno)",
                        new
                        {
                            med_id = medId,
                            req.ip_id,
                            req.custid,
                            entry_date = entryDate.ToDateTime(TimeOnly.MinValue),
                            entry_time = entryTime.ToTimeSpan(),
                            shift = string.IsNullOrWhiteSpace(req.shift) ? "MORNING" : req.shift.ToUpper(),
                            medicine_name = item.drug_name.Trim(),
                            dose = (decimal?)null,
                            unit = item.dose_unit,
                            route = item.route,
                            quantity = qty,
                            status = "GIVEN",
                            remarks = item.notes ?? req.topremarks,
                            usercode,
                            tenant_code,
                            morning = string.IsNullOrWhiteSpace(item.morning) ? "0" : item.morning.Trim(),
                            afternoon = string.IsNullOrWhiteSpace(item.afternoon) ? "0" : item.afternoon.Trim(),
                            evening = string.IsNullOrWhiteSpace(item.evening) ? "0" : item.evening.Trim(),
                            night = string.IsNullOrWhiteSpace(item.night) ? "0" : item.night.Trim(),
                            item.morning_dose,
                            item.afternoon_dose,
                            item.evening_dose,
                            item.night_dose,
                            item.dose_unit,
                            item.before_food,
                            item.after_food,
                            item.days,
                            item.drug_code,
                            item.generic_name,
                            item.drug_category,
                            item.rate,
                            item.mrp,
                            batch_no = string.IsNullOrWhiteSpace(item.batch_no) ? null : item.batch_no.Trim(),
                            item.batch_expiry_date,
                            item.stockcode,
                            item.warehousecode,
                            notes = item.notes ?? req.bottonremarks,
                            pr_code = prCode,
                            sno = item.sno > 0 ? item.sno : itemIndex++
                        }, tx);
                }

                tx.Commit();

                // If nurse notes for this admission are already authorized, immediately send to pharmacy
                bool isAuthorized = await db.ExecuteScalarAsync<bool>(@"
                    SELECT COALESCE(is_authorized, false)
                    FROM ip_nurse_note_authorization
                    WHERE ip_id = @ip_id AND tenant_code = @tenant_code
                    LIMIT 1",
                    new { req.ip_id, tenant_code });

                string pharmacyTag = "";
                if (isAuthorized)
                {
                    pharmacyTag = await SendMedicinesToPharmacy(db, req.ip_id, tenant_code);
                }

                return $"Success|Inserted:{insertedIds.Count}|PrCode:{prCode}" + pharmacyTag;
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return $"Error: {ex.Message}";
            }
        }

        public async Task<string> UpdateMedicine(UpdateMedicineRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            EnsureNurseMedicineColumns();

            decimal qty = req.quantity > 0 ? req.quantity : (req.qty.HasValue && req.qty.Value > 0 ? req.qty.Value : 1);

            int rows = await db.ExecuteAsync(@"
                UPDATE ip_nurse_note_medicine
                SET entry_date = COALESCE(@entry_date::date, entry_date),
                    entry_time = COALESCE(@entry_time::time, entry_time),
                    shift = COALESCE(@shift, shift),
                    medicine_name = COALESCE(@medicine_name, medicine_name),
                    dose = @dose,
                    unit = COALESCE(@unit, unit),
                    route = COALESCE(@route, route),
                    quantity = @quantity,
                    status = @status,
                    remarks = COALESCE(@remarks, remarks),
                    morning = COALESCE(@morning, morning),
                    afternoon = COALESCE(@afternoon, afternoon),
                    evening = COALESCE(@evening, evening),
                    night = COALESCE(@night, night),
                    morning_dose = COALESCE(@morning_dose, morning_dose),
                    afternoon_dose = COALESCE(@afternoon_dose, afternoon_dose),
                    evening_dose = COALESCE(@evening_dose, evening_dose),
                    night_dose = COALESCE(@night_dose, night_dose),
                    dose_unit = COALESCE(@dose_unit, dose_unit),
                    before_food = COALESCE(@before_food, before_food),
                    after_food = COALESCE(@after_food, after_food),
                    days = COALESCE(@days, days),
                    drug_code = COALESCE(@drug_code, drug_code),
                    generic_name = COALESCE(@generic_name, generic_name),
                    drug_category = COALESCE(@drug_category, drug_category),
                    rate = COALESCE(@rate, rate),
                    mrp = COALESCE(@mrp, mrp),
                    batch_no = COALESCE(@batch_no, batch_no),
                    batch_expiry_date = COALESCE(@batch_expiry_date, batch_expiry_date),
                    stockcode = COALESCE(@stockcode, stockcode),
                    warehousecode = COALESCE(@warehousecode, warehousecode),
                    notes = COALESCE(@notes, notes),
                    pr_code = COALESCE(@pr_code, pr_code),
                    sno = COALESCE(@sno, sno),
                    updated_at = now()
                WHERE med_id = @med_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.med_id,
                    entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                    entry_time = req.entry_time?.ToTimeSpan(),
                    req.shift,
                    req.medicine_name,
                    req.dose,
                    unit = req.unit ?? req.dose_unit,
                    req.route,
                    quantity = qty,
                    status = string.IsNullOrWhiteSpace(req.status) ? "GIVEN" : req.status.ToUpper(),
                    remarks = req.remarks ?? req.notes,
                    req.morning,
                    req.afternoon,
                    req.evening,
                    req.night,
                    req.morning_dose,
                    req.afternoon_dose,
                    req.evening_dose,
                    req.night_dose,
                    dose_unit = req.dose_unit ?? req.unit,
                    req.before_food,
                    req.after_food,
                    req.days,
                    req.drug_code,
                    req.generic_name,
                    req.drug_category,
                    req.rate,
                    req.mrp,
                    batch_no = string.IsNullOrWhiteSpace(req.batch_no) ? null : req.batch_no.Trim(),
                    req.batch_expiry_date,
                    req.stockcode,
                    req.warehousecode,
                    notes = req.notes ?? req.remarks,
                    req.pr_code,
                    req.sno,
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
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),   // NEW
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),   // NEW
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
                SET entry_date = COALESCE(@entry_date::date, entry_date),
                    entry_time = COALESCE(@entry_time::time, entry_time),
                    shift = @shift, note = @note, updated_at = now()
                WHERE other_id = @other_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.other_id,
                    entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                    entry_time = req.entry_time?.ToTimeSpan(),
                    req.shift,
                    req.note,
                    tenant_code
                });

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
                @"SELECT s.*,
                         uc.quantity, uc.rate, uc.amount, uc.billedstatus, uc.billno
                  FROM ip_nurse_note_service s
                  LEFT JOIN unbilledcharges uc
                    ON (uc.entrytype = 'SERVICE' OR uc.entrytype = 'IP_SERVICE')
                   AND uc.entryid = s.service_id::text
                   AND uc.tenant_code = s.tenant_code
                  WHERE s.ip_id = @ip_id AND s.service_id = @service_id AND s.tenant_code = @tenant_code AND s.isdeleted = false",
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
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),   // NEW
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),   // NEW
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
        SET entry_date = COALESCE(@entry_date::date, entry_date),
            entry_time = COALESCE(@entry_time::time, entry_time),
            shift = @shift, doctor_name = @doctor_name, specialization = @specialization,
            visit_time = @visit_time, diagnosis_notes = @diagnosis_notes, advice = @advice,
            remarks = @remarks, updated_at = now()
        WHERE visit_id = @visit_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.visit_id,
                    entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                    entry_time = req.entry_time?.ToTimeSpan(),
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
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),   // NEW
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),   // NEW
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
        SET entry_date = COALESCE(@entry_date::date, entry_date),
            entry_time = COALESCE(@entry_time::time, entry_time),
            shift = @shift, scheduled_date = @scheduled_date, scheduled_time = @scheduled_time,
            item_type = @item_type, item_name = @item_name, instructions = @instructions,
            status = @status, remarks = @remarks, updated_at = now()
        WHERE schedule_id = @schedule_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.schedule_id,
                    entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                    entry_time = req.entry_time?.ToTimeSpan(),
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
        // ═══════════════════════════════════════════════════════
        // 9. AUTHORIZATION
        // First call  -> inserts row (authorized_by / authorized_at set once)
        // Later calls -> keeps original authorizer, stamps updated_by / updated_at
        // ═══════════════════════════════════════════════════════
        public async Task<string> AuthorizeNurseNotes(AuthorizeNurseNotesRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            // xmax = 0 is true only when the row was freshly inserted (not updated by ON CONFLICT)
            bool inserted = await db.ExecuteScalarAsync<bool>(@"
        INSERT INTO ip_nurse_note_authorization
        (auth_id, ip_id, custid, is_authorized, authorized_by, authorized_at, tenant_code)
        VALUES
        (@auth_id, @ip_id, @custid, true, @usercode, now(), @tenant_code)
        ON CONFLICT (ip_id, tenant_code)
        DO UPDATE SET
            is_authorized = true,
            updated_by    = @usercode,
            updated_at    = now()
        RETURNING (xmax = 0);",
                new
                {
                    auth_id = Guid.NewGuid(),
                    req.ip_id,
                    req.custid,
                    usercode,
                    tenant_code
                });

            string pharmacyTag = await SendMedicinesToPharmacy(db, req.ip_id, tenant_code);

            return (inserted ? "Success|Authorized" : "Success|AuthorizationUpdated") + pharmacyTag;

        }

        public async Task<NurseNoteAuthorizationModel?> GetNurseNoteAuthorization(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseNoteAuthorizationModel>(
                @"SELECT * FROM ip_nurse_note_authorization
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                new { ip_id, tenant_code });
        }
        private async Task<string> SendMedicinesToPharmacy(IDbConnection db, Guid ip_id, string tenant_code)
        {
            EnsureNurseMedicineColumns();
            if (db.State != ConnectionState.Open) db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Atomically claim all not-yet-sent rows for this admission
                var meds = (await db.QueryAsync<NurseMedicineModel>(@"
                    UPDATE ip_nurse_note_medicine
                    SET sent_to_pharmacy = true,
                        sent_to_pharmacy_at = now(),
                        updated_at = now()
                    WHERE ip_id = @ip_id
                      AND tenant_code = @tenant_code
                      AND isdeleted = false
                      AND (sent_to_pharmacy = false OR sent_to_pharmacy IS NULL)
                    RETURNING *",
                    new { ip_id, tenant_code }, tx)).ToList();

                if (meds.Count == 0)
                {
                    tx.Commit();
                    return "|Pharmacy:NothingToSend";
                }

                decimal resolvedCustId = meds[0].custid;
                if (resolvedCustId <= 0)
                {
                    var cid = await db.QueryFirstOrDefaultAsync<decimal?>(
                        "SELECT custid FROM ip_registration WHERE ip_id = @ip_id AND tenant_code = @tenant_code LIMIT 1",
                        new { ip_id, tenant_code }, tx);
                    if (cid.HasValue && cid.Value > 0)
                        resolvedCustId = cid.Value;
                }

                // If rows share a pr_code, use it; otherwise generate a clean unique pr_code
                string batchPrCode = meds.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.pr_code))?.pr_code
                    ?? $"IPMED/{DateTime.UtcNow:yyyyMMddHHmmss}/{ip_id.ToString()[..8]}";

                var pharmacyReq = new ReceivePrescriptionRequest
                {
                    custid = resolvedCustId,
                    op_id = null,
                    ip_id = ip_id.ToString(),
                    sheet_id = null,
                    pr_code = batchPrCode,
                    items = meds.Select(m => new PrescriptionQueueItem
                    {
                        pr_det_id = m.med_id.ToString(),
                        drug_name = m.medicine_name,
                        qty = m.quantity > 0 ? m.quantity : 1,
                        morning = m.morning,
                        afternoon = m.afternoon,
                        evening = m.evening,
                        night = m.night,
                        morning_dose = m.morning_dose,
                        afternoon_dose = m.afternoon_dose,
                        evening_dose = m.evening_dose,
                        night_dose = m.night_dose,
                        dose_unit = m.dose_unit ?? m.unit,
                        before_food = m.before_food,
                        after_food = m.after_food,
                        days = m.days,
                        route = m.route,
                        notes = string.IsNullOrWhiteSpace(m.remarks)
                                    ? (m.dose.HasValue ? $"Dose: {m.dose} {m.unit}" : m.notes)
                                    : m.remarks
                    }).ToList()
                };

                var pharmRes = await _itemMasterCls.ReceivePrescription(pharmacyReq, tenant_code);
                if (!pharmRes.StartsWith("Success", StringComparison.OrdinalIgnoreCase))
                {
                    tx.Rollback();   // flags revert -> will be retried on next authorize or add
                    return $"|Pharmacy:Failed({pharmRes})";
                }

                tx.Commit();
                return $"|Pharmacy:Sent({meds.Count})";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return $"|Pharmacy:Failed({ex.Message})";
            }
        }
        // ═══ 10. DIABETIC CHART ═══
        public async Task<string> AddDiabetic(AddDiabeticRequest req, int usercode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            if (!await IpExists(db, req.ip_id, tenant_code)) return "IP Registration not found";

            var data = new NurseDiabeticModel
            {
                ip_id = req.ip_id,
                custid = req.custid,
                entry_date = req.entry_date ?? DateOnly.FromDateTime(DateTime.UtcNow),   // NEW
                entry_time = req.entry_time ?? TimeOnly.FromDateTime(DateTime.UtcNow),   // NEW
                shift = req.shift,
                reading_type = req.reading_type.ToUpper(),
                blood_sugar = req.blood_sugar,
                insulin_type = req.insulin_type,
                insulin_dose = req.insulin_dose,
                insulin_unit = req.insulin_unit,
                site = req.site,
                remarks = req.remarks,
                usercode = usercode,
                tenant_code = tenant_code
            };

            await db.ExecuteAsync(@"
        INSERT INTO ip_nurse_note_diabetic
        (diabetic_id, ip_id, custid, entry_date, entry_time, shift, reading_type, blood_sugar,
         insulin_type, insulin_dose, insulin_unit, site, remarks, usercode, tenant_code,
         isdeleted, created_at, updated_at)
        VALUES
        (@diabetic_id, @ip_id, @custid, @entry_date, @entry_time, @shift, @reading_type, @blood_sugar,
         @insulin_type, @insulin_dose, @insulin_unit, @site, @remarks, @usercode, @tenant_code,
         @isdeleted, @created_at, @updated_at)",
                new
                {
                    data.diabetic_id,
                    data.ip_id,
                    data.custid,
                    entry_date = data.entry_date.ToDateTime(TimeOnly.MinValue),
                    entry_time = data.entry_time.ToTimeSpan(),
                    data.shift,
                    data.reading_type,
                    data.blood_sugar,
                    data.insulin_type,
                    data.insulin_dose,
                    data.insulin_unit,
                    data.site,
                    data.remarks,
                    data.usercode,
                    data.tenant_code,
                    data.isdeleted,
                    data.created_at,
                    data.updated_at
                });

            return $"Success|Id:{data.diabetic_id}";
        }

        public async Task<string> UpdateDiabetic(UpdateDiabeticRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(@"
        UPDATE ip_nurse_note_diabetic
        SET entry_date = COALESCE(@entry_date::date, entry_date),
            entry_time = COALESCE(@entry_time::time, entry_time),
            shift = @shift, reading_type = @reading_type, blood_sugar = @blood_sugar,
            insulin_type = @insulin_type, insulin_dose = @insulin_dose, insulin_unit = @insulin_unit,
            site = @site, remarks = @remarks, updated_at = now()
        WHERE diabetic_id = @diabetic_id AND tenant_code = @tenant_code AND isdeleted = false",
                new
                {
                    req.diabetic_id,
                    entry_date = req.entry_date?.ToDateTime(TimeOnly.MinValue),
                    entry_time = req.entry_time?.ToTimeSpan(),
                    req.shift,
                    reading_type = req.reading_type.ToUpper(),
                    req.blood_sugar,
                    req.insulin_type,
                    req.insulin_dose,
                    req.insulin_unit,
                    req.site,
                    req.remarks,
                    tenant_code
                });
            return rows > 0 ? "Success" : "Record not found";
        }

        public async Task<List<NurseDiabeticModel>> GetDiabetic(Guid ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            var res = await db.QueryAsync<NurseDiabeticModel>(
                @"SELECT * FROM ip_nurse_note_diabetic
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code AND isdeleted = false
          ORDER BY entry_date DESC, entry_time DESC",
                new { ip_id, tenant_code });
            return res.ToList();
        }

        public async Task<NurseDiabeticModel?> GetDiabeticById(Guid ip_id, Guid diabetic_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            return await db.QueryFirstOrDefaultAsync<NurseDiabeticModel>(
                @"SELECT * FROM ip_nurse_note_diabetic
          WHERE ip_id = @ip_id AND diabetic_id = @diabetic_id AND tenant_code = @tenant_code AND isdeleted = false",
                new { ip_id, diabetic_id, tenant_code });
        }

        public async Task<string> DeleteDiabetic(Guid diabetic_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            int rows = await db.ExecuteAsync(
                @"UPDATE ip_nurse_note_diabetic SET isdeleted = true, updated_at = now()
          WHERE diabetic_id = @diabetic_id AND tenant_code = @tenant_code",
                new { diabetic_id, tenant_code });
            return rows > 0 ? "Success" : "Record not found";
        }
    }
}
