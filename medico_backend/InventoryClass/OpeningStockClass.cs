using ClosedXML.Excel;
using Dapper;
using medico_backend.InventoryModel;
using Npgsql;
using System.Globalization;
using System.Text.RegularExpressions;

namespace medico_backend.InventoryClass
{
    public class OpeningStockUploadClass
    {
        private readonly string con;

        public OpeningStockUploadClass(IConfiguration config)
        {
            con = config.GetConnectionString("inventory_conn");
        }

        // Accepted header names (spaces / dots / underscores / case are ignored)
        private static readonly string[] ItemAliases = { "itemname", "item", "productname", "product" };
        private static readonly string[] BatchAliases = { "batchno", "batch", "batchnumber" };
        private static readonly string[] ExpAliases = { "expdate", "expirydate", "expiry", "exp" };
        private static readonly string[] StripAliases = { "openingstrip", "openingstrips" };
        private static readonly string[] LooseAliases = { "openingloose" };

        private sealed class ResolvedRow
        {
            public opening_stock_excel_row row { get; set; } = null!;
            public opening_stock_item_info item { get; set; } = null!;
            public decimal openingstock { get; set; }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MAIN — validate everything first, then write. All-or-nothing.
        // ═════════════════════════════════════════════════════════════════════
        public async Task<opening_stock_upload_result> UploadOpeningStock(
            opening_stock_upload_request req, string tenantcode)
        {
            var result = new opening_stock_upload_result();

            string defaultBatch = (req.defaultbatchno ?? "").Trim();
            var rows = ReadExcel(req.file, result, defaultBatch, out int totalRows);
            result.totalrows = totalRows;

            if (totalRows == 0)
                throw new Exception("No data rows found in the Excel file.");

            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();
            using var tran = await conn.BeginTransactionAsync();

            try
            {
                // Serialize concurrent uploads for the same tenant + warehouse
                await conn.ExecuteAsync(
                    "SELECT pg_advisory_xact_lock(hashtext(@lockkey));",
                    new { lockkey = $"OPENINGSTOCK_{tenantcode}_{req.warehousecode}" },
                    tran);

                // ── Warehouse must exist ──
                var whFound = await conn.ExecuteScalarAsync<long?>(@"
                    SELECT warehousecode FROM public.warehouse_master
                    WHERE warehousecode = @warehousecode
                      AND tenantcode = @tenantcode
                      AND isdeleted = false;",
                    new { req.warehousecode, tenantcode }, tran);

                if (whFound == null)
                    throw new Exception($"Warehouse {req.warehousecode} not found.");

                // ── Item master lookup (name -> item, pack size) ──
                var items = (await conn.QueryAsync<opening_stock_item_info>(@"
                    SELECT itemcode, itemname,
                           COALESCE(packsize, 1)     AS packsize,
                           COALESCE(purchaserate, 0) AS purchaserate,
                           COALESCE(mrp, 0)          AS mrp
                    FROM public.item_master
                    WHERE deleted = false
                      AND tenantcode = @tenantcode;",
                    new { tenantcode }, tran)).ToList();

                var itemLookup = items
                    .GroupBy(i => NormName(i.itemname))
                    .ToDictionary(g => g.Key, g => g.ToList());

                // ── Resolve every row ──
                var resolved = new List<ResolvedRow>();
                var byKey = new Dictionary<string, ResolvedRow>();

                foreach (var r in rows)
                {
                    if (string.IsNullOrWhiteSpace(r.itemname))
                    { AddError(result, r, "Item Name is required."); continue; }

                    if (string.IsNullOrWhiteSpace(r.batchno))
                    { AddError(result, r, "Batch No is required."); continue; }

                    if (!itemLookup.TryGetValue(NormName(r.itemname), out var matches))
                    { AddError(result, r, "Item not found in item master."); continue; }

                    if (matches.Count > 1)
                    { AddError(result, r, "More than one item in item master has this name."); continue; }

                    var item = matches[0];

                    if (item.packsize <= 0)
                    { AddError(result, r, "Pack size in item master must be greater than 0."); continue; }

                    string key = $"{item.itemcode}|{r.batchno}";

                    // Same item + batch twice in the file -> add the quantities together
                    if (byKey.TryGetValue(key, out var dup))
                    {
                        dup.row.openingstrip += r.openingstrip;
                        dup.row.openingloose += r.openingloose;
                        dup.row.expirydate ??= r.expirydate;
                        dup.openingstock = (dup.row.openingstrip * item.packsize) + dup.row.openingloose;

                        result.warnings.Add(
                            $"Row {r.rownumber} ({r.itemname}, batch {r.batchno}) has the same item + batch as an earlier row; quantities were added together.");
                        continue;
                    }

                    var resolvedRow = new ResolvedRow
                    {
                        row = r,
                        item = item,
                        // opening stock = (strip * pack size) + loose
                        openingstock = (r.openingstrip * item.packsize) + r.openingloose
                    };

                    byKey[key] = resolvedRow;
                    resolved.Add(resolvedRow);
                }

                result.skipped = result.errors.Count;

                // Strict mode: any error -> nothing is written
                if (result.errors.Count > 0 && !req.skiperrors)
                {
                    await tran.RollbackAsync();
                    result.success = false;
                    result.message = $"{result.errors.Count} row(s) have errors. No data was saved.";
                    return result;
                }

                if (resolved.Count == 0)
                {
                    await tran.RollbackAsync();
                    result.success = false;
                    result.message = "No valid rows to save.";
                    return result;
                }

                // ── Existing stock rows in this warehouse ──
                var existingRows = await conn.QueryAsync<opening_stock_existing>(@"
                    SELECT stockcode, itemcode, batchno
                    FROM public.stock_master
                    WHERE warehousecode = @warehousecode
                      AND tenantcode = @tenantcode
                      AND deleted = false;",
                    new { req.warehousecode, tenantcode }, tran);

                var existing = new Dictionary<string, long>();
                foreach (var e in existingRows)
                    existing.TryAdd($"{e.itemcode}|{(e.batchno ?? "").Trim()}", e.stockcode);

                // ── Apply ──
                foreach (var x in resolved)
                {
                    string key = $"{x.item.itemcode}|{x.row.batchno}";

                    if (existing.TryGetValue(key, out long stockcode))
                    {
                        // Keep other movements intact: closing moves by the CHANGE in opening
                        await conn.ExecuteAsync(@"
                            UPDATE public.stock_master
                            SET
                                closingstock = closingstock + (@openingstock - openingstock),
                                stockvalue   = (closingstock + (@openingstock - openingstock)) * unitcost,
                                openingstock = @openingstock,
                                openingstrip = @openingstrip,
                                openingloose = @openingloose,
                                expirydate   = COALESCE(@expirydate, expirydate),
                                modifieddate = CURRENT_TIMESTAMP,
                                usercode     = COALESCE(@usercode, usercode)
                            WHERE stockcode = @stockcode
                              AND tenantcode = @tenantcode;",
                            new
                            {
                                openingstock = x.openingstock,
                                openingstrip = x.row.openingstrip,
                                openingloose = x.row.openingloose,
                                expirydate = x.row.expirydate,
                                usercode = req.usercode,
                                stockcode,
                                tenantcode
                            }, tran);

                        result.updated++;
                    }
                    else
                    {
                        decimal unitcost = x.item.purchaserate / x.item.packsize;
                        decimal mrpunit = x.item.mrp / x.item.packsize;

                        await conn.ExecuteAsync(@"
                            INSERT INTO public.stock_master
                            (
                                itemcode, warehousecode, branchcode,
                                openingstock, openingstrip, openingloose,
                                purchasedqty, soldqty, damagedqty, returnqty,
                                closingstock, unitcost, mrpunit, stockvalue,
                                batchno, expirydate,
                                isactive, deleted, createddate,
                                usercode, tenantcode, companycode
                            )
                            VALUES
                            (
                                @itemcode, @warehousecode, @branchcode,
                                @openingstock, @openingstrip, @openingloose,
                                0, 0, 0, 0,
                                @openingstock, @unitcost, @mrpunit, @stockvalue,
                                @batchno, @expirydate,
                                true, false, CURRENT_TIMESTAMP,
                                @usercode, @tenantcode, @companycode
                            );",
                            new
                            {
                                x.item.itemcode,
                                req.warehousecode,
                                req.branchcode,
                                openingstock = x.openingstock,
                                openingstrip = x.row.openingstrip,
                                openingloose = x.row.openingloose,
                                unitcost,
                                mrpunit,
                                stockvalue = x.openingstock * unitcost,
                                batchno = x.row.batchno,
                                expirydate = x.row.expirydate,
                                req.usercode,
                                tenantcode,
                                req.companycode
                            }, tran);

                        result.inserted++;
                    }
                }

                await tran.CommitAsync();

                result.success = true;
                result.message = result.skipped > 0
                    ? $"Opening stock uploaded. Inserted: {result.inserted}, Updated: {result.updated}, Skipped: {result.skipped}."
                    : $"Opening stock uploaded. Inserted: {result.inserted}, Updated: {result.updated}.";
                return result;
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                throw new Exception("Opening stock upload failed: " + ex.Message);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TEMPLATE — headers only (a sample row could get uploaded by mistake)
        // ═════════════════════════════════════════════════════════════════════
        public byte[] BuildTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("OpeningStock");

            string[] headers = { "Item Name", "Batch No", "Exp Date", "Opening Strip", "Opening Loose" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
            }

            ws.Column(3).Style.DateFormat.Format = "dd-mm-yyyy";
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  EXCEL READING
        // ═════════════════════════════════════════════════════════════════════
        private List<opening_stock_excel_row> ReadExcel(
            IFormFile file, opening_stock_upload_result result, string defaultBatch, out int totalRows)
        {
            var errors = result.errors;
            int batchDefaulted = 0;
            totalRows = 0;
            var list = new List<opening_stock_excel_row>();

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            var ws = workbook.Worksheets.FirstOrDefault();
            var lastRowUsed = ws?.LastRowUsed();
            var lastColUsed = ws?.LastColumnUsed();

            if (ws == null || lastRowUsed == null || lastColUsed == null)
                throw new Exception("The Excel sheet is empty.");

            int lastRow = lastRowUsed.RowNumber();
            int lastCol = lastColUsed.ColumnNumber();

            // Map header names -> column numbers (row 1)
            var headerMap = new Dictionary<string, int>();
            for (int c = 1; c <= lastCol; c++)
            {
                string key = Norm(ws.Cell(1, c).GetFormattedString());
                if (key.Length > 0 && !headerMap.ContainsKey(key))
                    headerMap[key] = c;
            }

            int colItem = FindCol(headerMap, ItemAliases);
            int colBatch = FindCol(headerMap, BatchAliases);
            int colExp = FindCol(headerMap, ExpAliases);
            int colStrip = FindCol(headerMap, StripAliases);
            int colLoose = FindCol(headerMap, LooseAliases);

            var missing = new List<string>();
            if (colItem == 0) missing.Add("Item Name");
            if (colBatch == 0) missing.Add("Batch No");
            if (colExp == 0) missing.Add("Exp Date");
            if (colStrip == 0) missing.Add("Opening Strip");
            if (colLoose == 0) missing.Add("Opening Loose");

            if (missing.Count > 0)
                throw new Exception("Missing column(s) in Excel header: " + string.Join(", ", missing));

            for (int r = 2; r <= lastRow; r++)
            {
                string itemname = CellText(ws.Cell(r, colItem));
                string batchno = CellText(ws.Cell(r, colBatch));

                // Skip fully blank rows
                if (itemname.Length == 0 && batchno.Length == 0 &&
                    CellText(ws.Cell(r, colExp)).Length == 0 &&
                    CellText(ws.Cell(r, colStrip)).Length == 0 &&
                    CellText(ws.Cell(r, colLoose)).Length == 0)
                    continue;

                totalRows++;

                // Blank batch -> default batch (so sales / returns can still match on batch no)
                if (batchno.Length == 0 && defaultBatch.Length > 0)
                {
                    batchno = defaultBatch;
                    batchDefaulted++;
                }

                var row = new opening_stock_excel_row
                {
                    rownumber = r,
                    itemname = itemname,
                    batchno = batchno
                };

                bool ok = true;

                if (!TryParseExpiry(ws.Cell(r, colExp), out DateTime? exp))
                {
                    errors.Add(new opening_stock_upload_error
                    {
                        rownumber = r,
                        itemname = itemname,
                        batchno = batchno,
                        message = $"Invalid Exp Date '{CellText(ws.Cell(r, colExp))}'."
                    });
                    ok = false;
                }
                row.expirydate = exp;

                if (!TryParseQty(ws.Cell(r, colStrip), out decimal strip) || strip < 0)
                {
                    errors.Add(new opening_stock_upload_error
                    {
                        rownumber = r,
                        itemname = itemname,
                        batchno = batchno,
                        message = $"Invalid Opening Strip '{CellText(ws.Cell(r, colStrip))}'."
                    });
                    ok = false;
                }
                row.openingstrip = strip;

                if (!TryParseQty(ws.Cell(r, colLoose), out decimal loose) || loose < 0)
                {
                    errors.Add(new opening_stock_upload_error
                    {
                        rownumber = r,
                        itemname = itemname,
                        batchno = batchno,
                        message = $"Invalid Opening Loose '{CellText(ws.Cell(r, colLoose))}'."
                    });
                    ok = false;
                }
                row.openingloose = loose;

                if (ok) list.Add(row);
            }

            if (batchDefaulted > 0)
                result.warnings.Add($"{batchDefaulted} row(s) had no Batch No and were saved with batch '{defaultBatch}'.");

            return list;
        }

        // ─── helpers ──────────────────────────────────────────────────────────
        private static string Norm(string? s) =>
            new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private static string NormName(string? s) =>
            Regex.Replace((s ?? "").Trim(), @"\s+", " ").ToUpperInvariant();

        private static int FindCol(Dictionary<string, int> map, string[] aliases)
        {
            foreach (var a in aliases)
                if (map.TryGetValue(a, out int col)) return col;
            return 0;
        }

        private static void AddError(opening_stock_upload_result result, opening_stock_excel_row r, string msg)
        {
            result.errors.Add(new opening_stock_upload_error
            {
                rownumber = r.rownumber,
                itemname = r.itemname,
                batchno = r.batchno,
                message = msg
            });
        }

        private static string CellText(IXLCell cell) =>
            cell.GetFormattedString()?.Trim() ?? "";

        // Blank quantity = 0
        private static bool TryParseQty(IXLCell cell, out decimal qty)
        {
            qty = 0;

            if (cell.IsEmpty() || CellText(cell).Length == 0) return true;

            if (cell.Value.IsNumber)
            {
                qty = Convert.ToDecimal(cell.Value.GetNumber());
                return true;
            }

            return decimal.TryParse(CellText(cell), NumberStyles.Number,
                CultureInfo.InvariantCulture, out qty);
        }

        // Handles real Excel dates, dd-MM-yyyy text, and month/year expiries (MM/yy -> end of month)
        private static bool TryParseExpiry(IXLCell cell, out DateTime? date)
        {
            date = null;

            if (cell.IsEmpty() || CellText(cell).Length == 0) return true;

            if (cell.Value.IsDateTime)
            {
                date = cell.Value.GetDateTime().Date;
                return true;
            }

            if (cell.Value.IsNumber)
            {
                try { date = DateTime.FromOADate(cell.Value.GetNumber()).Date; return true; }
                catch { return false; }
            }

            string text = CellText(cell);

            // Placeholders such as "  -   -", "NA", "NIL" mean "no expiry date"
            if (Regex.IsMatch(text, @"^[-/._\s]*$") ||
                text.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("NIL", StringComparison.OrdinalIgnoreCase))
                return true;

            string[] fullFormats =
            {
                "dd-MM-yyyy", "dd/MM/yyyy", "d-M-yyyy", "d/M/yyyy",
                "yyyy-MM-dd", "dd-MMM-yyyy", "dd.MM.yyyy"
            };

            if (DateTime.TryParseExact(text, fullFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var full))
            {
                date = full.Date;
                return true;
            }

            string[] monthYearFormats =
            {
                "MM/yyyy", "M/yyyy", "MM-yyyy", "M-yyyy",
                "MM/yy", "M/yy", "MMM-yyyy", "MMM-yy", "MMM/yy"
            };

            if (DateTime.TryParseExact(text, monthYearFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var my))
            {
                date = new DateTime(my.Year, my.Month, DateTime.DaysInMonth(my.Year, my.Month));
                return true;
            }

            return false;
        }
    }
}