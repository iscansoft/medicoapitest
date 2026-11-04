using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.Model;
using Medico_Backend.Model;
using Npgsql;
using System.Data;

namespace medico_backend.Class
{
    public class UnbilledChargesClass
    {
        private readonly string _conn;
        public UnbilledChargesClass(IConfiguration cfg) =>
            _conn = cfg.GetConnectionString("conn")!;

        private IDbConnection GetConnection() => new NpgsqlConnection(_conn);

        // ── Add consultation fee after OP confirmation ──────────────
        public async Task<string> AddConsultationCharge(AddUnbilledConsultationRequest req, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                // Guard against double-adding consultation charge for the same OP visit
                int already = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM unbilledcharges
                      WHERE opvisitid = @op_id AND entrytype = 'CONSULTATION'
                      AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                    new { req.op_id, tenant_code });

                if (already > 0)
                    return "Consultation charge already pending for this visit";
                
                // ✅ NEW — resolve item/test name for this tcode, so callers in
                // OpRegistrationClass don't need to pass or know about it.
                string? itemName = req.particulars;   // caller can still override if they passed one
                if (string.IsNullOrWhiteSpace(itemName) && req.tcode.HasValue)
                {
                    itemName = await db.ExecuteScalarAsync<string?>(
                        @"SELECT name FROM test_master
                  WHERE tcode = @tcode
                  AND   TRIM(tenant_code) = TRIM(@tenant_code)
                  AND   deleted = false",
                        new { req.tcode, tenant_code });
                }

                var row = new UnbilledChargeRow
                {
                    unbilledid = Guid.NewGuid().ToString(),
                    entrytype = "CONSULTATION",
                    entryid = req.op_id,
                    chargedate = DateTime.UtcNow,
                    custid = req.custid,
                    opvisitid = req.op_id,
                    tcode = req.tcode,
                    quantity = req.quantity,
                    rate = req.rate,
                    amount = req.amount,
                    discount = 0,
                    charityamount = 0,
                    billedstatus = false,
                    tenant_code = tenant_code,
                    particulars = itemName ?? "CONSULTATION"
                };

                await db.InsertAsync(row);
                return $"Success|UnbilledId:{row.unbilledid}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Add investigation test charges (called from case sheet save, same tx) ──
        public async Task AddInvestigationChargeRow(
    IDbConnection db, IDbTransaction tx, string? op_id, Guid? ip_id, decimal custid, string tenant_code,
    string entryId, int? testCode, decimal quantity, decimal? rate, decimal? amount)
        {
            var row = new UnbilledChargeRow
            {
                unbilledid = Guid.NewGuid().ToString(),
                entrytype = "INVESTIGATION",
                entryid = entryId,
                chargedate = DateTime.UtcNow,
                custid = custid,
                opvisitid = op_id,
                ip_id = ip_id,
                tcode = testCode,
                quantity = (double?)quantity,
                rate = (double?)rate,
                amount = (double?)amount,
                discount = 0,
                charityamount = 0,
                billedstatus = false,
                tenant_code = tenant_code
            };
            await db.InsertAsync(row, tx);
        }

        // ── Fetch pending unbilled charges for the billing screen ──
        public async Task<List<UnbilledChargeRow>> GetUnbilledByVisit(
            string? opvisitid, string? ip_id, string tenant_code)
        {
            using var db = GetConnection();
            db.Open();   // ✅ explicit open, same as GetAllOpList

            Guid? ipGuid = null;
            if (!string.IsNullOrWhiteSpace(ip_id))
            {
                if (Guid.TryParse(ip_id.Trim(), out var parsedGuid))
                {
                    ipGuid = parsedGuid;
                }
                else
                {
                    ipGuid = await db.QueryFirstOrDefaultAsync<Guid?>(
                        @"SELECT ip_id FROM ip_registration
                          WHERE (ip_no = @ip_no OR ip_id::text = @ip_no)
                            AND tenant_code = @tenant_code
                            AND COALESCE(isdeleted, false) = false
                          LIMIT 1",
                        new { ip_no = ip_id.Trim(), tenant_code });
                }
            }

            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT
              u.*,
              CASE
                  WHEN u.billedstatus = true
                   AND lrm.requestguid IS NOT NULL
                   AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
                  THEN true
                  ELSE false
              END AS paid_status
          FROM unbilledcharges u
          LEFT JOIN lab_request_master lrm
                 ON  lrm.requestguid = u.billid
                 AND lrm.tenant_code = u.tenant_code
                 AND COALESCE(lrm.isdeleted, false) = false
                 AND COALESCE(lrm.deleted,   false) = false
          WHERE ((@opvisitid IS NOT NULL AND u.opvisitid = @opvisitid)
                 OR (@ipGuid IS NOT NULL AND u.ip_id = @ipGuid))
          AND u.tenant_code = @tenant_code
          AND (u.billedstatus = false OR u.billedstatus IS NULL)
          ORDER BY u.chargedate",
                new { opvisitid, ipGuid, tenant_code },
                commandTimeout: 60   // ✅ same timeout bump as GetAllOpList
            );
            return rows.ToList();
        }

        public async Task<List<UnbilledChargeRow>> GetUnbilledByCustomer(decimal custid, string tenant_code)
        {
            using var db = GetConnection();
            db.Open();

            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT
              u.*,
              CASE
                  WHEN u.billedstatus = true
                   AND lrm.requestguid IS NOT NULL
                   AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
                  THEN true
                  ELSE false
              END AS paid_status
          FROM unbilledcharges u
          LEFT JOIN lab_request_master lrm
                 ON  lrm.requestguid = u.billid
                 AND lrm.tenant_code = u.tenant_code
                 AND COALESCE(lrm.isdeleted, false) = false
                 AND COALESCE(lrm.deleted,   false) = false
          WHERE u.custid = @custid AND u.tenant_code = @tenant_code
          AND (u.billedstatus = false OR u.billedstatus IS NULL)
          ORDER BY u.chargedate",
                new { custid, tenant_code },
                commandTimeout: 60
            );
            return rows.ToList();
        }

        // ── Get all unbilled charges for a tenant, optionally narrowed by op_id or ip_id ──
        public async Task<List<UnbilledChargeRow>> GetAllUnbilled(
            string tenant_code, string? op_id = null, Guid? ip_id = null)
        {
            using var db = GetConnection();
            db.Open();

            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT
              u.*,
              CASE
                  WHEN u.billedstatus = true
                   AND lrm.requestguid IS NOT NULL
                   AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
                  THEN true
                  ELSE false
              END AS paid_status
          FROM unbilledcharges u
          LEFT JOIN lab_request_master lrm
                 ON  lrm.requestguid = u.billid
                 AND lrm.tenant_code = u.tenant_code
                 AND COALESCE(lrm.isdeleted, false) = false
                 AND COALESCE(lrm.deleted,   false) = false
          WHERE u.tenant_code = @tenant_code
          AND (@op_id IS NULL OR u.opvisitid = @op_id)
          AND (@ip_id IS NULL OR u.ip_id = @ip_id)
          ORDER BY u.chargedate",
                new { tenant_code, op_id, ip_id },
                commandTimeout: 60
            );
            return rows.ToList();
        }
        public async Task AddInvestigationChargeRow(
    IDbConnection db, string? op_id, Guid? ip_id, decimal custid, string tenant_code,
    string entryId, int? testCode, decimal quantity, decimal? rate, decimal? amount)
        {
            var row = new UnbilledChargeRow
            {
                unbilledid = Guid.NewGuid().ToString(),
                entrytype = "INVESTIGATION",
                entryid = entryId,
                chargedate = DateTime.UtcNow,
                custid = custid,
                opvisitid = op_id,
                ip_id = ip_id,              // NEW
                tcode = testCode,
                quantity = (double?)quantity,
                rate = (double?)rate,
                amount = (double?)amount,
                discount = 0,
                charityamount = 0,
                billedstatus = false,
                tenant_code = tenant_code
            };
            await db.InsertAsync(row);
        }
        // ── Core billing rule — see algorithm notes ──
        public decimal CalculateRoomRentDays(DateTime roomEntryTime, DateTime currentTime)
        {
            var totalHours = (currentTime - roomEntryTime).TotalHours;
            if (totalHours < 12)
                return 0m;

            int fullDays = (int)(totalHours / 24);
            double remainingHours = totalHours % 24;

            decimal charge = fullDays;
            if (remainingHours >= 12)
                charge += 0.5m;

            return charge;
        }
        private static DateTime NormalizeUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        // ── Recalculate ROOMRENT for a stay, only the unbilled remainder ──
        public async Task<string> RecalculateRoomRent(Guid ip_id, string tenant_code, DateTime? asOf = null)
        {
            try
            {
                using var db = GetConnection();

                // AFTER
                var ip = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT admitdate, dischargedate, ip_status, rmtcode, bedcode, custid, billing_rmtcode
      FROM ip_registration
      WHERE ip_id = @ip_id AND tenant_code = @tenant_code",
                    new { ip_id, tenant_code });

                if (ip == null)
                    return $"IP Registration not found for ip_id='{ip_id}' tenant_code='{tenant_code}'";

                // Rate lookup always uses the billing type chosen at admission.
                // Falls back to the physical room type only for legacy rows where billing_rmtcode was never set.
                int? effectiveRateRmt = (int?)ip.billing_rmtcode ?? (int?)ip.rmtcode;

                DateTime admitdate = NormalizeUtc((DateTime)ip.admitdate);
                bool isDischarged = ip.ip_status == "DISCHARGED" && ip.dischargedate != null;
                DateTime cutoffEnd = isDischarged
                    ? NormalizeUtc((DateTime)ip.dischargedate)
                    : (asOf ?? DateTime.UtcNow);

                var transfers = (await db.QueryAsync<dynamic>(
                    @"SELECT transferdate, currentroom, transroom, currentbed, transbed
              FROM public.bed_transfer
              WHERE lastvisitid = @ipIdStr AND tenant_code = @tenant_code
              ORDER BY transferdate ASC",
                    new { ipIdStr = ip_id.ToString(), tenant_code })).ToList();

                // Build segments, marking each as "closed" (ended by a real transfer/discharge)
                // or "open" (still ongoing — this is the only one that gets refreshed)
                var segments = new List<(int? rmtcode, int? bedcode, DateTime from, DateTime to, bool isClosed)>();
                DateTime segStart = admitdate;
                int? segRmt = transfers.Count > 0 ? (int?)transfers[0].currentroom : (int?)ip.rmtcode;
                int? segBed = transfers.Count > 0 ? (int?)transfers[0].currentbed : (int?)ip.bedcode;

                foreach (var t in transfers)
                {
                    DateTime segEnd = NormalizeUtc((DateTime)t.transferdate);
                    if (segEnd > segStart) segments.Add((segRmt, segBed, segStart, segEnd, true));  // closed by transfer
                    segStart = segEnd;
                    segRmt = (int?)t.transroom;
                    segBed = (int?)t.transbed;
                }
                if (cutoffEnd > segStart)
                    segments.Add((segRmt, segBed, segStart, cutoffEnd, isDischarged)); // closed only if discharged
                // ══════════════ INSERT THE CLIP LOGIC HERE ══════════════
                DateTime? lastBilledCutoff = await db.ExecuteScalarAsync<DateTime?>(
                    @"SELECT MAX(chargedate) FROM unbilledcharges
                      WHERE entrytype = 'ROOMRENT' AND ip_id = @ip_id AND tenant_code = @tenant_code
                      AND billedstatus = true",
                    new { ip_id, tenant_code });

                if (lastBilledCutoff.HasValue)
                {
                    var cutoff = NormalizeUtc(lastBilledCutoff.Value);
                    segments = segments
                        .Where(s => s.to > cutoff)
                        .Select(s => s.from < cutoff ? (s.rmtcode, s.bedcode, cutoff, s.to, s.isClosed) : s)
                        .ToList();
                }
                int inserted = 0, skippedExisting = 0, skippedNoRoomType = 0;
                async Task<List<dynamic>> LoadHeads(int? rmt)
                {
                    try
                    {
                        return (await db.QueryAsync<dynamic>(
                            @"SELECT COALESCE(g.subtestcode, g.roomchargehead) AS tcode,
                     g.testrate AS rate,
                     tm.name
              FROM public.test_group_rates g
              LEFT JOIN test_master tm
                     ON tm.tcode = COALESCE(g.subtestcode, g.roomchargehead)
                    AND TRIM(tm.tenant_code) = TRIM(g.tenant_code)
                    AND tm.deleted = false
              WHERE g.rmtcode = @rmt
              AND   TRIM(g.tenant_code) = TRIM(@tenant_code)
              ORDER BY g.id",
                            new { rmt, tenant_code })).ToList();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ROOMRENT-HEADS-ERROR] {ex.Message}");
                        return new List<dynamic>();
                    }
                }

                // try the billing room type first, then the physical room type
                var heads = await LoadHeads(effectiveRateRmt);
                if (heads.Count == 0 && (int?)ip.rmtcode != null && (int?)ip.rmtcode != effectiveRateRmt)
                    heads = await LoadHeads((int?)ip.rmtcode);

                Console.WriteLine($"[ROOMRENT-DEBUG] ip_id={ip_id} tenant='{tenant_code}' billing_rmt={(int?)ip.billing_rmtcode} physical_rmt={(int?)ip.rmtcode} heads={heads.Count} segments={segments.Count}");
                // AFTER
                foreach (var seg in segments)
                {
                    if (seg.rmtcode == null) continue;

                    decimal chargedDays = CalculateRoomRentDays(seg.from, seg.to);
                    if (chargedDays <= 0) continue;

                    // Build the lines to insert: split by charge head, or the old single line as fallback
                    var lines = new List<(int? tcode, string particulars, decimal rate)>();

                    if (heads.Count > 0)
                    {
                        foreach (var h in heads)
                        {
                            decimal headRate = (decimal)(h.rate ?? 0);
                            if (headRate <= 0) continue;
                            lines.Add(((int?)h.tcode, (string?)h.name ?? "ROOMRENT", headRate));
                        }
                    }
                    else
                    {
                        var roomType = await db.QueryFirstOrDefaultAsync<dynamic>(
                            @"SELECT roomrate FROM public.roomtype_master
              WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code
              AND (deleted IS NULL OR deleted = false)",
                            new { rmtcode = effectiveRateRmt, tenant_code });

                        if (roomType == null) { skippedNoRoomType++; continue; }
                        lines.Add((effectiveRateRmt, "ROOMRENT", (decimal)(roomType.roomrate ?? 0)));
                    }

                    if (lines.Count == 0) { skippedNoRoomType++; continue; }

                    if (seg.isClosed)
                    {
                        string closedKey = $"{ip_id}|SEG|{seg.to:o}|bed:{seg.bedcode}";

                        // matches old single-row key AND new per-head keys
                        var already = await db.ExecuteScalarAsync<int>(
                            @"SELECT COUNT(1) FROM unbilledcharges
              WHERE (entryid = @closedKey OR entryid LIKE @closedLike)
              AND tenant_code = @tenant_code",
                            new { closedKey, closedLike = closedKey + "|T%", tenant_code });

                        if (already > 0) { skippedExisting++; continue; }

                        foreach (var l in lines)
                        {
                            await db.InsertAsync(new UnbilledChargeRow
                            {
                                unbilledid = Guid.NewGuid().ToString(),
                                entrytype = "ROOMRENT",
                                entryid = heads.Count > 0 ? $"{closedKey}|T{l.tcode}" : closedKey,
                                chargedate = seg.to,
                                custid = (decimal)ip.custid,
                                ip_id = ip_id,
                                bedcode = seg.bedcode,
                                tcode = l.tcode,
                                quantity = (double)chargedDays,
                                rate = (double)l.rate,
                                amount = (double)(chargedDays * l.rate),
                                discount = 0,
                                charityamount = 0,
                                billedstatus = false,
                                tenant_code = tenant_code,
                                particulars = l.particulars
                            });
                            inserted++;
                        }
                    }
                    else
                    {
                        string openKey = $"{ip_id}|OPEN|bed:{seg.bedcode}";

                        // delete this open segment's pending rows (old single key + per-head keys)
                        await db.ExecuteAsync(
                            @"DELETE FROM unbilledcharges
              WHERE (entryid = @openKey OR entryid LIKE @openLike)
              AND tenant_code = @tenant_code
              AND (billedstatus = false OR billedstatus IS NULL)",
                            new { openKey, openLike = openKey + "|T%", tenant_code });

                        foreach (var l in lines)
                        {
                            await db.InsertAsync(new UnbilledChargeRow
                            {
                                unbilledid = Guid.NewGuid().ToString(),
                                entrytype = "ROOMRENT",
                                entryid = heads.Count > 0 ? $"{openKey}|T{l.tcode}" : openKey,
                                chargedate = seg.to,
                                custid = (decimal)ip.custid,
                                ip_id = ip_id,
                                bedcode = seg.bedcode,
                                tcode = l.tcode,
                                quantity = (double)chargedDays,
                                rate = (double)l.rate,
                                amount = (double)(chargedDays * l.rate),
                                discount = 0,
                                charityamount = 0,
                                billedstatus = false,
                                tenant_code = tenant_code,
                                particulars = l.particulars
                            });
                            inserted++;
                        }
                    }
                }

                return $"Success|Segments:{segments.Count}|Inserted:{inserted}|SkippedExisting:{skippedExisting}|SkippedNoRoomType:{skippedNoRoomType}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        // ── Room-rent breakdown for a stay, with the charge-head/testfeegroup split ──
        // Main summary — one row per segment, driven by roomtype_master.roomrate
        public async Task<List<dynamic>> GetIpRoomRentSummary(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            string sql = @"
        SELECT uc.unbilledid, uc.chargedate, uc.tcode AS rmtcode, uc.quantity,
               uc.rate, uc.amount, uc.billedstatus, uc.bedcode,
               rm.name AS roomtype_name, rm.roomrate,
               bm.bedname, bm.shortname
        FROM unbilledcharges uc
        LEFT JOIN public.roomtype_master rm
               ON rm.rmtcode = uc.tcode AND rm.tenant_code = uc.tenant_code
        LEFT JOIN public.bed_master bm
               ON bm.bedcode = uc.bedcode AND bm.tenant_code = uc.tenant_code
        WHERE uc.entrytype = 'ROOMRENT' AND uc.ip_id = @ip_id
          AND uc.tenant_code = @tenant_code
        ORDER BY uc.chargedate";
            var res = await db.QueryAsync<dynamic>(sql, new { ip_id, tenant_code });
            return res.ToList();
        }

        // Optional display-only breakdown — how a room type's rate splits across charge-heads.
        // Not used for billing math, just for showing "what's included" if the UI wants it.
        public async Task<List<TestGroupRateModel>> GetTestGroupBreakdown(int rmtcode, string tenant_code)
        {
            using var db = GetConnection();
            var res = await db.QueryAsync<TestGroupRateModel>(
                @"SELECT * FROM public.test_group_rates
          WHERE rmtcode = @rmtcode AND tenant_code = @tenant_code",
                new { rmtcode, tenant_code });
            return res.ToList();
        }

        // ── Void pending unbilled ROOMRENT charges for a cancelled admission ──
        // Billed rows (payment already collected) are left untouched — that's a refund concern.
        public async Task CloseUnbilledForIp(IDbConnection db, IDbTransaction tx, Guid ip_id, string tenant_code)
        {
            await db.ExecuteAsync(
                @"DELETE FROM unbilledcharges
          WHERE entrytype = 'ROOMRENT' AND ip_id = @ip_id
          AND tenant_code = @tenant_code AND (billedstatus = false OR billedstatus IS NULL)",
                new { ip_id, tenant_code }, tx);
        }

        // ── All charges for this IP stay have been billed? ──
        public async Task<bool> IsFullyBilled(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            int pending = await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM unbilledcharges
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code
          AND (billedstatus = false OR billedstatus IS NULL)",
                new { ip_id, tenant_code });
            return pending == 0;
        }


        public async Task<bool> IsPaymentSettled(Guid ip_id, string tenant_code)
        {
            using var db = GetConnection();
            double? outstandingBalance = await db.ExecuteScalarAsync<double?>(
                @"SELECT COALESCE(SUM(totalamount - COALESCE(paidamount,0)), 0)
          FROM lab_request_master
          WHERE ip_id = @ip_id AND tenant_code = @tenant_code
          AND (isdeleted = false OR isdeleted IS NULL)",
                new { ip_id, tenant_code });
            return (outstandingBalance ?? 0) <= 0.05;   // matches the 0.05 tolerance used elsewhere in HmsBillingClass
        }
        // ── Update a pending unbilled charge (e.g. correct amount before billing) ──
        public async Task<string> UpdateUnbilledCharge(UpdateUnbilledChargeRequest req, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                var existing = await db.QueryFirstOrDefaultAsync<UnbilledChargeRow>(
                    @"SELECT * FROM unbilledcharges
              WHERE unbilledid = @unbilledid AND tenant_code = @tenant_code",
                    new { req.unbilledid, tenant_code });

                if (existing == null)
                    return "Unbilled charge not found";

                if (existing.billedstatus == true)
                    return "Cannot update a charge that has already been billed";

                double newRate = req.rate ?? existing.rate ?? 0;
                double newQuantity = req.quantity ?? existing.quantity ?? 0;

                // If amount not explicitly passed, recalc from rate * quantity
                double newAmount = req.amount ?? (newRate * newQuantity);

                existing.rate = newRate;
                existing.quantity = newQuantity;
                existing.amount = newAmount;
                existing.discount = req.discount ?? existing.discount;
                existing.charityamount = req.charityamount ?? existing.charityamount;

                int rows = await db.ExecuteAsync(
                    @"UPDATE unbilledcharges
              SET rate = @rate,
                  quantity = @quantity,
                  amount = @amount,
                  discount = @discount,
                  charityamount = @charityamount
              WHERE unbilledid = @unbilledid
              AND   tenant_code = @tenant_code
              AND   (billedstatus = false OR billedstatus IS NULL)",
                    new
                    {
                        existing.rate,
                        existing.quantity,
                        existing.amount,
                        existing.discount,
                        existing.charityamount,
                        existing.unbilledid,
                        tenant_code
                    });

                return rows > 0 ? $"Success|UnbilledId:{existing.unbilledid}|Amount:{existing.amount}" : "Update failed";
            }
            catch (Exception ex) { return ex.Message; }
        }
        // ── Add IP registration/admission fee (mirrors AddConsultationCharge for OP) ──
        public async Task<string> AddIpRegistrationCharge(AddUnbilledIpRegistrationRequest req, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                // Guard against double-adding the registration fee for the same admission
                int already = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM unbilledcharges
              WHERE ip_id = @ip_id AND entrytype = 'REGISTRATION'
              AND tenant_code = @tenant_code
              AND (billedstatus = false OR billedstatus IS NULL)",
                    new { req.ip_id, tenant_code });

                if (already > 0)
                    return "Registration charge already pending for this admission";

                string? itemName = req.particulars;
                if (string.IsNullOrWhiteSpace(itemName) && req.tcode.HasValue)
                {
                    itemName = await db.ExecuteScalarAsync<string?>(
                        @"SELECT name FROM test_master
                  WHERE tcode = @tcode
                  AND   TRIM(tenant_code) = TRIM(@tenant_code)
                  AND   deleted = false",
                        new { req.tcode, tenant_code });
                }

                var row = new UnbilledChargeRow
                {
                    unbilledid = Guid.NewGuid().ToString(),
                    entrytype = "REGISTRATION",
                    entryid = req.ip_id.ToString(),
                    chargedate = DateTime.UtcNow,
                    custid = req.custid,
                    ip_id = req.ip_id,
                    tcode = req.tcode,
                    quantity = req.quantity,
                    rate = req.rate,
                    amount = req.amount,
                    discount = 0,
                    charityamount = 0,
                    billedstatus = false,
                    tenant_code = tenant_code,
                    particulars = itemName ?? "IP REGISTRATION"
                };

                await db.InsertAsync(row);
                return $"Success|UnbilledId:{row.unbilledid}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task AddOtChargeRow(
    IDbConnection db, IDbTransaction? tx, string? op_id, Guid? ip_id, decimal custid, string tenant_code,
    string entryType, string entryId, string itemName, decimal quantity, decimal? rate, decimal? amount,
    int? tcode = null)
        {
            var row = new UnbilledChargeRow
            {
                unbilledid = Guid.NewGuid().ToString(),
                entrytype = entryType,
                entryid = entryId,
                chargedate = DateTime.UtcNow,
                custid = custid,
                opvisitid = op_id,
                ip_id = ip_id,
                tcode = tcode,
                quantity = (double?)quantity,
                rate = (double?)rate,
                amount = (double?)amount,
                discount = 0,
                charityamount = 0,
                billedstatus = false,
                tenant_code = tenant_code,
                particulars = itemName
            };
            await db.InsertAsync(row, tx);   // Dapper.Contrib's InsertAsync overload accepts a nullable IDbTransaction fine
        }
        
        // ── Resolve name + rate for a tcode, shared by every module that
        // prices an item off test_master before pushing an unbilled charge
        // (OT consumables/implants, IP nurse-note services, etc.) ──
        public async Task<(string? name, decimal rate)> GetTestMasterRate(
            IDbConnection db, IDbTransaction? tx, int tcode, string tenant_code)
        {
            var testInfo = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT name, amount FROM test_master
          WHERE tcode = @tcode
          AND   TRIM(tenant_code) = TRIM(@tenant_code)
          AND   deleted = false",
                new { tcode, tenant_code }, tx);

            if (testInfo == null)
                return (null, 0);

            return ((string)testInfo.name, (decimal)(testInfo.amount ?? 0));
        }

        // ── Resolve tcode + rate for a service by NAME, shared by every module
        // that prices an item by matching test_master.name (dressing/service
        // registration in OP, IP nurse-note services, etc.) — same exact-then-
        // prefix match OpRegistrationClass.GetServiceCharge used to do inline. ──
        public async Task<(int? tcode, decimal rate)> GetTestMasterRateByName(
            IDbConnection db, IDbTransaction? tx, string service_name, string tenant_code)
        {
            if (string.IsNullOrWhiteSpace(service_name))
                return (null, 0);

            string sName = service_name.Trim();

            // 1️⃣ Exact match first (case-insensitive) — avoids picking up
            // unrelated rows that merely start with the same letters.
            var test = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT tcode, amount FROM test_master
          WHERE TRIM(tenant_code) = TRIM(@tenant_code)
          AND   deleted = false
          AND   name ILIKE @exact_name
          ORDER BY tcode
          LIMIT 1",
                new { tenant_code, exact_name = sName }, tx);

            // 2️⃣ Fallback: prefix match, only if no exact match was found
            if (test == null)
            {
                test = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT tcode, amount FROM test_master
              WHERE TRIM(tenant_code) = TRIM(@tenant_code)
              AND   deleted = false
              AND   name ILIKE @name_pattern
              ORDER BY tcode
              LIMIT 1",
                    new { tenant_code, name_pattern = $"{sName}%" }, tx);
            }

            // 3️⃣ Fallback: substring / contains match
            if (test == null)
            {
                test = await db.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT tcode, amount FROM test_master
              WHERE TRIM(tenant_code) = TRIM(@tenant_code)
              AND   deleted = false
              AND   (name ILIKE @contains OR @exact ILIKE ('%' || name || '%'))
              ORDER BY tcode
              LIMIT 1",
                    new { tenant_code, contains = $"%{sName}%", exact = sName }, tx);
            }

            if (test == null)
                return (null, 0);

            return ((int)test.tcode, (decimal)(test.amount ?? 0));
        }
        public async Task<string> DeleteUnbilledForOp(string op_id, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                // Debug: see exactly what's sitting in the table for this op_id first,
                // regardless of tenant/entrytype/billedstatus filters — tells us if the
                // row exists at all vs. exists but doesn't match on one of the filters.
                var probe = await db.QueryAsync<dynamic>(
                    @"SELECT unbilledid, entrytype, opvisitid, tenant_code, billedstatus
              FROM unbilledcharges
              WHERE TRIM(opvisitid) = TRIM(@op_id)",
                    new { op_id });

                foreach (var p in probe)
                {
                    Console.WriteLine($"[DELETE-UNBILL-PROBE] unbilledid={p.unbilledid} entrytype='{p.entrytype}' " +
                        $"opvisitid='{p.opvisitid}' tenant_code='{p.tenant_code}' billedstatus={p.billedstatus} " +
                        $"(expecting tenant_code='{tenant_code}')");
                }

                int rows = await db.ExecuteAsync(
                    @"DELETE FROM unbilledcharges
              WHERE TRIM(opvisitid)    = TRIM(@op_id)
              AND   TRIM(entrytype)    = 'CONSULTATION'
              AND   TRIM(tenant_code)  = TRIM(@tenant_code)
              AND   (billedstatus = false OR billedstatus IS NULL)",
                    new { op_id, tenant_code });

                Console.WriteLine($"[DELETE-UNBILL] op_id={op_id} tenant_code={tenant_code} rowsDeleted={rows}");

                return $"Success|Deleted:{rows}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DELETE-UNBILL-ERROR] {ex.Message}");
                return ex.Message;
            }
        }
        // ── Fetch charges that HAVE been billed for this visit — mirror of GetUnbilledByVisit ──
        public async Task<List<UnbilledChargeRow>> GetBilledByVisit(
            string? opvisitid, string? ip_id, string tenant_code)
        {
            using var db = GetConnection();
            db.Open();

            Guid? ipGuid = null;
            if (!string.IsNullOrWhiteSpace(ip_id))
            {
                if (Guid.TryParse(ip_id.Trim(), out var parsedGuid))
                {
                    ipGuid = parsedGuid;
                }
                else
                {
                    ipGuid = await db.QueryFirstOrDefaultAsync<Guid?>(
                        @"SELECT ip_id FROM ip_registration
                          WHERE (ip_no = @ip_no OR ip_id::text = @ip_no)
                            AND tenant_code = @tenant_code
                            AND COALESCE(isdeleted, false) = false
                          LIMIT 1",
                        new { ip_no = ip_id.Trim(), tenant_code });
                }
            }

            var rows = await db.QueryAsync<UnbilledChargeRow>(
                @"SELECT
              u.*,
              CASE
                  WHEN u.billedstatus = true
                   AND lrm.requestguid IS NOT NULL
                   AND (COALESCE(lrm.totalamount, 0) - COALESCE(lrm.paidamount, 0)) <= 0.05
                  THEN true
                  ELSE false
              END AS paid_status
          FROM unbilledcharges u
          LEFT JOIN lab_request_master lrm
                 ON  lrm.requestguid = u.billid
                 AND lrm.tenant_code = u.tenant_code
                 AND COALESCE(lrm.isdeleted, false) = false
                 AND COALESCE(lrm.deleted,   false) = false
          WHERE ((@opvisitid IS NOT NULL AND u.opvisitid = @opvisitid)
                 OR (@ipGuid IS NOT NULL AND u.ip_id = @ipGuid))
          AND u.tenant_code = @tenant_code
          AND u.billedstatus = true
          ORDER BY u.billeddate DESC NULLS LAST, u.chargedate",
                new { opvisitid, ipGuid, tenant_code },
                commandTimeout: 60
            );
            return rows.ToList();
        }
        // ── Add a non-consultation charge line (SERVICE / DRESSING) onto an
        // existing OP visit — used by ComboRegistration for "additional_items".
        // AddConsultationCharge always writes entrytype='CONSULTATION' and its
        // guard blocks a second CONSULTATION row per visit — reusing it for
        // additional items is exactly what was silently swallowing them.
        // This writes entrytype='SERVICE'/'DRESSING' and dedupes per tcode
        // instead of per-visit, so multiple different additional items can
        // coexist on the same visit without blocking each other or the primary.
        public async Task<string> AddVisitAdditionalCharge(AddUnbilledConsultationRequest req, string entryType, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                string normalizedType = string.IsNullOrWhiteSpace(entryType) ? "SERVICE" : entryType.ToUpper();

                int already = await db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM unbilledcharges
                      WHERE opvisitid = @op_id AND entrytype = @entryType
                      AND (tcode = @tcode OR (tcode IS NULL AND @tcode IS NULL))
                      AND tenant_code = @tenant_code
                      AND (billedstatus = false OR billedstatus IS NULL)",
                    new { req.op_id, entryType = normalizedType, req.tcode, tenant_code });

                if (already > 0)
                    return $"{normalizedType} charge already pending for this visit";

                string? itemName = req.particulars;
                if (string.IsNullOrWhiteSpace(itemName) && req.tcode.HasValue)
                {
                    itemName = await db.ExecuteScalarAsync<string?>(
                        @"SELECT name FROM test_master
                          WHERE tcode = @tcode
                          AND   TRIM(tenant_code) = TRIM(@tenant_code)
                          AND   deleted = false",
                        new { req.tcode, tenant_code });
                }

                var row = new UnbilledChargeRow
                {
                    unbilledid = Guid.NewGuid().ToString(),
                    entrytype = normalizedType,
                    entryid = req.op_id,
                    chargedate = DateTime.UtcNow,
                    custid = req.custid,
                    opvisitid = req.op_id,
                    tcode = req.tcode,
                    quantity = req.quantity,
                    rate = req.rate,
                    amount = req.amount,
                    discount = 0,
                    charityamount = 0,
                    billedstatus = false,
                    tenant_code = tenant_code,
                    particulars = itemName ?? normalizedType
                };

                await db.InsertAsync(row);
                return $"Success|UnbilledId:{row.unbilledid}";
            }
            catch (Exception ex) { return ex.Message; }
        }
        // ── Discard pending unbilled charges — one or many ──
        // Billed rows are never touched (that's a refund concern).
        // Normal charges are deleted.
        // ROOMRENT rows are closed out (billedstatus = true, billed qty/amount = 0, tagged [DISCARDED])
        // so RecalculateRoomRent does NOT recreate them.
        public async Task<DiscardUnbilledResult> DiscardUnbilledCharges(
            List<string> unbilledids, string tenant_code)
        {
            var result = new DiscardUnbilledResult();

            var ids = unbilledids
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .Distinct()
                .ToArray();

            result.requested = ids.Length;
            if (ids.Length == 0) return result;

            using var db = GetConnection();
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Load whatever exists for this tenant
                var existing = (await db.QueryAsync<UnbilledChargeRow>(
                    @"SELECT * FROM unbilledcharges
              WHERE unbilledid = ANY(@ids) AND tenant_code = @tenant_code",
                    new { ids, tenant_code }, tx)).ToDictionary(r => r.unbilledid!);

                var deletable = new List<string>();     // normal charges → hard delete
                var roomRentIds = new List<string>();   // ROOMRENT → close out as discarded

                foreach (var id in ids)
                {
                    if (!existing.TryGetValue(id, out var row))
                    {
                        result.skipped.Add(new DiscardSkipped { unbilledid = id, reason = "Not found" });
                        continue;
                    }
                    if (row.billedstatus == true)
                    {
                        result.skipped.Add(new DiscardSkipped { unbilledid = id, reason = "Already billed" });
                        continue;
                    }

                    if (string.Equals(row.entrytype?.Trim(), "ROOMRENT", StringComparison.OrdinalIgnoreCase))
                        roomRentIds.Add(id);
                    else
                        deletable.Add(id);
                }

                var processedIds = new List<string>();

                // ── 1) Normal charges: delete (re-check billedstatus to guard against a race with billing) ──
                if (deletable.Count > 0)
                {
                    var deletedIds = (await db.QueryAsync<string>(
                        @"DELETE FROM unbilledcharges
                  WHERE unbilledid = ANY(@deletable)
                  AND   tenant_code = @tenant_code
                  AND   (billedstatus = false OR billedstatus IS NULL)
                  RETURNING unbilledid",
                        new { deletable = deletable.ToArray(), tenant_code }, tx)).ToList();

                    processedIds.AddRange(deletedIds);

                    foreach (var id in deletable.Except(deletedIds))
                        result.skipped.Add(new DiscardSkipped { unbilledid = id, reason = "Billed while discarding" });
                }

                // ── 2) ROOMRENT: close out so RecalculateRoomRent won't recreate it ──
                if (roomRentIds.Count > 0)
                {
                    var closedIds = (await db.QueryAsync<string>(
                        @"UPDATE unbilledcharges
                  SET billedstatus   = true,
                      billeddate     = @now,
                      billedquantity = 0,
                      billedamount   = 0,
                      particulars    = COALESCE(particulars, 'ROOMRENT') || ' [DISCARDED]'
                  WHERE unbilledid = ANY(@roomRentIds)
                  AND   tenant_code = @tenant_code
                  AND   entrytype = 'ROOMRENT'
                  AND   (billedstatus = false OR billedstatus IS NULL)
                  RETURNING unbilledid",
                        new { roomRentIds = roomRentIds.ToArray(), tenant_code, now = DateTime.UtcNow }, tx)).ToList();

                    processedIds.AddRange(closedIds);

                    foreach (var id in roomRentIds.Except(closedIds))
                        result.skipped.Add(new DiscardSkipped { unbilledid = id, reason = "Billed while discarding" });
                }

                result.discarded_ids = processedIds;
                result.discarded = processedIds.Count;

                tx.Commit();
                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        // ── Add extra amount onto the pending REGISTRATION line (e.g. reimbursement fee).
        // If no pending registration line exists, falls back to inserting a new one. ──
        public async Task<string> AddOrMergeIpRegistrationCharge(AddUnbilledIpRegistrationRequest req, string tenant_code)
        {
            try
            {
                using var db = GetConnection();

                int rows = await db.ExecuteAsync(
                    @"UPDATE unbilledcharges
              SET rate   = COALESCE(rate, 0)   + @amount,
                  amount = COALESCE(amount, 0) + @amount
              WHERE ip_id = @ip_id AND entrytype = 'REGISTRATION'
              AND tenant_code = @tenant_code
              AND (billedstatus = false OR billedstatus IS NULL)",
                    new { req.amount, req.ip_id, tenant_code });

                if (rows > 0) return "Success|Merged";

                return await AddIpRegistrationCharge(req, tenant_code);
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
