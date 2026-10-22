using Amazon.S3.Model;
using Dapper;
using medico_backend.InventoryClass;
using medico_backend.InventoryModel;
using medico_backend.Model;
using Npgsql;
using System.Data;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

namespace medico_backend.Class
{
    public class NewOPCaseSheetClass
    {
        private readonly string _db_conn;
        private readonly UnbilledChargesClass _unbilledCls;
        private readonly ItemMasterClass _itemMasterCls;

        public NewOPCaseSheetClass(IConfiguration configuration, UnbilledChargesClass unbilledCls, ItemMasterClass itemMasterCls)
        {
            _db_conn = configuration.GetConnectionString("conn")!;
            _unbilledCls = unbilledCls;
            _itemMasterCls = itemMasterCls;
        }

        // ─────────────────────────────────────────────────────────
        // GENERATE PRESCRIPTION CODE
        // Format: PR/2026/06/0001 — resets each month per tenant
        // Locked via pg_advisory_xact_lock so concurrent saves can't
        // read the same MAX(...) before either commits.
        // ─────────────────────────────────────────────────────────
        private async Task<string> GeneratePrCode(IDbConnection db, IDbTransaction tx, string tenant_code)
        {
            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            await db.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtext(@lockKey))",
                new { lockKey = $"prcode:{tenant_code}:{year}:{month}" }, tx);

            string sql = @"SELECT COALESCE(MAX(
                       CAST(SPLIT_PART(pr_code, '/', 4) AS INT)
                   ), 0) + 1
                   FROM op_prescription_master
                   WHERE tenant_code = @tenant_code
                   AND   SPLIT_PART(pr_code, '/', 2) = @year   -- isdeleted filter removed:
                   AND   SPLIT_PART(pr_code, '/', 3) = @month";

            int next = await db.ExecuteScalarAsync<int>(sql, new { tenant_code, year, month }, tx);
            return $"PR/{year}/{month}/{next:D4}";
        }

        // ─────────────────────────────────────────────────────────
        // GENERATE INVESTIGATION CODE
        // Format: INV/2026/06/0001
        // ─────────────────────────────────────────────────────────
        private async Task<string> GenerateInvCode(IDbConnection db, IDbTransaction tx, string tenant_code)
        {
            var now = DateTime.UtcNow;
            string year = now.Year.ToString();
            string month = now.Month.ToString("D2");

            await db.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtext(@lockKey))",
                new { lockKey = $"invcode:{tenant_code}:{year}:{month}" }, tx);

            string sql = @"SELECT COALESCE(MAX(
                               CAST(SPLIT_PART(inv_code, '/', 4) AS INT)
                           ), 0) + 1
                           FROM op_investigation_master
                           WHERE tenant_code = @tenant_code
                           AND   isdeleted   = false
                           AND   SPLIT_PART(inv_code, '/', 2) = @year
                           AND   SPLIT_PART(inv_code, '/', 3) = @month";

            int next = await db.ExecuteScalarAsync<int>(sql, new { tenant_code, year, month }, tx);
            return $"INV/{year}/{month}/{next:D4}";
        }
        private sealed class ExistingInvRow
        {
            public Guid inv_det_id { get; set; }
            public int? test_code { get; set; }
            public string? test_name { get; set; }
            public bool? is_billed { get; set; }
        }
        // ─────────────────────────────────────────────────────────
        // SAVE OP/IP CASE SHEET
        // Accepts EITHER op_id OR ip_id (or both). Entire operation is
        // wrapped in a single transaction.
        // ─────────────────────────────────────────────────────────
        public async Task<string> SaveCaseSheet(SaveCaseSheetRequest req, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                bool hasOp = !string.IsNullOrWhiteSpace(req.op_id);
                bool hasIp = !string.IsNullOrWhiteSpace(req.ip_id);

                if (!hasOp && !hasIp)
                    return "Either op_id or ip_id is required";

                // ── 1. Validate whichever visit type was supplied ──────
                if (hasOp)
                {
                    var opExists = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(1) FROM op_registration
                          WHERE  op_id       = CAST(@op_id AS uuid)
                          AND    tenant_code = @tenant_code
                          AND    isdeleted   = false",
                        new { req.op_id, tenant_code }, tx);

                    if (opExists == 0)
                        return "OP Registration not found";
                }

                if (hasIp)
                {
                    var ipExists = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(1) FROM ip_registration
                          WHERE  ip_id       = CAST(@ip_id AS uuid)
                          AND    tenant_code = @tenant_code",
                        new { req.ip_id, tenant_code }, tx);

                    if (ipExists == 0)
                        return "IP Registration not found";
                }

                string? finalOpId = hasOp ? req.op_id : null;
                string? finalIpId = hasIp ? req.ip_id : null;

                // ── 2. Upsert op_case_sheet ───────────────────────
                bool isNew = string.IsNullOrWhiteSpace(req.sheet_id);
                Guid sheetId = isNew ? Guid.NewGuid() : Guid.Parse(req.sheet_id!);

                if (isNew)
                {
                    await db.ExecuteAsync(
    @"INSERT INTO op_case_sheet
      (sheet_id, op_id, ip_id, custid, dcode, visit_date,
       chief_complaint, symptoms, examination,
       advise, notes, followup_date, followup_notes,
       is_consulted, refer_to_ip, sheet_status,
       referred_to_visiting_doctor, referred_to_operation, 
       tenant_code, isdeleted, created_at, updated_at)
      VALUES
      (@sheet_id, CAST(@op_id AS uuid), CAST(@ip_id AS uuid), @custid, @dcode, NOW()::date,
       @chief_complaint, @symptoms, @examination,
       @advise, @notes, @followup_date, @followup_notes,
       false, @refer_to_ip, @sheet_status,
       @referred_to_visiting_doctor,@referred_to_operation,
       @tenant_code, false, NOW(), NOW())",
    new
    {
        sheet_id = sheetId,
        op_id = finalOpId,
        ip_id = finalIpId,
        req.custid,
        req.dcode,
        req.chief_complaint,
        req.symptoms,
        req.examination,
        req.advise,
        req.notes,
        req.followup_date,
        req.followup_notes,
        req.refer_to_ip,          // <-- new
        req.sheet_status,
        req.referred_to_visiting_doctor,   // NEW
        req.referred_to_operation,
        tenant_code
    }, tx);
                }
                else
                {
                    var exists = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(1) FROM op_case_sheet
                          WHERE  sheet_id    = @sheet_id
                          AND    tenant_code = @tenant_code
                          AND    isdeleted   = false",
                        new { sheet_id = sheetId, tenant_code }, tx);

                    if (exists == 0) return "Case sheet not found";

                    await db.ExecuteAsync(
    @"UPDATE op_case_sheet
      SET    chief_complaint = @chief_complaint,
             symptoms        = @symptoms,
             examination     = @examination,
             advise          = @advise,
             notes           = @notes,
             followup_date   = @followup_date,
             followup_notes  = @followup_notes,
             sheet_status    = @sheet_status,
             refer_to_ip     = @refer_to_ip,
             referred_to_visiting_doctor = @referred_to_visiting_doctor,  -- NEW
             referred_to_operation       = @referred_to_operation,
             updated_at      = NOW()
             WHERE  sheet_id    = @sheet_id
      AND    tenant_code = @tenant_code",
    new
    {
        sheet_id = sheetId,
        req.chief_complaint,
        req.symptoms,
        req.examination,
        req.advise,
        req.notes,
        req.followup_date,
        req.followup_notes,
        req.sheet_status,
        req.refer_to_ip,          // <-- new
        req.referred_to_visiting_doctor,   // NEW
        req.referred_to_operation,
        tenant_code
    }, tx);
                }
                // ── 3b. Visiting doctors list (delete + re-insert) ────────
                await db.ExecuteAsync(
                    "DELETE FROM op_case_sheet_visiting_doctors WHERE sheet_id = @sheet_id",
                    new { sheet_id = sheetId }, tx);

                foreach (var vd in req.visiting_doctor_list)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO op_case_sheet_visiting_doctors
          (visiting_doc_id, sheet_id, op_id, ip_id, custid, sno,
           doctor_name, dcode, notes, tenant_code, created_at)
          VALUES
          (gen_random_uuid(), @sheet_id, CAST(@op_id AS uuid), CAST(@ip_id AS uuid), @custid, @sno,
           @doctor_name, @dcode, @notes, @tenant_code, NOW())",
                        new
                        {
                            sheet_id = sheetId,
                            op_id = finalOpId,
                            ip_id = finalIpId,
                            req.custid,
                            vd.sno,
                            vd.doctor_name,
                            vd.dcode,
                            vd.notes,
                            tenant_code
                        }, tx);
                }

                // ── 3. Symptoms list (delete + re-insert) ─────────
                await db.ExecuteAsync(
                    "DELETE FROM op_case_sheet_symptoms WHERE sheet_id = @sheet_id",
                    new { sheet_id = sheetId }, tx);

                foreach (var s in req.symptom_list)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO op_case_sheet_symptoms
                          (symptom_id, sheet_id, op_id, ip_id, custid, sno,
                           symptom_text, duration, severity, notes, tenant_code, created_at)
                          VALUES
                          (gen_random_uuid(), @sheet_id, CAST(@op_id AS uuid), CAST(@ip_id AS uuid), @custid, @sno,
                           @symptom_text, @duration, @severity, @snotes, @tenant_code, NOW())",
                        new
                        {
                            sheet_id = sheetId,
                            op_id = finalOpId,
                            ip_id = finalIpId,
                            req.custid,
                            s.sno,
                            s.symptom_text,
                            s.duration,
                            s.severity,
                            snotes = s.notes,
                            tenant_code
                        }, tx);
                }

                // ── 4. Diagnosis list (delete + re-insert) ────────
                await db.ExecuteAsync(
                    "DELETE FROM op_case_sheet_diagnosis WHERE sheet_id = @sheet_id",
                    new { sheet_id = sheetId }, tx);

                foreach (var d in req.diagnosis_list)
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO op_case_sheet_diagnosis
                          (diag_id, sheet_id, op_id, ip_id, custid, dcode, visit_date, sno,
                           icd_code, icd_description, diagnosis_text,
                           diagnosis_type, condition_type, severity, status,
                           tenant_code, isdeleted, created_at)
                          VALUES
                          (gen_random_uuid(), @sheet_id, CAST(@op_id AS uuid), CAST(@ip_id AS uuid),
                           @custid, @dcode, NOW()::date, @sno,
                           @icd_code, @icd_description, @diagnosis_text,
                           @diagnosis_type, @condition_type, @severity, @status,
                           @tenant_code, false, NOW())",
                        new
                        {
                            sheet_id = sheetId,
                            op_id = finalOpId,
                            ip_id = finalIpId,
                            req.custid,
                            req.dcode,
                            d.sno,
                            d.icd_code,
                            d.icd_description,
                            d.diagnosis_text,
                            d.diagnosis_type,
                            d.condition_type,
                            d.severity,
                            d.status,
                            tenant_code
                        }, tx);
                }

                // ── 5. Prescription ──────────────────────────────
                string? prCode = null;
                if (req.prescription != null && req.prescription.items.Any())
                {
                    prCode = await SavePrescription(
                        db, tx, req.prescription, sheetId,
                        finalOpId, finalIpId,
                        req.custid, req.dcode, tenant_code);
                }

                // ── 6. Investigation ──────────────────────────────
                string? invCode = null;
                if (req.investigation != null && req.investigation.tests.Any())
                {
                    invCode = await SaveInvestigation(
                        db, tx, req.investigation, sheetId,
                        finalOpId, finalIpId,
                        req.custid, req.dcode, tenant_code);
                }

                tx.Commit();

                return $"Success" +
                       $"|SheetId:{sheetId}" +
                       $"|PrCode:{prCode ?? "none"}" +
                       $"|InvCode:{invCode ?? "none"}";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { /* already rolled back / disposed */ }
                return ex.Message;
            }
        }

        private async Task<string> SavePrescription(
    IDbConnection db,
    IDbTransaction tx,
    CaseSheetPrescriptionRequest req,
    Guid sheetId,
    string? op_id,
    string? ip_id,
    decimal custid,
    int dcode,
    string tenant_code)
        {
            bool isNew = string.IsNullOrWhiteSpace(req.pr_code);
            string prCode = isNew
                ? await GeneratePrCode(db, tx, tenant_code)
                : req.pr_code!;

            Guid prId;

            if (isNew)
            {
                prId = Guid.NewGuid();
                await db.ExecuteAsync(
                    @"INSERT INTO op_prescription_master
              (pr_id, pr_code, sheet_id, op_id, ip_id, custid, dcode, visit_date,
               pr_date, topremarks, bottonremarks,
               is_dispensed, tenant_code, isdeleted, created_at, updated_at)
              VALUES
              (@pr_id, @pr_code, @sheet_id, CAST(@op_id AS uuid), CAST(@ip_id AS uuid),
               @custid, @dcode, NOW()::date,
               NOW(), @topremarks, @bottonremarks,
               false, @tenant_code, false, NOW(), NOW())",
                    new { pr_id = prId, pr_code = prCode, sheet_id = sheetId, op_id, ip_id, custid, dcode, req.topremarks, req.bottonremarks, tenant_code }, tx);
            }
            else
            {
                var mst = await db.QueryFirstOrDefaultAsync(
                    @"SELECT pr_id FROM op_prescription_master
              WHERE  pr_code     = @pr_code
              AND    tenant_code = @tenant_code
              AND    isdeleted   = false",
                    new { pr_code = prCode, tenant_code }, tx);

                if (mst == null) throw new Exception("Prescription not found for update");
                prId = (Guid)mst.pr_id;

                await db.ExecuteAsync(
                    @"UPDATE op_prescription_master
              SET    topremarks    = @topremarks,
                     bottonremarks = @bottonremarks,
                     updated_at    = NOW()
              WHERE  pr_code       = @pr_code
              AND    tenant_code   = @tenant_code",
                    new { pr_code = prCode, req.topremarks, req.bottonremarks, tenant_code }, tx);
            }

            // Delete + re-insert detail lines
            await db.ExecuteAsync(
                "DELETE FROM op_prescription_detail WHERE pr_id = @pr_id",
                new { pr_id = prId }, tx);

            foreach (var item in req.items)
            {
                Guid? diagId = string.IsNullOrWhiteSpace(item.diag_id)
                    ? null
                    : Guid.Parse(item.diag_id);

                Guid prDetId = Guid.NewGuid();   // generated here so we can link unbilledcharges

                await db.ExecuteAsync(
                    @"INSERT INTO op_prescription_detail
              (pr_det_id, pr_id, pr_code, diag_id, sno,
               drug_name, drug_code, generic_name, drug_category,
               morning, afternoon, evening, night,
               morning_dose, afternoon_dose, evening_dose, night_dose, dose_unit,
               before_food, after_food, days, qty, route,
               rate, mrp, is_billed, notes,
               tenant_code, isdeleted, created_at)
              VALUES
              (@pr_det_id, @pr_id, @pr_code, @diag_id, @sno,
               @drug_name, @drug_code, @generic_name, @drug_category,
               @morning, @afternoon, @evening, @night,
               @morning_dose, @afternoon_dose, @evening_dose, @night_dose, @dose_unit,
               @before_food, @after_food, @days, @qty, @route,
               @rate, @mrp, false, @notes,
               @tenant_code, false, NOW())",
                    new
                    {
                        pr_det_id = prDetId,
                        pr_id = prId,
                        pr_code = prCode,
                        diag_id = diagId,
                        item.sno,
                        item.drug_name,
                        item.drug_code,
                        item.generic_name,
                        item.drug_category,
                        item.morning,
                        item.afternoon,
                        item.evening,
                        item.night,
                        item.morning_dose,      
                        item.afternoon_dose,    
                        item.evening_dose,      
                        item.night_dose,        
                        item.dose_unit,
                        item.before_food,
                        item.after_food,
                        item.days,
                        item.qty,
                        item.route,
                        item.rate,
                        item.mrp,
                        item.notes,
                        tenant_code
                    }, tx);


            }

            return prCode;
        }

        // ─────────────────────────────────────────────────────────
        // SAVE INVESTIGATION
        // ─────────────────────────────────────────────────────────


        private async Task<string> SaveInvestigation(
            IDbConnection db,
            IDbTransaction tx,
            CaseSheetInvestigationRequest req,
            Guid sheetId,
            string? op_id,
            string? ip_id,
            decimal custid,
            int dcode,
            string tenant_code)
        {
            bool isNew = string.IsNullOrWhiteSpace(req.inv_id);
            Guid invId;
            string invCode;

            if (isNew)
            {
                invId = Guid.NewGuid();
                invCode = await GenerateInvCode(db, tx, tenant_code);

                await db.ExecuteAsync(
                    @"INSERT INTO op_investigation_master
                      (inv_id, inv_code, sheet_id, op_id, ip_id, custid, dcode, visit_date,
                       inv_date, notes, is_urgent, status,
                       tenant_code, isdeleted, created_at, updated_at)
                      VALUES
                      (@inv_id, @inv_code, @sheet_id, CAST(@op_id AS uuid), CAST(@ip_id AS uuid),
                       @custid, @dcode, NOW()::date,
                       NOW(), @notes, @is_urgent, 'ORDERED',
                       @tenant_code, false, NOW(), NOW())",
                    new
                    {
                        inv_id = invId,
                        inv_code = invCode,
                        sheet_id = sheetId,
                        op_id,
                        ip_id,
                        custid,
                        dcode,
                        req.notes,
                        is_urgent = req.is_urgent,
                        tenant_code
                    }, tx);
            }
            else
            {
                invId = Guid.Parse(req.inv_id!);
                var mst = await db.QueryFirstOrDefaultAsync(
                    @"SELECT inv_code FROM op_investigation_master
                      WHERE  inv_id      = @inv_id
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { inv_id = invId, tenant_code }, tx);

                if (mst == null) throw new Exception("Investigation not found for update");
                invCode = (string)mst.inv_code;

                await db.ExecuteAsync(
                    @"UPDATE op_investigation_master
                      SET    notes      = @notes,
                             is_urgent  = @is_urgent,
                             updated_at = NOW()
                      WHERE  inv_id      = @inv_id
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { inv_id = invId, req.notes, is_urgent = req.is_urgent, tenant_code }, tx);
            }

            // 1. Clear only PENDING unbilled investigation rows (billed rows untouched)
            await db.ExecuteAsync(
                @"DELETE FROM unbilledcharges
      WHERE entrytype = 'INVESTIGATION'
      AND (
            (@op_id IS NOT NULL AND opvisitid = @op_id)
         OR (@ip_id IS NOT NULL AND ip_id = CAST(@ip_id AS uuid))
          )
      AND tenant_code = @tenant_code
      AND (billedstatus = false OR billedstatus IS NULL)",
                new { op_id, ip_id, tenant_code }, tx);

            // 2. Load existing tests + find which are already billed
            var existingRows = (await db.QueryAsync<ExistingInvRow>(
                @"SELECT inv_det_id, test_code, test_name, is_billed
      FROM op_investigation_detail
      WHERE inv_id = @inv_id AND isdeleted = false",
                new { inv_id = invId }, tx)).ToList();

            var existingIds = existingRows.Select(r => r.inv_det_id.ToString()).ToArray();

            var billedSet = existingIds.Length == 0
                ? new HashSet<string>()
                : (await db.QueryAsync<string>(
                    @"SELECT entryid FROM unbilledcharges
          WHERE entrytype = 'INVESTIGATION'
          AND   billedstatus = true
          AND   tenant_code = @tenant_code
          AND   entryid = ANY(@ids)",
                    new { ids = existingIds, tenant_code }, tx)).ToHashSet();

            var matched = new HashSet<Guid>();

            foreach (var test in req.tests)
            {
                Guid? diagId = string.IsNullOrWhiteSpace(test.diag_id) ? null : Guid.Parse(test.diag_id);

                // match an existing row (same test_code, or same name when no code)
                var match = existingRows.FirstOrDefault(r =>
                    !matched.Contains(r.inv_det_id) &&
                    (test.test_code.HasValue
                        ? r.test_code == test.test_code
                        : string.Equals(r.test_name, test.test_name, StringComparison.OrdinalIgnoreCase)));

                if (match != null)
                {
                    matched.Add(match.inv_det_id);
                    bool alreadyBilled = match.is_billed == true || billedSet.Contains(match.inv_det_id.ToString());

                    if (alreadyBilled)
                    {
                        // billed → only touch sno/diag, never re-push to unbilling
                        await db.ExecuteAsync(
                            @"UPDATE op_investigation_detail
                  SET sno = @sno, diag_id = @diag_id, updated_at = NOW()
                  WHERE inv_det_id = @inv_det_id",
                            new { inv_det_id = match.inv_det_id, test.sno, diag_id = diagId }, tx);
                        continue;
                    }

                    // existing but still unbilled → update, keep same id, re-push (pending row was cleared above)
                    await db.ExecuteAsync(
                        @"UPDATE op_investigation_detail
              SET sno=@sno, diag_id=@diag_id, test_name=@test_name, test_code=@test_code,
                  test_category=@test_category, quantity=@quantity, rate=@rate,
                  amount=@amount, updated_at=NOW()
              WHERE inv_det_id=@inv_det_id",
                        new
                        {
                            inv_det_id = match.inv_det_id,
                            test.sno,
                            diag_id = diagId,
                            test.test_name,
                            test.test_code,
                            test.test_category,
                            test.quantity,
                            test.rate,
                            test.amount
                        }, tx);

                    await _unbilledCls.AddInvestigationChargeRow(
                        db, tx, op_id,
                        string.IsNullOrWhiteSpace(ip_id) ? (Guid?)null : Guid.Parse(ip_id),
                        custid, tenant_code,
                        match.inv_det_id.ToString(), test.test_code, test.quantity, test.rate, test.amount);
                    continue;
                }

                // brand-new test → insert + push to unbilling
                Guid invDetId = Guid.NewGuid();
                await db.ExecuteAsync(
                    @"INSERT INTO op_investigation_detail
          (inv_det_id, inv_id, diag_id, sno, test_name, test_code, test_category,
           quantity, rate, amount, result_status, is_billed, tenant_code, isdeleted, created_at, updated_at)
          VALUES
          (@inv_det_id, @inv_id, @diag_id, @sno, @test_name, @test_code, @test_category,
           @quantity, @rate, @amount, 'PENDING', false, @tenant_code, false, NOW(), NOW())",
                    new
                    {
                        inv_det_id = invDetId,
                        inv_id = invId,
                        diag_id = diagId,
                        test.sno,
                        test.test_name,
                        test.test_code,
                        test.test_category,
                        test.quantity,
                        test.rate,
                        test.amount,
                        tenant_code
                    }, tx);

                await _unbilledCls.AddInvestigationChargeRow(
                    db, tx, op_id,
                    string.IsNullOrWhiteSpace(ip_id) ? (Guid?)null : Guid.Parse(ip_id),
                    custid, tenant_code,
                    invDetId.ToString(), test.test_code, test.quantity, test.rate, test.amount);
            }

            // 3. Remove tests the doctor deleted from the list — but never a billed one
            var removeIds = existingRows
                .Where(r => !matched.Contains(r.inv_det_id)
                         && r.is_billed != true
                         && !billedSet.Contains(r.inv_det_id.ToString()))
                .Select(r => r.inv_det_id).ToArray();

            if (removeIds.Length > 0)
            {
                await db.ExecuteAsync(
                    "DELETE FROM op_investigation_detail WHERE inv_det_id = ANY(@removeIds)",
                    new { removeIds }, tx);
            }

            return invCode;
        }

        // ─────────────────────────────────────────────────────────
        // FINALIZE CASE SHEET (DRAFT → FINAL)
        // ─────────────────────────────────────────────────────────
        public async Task<string> FinalizeCaseSheet(
            FinalizeCaseSheetRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_case_sheet
                      SET    sheet_status  = 'FINAL',
                             is_consulted  = @is_consulted,
                             updated_at    = NOW()
                      WHERE  sheet_id      = CAST(@sheet_id AS uuid)
                      AND    tenant_code   = @tenant_code
                      AND    isdeleted     = false",
                    new { req.sheet_id, req.is_consulted, tenant_code });

                if (rows == 0) return "Case sheet not found";

                // ── Push prescription to pharmacy (best-effort; won't undo finalize) ──
                // ── Push ALL prescriptions of this sheet to pharmacy (best-effort) ──
                var prMasters = (await db.QueryAsync<OpPrescriptionMasterModel>(
                    @"SELECT pr_id, pr_code, sheet_id, op_id, ip_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             pr_date, topremarks, bottonremarks,
             is_dispensed, tenant_code, isdeleted, created_at, updated_at
      FROM   op_prescription_master
      WHERE  sheet_id    = CAST(@sheet_id AS uuid)
      AND    tenant_code = @tenant_code
      AND    isdeleted   = false
      ORDER  BY created_at",
                    new { req.sheet_id, tenant_code })).ToList();

                foreach (var prMst in prMasters)
                {
                    var items = (await db.QueryAsync<OpPrescriptionDetailModel>(
                        @"SELECT * FROM op_prescription_detail
          WHERE pr_id = @pr_id AND isdeleted = false ORDER BY sno",
                        new { pr_id = prMst.pr_id })).ToList();

                    if (!items.Any()) continue;

                    var pharmacyReq = new ReceivePrescriptionRequest
                    {
                        custid = prMst.custid,
                        op_id = prMst.op_id?.ToString(),
                        ip_id = prMst.ip_id?.ToString(),
                        sheet_id = prMst.sheet_id?.ToString(),
                        pr_code = prMst.pr_code,
                        items = items.Select(i => new PrescriptionQueueItem
                        {
                            pr_det_id = i.pr_det_id.ToString(),
                            drug_name = i.drug_name,
                            qty = i.qty ?? 0,
                            morning = i.morning,
                            afternoon = i.afternoon,
                            evening = i.evening,
                            night = i.night,
                            morning_dose = i.morning_dose,
                            afternoon_dose = i.afternoon_dose,
                            evening_dose = i.evening_dose,
                            night_dose = i.night_dose,
                            dose_unit = i.dose_unit,
                            before_food = i.before_food,
                            after_food = i.after_food,
                            days = i.days,
                            route = i.route,
                            notes = i.notes
                        }).ToList()
                    };

                    try { await _itemMasterCls.ReceivePrescription(pharmacyReq, tenant_code); }
                    catch { /* finalize already succeeded */ }
                }

                return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // GET CASE SHEET BY VISIT (OP or IP) — full view
        // ─────────────────────────────────────────────────────────
        public async Task<CaseSheetViewModel?> GetCaseSheetByVisit(
            string? op_id, string? ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            bool hasOp = !string.IsNullOrWhiteSpace(op_id);
            bool hasIp = !string.IsNullOrWhiteSpace(ip_id);

            if (!hasOp && !hasIp)
                return null;

            var sheet = await db.QueryFirstOrDefaultAsync<OpCaseSheetModel>(
    @"SELECT sheet_id, op_id, ip_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             chief_complaint, symptoms, examination,
             advise, notes,
             followup_date::timestamp AS followup_date,
             followup_notes, is_consulted, refer_to_ip, sheet_status,
             referred_to_visiting_doctor, referred_to_operation,
             tenant_code, isdeleted, created_at, updated_at
      FROM   op_case_sheet
      WHERE  ((@op_id IS NOT NULL AND op_id = CAST(@op_id AS uuid))
              OR (@ip_id IS NOT NULL AND ip_id = CAST(@ip_id AS uuid)))
      AND    tenant_code = @tenant_code
      AND    isdeleted   = false
      ORDER  BY created_at DESC
      LIMIT  1",
    new { op_id = hasOp ? op_id : null, ip_id = hasIp ? ip_id : null, tenant_code });

            if (sheet == null) return null;

            var vm = new CaseSheetViewModel
            {
                sheet_id = sheet.sheet_id.ToString(),
                op_id = sheet.op_id?.ToString(),
                ip_id = sheet.ip_id?.ToString(),
                custid = sheet.custid,
                dcode = sheet.dcode,
                visit_date = sheet.visit_date,
                chief_complaint = sheet.chief_complaint,
                symptoms = sheet.symptoms,
                examination = sheet.examination,
                advise = sheet.advise,
                notes = sheet.notes,
                followup_date = sheet.followup_date,
                followup_notes = sheet.followup_notes,
                is_consulted = sheet.is_consulted,
                refer_to_ip = sheet.refer_to_ip,      // <-- new
                referred_to_visiting_doctor = sheet.referred_to_visiting_doctor,  // NEW
                referred_to_operation = sheet.referred_to_operation,
                sheet_status = sheet.sheet_status
            };
            vm.visiting_doctor_list = (await db.QueryAsync<OpCaseSheetVisitingDoctorModel>(
    @"SELECT * FROM op_case_sheet_visiting_doctors
      WHERE  sheet_id = @sheet_id
      ORDER  BY sno",
    new { sheet_id = sheet.sheet_id })).ToList();
            // Symptoms
            vm.symptom_list = (await db.QueryAsync<OpCaseSheetSymptomModel>(
                @"SELECT * FROM op_case_sheet_symptoms
                  WHERE  sheet_id = @sheet_id
                  ORDER  BY sno",
                new { sheet_id = sheet.sheet_id })).ToList();

            // Diagnosis
            vm.diagnosis_list = (await db.QueryAsync<OpCaseSheetDiagnosisModel>(
    @"SELECT diag_id, sheet_id, op_id, ip_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             sno, icd_code, icd_description, diagnosis_text,
             diagnosis_type, condition_type, severity, status,
             tenant_code, isdeleted, created_at
      FROM   op_case_sheet_diagnosis
      WHERE  sheet_id  = @sheet_id
      AND    isdeleted = false
      ORDER  BY sno",
    new { sheet_id = sheet.sheet_id })).ToList();

            // Prescription
            // Prescription: all prescriptions of this visit
            vm.prescription_list = await GetPrescriptionList(
                sheet.op_id?.ToString(), sheet.ip_id?.ToString(), tenant_code);
            vm.prescription = vm.prescription_list.FirstOrDefault();   // latest, for old screens

            // Investigation
            var invMst = await db.QueryFirstOrDefaultAsync<OpInvestigationMasterModel>(
    @"SELECT inv_id, inv_code, sheet_id, op_id, ip_id, custid, dcode,
             visit_date::timestamp AS visit_date,
             inv_date, notes, is_urgent, status,
             tenant_code, isdeleted, created_at, updated_at
      FROM   op_investigation_master
      WHERE  sheet_id    = @sheet_id
      AND    tenant_code = @tenant_code
      AND    isdeleted   = false
      ORDER  BY created_at DESC
      LIMIT  1",
    new { sheet_id = sheet.sheet_id, tenant_code });

            if (invMst != null)
            {
                var tests = (await db.QueryAsync<OpInvestigationDetailModel>(
                    @"SELECT * FROM op_investigation_detail
                      WHERE  inv_id     = @inv_id
                      AND    isdeleted  = false
                      ORDER  BY sno",
                    new { inv_id = invMst.inv_id })).ToList();

                vm.investigation = new CaseSheetInvestigationViewModel
                {
                    inv_id = invMst.inv_id.ToString(),
                    inv_code = invMst.inv_code,
                    inv_date = invMst.inv_date,
                    notes = invMst.notes,
                    is_urgent = invMst.is_urgent,
                    status = invMst.status,
                    tests = tests
                };
            }

            return vm;
        }

        // ─────────────────────────────────────────────────────────
        // GET PRESCRIPTION BY VISIT (OP or IP)
        // ─────────────────────────────────────────────────────────
        public async Task<CaseSheetPrescriptionViewModel?> GetPrescription(
            string? op_id, string? ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            bool hasOp = !string.IsNullOrWhiteSpace(op_id);
            bool hasIp = !string.IsNullOrWhiteSpace(ip_id);

            if (!hasOp && !hasIp)
                return null;

            var prMst = await db.QueryFirstOrDefaultAsync<OpPrescriptionMasterModel>(
    @"SELECT p.pr_id, p.pr_code, p.sheet_id, p.op_id, p.ip_id, p.custid, p.dcode,
             p.visit_date::timestamp AS visit_date,
             p.pr_date, p.topremarks, p.bottonremarks,
             p.is_dispensed, p.tenant_code, p.isdeleted, p.created_at, p.updated_at
      FROM   op_prescription_master p
      JOIN   op_case_sheet s ON s.sheet_id = p.sheet_id
      WHERE  ((@op_id IS NOT NULL AND s.op_id = CAST(@op_id AS uuid))
              OR (@ip_id IS NOT NULL AND s.ip_id = CAST(@ip_id AS uuid)))
      AND    p.tenant_code = @tenant_code
      AND    p.isdeleted   = false
      ORDER  BY p.created_at DESC
      LIMIT  1",
    new { op_id = hasOp ? op_id : null, ip_id = hasIp ? ip_id : null, tenant_code });

            if (prMst == null) return null;

            var prDet = (await db.QueryAsync<OpPrescriptionDetailModel>(
                @"SELECT * FROM op_prescription_detail
                  WHERE  pr_id     = @pr_id
                  AND    isdeleted = false
                  ORDER  BY sno",
                new { pr_id = prMst.pr_id })).ToList();

            return new CaseSheetPrescriptionViewModel
            {
                pr_id = prMst.pr_id.ToString(),
                pr_code = prMst.pr_code,
                pr_date = prMst.pr_date,
                topremarks = prMst.topremarks,
                bottonremarks = prMst.bottonremarks,
                is_dispensed = prMst.is_dispensed,
                items = prDet
            };
        }

        // ─────────────────────────────────────────────────────────
        // GET INVESTIGATION BY VISIT (OP or IP)
        // ─────────────────────────────────────────────────────────
        public async Task<CaseSheetInvestigationViewModel?> GetInvestigation(
            string? op_id, string? ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            bool hasOp = !string.IsNullOrWhiteSpace(op_id);
            bool hasIp = !string.IsNullOrWhiteSpace(ip_id);

            if (!hasOp && !hasIp)
                return null;

            var invMst = await db.QueryFirstOrDefaultAsync<OpInvestigationMasterModel>(
    @"SELECT i.inv_id, i.inv_code, i.sheet_id, i.op_id, i.ip_id, i.custid, i.dcode,
             i.visit_date::timestamp AS visit_date,
             i.inv_date, i.notes, i.is_urgent, i.status,
             i.tenant_code, i.isdeleted, i.created_at, i.updated_at
      FROM   op_investigation_master i
      JOIN   op_case_sheet s ON s.sheet_id = i.sheet_id
      WHERE  ((@op_id IS NOT NULL AND s.op_id = CAST(@op_id AS uuid))
              OR (@ip_id IS NOT NULL AND s.ip_id = CAST(@ip_id AS uuid)))
      AND    i.tenant_code = @tenant_code
      AND    i.isdeleted   = false
      ORDER  BY i.created_at DESC
      LIMIT  1",
    new { op_id = hasOp ? op_id : null, ip_id = hasIp ? ip_id : null, tenant_code });

            if (invMst == null) return null;

            var tests = (await db.QueryAsync<OpInvestigationDetailModel>(
                @"SELECT * FROM op_investigation_detail
                  WHERE  inv_id    = @inv_id
                  AND    isdeleted = false
                  ORDER  BY sno",
                new { inv_id = invMst.inv_id })).ToList();

            return new CaseSheetInvestigationViewModel
            {
                inv_id = invMst.inv_id.ToString(),
                inv_code = invMst.inv_code,
                inv_date = invMst.inv_date,
                notes = invMst.notes,
                is_urgent = invMst.is_urgent,
                status = invMst.status,
                tests = tests
            };
        }

        // ─────────────────────────────────────────────────────────
        // UPDATE INVESTIGATION RESULT
        // ─────────────────────────────────────────────────────────
        public async Task<string> UpdateInvestigationResult(
            UpdateInvestigationResultRequest req, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_investigation_detail
                      SET    result_value  = @result_value,
                             result_notes  = @result_notes,
                             result_status = @result_status,
                             result_date   = NOW(),
                             updated_at    = NOW()
                      WHERE  inv_det_id   = CAST(@inv_det_id AS uuid)
                      AND    tenant_code  = @tenant_code",
                    new
                    {
                        req.inv_det_id,
                        req.result_value,
                        req.result_notes,
                        req.result_status,
                        tenant_code
                    });

                if (rows > 0)
                {
                    await db.ExecuteAsync(
                        @"UPDATE op_investigation_master
                          SET    status     = CASE
                                               WHEN (
                                                   SELECT COUNT(*) FROM op_investigation_detail
                                                   WHERE inv_id = im.inv_id
                                                   AND result_status = 'PENDING'
                                                   AND isdeleted = false
                                               ) = 0 THEN 'COMPLETED'
                                               ELSE 'PARTIAL'
                                             END,
                                 updated_at = NOW()
                          FROM   op_investigation_master im
                          JOIN   op_investigation_detail d ON d.inv_id = im.inv_id
                          WHERE  d.inv_det_id = CAST(@inv_det_id AS uuid)
                          AND    im.tenant_code = @tenant_code",
                        new { req.inv_det_id, tenant_code });
                }

                return rows > 0 ? "Success" : "Investigation detail not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // DELETE PRESCRIPTION  (soft delete)
        // ─────────────────────────────────────────────────────────
        public async Task<string> DeletePrescription(string pr_code, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_prescription_master
                      SET    isdeleted  = true,
                             updated_at = NOW()
                      WHERE  pr_code     = @pr_code
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { pr_code, tenant_code });

                return rows > 0 ? "Success" : "Prescription not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // DELETE INVESTIGATION  (soft delete)
        // ─────────────────────────────────────────────────────────
        public async Task<string> DeleteInvestigation(string inv_id, string tenant_code)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                int rows = await db.ExecuteAsync(
                    @"UPDATE op_investigation_master
                      SET    isdeleted  = true,
                             updated_at = NOW()
                      WHERE  inv_id      = CAST(@inv_id AS uuid)
                      AND    tenant_code = @tenant_code
                      AND    isdeleted   = false",
                    new { inv_id, tenant_code });

                return rows > 0 ? "Success" : "Investigation not found";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ─────────────────────────────────────────────────────────
        // GET PATIENT HISTORY (all visits case sheets) — custid based
        // One row per visit (op_id, or ip_id when op_id is null). Child data
        // (symptoms/diagnosis/prescription/investigation) is looked up by the
        // VISIT (op_id/ip_id), not the exact sheet_id — because duplicate
        // op_case_sheet rows for the same visit can each hold a different
        // piece of the data (e.g. prescription on one sheet, investigation
        // on another). Matching by visit instead of sheet_id ensures nothing
        // gets silently dropped.
        // ─────────────────────────────────────────────────────────
        public async Task<List<CaseSheetViewModel>> GetPatientHistory(
            decimal custid, string tenant_code, int pageSize = 10, int pageNo = 1)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            string sheetsSql = @"
        SELECT * FROM (
            SELECT DISTINCT ON (COALESCE(s.op_id::text, s.ip_id::text))
                s.sheet_id, s.op_id, s.ip_id, s.custid, s.dcode,
                s.visit_date::timestamp AS visit_date,
                s.chief_complaint, s.symptoms, s.examination,
                s.advise, s.notes,
                s.followup_date::timestamp AS followup_date,
                s.followup_notes, s.is_consulted, s.sheet_status,
                s.refer_to_ip,
                s.referred_to_visiting_doctor,
                s.referred_to_operation, 
                s.tenant_code, s.isdeleted, s.created_at, s.updated_at,
                c.name AS patient_name,
                c.mobile,
                d.name AS doctor_name
            FROM   op_case_sheet s
            LEFT JOIN customerdb.customer_master c ON c.custid = s.custid
            LEFT JOIN doctor_master d ON d.dcode = s.dcode AND d.tenant_code = s.tenant_code
            WHERE  s.custid      = @custid
            AND    s.tenant_code = @tenant_code
            AND    s.isdeleted   = false
            ORDER  BY COALESCE(s.op_id::text, s.ip_id::text), s.updated_at DESC
        ) dedup
        ORDER BY visit_date DESC
        LIMIT  @pageSize OFFSET @offset";

            var sheets = (await db.QueryAsync(sheetsSql,
                new { custid, tenant_code, pageSize, offset = (pageNo - 1) * pageSize })).ToList();

            var result = new List<CaseSheetViewModel>();

            foreach (var s in sheets)
            {
                Guid sheetId = (Guid)s.sheet_id;
                string? opId = s.op_id?.ToString();
                string? ipId = s.ip_id?.ToString();
                bool hasOp = !string.IsNullOrWhiteSpace(opId);
                bool hasIp = !string.IsNullOrWhiteSpace(ipId);

                var vm = new CaseSheetViewModel
                {
                    sheet_id = sheetId.ToString(),
                    op_id = opId,
                    ip_id = ipId,
                    custid = s.custid,
                    dcode = s.dcode,
                    visit_date = s.visit_date,
                    chief_complaint = s.chief_complaint,
                    symptoms = s.symptoms,
                    examination = s.examination,
                    advise = s.advise,
                    notes = s.notes,
                    followup_date = s.followup_date,
                    followup_notes = s.followup_notes,
                    is_consulted = s.is_consulted,
                    sheet_status = s.sheet_status,
                    refer_to_ip = s.refer_to_ip,
                    referred_to_visiting_doctor = s.referred_to_visiting_doctor,
                    referred_to_operation = s.referred_to_operation
                };
                vm.visiting_doctor_list = (await db.QueryAsync<OpCaseSheetVisitingDoctorModel>(
    @"SELECT vd.* FROM op_case_sheet_visiting_doctors vd
      JOIN   op_case_sheet cs ON cs.sheet_id = vd.sheet_id
      WHERE  cs.tenant_code = @tenant_code
      AND    cs.isdeleted   = false
      AND    ((@op_id IS NOT NULL AND cs.op_id = CAST(@op_id AS uuid))
              OR (@ip_id IS NOT NULL AND cs.ip_id = CAST(@ip_id AS uuid)))
      ORDER  BY vd.sno",
    new { op_id = hasOp ? opId : null, ip_id = hasIp ? ipId : null, tenant_code })).ToList();
                // Symptoms — matched by visit (op_id/ip_id), not just this one sheet_id,
                // so symptoms saved under a sibling duplicate sheet still show up.
                vm.symptom_list = (await db.QueryAsync<OpCaseSheetSymptomModel>(
                    @"SELECT sym.* FROM op_case_sheet_symptoms sym
              JOIN   op_case_sheet cs ON cs.sheet_id = sym.sheet_id
              WHERE  cs.tenant_code = @tenant_code
              AND    cs.isdeleted   = false
              AND    ((@op_id IS NOT NULL AND cs.op_id = CAST(@op_id AS uuid))
                      OR (@ip_id IS NOT NULL AND cs.ip_id = CAST(@ip_id AS uuid)))
              ORDER  BY sym.sno",
                    new { op_id = hasOp ? opId : null, ip_id = hasIp ? ipId : null, tenant_code })).ToList();

                // Diagnosis — same visit-based match
                vm.diagnosis_list = (await db.QueryAsync<OpCaseSheetDiagnosisModel>(
                    @"SELECT diag.diag_id, diag.sheet_id, diag.op_id, diag.ip_id, diag.custid, diag.dcode,
                     diag.visit_date::timestamp AS visit_date,
                     diag.sno, diag.icd_code, diag.icd_description, diag.diagnosis_text,
                     diag.diagnosis_type, diag.condition_type, diag.severity, diag.status,
                     diag.tenant_code, diag.isdeleted, diag.created_at
              FROM   op_case_sheet_diagnosis diag
              JOIN   op_case_sheet cs ON cs.sheet_id = diag.sheet_id
              WHERE  diag.isdeleted  = false
              AND    cs.tenant_code  = @tenant_code
              AND    cs.isdeleted    = false
              AND    ((@op_id IS NOT NULL AND cs.op_id = CAST(@op_id AS uuid))
                      OR (@ip_id IS NOT NULL AND cs.ip_id = CAST(@ip_id AS uuid)))
              ORDER  BY diag.sno",
                    new { op_id = hasOp ? opId : null, ip_id = hasIp ? ipId : null, tenant_code })).ToList();

                // Prescription — matched by visit directly against op_prescription_master
                // (which already carries op_id/ip_id), same pattern as GetPrescription()
                // Prescription: all prescriptions of this visit
                vm.prescription_list = await GetPrescriptionList(opId, ipId, tenant_code);
                vm.prescription = vm.prescription_list.FirstOrDefault();

                // Investigation — matched by visit directly against op_investigation_master
                var invMst = await db.QueryFirstOrDefaultAsync<OpInvestigationMasterModel>(
                    @"SELECT inv_id, inv_code, sheet_id, op_id, ip_id, custid, dcode,
                     visit_date::timestamp AS visit_date,
                     inv_date, notes, is_urgent, status,
                     tenant_code, isdeleted, created_at, updated_at
              FROM   op_investigation_master
              WHERE  ((@op_id IS NOT NULL AND op_id = CAST(@op_id AS uuid))
                      OR (@ip_id IS NOT NULL AND ip_id = CAST(@ip_id AS uuid)))
              AND    tenant_code = @tenant_code
              AND    isdeleted   = false
              ORDER  BY created_at DESC
              LIMIT  1",
                    new { op_id = hasOp ? opId : null, ip_id = hasIp ? ipId : null, tenant_code });

                if (invMst != null)
                {
                    var tests = (await db.QueryAsync<OpInvestigationDetailModel>(
                        @"SELECT * FROM op_investigation_detail
                  WHERE  inv_id    = @inv_id
                  AND    isdeleted = false
                  ORDER  BY sno",
                        new { inv_id = invMst.inv_id })).ToList();

                    vm.investigation = new CaseSheetInvestigationViewModel
                    {
                        inv_id = invMst.inv_id.ToString(),
                        inv_code = invMst.inv_code,
                        inv_date = invMst.inv_date,
                        notes = invMst.notes,
                        is_urgent = invMst.is_urgent,
                        status = invMst.status,
                        tests = tests
                    };
                }

                result.Add(vm);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // ICD-10 SEARCH
        // ─────────────────────────────────────────────────────────
        public async Task<List<dynamic>> SearchIcd(string query, int limit = 20)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var results = await db.QueryAsync(
                @"SELECT icd_code, icd_description, icd_category
                  FROM   icd_master
                  WHERE  is_active = true
                  AND    (icd_code ILIKE @q OR icd_description ILIKE @q)
                  ORDER  BY icd_code
                  LIMIT  @limit",
                new { q = $"%{query}%", limit });

            return results.Cast<dynamic>().ToList();
        }

        public async Task<List<IcdMasterModel>> GetAllIcd()
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                var result = await db.QueryAsync<IcdMasterModel>(
                    @"SELECT 
                        icd_id,
                        icd_code,
                        icd_description,
                        icd_category,
                        icd_chapter,
                        is_active
                      FROM icd_master
                      WHERE is_active = true
                      ORDER BY icd_code");

                return result.ToList();
            }
            catch
            {
                return new List<IcdMasterModel>();
            }
        }
        public async Task<dynamic> GetAllInvestigations(string tenant_code, string? search = "")
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(_db_conn);

                string sql = @"
        SELECT
    im.inv_id,
    im.inv_code,
    im.inv_date,
    im.status AS report_status,
    cs.sheet_status,

    im.custid,
    cm.name AS patient_name,
    cm.mobile,
    crm.custcode,

    im.dcode,
    dm.name,

    im.op_id,
    im.ip_id,

    COUNT(id.inv_det_id) AS total_tests

FROM op_investigation_master im

INNER JOIN op_case_sheet cs
    ON cs.sheet_id = im.sheet_id

INNER JOIN customerdb.customer_master cm
    ON cm.custid = im.custid

LEFT JOIN customerdb.customer_registration_master crm
    ON crm.custid = cm.custid
   AND crm.tenant_code = im.tenant_code

LEFT JOIN doctor_master dm
    ON dm.dcode = im.dcode
   AND dm.tenant_code = im.tenant_code

LEFT JOIN op_investigation_detail id
    ON id.inv_id = im.inv_id
   AND id.isdeleted = false

WHERE im.tenant_code = @tenant_code
  AND im.isdeleted = false
  AND (
        @search = ''
        OR cm.name ILIKE @like
        OR cm.mobile ILIKE @like
        OR crm.custcode ILIKE @like
        OR dm.name ILIKE @like
        OR im.inv_code ILIKE @like
      )

GROUP BY
    im.inv_id,
    im.inv_code,
    im.inv_date,
    im.status,
    cs.sheet_status,
    im.custid,
    cm.name,
    cm.mobile,
    crm.custcode,
    im.dcode,
    dm.name,
    im.op_id,
    im.ip_id

ORDER BY im.created_at DESC;";

                return await db.QueryAsync(sql, new
                {
                    tenant_code,
                    search = search ?? "",
                    like = $"%{search}%"
                });
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        // Save ONE prescription (new if pr_code is null, update if pr_code is given)
        public async Task<string> SavePrescriptionOnly(SavePrescriptionOnlyRequest req, string tenant_code)
        {
            bool hasOp = !string.IsNullOrWhiteSpace(req.op_id);
            bool hasIp = !string.IsNullOrWhiteSpace(req.ip_id);
            if (!hasOp && !hasIp) return "Either op_id or ip_id is required";
            if (req.prescription == null || !req.prescription.items.Any()) return "At least one drug is required";

            using IDbConnection db = new NpgsqlConnection(_db_conn);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var sheetId = await db.ExecuteScalarAsync<Guid?>(
                    @"SELECT sheet_id FROM op_case_sheet
              WHERE ((@op_id IS NOT NULL AND op_id = CAST(@op_id AS uuid))
                  OR (@ip_id IS NOT NULL AND ip_id = CAST(@ip_id AS uuid)))
              AND tenant_code = @tenant_code AND isdeleted = false
              ORDER BY created_at DESC LIMIT 1",
                    new { op_id = hasOp ? req.op_id : null, ip_id = hasIp ? req.ip_id : null, tenant_code }, tx);

                if (sheetId == null) return "Case sheet not found. Save the case sheet first.";

                string prCode = await SavePrescription(
                    db, tx, req.prescription, sheetId.Value,
                    hasOp ? req.op_id : null, hasIp ? req.ip_id : null,
                    req.custid, req.dcode, tenant_code);

                tx.Commit();
                return $"Success|PrCode:{prCode}";
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return ex.Message;
            }
        }

        // ALL prescriptions of a visit, newest first
        public async Task<List<CaseSheetPrescriptionViewModel>> GetPrescriptionList(
            string? op_id, string? ip_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);
            bool hasOp = !string.IsNullOrWhiteSpace(op_id);
            bool hasIp = !string.IsNullOrWhiteSpace(ip_id);
            if (!hasOp && !hasIp) return new();

            var masters = (await db.QueryAsync<OpPrescriptionMasterModel>(
                @"SELECT pr_id, pr_code, sheet_id, op_id, ip_id, custid, dcode,
                 visit_date::timestamp AS visit_date,
                 pr_date, topremarks, bottonremarks,
                 is_dispensed, tenant_code, isdeleted, created_at, updated_at
          FROM op_prescription_master
          WHERE ((@op_id IS NOT NULL AND op_id = CAST(@op_id AS uuid))
              OR (@ip_id IS NOT NULL AND ip_id = CAST(@ip_id AS uuid)))
          AND tenant_code = @tenant_code AND isdeleted = false
          ORDER BY created_at DESC",
                new { op_id = hasOp ? op_id : null, ip_id = hasIp ? ip_id : null, tenant_code })).ToList();

            var result = new List<CaseSheetPrescriptionViewModel>();
            foreach (var m in masters)
            {
                var items = (await db.QueryAsync<OpPrescriptionDetailModel>(
                    @"SELECT * FROM op_prescription_detail
              WHERE pr_id = @pr_id AND isdeleted = false ORDER BY sno",
                    new { pr_id = m.pr_id })).ToList();

                result.Add(new CaseSheetPrescriptionViewModel
                {
                    pr_id = m.pr_id.ToString(),
                    pr_code = m.pr_code,
                    pr_date = m.pr_date,
                    topremarks = m.topremarks,
                    bottonremarks = m.bottonremarks,
                    is_dispensed = m.is_dispensed,
                    items = items
                });
            }
            return result;
        }
        // ─────────────────────────────────────────────────────────
        // GET LAST VISIT PRESCRIPTION (by patient)
        // Finds the patient's most recent visit that has a prescription
        // and returns all prescriptions of that visit.
        // Optional exclude_op_id / exclude_ip_id skip the current visit.
        // ─────────────────────────────────────────────────────────
        public async Task<LastVisitPrescriptionViewModel?> GetLastVisitPrescription(
            decimal custid, string tenant_code,
            string? exclude_op_id = null, string? exclude_ip_id = null)
        {
            using IDbConnection db = new NpgsqlConnection(_db_conn);

            var last = await db.QueryFirstOrDefaultAsync(
                @"SELECT p.op_id, p.ip_id,
                 p.visit_date::timestamp AS visit_date,
                 p.dcode,
                 d.name AS doctor_name
          FROM   op_prescription_master p
          LEFT JOIN doctor_master d
                 ON d.dcode = p.dcode AND d.tenant_code = p.tenant_code
          WHERE  p.custid      = @custid
          AND    p.tenant_code = @tenant_code
          AND    p.isdeleted   = false
          AND    (@exclude_op_id IS NULL
                  OR p.op_id IS DISTINCT FROM CAST(@exclude_op_id AS uuid))
          AND    (@exclude_ip_id IS NULL
                  OR p.ip_id IS DISTINCT FROM CAST(@exclude_ip_id AS uuid))
          ORDER  BY p.visit_date DESC, p.created_at DESC
          LIMIT  1",
                new
                {
                    custid,
                    tenant_code,
                    exclude_op_id = string.IsNullOrWhiteSpace(exclude_op_id) ? null : exclude_op_id,
                    exclude_ip_id = string.IsNullOrWhiteSpace(exclude_ip_id) ? null : exclude_ip_id
                });

            if (last == null) return null;

            string? opId = last.op_id?.ToString();
            string? ipId = last.ip_id?.ToString();

            // reuse existing method: all prescriptions (with items) of that visit
            var list = await GetPrescriptionList(opId, ipId, tenant_code);

            return new LastVisitPrescriptionViewModel
            {
                custid = custid,
                op_id = opId,
                ip_id = ipId,
                visit_date = last.visit_date,
                dcode = (int)last.dcode,
                doctor_name = last.doctor_name,
                prescription_list = list
            };
        }


    }
}