using Dapper;
using Dapper.Contrib.Extensions;
using medico_backend.InventoryModel;
using Npgsql;
using OfficeOpenXml;
using System.Data;

namespace medico_backend.InventoryClass
{
    public class ItemMasterClass
    {
        private readonly string con;

        public ItemMasterClass(IConfiguration config)
        {
            con = config.GetConnectionString("inventory_conn");
        }

        // ─── ITEM MASTER ──────────────────────────────────────────────────────────────

        public async Task<string> InsertItem(item_master item)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    await db.InsertAsync(item);

                    return "Item Inserted Successfully";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public async Task<string> UpdateItem(item_master item)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    bool result = await db.UpdateAsync(item);

                    return result
                        ? "Item Updated Successfully"
                        : "Item Not Found";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public async Task<string> DeleteItem(long itemcode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.item_master
                        SET deleted = true, isactive = false
                        WHERE itemcode = @itemcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, new { itemcode, tenantcode });
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Item delete failed: " + ex.Message);
            }
        }

        public async Task<IEnumerable<item_master>> GetAllItems(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<item_master>(@"
                SELECT * FROM public.item_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY itemcode DESC;",
                new { tenantcode });
        }

        public async Task<item_master?> GetItemByCode(long itemcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryFirstOrDefaultAsync<item_master>(@"
                SELECT * FROM public.item_master
                WHERE itemcode = @itemcode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { itemcode, tenantcode });
        }

        // ─── VENDOR MASTER ────────────────────────────────────────────────────────────

        public async Task<long> UpsertVendor(vendor_master vendor)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    if (vendor.vendorcode == 0)
                    {
                        // Insert
                        long id = await db.InsertAsync(vendor);
                        return id;
                    }
                    else
                    {
                        // Update
                        bool updated = await db.UpdateAsync(vendor);

                        if (updated)
                            return vendor.vendorcode;
                        else
                            throw new Exception("Vendor not found");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor upsert failed: " + ex.Message);
            }
        }

        public async Task<string> UpdateVendor(vendor_master vendor)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.vendor_master
                        SET
                            vendorname            = @vendorname,
                            shortname             = @shortname,
                            vendortype            = @vendortype,
                            contactperson         = @contactperson,
                            phonenumber           = @phonenumber,
                            alternatephonenumber  = @alternatephonenumber,
                            emailid               = @emailid,
                            website               = @website,
                            gstnumber             = @gstnumber,
                            pannumber             = @pannumber,
                            taxid                 = @taxid,
                            registrationnumber    = @registrationnumber,
                            addressline1          = @addressline1,
                            addressline2          = @addressline2,
                            landmark              = @landmark,
                            city                  = @city,
                            district              = @district,
                            state                 = @state,
                            postalcode            = @postalcode,
                            countrycode           = @countrycode,
                            countryname           = @countryname,
                            currencycode          = @currencycode,
                            paymentterms          = @paymentterms,
                            creditperiod          = @creditperiod,
                            bankname              = @bankname,
                            accountnumber         = @accountnumber,
                            ifsccode              = @ifsccode,
                            swiftcode             = @swiftcode,
                            ibannumber            = @ibannumber,
                            isactive              = @isactive,
                            deleted               = @deleted,
                            modifieddate          = CURRENT_TIMESTAMP,
                            usercode              = @usercode,
                            tenantcode            = @tenantcode,
                            branchcode            = @branchcode
                        WHERE vendorcode = @vendorcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, vendor);
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor update failed: " + ex.Message);
            }
        }

        public async Task<string> DeleteVendor(long vendorcode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.vendor_master
                        SET deleted = true, isactive = false, modifieddate = CURRENT_TIMESTAMP
                        WHERE vendorcode = @vendorcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, new { vendorcode, tenantcode });
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Vendor delete failed: " + ex.Message);
            }
        }

        public async Task<IEnumerable<vendor_master>> GetAllVendors(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<vendor_master>(@"
                SELECT * FROM public.vendor_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY vendorcode DESC;",
                new { tenantcode });
        }

        public async Task<vendor_master?> GetVendorByCode(long vendorcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryFirstOrDefaultAsync<vendor_master>(@"
                SELECT * FROM public.vendor_master
                WHERE vendorcode = @vendorcode
                  AND tenantcode = @tenantcode
                  AND deleted = false;",
                new { vendorcode, tenantcode });
        }

        // ─── PURCHASE MASTER ──────────────────────────────────────────────────────────
        private async Task<long> GetNextGrnCode(IDbConnection db, IDbTransaction transaction, string tenantcode)
        {
            int year = DateTime.Now.Year;
            long prefix = (long)year * 10000;   // e.g. 2026 -> 20260000

            // Serialize concurrent inserts for the same tenant+year (released automatically at tx end)
            await db.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtext(@lockkey));",
                new { lockkey = tenantcode + "_" + year },
                transaction);

            string query = @"
        SELECT MAX(billno::bigint)
        FROM public.purchase_master
        WHERE tenantcode = @tenantcode
          AND billno ~ '^[0-9]+$'
          AND billno::bigint >= @prefix
          AND billno::bigint <  @prefixEnd;";

            long? maxCode = await db.ExecuteScalarAsync<long?>(
                query,
                new
                {
                    tenantcode,
                    prefix,
                    prefixEnd = prefix + 10000
                },
                transaction);

            return maxCode.HasValue ? maxCode.Value + 1 : prefix + 1;
        }

        public async Task<long> InsertPurchase(purchase_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        long billno = await GetNextGrnCode(
     db,
     transaction,
     request.master.tenantcode
 );

                        // Numeric characters only
                        request.master.billno = billno.ToString();
                        // Purchase Master Insert
                        string masterQuery = @"
    INSERT INTO purchase_master
    (
        billno,
        billdate,
        invoiceno,
        invoicedate,
        vendorcode,
        grossamount,
        overalldiscount,
        discountamount,
        totalamount,
        taxamount,
        netamount,
        paymentmode,
        paymentstatus,
        currencycode,
        remarks,
        isactive,
        deleted,
        createddate,
        usercode,
        tenantcode,
        branchcode,
        companycode,
        grncode
    )
    VALUES
    (
        @billno,
        @billdate,
        @invoiceno,
        @invoicedate,
        @vendorcode,
        @grossamount,
        @overalldiscount,
        @discountamount,
        @totalamount,
        @taxamount,
        @netamount,
        @paymentmode,
        @paymentstatus,
        @currencycode,
        @remarks,
        @isactive,
        @deleted,
        CURRENT_TIMESTAMP,
        @usercode,
        @tenantcode,
        @branchcode,
        @companycode,
        @grncode
    )
    RETURNING purchasecode;";

                        long purchasecode = await db.ExecuteScalarAsync<long>(
                            masterQuery,
                            request.master,
                            transaction);


                        // Purchase Detail Insert
                        string detailQuery = @"
            INSERT INTO purchase_detail
   (
       purchasecode, itemcode, quantity, freequantity, uomcode, rate,
       discountpercentage, discountamount, taxpercentage, taxamount,
       amount, totalamount, batchno, manufacturingdate, expirydate,
       orderedqty, receivedqty, rejectedqty, warehousecode,
       packsize, packg, mrp,
       tenantcode
   )
   VALUES
   (
       @purchasecode, @itemcode, @quantity, @freequantity, @uomcode, @rate,
       @discountpercentage, @discountamount, @taxpercentage, @taxamount,
       @amount, @totalamount, @batchno, @manufacturingdate, @expirydate,
       @orderedqty, @receivedqty, @rejectedqty, @warehousecode,
       @packsize, @packg, @mrp,
       @tenantcode
   );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = purchasecode;
                            item.packg = item.quantity * item.packsize;   // total strips/units = qty * packsize

                            await db.ExecuteAsync(
                                detailQuery,
                                item,
                                transaction);

                            // Guard against divide-by-zero
                            if (item.packsize <= 0)
                                throw new Exception($"Invalid packsize for item {item.itemcode}: packsize must be greater than 0");

                            decimal unitPrice = item.rate / item.packsize;   // strip/unit price
                            decimal mrpUnit = item.mrp / item.packsize;

                            // Check stock exists
                            string checkStockQuery = @"
       SELECT stockcode
       FROM stock_master
       WHERE itemcode = @itemcode
       AND warehousecode = @warehousecode
       AND batchno = @batchno;";

                            var stockcode = await db.ExecuteScalarAsync<long?>(
                                checkStockQuery,
                                new
                                {
                                    item.itemcode,
                                    item.warehousecode,
                                    item.batchno
                                },
                                transaction);

                            // Update Existing Stock
                            if (stockcode.HasValue)
                            {
                                string updateStockQuery = @"
   UPDATE stock_master
   SET
       purchasedqty = purchasedqty + @packg,
       closingstock = closingstock + @packg,
       unitcost = @unitPrice,
       mrpunit = @mrpUnit,
       stockvalue =
           (closingstock + @packg) * @unitPrice,
       modifieddate = CURRENT_TIMESTAMP
   WHERE stockcode = @stockcode;";

                                await db.ExecuteAsync(
                                    updateStockQuery,
                                    new
                                    {
                                        stockcode = stockcode.Value,
                                        item.packg,
                                        unitPrice,
                                        mrpUnit
                                    },
                                    transaction);
                            }
                            else
                            {
                                string insertStockQuery = @"
   INSERT INTO stock_master
   (
       itemcode,
       warehousecode,
       branchcode,
       openingstock,
       purchasedqty,
       soldqty,
       damagedqty,
       returnqty,
       closingstock,
       unitcost,
       mrpunit,
       stockvalue,
       batchno,
       manufacturingdate,
       expirydate,
       isactive,
       deleted,
       createddate,
       usercode,
       tenantcode,
       companycode
   )
   VALUES
   (
       @itemcode,
       @warehousecode,
       @branchcode,
       0,
       @packg,
       0,
       0,
       0,
       @packg,
       @unitPrice,
       @mrpUnit,
       (@packg * @unitPrice),
       @batchno,
       @manufacturingdate,
       @expirydate,
       true,
       false,
       CURRENT_TIMESTAMP,
       @usercode,
       @tenantcode,
       @companycode
   );";

                                await db.ExecuteAsync(
                                    insertStockQuery,
                                    new
                                    {
                                        item.itemcode,
                                        item.warehousecode,
                                        branchcode = request.master.branchcode,
                                        item.packg,
                                        unitPrice,
                                        mrpUnit,
                                        item.batchno,
                                        item.manufacturingdate,
                                        item.expirydate,
                                        usercode = request.master.usercode,
                                        tenantcode = request.master.tenantcode,
                                        companycode = request.master.companycode
                                    },
                                    transaction);
                            }
                        }

                        transaction.Commit();

                        return purchasecode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Purchase Insert Failed : " + ex.Message);
                    }
                }
            }
        }

        public async Task<long> UpdatePurchase(purchase_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        string masterQuery = @"
                UPDATE public.purchase_master
                SET
                    billno                 = @billno,
                    billdate               = @billdate,
                    invoiceno              = @invoiceno,
                    invoicedate            = @invoicedate,
                    vendorcode             = @vendorcode,
                    grossamount            = @grossamount,
                    overalldiscount        = @overalldiscount,
                    discountamount         = @discountamount,
                    totalamount            = @totalamount,
                    taxamount              = @taxamount,
                    netamount              = @netamount,
                    transportationcharges  = @transportationcharges,
                    roundoff               = @roundoff,
                    paymentmode            = @paymentmode,
                    paymentstatus          = @paymentstatus,
                    currencycode           = @currencycode,
                    isactive               = @isactive,
                    deleted                = @deleted,
                    remarks                = @remarks,
                    modifieddate           = CURRENT_TIMESTAMP,
                    usercode               = @usercode,
                    tenantcode             = @tenantcode,
                    branchcode             = @branchcode,
                    companycode            = @companycode,
                    grncode                = @grncode
                WHERE purchasecode = @purchasecode
                  AND tenantcode = @tenantcode;";

                        int masterRows = await db.ExecuteAsync(
                            masterQuery,
                            request.master,
                            transaction);

                        if (masterRows == 0)
                            throw new Exception("Purchase record not found.");

                        await db.ExecuteAsync(
                            @"DELETE FROM public.purchase_detail
                      WHERE purchasecode = @purchasecode;",
                            new
                            {
                                purchasecode = request.master.purchasecode
                            },
                            transaction);

                        string detailQuery = @"
               INSERT INTO public.purchase_detail
    (
        purchasecode, itemcode, quantity, freequantity, uomcode, rate,
        discountpercentage, discountamount, taxpercentage, taxamount,
        amount, totalamount, batchno, manufacturingdate, expirydate,
        orderedqty, receivedqty, rejectedqty, warehousecode,
        packaging, manufacturercode,
        packsize, packg, mrp,
        tenantcode
    )
    VALUES
    (
        @purchasecode, @itemcode, @quantity, @freequantity, @uomcode, @rate,
        @discountpercentage, @discountamount, @taxpercentage, @taxamount,
        @amount, @totalamount, @batchno, @manufacturingdate, @expirydate,
        @orderedqty, @receivedqty, @rejectedqty, @warehousecode,
        @packaging, @manufacturercode,
        @packsize, @packg, @mrp,
        @tenantcode
    );";

                        foreach (var item in request.details)
                        {
                            item.purchasecode = request.master.purchasecode;
                            item.packg = item.quantity * item.packsize;

                            await db.ExecuteAsync(
                                detailQuery,
                                item,
                                transaction);
                        }

                        transaction.Commit();

                        return request.master.purchasecode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Purchase update failed: {ex.Message}");
                    }
                }
            }
        }

        public async Task<string> DeletePurchase(long purchasecode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        int masterRows = await db.ExecuteAsync(@"
                UPDATE public.purchase_master
                SET
                    deleted = true,
                    isactive = false,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE purchasecode = @purchasecode
                  AND tenantcode = @tenantcode;",
                            new
                            {
                                purchasecode,
                                tenantcode
                            },
                            transaction);

                        await db.ExecuteAsync(
                            @"DELETE FROM public.purchase_detail
                      WHERE purchasecode = @purchasecode;",
                            new { purchasecode },
                            transaction);

                        transaction.Commit();

                        return masterRows > 0 ? "Success" : "Failed";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Purchase delete failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<IEnumerable<purchase_request>> GetAllPurchases(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var query = @"
    SELECT
    m.purchasecode,
    m.billno,
    m.billdate::timestamp,
    m.invoiceno,
    m.invoicedate::timestamp,   -- ← cast nullable date
    m.vendorcode,
    m.grossamount,
    m.overalldiscount,      
    m.discountamount,
    m.totalamount,
    m.taxamount,
    m.netamount,
    m.paymentmode,
    m.paymentstatus,
    m.currencycode,
    m.isactive,
    m.deleted,
    m.remarks,
    m.createddate::timestamp,
    m.modifieddate::timestamp,
    m.usercode,
    m.tenantcode,
    m.branchcode,
    m.companycode,
    m.grncode,
    m.transportationcharges,
    m.roundoff,
    
    d.purchasedetailcode,
    d.purchasecode,
    d.itemcode,
    d.quantity,
    d.freequantity,
    d.uomcode,
    d.rate,
    d.discountpercentage,
    d.discountamount,
    d.taxpercentage,
    d.taxamount,
    d.amount,
    d.totalamount,
    d.batchno,
    d.manufacturingdate::timestamp,   -- ← cast
    d.expirydate::timestamp,          -- ← cast
    d.orderedqty,
    d.receivedqty,
    d.rejectedqty,
    d.warehousecode,
    d.tenantcode,
    d.packaging,
    d.manufacturercode,
    d.packsize,
    d.packg,
    d.mrp

    FROM public.purchase_master m

    LEFT JOIN public.purchase_detail d
        ON m.purchasecode = d.purchasecode

    WHERE m.deleted = false
      AND m.tenantcode = @tenantcode

    ORDER BY m.purchasecode DESC;";

            var purchaseDictionary = new Dictionary<long, purchase_request>();

            await db.QueryAsync<purchase_master, purchase_detail, purchase_request>(
                query,
                (master, detail) =>
                {
                    if (!purchaseDictionary.TryGetValue(master.purchasecode, out var purchase))
                    {
                        purchase = new purchase_request
                        {
                            master = master,
                            details = new List<purchase_detail>()
                        };

                        purchaseDictionary.Add(master.purchasecode, purchase);
                    }

                    if (detail != null && detail.purchasedetailcode != 0)
                    {
                        purchase.details.Add(detail);
                    }

                    return purchase;
                },
                new { tenantcode },
                splitOn: "purchasedetailcode"
            );

            return purchaseDictionary.Values.ToList();
        }

        public async Task<purchase_request?> GetPurchaseByCode(
            long purchasecode,
            string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var query = @"
    SELECT
        m.purchasecode,
        m.billno,
        m.billdate,
        m.invoiceno,
        m.invoicedate,
        m.vendorcode,
        m.grossamount,
        m.discountamount,
        m.taxamount,
        m.netamount,
        m.paymentmode,
        m.paymentstatus,
        m.currencycode,
        m.isactive,
        m.deleted,
        m.remarks,
        m.createddate,
        m.modifieddate,
        m.usercode,
        m.tenantcode,
        m.branchcode,
        m.companycode,
        m.grncode,

        d.purchasedetailcode,
        d.purchasecode,
        d.itemcode,
        d.quantity,
        d.freequantity,
        d.uomcode,
        d.rate,
        d.discountpercentage,
        d.discountamount,
        d.taxpercentage,
        d.taxamount,
        d.amount,
        d.totalamount,
        d.batchno,
        d.manufacturingdate,
        d.expirydate,
        d.orderedqty,
        d.receivedqty,
        d.rejectedqty,
        d.warehousecode,
        d.tenantcode,
        d.packsize,
        d.packg,
        d.mrp

    FROM public.purchase_master m

    LEFT JOIN public.purchase_detail d
        ON m.purchasecode = d.purchasecode

    WHERE m.purchasecode = @purchasecode
      AND m.tenantcode = @tenantcode
      AND m.deleted = false;";

            purchase_request? response = null;

            await db.QueryAsync<purchase_master, purchase_detail, purchase_request>(
                query,
                (master, detail) =>
                {
                    if (response == null)
                    {
                        response = new purchase_request
                        {
                            master = master,
                            details = new List<purchase_detail>()
                        };
                    }

                    if (detail != null && detail.purchasedetailcode != 0)
                    {
                        response.details.Add(detail);
                    }

                    return response;
                },
                new
                {
                    purchasecode,
                    tenantcode
                },
                splitOn: "purchasedetailcode"
            );

            return response;
        }
        // ─── STOCK MASTER ─────────────────────────────────────────────────────────────

        public async Task<long> InsertStock(stock_master stock)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        INSERT INTO public.stock_master
                        (
                            itemcode, warehousecode, branchcode, locationcode,
                            openingstock,openingstrip, openingloose, purchasedqty, soldqty, damagedqty, returnqty, closingstock,
                            unitcost, stockvalue,
                            batchno, manufacturingdate, expirydate,
                            isactive, deleted, createddate,
                            usercode, tenantcode, companycode
                        )
                        VALUES
                        (
                            @itemcode, @warehousecode, @branchcode, @locationcode,
                            @openingstock, @openingstrip, @openingloose,@purchasedqty, @soldqty, @damagedqty, @returnqty, @closingstock,
                            @unitcost, @stockvalue,
                            @batchno, @manufacturingdate, @expirydate,
                            @isactive, @deleted, CURRENT_TIMESTAMP,
                            @usercode, @tenantcode, @companycode
                        )
                        RETURNING stockcode;";

                    return await db.ExecuteScalarAsync<long>(query, stock);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock insert failed: " + ex.Message);
            }
        }

        public async Task<string> UpdateStock(stock_master stock)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.stock_master
                        SET
                            itemcode          = @itemcode,
                            warehousecode     = @warehousecode,
                            branchcode        = @branchcode,
                            locationcode      = @locationcode,
                            openingstock      = @openingstock,
                            openingstrip      = @openingstrip,
                            openingloose      = @openingloose,
                            purchasedqty      = @purchasedqty,
                            soldqty           = @soldqty,
                            damagedqty        = @damagedqty,
                            returnqty         = @returnqty,
                            closingstock      = @closingstock,
                            unitcost          = @unitcost,
                            stockvalue        = @stockvalue,
                            batchno           = @batchno,
                            manufacturingdate = @manufacturingdate,
                            expirydate        = @expirydate,
                            isactive          = @isactive,
                            deleted           = @deleted,
                            modifieddate      = CURRENT_TIMESTAMP,
                            usercode          = @usercode,
                            tenantcode        = @tenantcode,
                            companycode       = @companycode
                        WHERE stockcode = @stockcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, stock);
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock update failed: " + ex.Message);
            }
        }

        public async Task<string> DeleteStock(long stockcode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                        UPDATE public.stock_master
                        SET deleted = true, isactive = false, modifieddate = CURRENT_TIMESTAMP
                        WHERE stockcode = @stockcode
                          AND tenantcode = @tenantcode;";

                    int rows = await db.ExecuteAsync(query, new { stockcode, tenantcode });
                    return rows > 0 ? "Success" : "Failed";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Stock delete failed: " + ex.Message);
            }
        }

        public async Task<IEnumerable<stock_master>> GetAllStocks(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            return await db.QueryAsync<stock_master>(@"
        SELECT 
            stockcode,
            itemcode,
            warehousecode,
            branchcode,
            locationcode,
            openingstock,
            openingstrip,
            openingloose,
            purchasedqty,
            soldqty,
            damagedqty,
            returnqty,
            closingstock,
            unitcost,
            mrpunit,
            stockvalue,
            batchno,

            manufacturingdate::timestamp AS manufacturingdate,
            expirydate::timestamp AS expirydate,

            isactive,
            deleted,
            createddate,
            modifieddate,
            usercode,
            tenantcode,
            companycode
        FROM public.stock_master
        WHERE deleted = false
          AND tenantcode = @tenantcode
        ORDER BY stockcode DESC;",
                new { tenantcode });
        }

        public async Task<stock_master?> GetStockByCode(long stockcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            return await db.QueryFirstOrDefaultAsync<stock_master>(@"
        SELECT 
            stockcode,
            itemcode,
            warehousecode,
            branchcode,
            locationcode,
            openingstock,
            openingstrip,
            openingloose,
            purchasedqty,
            soldqty,
            damagedqty,
            returnqty,
            closingstock,
            unitcost,
            mrpunit,
            stockvalue,
            batchno,

            manufacturingdate::timestamp AS manufacturingdate,
            expirydate::timestamp AS expirydate,

            isactive,
            deleted,
            createddate,
            modifieddate,
            usercode,
            tenantcode,
            companycode
        FROM public.stock_master
        WHERE stockcode = @stockcode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { stockcode, tenantcode });
        }

        public async Task<IEnumerable<stock_master>> GetStockByItem(long itemcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            return await db.QueryAsync<stock_master>(@"
        SELECT 
            stockcode,
            itemcode,
            warehousecode,
            branchcode,
            locationcode,
            openingstock,
            openingstrip,
            openingloose,
            purchasedqty,
            soldqty,
            damagedqty,
            returnqty,
            closingstock,
            unitcost,
            mrpunit,
            stockvalue,
            batchno,

            manufacturingdate::timestamp AS manufacturingdate,
            expirydate::timestamp AS expirydate,

            isactive,
            deleted,
            createddate,
            modifieddate,
            usercode,
            tenantcode,
            companycode
        FROM public.stock_master
        WHERE itemcode = @itemcode
          AND tenantcode = @tenantcode
          AND deleted = false
        ORDER BY stockcode DESC;",
                new { itemcode, tenantcode });
        }

        public async Task<long> InsertIndent(indent_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        string masterQuery = @"
                    INSERT INTO public.indent_master
                    (
                        indentno, indentdate, requestedby,
                        departmentcode, branchcode, remarks,
                        approvalstatus, isactive, deleted,
                        createddate, tenantcode
                    )
                    VALUES
                    (
                        @indentno, @indentdate, @requestedby,
                        @departmentcode, @branchcode, @remarks,
                        @approvalstatus, @isactive, @deleted,
                        CURRENT_TIMESTAMP, @tenantcode
                    )
                    RETURNING indentcode;";

                        long indentcode = await db.ExecuteScalarAsync<long>(masterQuery, request.master, transaction);

                        string detailQuery = @"
                    INSERT INTO public.indent_detail
                    (indentcode, itemcode, purchasedetailcode, requestedqty, approvedqty, issuedqty, remarks)
                    VALUES
                    (@indentcode, @itemcode, @purchasedetailcode, @requestedqty, @approvedqty, @issuedqty, @remarks);";

                        foreach (var item in request.details)
                        {
                            item.indentcode = indentcode;
                            await db.ExecuteAsync(detailQuery, item, transaction);
                        }

                        if (string.Equals(request.master.approvalstatus, "Approved", StringComparison.OrdinalIgnoreCase))
                        {
                            await IssueIndentStock(db, transaction, request.details, request.master.tenantcode);
                        }

                        transaction.Commit();
                        return indentcode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Indent insert failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<long> UpdateIndent(
indent_request request,
string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // =====================================================
                        // 1. Update Master
                        // =====================================================

                        string updateMaster = @"
              UPDATE public.indent_master
              SET
                  indentno = @indentno,
                  indentdate = @indentdate,
                  requestedby = @requestedby,
                  departmentcode = @departmentcode,
                  branchcode = @branchcode,
                  remarks = @remarks,
                  approvalstatus = @approvalstatus
              WHERE indentcode = @indentcode
                AND tenantcode = @tenantcode;
          ";

                        int masterRows = await db.ExecuteAsync(
                            updateMaster,
                            new
                            {
                                indentno = request.master.indentno,
                                indentdate = request.master.indentdate,
                                requestedby = request.master.requestedby,
                                departmentcode = request.master.departmentcode,
                                branchcode = request.master.branchcode,
                                remarks = request.master.remarks,
                                approvalstatus = request.master.approvalstatus,
                                indentcode = request.master.indentcode,
                                tenantcode = tenantcode
                            },
                            transaction
                        );

                        // =====================================================
                        // IMPORTANT: Check whether master was actually updated
                        // =====================================================

                        if (masterRows == 0)
                        {
                            throw new Exception(
                                $"Indent master not found. " +
                                $"IndentCode: {request.master.indentcode}, " +
                                $"TenantCode: {tenantcode}"
                            );
                        }

                        // =====================================================
                        // 2. Delete Existing Details
                        // =====================================================

                        string deleteDetail = @"
              DELETE FROM public.indent_detail
              WHERE indentcode = @indentcode;
          ";

                        await db.ExecuteAsync(
                            deleteDetail,
                            new
                            {
                                indentcode = request.master.indentcode
                            },
                            transaction
                        );

                        // =====================================================
                        // 3. Insert Details
                        // =====================================================

                        string insertDetail = @"
    INSERT INTO public.indent_detail
    (
        indentcode,
        itemcode,
        purchasedetailcode,
        requestedqty,
        approvedqty,
        issuedqty,
        remarks
    )
    VALUES
    (
        @indentcode,
        @itemcode,
        @purchasedetailcode,
        @requestedqty,
        @approvedqty,
        @issuedqty,
        @remarks
    );
";

                        foreach (var item in request.details)
                        {
                            item.indentcode = request.master.indentcode;

                            await db.ExecuteAsync(
                                insertDetail,
                                new
                                {
                                    indentcode = item.indentcode,
                                    itemcode = item.itemcode,
                                    purchasedetailcode = item.purchasedetailcode,
                                    requestedqty = item.requestedqty,
                                    approvedqty = item.approvedqty,
                                    issuedqty = item.issuedqty,
                                    remarks = item.remarks
                                },
                                transaction
                            );
                        }

                        // =====================================================
                        // 4. Commit
                        // =====================================================

                        transaction.Commit();

                        return request.master.indentcode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();

                        throw new Exception(
                            "Indent update failed: " + ex.Message,
                            ex
                        );
                    }
                }
            }
        }

        // ── Helper: draw issued quantity down from the specific purchase batch,
        //     and mirror the reduction onto stock_master (same pattern as InsertPurchaseReturn) ──
        private async Task IssueIndentStock(
            IDbConnection db, IDbTransaction transaction,
            IEnumerable<indent_detail> details, string tenantcode)
        {
            foreach (var item in details)
            {
                if (item.issuedqty <= 0) continue;

                // Pull the specific purchase batch this indent item is issued against.
                var purchase = await db.QueryFirstOrDefaultAsync<purchase_detail>(@"
            SELECT
                purchasedetailcode, purchasecode, itemcode,
                receivedqty,
                COALESCE(returnedqty, 0) AS returnedqty,
                COALESCE(issuedqty, 0) AS issuedqty,
                batchno, warehousecode, rate, tenantcode
            FROM public.purchase_detail
            WHERE purchasedetailcode = @purchasedetailcode
              AND tenantcode = @tenantcode;",
                    new { item.purchasedetailcode, tenantcode }, transaction);

                if (purchase == null)
                    throw new Exception($"Purchase batch {item.purchasedetailcode} not found for item {item.itemcode}");

                decimal availableqty = purchase.receivedqty - purchase.returnedqty - purchase.issuedqty;

                if (item.issuedqty > availableqty)
                    throw new Exception($"Cannot issue {item.issuedqty} for item {item.itemcode}; only {availableqty} available in batch {purchase.batchno}");

                // 1) Update purchase_detail — track cumulative issued qty against this batch
                await db.ExecuteAsync(@"
            UPDATE public.purchase_detail
            SET issuedqty = COALESCE(issuedqty, 0) + @issuedqty
            WHERE purchasedetailcode = @purchasedetailcode;",
                    new { item.issuedqty, item.purchasedetailcode }, transaction);

                // 2) Update stock_master — reduce closing stock for that item/batch/warehouse
                int rows = await db.ExecuteAsync(@"
            UPDATE public.stock_master
            SET
                closingstock = closingstock - @issuedqty,
                stockvalue   = (closingstock - @issuedqty) * unitcost,
                modifieddate = CURRENT_TIMESTAMP
            WHERE itemcode = @itemcode
              AND batchno = @batchno
              AND warehousecode = @warehousecode
              AND tenantcode = @tenantcode;",
                    new
                    {
                        item.issuedqty,
                        item.itemcode,
                        purchase.batchno,
                        purchase.warehousecode,
                        tenantcode
                    }, transaction);

                if (rows == 0)
                    throw new Exception($"Stock not found for item {item.itemcode}, batch {purchase.batchno}, warehouse {purchase.warehousecode}");
            }
        }

        public async Task<string> DeleteIndent(long indentcode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        await db.ExecuteAsync(@"
                            UPDATE public.indent_master
                            SET deleted = true, isactive = false
                            WHERE indentcode = @indentcode
                              AND tenantcode = @tenantcode;",
                            new { indentcode, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM public.indent_detail WHERE indentcode = @indentcode;",
                            new { indentcode }, transaction);

                        transaction.Commit();
                        return "Success";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Indent delete failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<IEnumerable<object>> GetAllIndents(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var masters = await db.QueryAsync<indent_master>(@"
        SELECT *
        FROM indent_master
        WHERE deleted = false
          AND tenantcode = @tenantcode
        ORDER BY indentcode DESC;",
                new { tenantcode });

            var result = new List<object>();

            foreach (var master in masters)
            {
                var details = await db.QueryAsync<indent_detail>(@"
            SELECT *
            FROM indent_detail
            WHERE indentcode = @indentcode;",
                    new
                    {
                        indentcode = master.indentcode,
                        tenantcode
                    });

                result.Add(new
                {
                    master,
                    details
                });
            }

            return result;
        }

        public async Task<object?> GetIndentByCode(long indentcode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<indent_master>(@"
        SELECT *
        FROM indent_master
        WHERE indentcode = @indentcode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { indentcode, tenantcode });

            if (master == null)
                return null;

            var details = await db.QueryAsync<indent_detail>(@"
        SELECT *
        FROM indent_detail
        WHERE indentcode = @indentcode;",
                new { indentcode, tenantcode });

            return new
            {
                master,
                details
            };
        }

        // ─── PURCHASE ENTRY (GRN) ─────────────────────────────────────────────────────

        public async Task<long> InsertPurchaseEntry(purchase_entry_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        var purchaseentrycode = await db.ExecuteScalarAsync<long>(@"
                            INSERT INTO purchase_entry_master
                            (
                                grnno, grndate, receivedby,
                                billno, billdate, invoiceno, invoicedate,
                                vendorcode,
                                totalqty, receivedqty,
                                grossamount, discountamount, taxamount, othercharges, netamount,
                                paymentmode, paymentstatus,
                                approvalstatus, posted,
                                remarks, isactive, deleted,
                                createddate, usercode,
                                tenantcode, branchcode, companycode
                            )
                            VALUES
                            (
                                @grnno, @grndate, @receivedby,
                                @billno, @billdate, @invoiceno, @invoicedate,
                                @vendorcode,
                                @totalqty, @receivedqty,
                                @grossamount, @discountamount, @taxamount, @othercharges, @netamount,
                                @paymentmode, @paymentstatus,
                                'PENDING', false,
                                @remarks, @isactive, @deleted,
                                CURRENT_TIMESTAMP, @usercode,
                                @tenantcode, @branchcode, @companycode
                            )
                            RETURNING purchaseentrycode;",
                            request.master, transaction);

                        foreach (var item in request.details)
                        {
                            item.purchaseentrycode = purchaseentrycode;

                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_entry_detail
                                (
                                    purchaseentrycode, itemcode,
                                    orderedqty, receivedqty, rejectedqty, quantity,
                                    rate, discountpercentage, discountamount,
                                    taxpercentage, taxamount,
                                    amount, totalamount,
                                    batchno, manufacturingdate, expirydate,
                                    warehousecode, tenantcode
                                )
                                VALUES
                                (
                                    @purchaseentrycode, @itemcode,
                                    @orderedqty, @receivedqty, @rejectedqty, @quantity,
                                    @rate, @discountpercentage, @discountamount,
                                    @taxpercentage, @taxamount,
                                    @amount, @totalamount,
                                    @batchno, @manufacturingdate, @expirydate,
                                    @warehousecode, @tenantcode
                                );",
                                item, transaction);
                        }

                        var purchasecode = await db.ExecuteScalarAsync<long>(@"
                            INSERT INTO purchase_master
                            (
                                billno, billdate, invoiceno, invoicedate,
                                vendorcode,
                                grossamount, discountamount, taxamount, netamount,
                                paymentmode, paymentstatus, currencycode,
                                isactive, deleted, remarks,
                                createddate, usercode,
                                tenantcode, branchcode, companycode,
                                grncode
                            )
                            VALUES
                            (
                                @billno, @billdate, @invoiceno, @invoicedate,
                                @vendorcode,
                                @grossamount, @discountamount, @taxamount, @netamount,
                                @paymentmode, @paymentstatus, @currencycode,
                                @isactive, @deleted, @remarks,
                                CURRENT_TIMESTAMP, @usercode,
                                @tenantcode, @branchcode, @companycode,
                                @grncode
                            )
                            RETURNING purchasecode;",
                            new
                            {
                                request.master.billno,
                                request.master.billdate,
                                request.master.invoiceno,
                                request.master.invoicedate,
                                request.master.vendorcode,
                                request.master.grossamount,
                                request.master.discountamount,
                                request.master.taxamount,
                                request.master.netamount,
                                request.master.paymentmode,
                                request.master.paymentstatus,
                                currencycode = "INR",
                                request.master.isactive,
                                request.master.deleted,
                                request.master.remarks,
                                request.master.usercode,
                                request.master.tenantcode,
                                request.master.branchcode,
                                request.master.companycode,
                                grncode = purchaseentrycode
                            },
                            transaction);

                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_detail
                                (
                                    purchasecode, itemcode, quantity,
                                    rate, discountpercentage, discountamount,
                                    taxpercentage, taxamount,
                                    amount, totalamount,
                                    batchno, manufacturingdate, expirydate,
                                    tenantcode
                                )
                                VALUES
                                (
                                    @purchasecode, @itemcode, @qty,
                                    @rate, @discountpercentage, @discountamount,
                                    @taxpercentage, @taxamount,
                                    @amount, @totalamount,
                                    @batchno, @manufacturingdate, @expirydate,
                                    @tenantcode
                                );",
                                new
                                {
                                    purchasecode,
                                    item.itemcode,
                                    qty = item.receivedqty,
                                    item.rate,
                                    item.discountpercentage,
                                    item.discountamount,
                                    item.taxpercentage,
                                    item.taxamount,
                                    item.amount,
                                    item.totalamount,
                                    item.batchno,
                                    item.manufacturingdate,
                                    item.expirydate,
                                    item.tenantcode
                                },
                                transaction);
                        }

                        transaction.Commit();
                        return purchaseentrycode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Insert failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<string> UpdatePurchaseEntry(purchase_entry_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        var entryId = request.master.purchaseentrycode;
                        var tenantcode = request.master.tenantcode;

                        var oldItems = await db.QueryAsync<purchase_entry_detail>(
                            "SELECT * FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = entryId }, transaction);

                        foreach (var item in oldItems)
                        {
                            await db.ExecuteAsync(@"
                                UPDATE stock_master
                                SET purchasedqty = purchasedqty - @qty,
                                    closingstock = closingstock - @qty
                                WHERE itemcode = @itemcode
                                  AND tenantcode = @tenantcode;",
                                new { qty = item.receivedqty, item.itemcode, tenantcode }, transaction);
                        }

                        await db.ExecuteAsync(@"
                            UPDATE purchase_entry_master
                            SET
                                billno         = @billno,
                                billdate       = @billdate,
                                invoiceno      = @invoiceno,
                                invoicedate    = @invoicedate,
                                vendorcode     = @vendorcode,
                                totalqty       = @totalqty,
                                receivedqty    = @receivedqty,
                                grossamount    = @grossamount,
                                discountamount = @discountamount,
                                taxamount      = @taxamount,
                                netamount      = @netamount,
                                paymentmode    = @paymentmode,
                                paymentstatus  = @paymentstatus,
                                remarks        = @remarks,
                                modifieddate   = CURRENT_TIMESTAMP
                            WHERE purchaseentrycode = @purchaseentrycode
                              AND tenantcode = @tenantcode;",
                            request.master, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = entryId }, transaction);

                        foreach (var item in request.details)
                        {
                            item.purchaseentrycode = entryId;

                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_entry_detail
                                (purchaseentrycode, itemcode, receivedqty, rate, amount, totalamount, tenantcode)
                                VALUES
                                (@purchaseentrycode, @itemcode, @receivedqty, @rate, @amount, @totalamount, @tenantcode);",
                                item, transaction);
                        }

                        await db.ExecuteAsync(@"
                            UPDATE purchase_master
                            SET
                                billno         = @billno,
                                billdate       = @billdate,
                                invoiceno      = @invoiceno,
                                invoicedate    = @invoicedate,
                                vendorcode     = @vendorcode,
                                grossamount    = @grossamount,
                                discountamount = @discountamount,
                                taxamount      = @taxamount,
                                netamount      = @netamount,
                                paymentmode    = @paymentmode,
                                paymentstatus  = @paymentstatus,
                                remarks        = @remarks,
                                modifieddate   = CURRENT_TIMESTAMP
                            WHERE grncode = @purchaseentrycode
                              AND tenantcode = @tenantcode;",
                            request.master, transaction);

                        var purchasecode = await db.ExecuteScalarAsync<long>(
                            "SELECT purchasecode FROM purchase_master WHERE grncode = @id AND tenantcode = @tenantcode",
                            new { id = entryId, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_detail WHERE purchasecode = @pc",
                            new { pc = purchasecode }, transaction);

                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                                INSERT INTO purchase_detail
                                (purchasecode, itemcode, quantity, rate, totalamount, tenantcode)
                                VALUES
                                (@purchasecode, @itemcode, @qty, @rate, @totalamount, @tenantcode);",
                                new
                                {
                                    purchasecode,
                                    item.itemcode,
                                    qty = item.receivedqty,
                                    item.rate,
                                    item.totalamount,
                                    item.tenantcode
                                },
                                transaction);
                        }

                        foreach (var item in request.details)
                        {
                            await db.ExecuteAsync(@"
                                UPDATE stock_master
                                SET purchasedqty = purchasedqty + @qty,
                                    closingstock = closingstock + @qty
                                WHERE itemcode = @itemcode
                                  AND tenantcode = @tenantcode;",
                                new { qty = item.receivedqty, item.itemcode, item.tenantcode }, transaction);
                        }

                        transaction.Commit();
                        return "Success";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Update failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<string> DeletePurchaseEntry(long purchaseentrycode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        var items = await db.QueryAsync<purchase_entry_detail>(
                            "SELECT * FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = purchaseentrycode }, transaction);

                        foreach (var item in items)
                        {
                            await db.ExecuteAsync(@"
                                UPDATE stock_master
                                SET purchasedqty = purchasedqty - @qty,
                                    closingstock = closingstock - @qty
                                WHERE itemcode = @itemcode
                                  AND tenantcode = @tenantcode;",
                                new { qty = item.receivedqty, item.itemcode, tenantcode }, transaction);
                        }

                        var purchasecode = await db.ExecuteScalarAsync<long>(
                            "SELECT purchasecode FROM purchase_master WHERE grncode = @id AND tenantcode = @tenantcode",
                            new { id = purchaseentrycode, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_detail WHERE purchasecode = @pc",
                            new { pc = purchasecode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_master WHERE purchasecode = @pc AND tenantcode = @tenantcode",
                            new { pc = purchasecode, tenantcode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_detail WHERE purchaseentrycode = @id",
                            new { id = purchaseentrycode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM purchase_entry_master WHERE purchaseentrycode = @id AND tenantcode = @tenantcode",
                            new { id = purchaseentrycode, tenantcode }, transaction);

                        transaction.Commit();
                        return "Success";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Delete failed: " + ex.Message);
                    }
                }
            }
        }

        public async Task<List<purchase_entry_request>> GetAllPurchaseEntries(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var masters = (await db.QueryAsync<purchase_entry_master>(@"
        SELECT 
            purchaseentrycode,
            grnno,
            grndate,
            receivedby,
            billno,
            TO_CHAR(billdate, 'YYYY-MM-DD') AS billdate,
            TO_CHAR(invoicedate, 'YYYY-MM-DD') AS invoicedate,
            vendorcode,
            totalqty,
            receivedqty,
            grossamount,
            discountamount,
            taxamount,
            othercharges,
            netamount,
            paymentmode,
            paymentstatus,
            approvalstatus,
            posted,
            remarks,
            isactive,
            deleted,
            usercode,
            tenantcode,
            branchcode,
            companycode
        FROM public.purchase_entry_master
        WHERE deleted = false
          AND tenantcode = @tenantcode
        ORDER BY purchaseentrycode DESC;",
                new { tenantcode })).ToList();

            var details = (await db.QueryAsync<purchase_entry_detail>(@"
        SELECT 
            purchaseentrydetailcode,
            purchaseentrycode,
            itemcode,
            orderedqty,
            receivedqty,
            rejectedqty,
            quantity,
            rate,
            discountpercentage,
            discountamount,
            taxpercentage,
            taxamount,
            amount,
            totalamount,
            batchno,
            TO_CHAR(manufacturingdate, 'YYYY-MM-DD') AS manufacturingdate,
            TO_CHAR(expirydate, 'YYYY-MM-DD') AS expirydate,
            warehousecode,
            tenantcode
        FROM public.purchase_entry_detail
        WHERE tenantcode = @tenantcode;",
                new { tenantcode })).ToList();

            var result = masters.Select(m => new purchase_entry_request
            {
                master = m,
                details = details
                    .Where(d => d.purchaseentrycode == m.purchaseentrycode)
                    .ToList()
            }).ToList();

            return result;
        }
        public async Task<purchase_entry_request?> GetPurchaseEntryByCode(long purchaseentrycode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<purchase_entry_master>(@"
        SELECT 
            purchaseentrycode,
            grnno,
            grndate,
            receivedby,
            billno,
            TO_CHAR(billdate, 'YYYY-MM-DD') AS billdate,
            TO_CHAR(invoicedate, 'YYYY-MM-DD') AS invoicedate,
            vendorcode,
            totalqty,
            receivedqty,
            grossamount,
            discountamount,
            taxamount,
            othercharges,
            netamount,
            paymentmode,
            paymentstatus,
            approvalstatus,
            posted,
            remarks,
            isactive,
            deleted,
            TO_CHAR(createddate, 'YYYY-MM-DD') AS createddate,
            TO_CHAR(modifieddate, 'YYYY-MM-DD') AS modifieddate,
            usercode,
            tenantcode,
            branchcode,
            companycode
        FROM public.purchase_entry_master
        WHERE purchaseentrycode = @purchaseentrycode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { purchaseentrycode, tenantcode });

            if (master == null) return null;

            var details = await db.QueryAsync<purchase_entry_detail>(@"
        SELECT 
            purchaseentrydetailcode,
            purchaseentrycode,
            itemcode,
            orderedqty,
            receivedqty,
            rejectedqty,
            quantity,
            rate,
            discountpercentage,
            discountamount,
            taxpercentage,
            taxamount,
            amount,
            totalamount,
            batchno,
            TO_CHAR(manufacturingdate, 'YYYY-MM-DD') AS manufacturingdate,
            TO_CHAR(expirydate, 'YYYY-MM-DD') AS expirydate,
            warehousecode,
            tenantcode
        FROM public.purchase_entry_detail
        WHERE purchaseentrycode = @purchaseentrycode;",
                new { purchaseentrycode });

            return new purchase_entry_request
            {
                master = master,
                details = details.ToList()
            };
        }


        // ─── EXCEL BULK UPLOAD ────────────────────────────────────────────────────────

        // tenantcode parameter added — passed from controller header
        public async Task ProcessExcel(string filePath, string tenantcode)
        {
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                int rowCount = worksheet.Dimension.Rows;

                var items = new List<item_master>();

                for (int row = 2; row <= rowCount; row++)
                {
                    var item = new item_master
                    {
                        itemname = worksheet.Cells[row, 1].Text,
                        shortname = worksheet.Cells[row, 2].Text,
                        description = worksheet.Cells[row, 3].Text,
                        categorycode = int.Parse(worksheet.Cells[row, 4].Text),
                        subcategorycode = int.Parse(worksheet.Cells[row, 5].Text),
                        purchaserate = decimal.Parse(worksheet.Cells[row, 6].Text),
                        tenantcode = tenantcode   // set from header
                    };

                    items.Add(item);
                }

                await InsertBulk(items, tenantcode);
            }
        }

        public async Task InsertBulk(List<item_master> items, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
                    INSERT INTO item_master
                    (itemname, shortname, description, categorycode, subcategorycode, purchaserate, tenantcode)
                    VALUES
                    (@itemname, @shortname, @description, @categorycode, @subcategorycode, @purchaserate, @tenantcode)";

                foreach (var item in items)
                {
                    item.tenantcode = tenantcode; // ensure every row carries the correct tenant
                    await db.ExecuteAsync(query, item);
                }
            }
        }
        public async Task<long> InsertCategory(category_master category)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    long categorycode = await db.ExecuteScalarAsync<long>(
                        "SELECT COALESCE(MAX(categorycode),0)+1 FROM category_master");

                    category.categorycode = categorycode;

                    string query = @"
                    INSERT INTO category_master
                    (
                        categorycode,
                        categoryname,
                        shortname,
                        description,
                        parentcategorycode,
                        isactive,
                        deleted,
                        createddate,
                        usercode,
                        tenantcode
                    )
                    VALUES
                    (
                        @categorycode,
                        @categoryname,
                        @shortname,
                        @description,
                        @parentcategorycode,
                        @isactive,
                        @deleted,
                        CURRENT_TIMESTAMP,
                        @usercode,
                        @tenantcode
                    )";

                    await db.ExecuteAsync(query, category);

                    return categorycode;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Insert failed : " + ex.Message);
            }
        }

        // GET ALL CATEGORIES
        public async Task<IEnumerable<category_master>> GetCategories(string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM category_master
                    WHERE tenantcode = @tenantcode
                    AND deleted = false
                    ORDER BY categorycode";

                    return await db.QueryAsync<category_master>(
                        query,
                        new { tenantcode });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get failed : " + ex.Message);
            }
        }

        // GET CATEGORY BY ID
        public async Task<category_master?> GetCategoryById(
            long categorycode,
            string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM category_master
                    WHERE categorycode = @categorycode
                    AND tenantcode = @tenantcode
                    AND deleted = false";

                    return await db.QueryFirstOrDefaultAsync<category_master>(
                        query,
                        new { categorycode, tenantcode });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get by id failed : " + ex.Message);
            }
        }

        // UPDATE CATEGORY
        public async Task<bool> UpdateCategory(category_master category)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE category_master
                    SET
                        categoryname = @categoryname,
                        shortname = @shortname,
                        description = @description,
                        parentcategorycode = @parentcategorycode,
                        isactive = @isactive,
                        deleted = @deleted,
                        usercode = @usercode,
                        tenantcode = @tenantcode
                    WHERE categorycode = @categorycode
                    AND tenantcode = @tenantcode";

                    int rows = await db.ExecuteAsync(query, category);

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Update failed : " + ex.Message);
            }
        }

        // DELETE CATEGORY
        public async Task<bool> DeleteCategory(
            long categorycode,
            string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE category_master
                    SET
                        deleted = true,
                        isactive = false
                    WHERE categorycode = @categorycode
                    AND tenantcode = @tenantcode";

                    int rows = await db.ExecuteAsync(
                        query,
                        new { categorycode, tenantcode });

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Delete failed : " + ex.Message);
            }
        }
        // INSERT
        public async Task<long> InsertUom(uom_master model)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    db.Open();

                    string query = @"
                    INSERT INTO uom_master
                    (
                        orderno,
                        name,
                        shortname,
                        description,
                        deleted,
                        usercode,
                        computercode,
                        entereddate,
                        ibsdate,
                        packsize,
                        tenant_code
                    )
                    VALUES
                    (
                        @orderno,
                        @name,
                        @shortname,
                        @description,
                        @deleted,
                        @usercode,
                        @computercode,
                        CURRENT_TIMESTAMP,
                        CURRENT_TIMESTAMP,
                        @packsize,
                        @tenant_code
                    )
                    RETURNING ucode;";

                    return await db.ExecuteScalarAsync<long>(query, model);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Insert failed : " + ex.Message);
            }
        }

        // GET ALL
        public async Task<IEnumerable<uom_master>> GetAllUom(string tenant_code)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM uom_master
                    WHERE deleted = false
                    AND tenant_code = @tenant_code
                    ORDER BY ucode DESC";

                    return await db.QueryAsync<uom_master>(query, new { tenant_code });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get failed : " + ex.Message);
            }
        }

        // GET BY ID
        public async Task<uom_master?> GetUomByCode(long ucode, string tenant_code)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    SELECT *
                    FROM uom_master
                    WHERE ucode = @ucode
                    AND tenant_code = @tenant_code
                    AND deleted = false";

                    return await db.QueryFirstOrDefaultAsync<uom_master>(
                        query,
                        new { ucode, tenant_code });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Get by code failed : " + ex.Message);
            }
        }

        // UPDATE
        public async Task<bool> UpdateUom(uom_master model)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE uom_master
                    SET
                        orderno = @orderno,
                        name = @name,
                        shortname = @shortname,
                        description = @description,
                        deleted = @deleted,
                        usercode = @usercode,
                        computercode = @computercode,
                        packsize = @packsize,
                        tenant_code = @tenant_code
                    WHERE ucode = @ucode
                    AND tenant_code = @tenant_code";

                    int rows = await db.ExecuteAsync(query, model);

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Update failed : " + ex.Message);
            }
        }

        // DELETE
        public async Task<bool> DeleteUom(long ucode, string tenant_code)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                    UPDATE uom_master
                    SET deleted = true
                    WHERE ucode = @ucode
                    AND tenant_code = @tenant_code";

                    int rows = await db.ExecuteAsync(
                        query,
                        new { ucode, tenant_code });

                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Delete failed : " + ex.Message);
            }
        }
        public async Task<string> InsertParentCategory(parent_category_master model)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        INSERT INTO parent_category_master
        (
            parentcategoryname,
            shortname,
            description,
            isactive,
            deleted,
            createddate,
            tenantcode
        )
        VALUES
        (
            @parentcategoryname,
            @shortname,
            @description,
            @isactive,
            @deleted,
            CURRENT_TIMESTAMP,
            @tenantcode
        )";

                await db.ExecuteAsync(query, model);

                return "Inserted Successfully";
            }
        }

        public async Task<IEnumerable<parent_category_master>> GetParentCategories(string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        SELECT *
        FROM parent_category_master
        WHERE deleted = false
        AND tenantcode = @tenantcode
        ORDER BY parentcategorycode";

                return await db.QueryAsync<parent_category_master>(
                    query,
                    new { tenantcode });
            }
        }

        public async Task<string> UpdateParentCategory(parent_category_master model)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        UPDATE parent_category_master
        SET
            parentcategoryname = @parentcategoryname,
            shortname = @shortname,
            description = @description,
            isactive = @isactive,
            deleted = @deleted
        WHERE parentcategorycode = @parentcategorycode
        AND tenantcode = @tenantcode";

                await db.ExecuteAsync(query, model);

                return "Updated Successfully";
            }
        }

        public async Task<string> DeleteParentCategory(int parentcategorycode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
        UPDATE parent_category_master
        SET deleted = true
        WHERE parentcategorycode = @parentcategorycode
        AND tenantcode = @tenantcode";

                await db.ExecuteAsync(query,
                    new { parentcategorycode, tenantcode });

                return "Deleted Successfully";
            }
        }
        public async Task<string> InsertLedger(ledger_master model)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
            INSERT INTO ledger_master
            (
                ledgername, lcode, ldgcode,
                taxtype, taxsubtype, taxpercentage,
                gstpercentage, hsncode,
                isactive, deleted, createddate,
                tenantcode
            )
            VALUES
            (
                @ledgername, @lcode, @ldgcode,
                @taxtype, @taxsubtype, @taxpercentage,
                @gstpercentage, @hsncode,
                @isactive, @deleted, CURRENT_TIMESTAMP,
                @tenantcode
            )
            RETURNING ledgercode;";

                await db.ExecuteScalarAsync<long>(query, model);
                return "Inserted Successfully";
            }
        }

        public async Task<IEnumerable<ledger_master>> GetLedger(string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
            SELECT * FROM ledger_master
            WHERE deleted = false
            AND tenantcode = @tenantcode
            ORDER BY ledgercode";

                return await db.QueryAsync<ledger_master>(query, new { tenantcode });
            }
        }

        public async Task<string> UpdateLedger(ledger_master model)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                UPDATE ledger_master
                SET
                    ledgername    = @ledgername,
                    lcode         = @lcode,
                    ldgcode       = @ldgcode,
                    taxtype       = @taxtype,
                    taxsubtype    = @taxsubtype,
                    taxpercentage = @taxpercentage,
                    gstpercentage = @gstpercentage,
                    hsncode       = @hsncode,
                    isactive      = @isactive,
                    deleted       = @deleted
                WHERE ledgercode = @ledgercode
                AND tenantcode = @tenantcode";

                    int rows = await db.ExecuteAsync(query, model);
                    return rows > 0 ? "Updated Successfully" : "Failed";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<bool> DeleteLedger(int ledgercode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                string query = @"
            UPDATE ledger_master
            SET deleted = true, isactive = false
            WHERE ledgercode = @ledgercode
            AND tenantcode = @tenantcode";

                var rows = await db.ExecuteAsync(query, new { ledgercode, tenantcode });
                return rows > 0;
            }
        }
        // INSERT LEDGER TYPE
        public async Task<string> InsertLedgerType(ledger_type_master ledger)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(con);
                ledger.ledgertypecode = null;
                ledger.createddate = DateTime.UtcNow;
                ledger.deleted = false;
                ledger.isactive = true;

                await db.InsertAsync(ledger);
                return "Inserted Successfully";
            }
            catch (Exception ex)
            {
                Console.WriteLine("InsertLedgerType ERROR: " + ex.Message);
                return ex.Message;
            }
        }


        // UPDATE LEDGER TYPE
        public async Task<string> UpdateLedgerType(ledger_type_master ledger)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_type_master
        SET
            ledgertypename = @ledgertypename,
            shortname = @shortname,
            description = @description,
            naturetype = @naturetype,
            isactive = @isactive,
            tenantcode = @tenantcode,
            isgstapplicable = @isgstapplicable,
            isvatapplicable = @isvatapplicable,
            sgstpercentage = @sgstpercentage,
            cgstpercentage = @cgstpercentage,
            igstpercentage = @igstpercentage
        WHERE ledgertypecode = @ledgertypecode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgertypecode", ledger.ledgertypecode);
                    cmd.Parameters.AddWithValue("@ledgertypename", ledger.ledgertypename);
                    cmd.Parameters.AddWithValue("@shortname", ledger.shortname);
                    cmd.Parameters.AddWithValue("@description", ledger.description ?? "");
                    cmd.Parameters.AddWithValue("@naturetype", ledger.naturetype);
                    cmd.Parameters.AddWithValue("@isactive", ledger.isactive);
                    cmd.Parameters.AddWithValue("@tenantcode", ledger.tenantcode);
                    cmd.Parameters.AddWithValue("@isgstapplicable", ledger.isgstapplicable);
                    cmd.Parameters.AddWithValue("@isvatapplicable", ledger.isvatapplicable);
                    cmd.Parameters.AddWithValue("@sgstpercentage", ledger.sgstpercentage);
                    cmd.Parameters.AddWithValue("@cgstpercentage", ledger.cgstpercentage);
                    cmd.Parameters.AddWithValue("@igstpercentage", ledger.igstpercentage);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows == 0)
                        return "Ledger Type not found";
                }
            }

            return "Updated Successfully";
        }


        // DELETE LEDGER TYPE
        public async Task<bool> DeleteLedgerType(int ledgertypecode, string tenantcode)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_type_master
        SET deleted = true
        WHERE ledgertypecode = @ledgertypecode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgertypecode", ledgertypecode);
                    cmd.Parameters.AddWithValue("@tenantcode", tenantcode);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    return rows > 0;
                }
            }
        }



        // INSERT LEDGER GROUP
        public async Task<string> InsertLedgerGroup(ledger_group_master ledger)
        {
            try
            {
                using (var conn = new NpgsqlConnection(con))
                {
                    await conn.OpenAsync();
                    ledger.ledgergroupcode = null;
                    await conn.InsertAsync(ledger);

                    return "Inserted Successfully";
                }
            }
            catch (Exception ex)
            {
                return ex.Message.ToString();
            }
        }



        // UPDATE LEDGER GROUP
        public async Task<string> UpdateLedgerGroup(ledger_group_master ledger)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_group_master
        SET
            ledgergroupname = @ledgergroupname,
            shortname = @shortname,
            ledgertypecode = @ledgertypecode,
            description = @description,
            isactive = @isactive,
            tenantcode = @tenantcode
        WHERE ledgergroupcode = @ledgergroupcode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgergroupcode", ledger.ledgergroupcode);
                    cmd.Parameters.AddWithValue("@ledgergroupname", ledger.ledgergroupname);
                    cmd.Parameters.AddWithValue("@shortname", ledger.shortname);
                    cmd.Parameters.AddWithValue("@ledgertypecode", ledger.ledgertypecode);
                    cmd.Parameters.AddWithValue("@description", ledger.description ?? "");
                    cmd.Parameters.AddWithValue("@isactive", ledger.isactive);
                    cmd.Parameters.AddWithValue("@tenantcode", ledger.tenantcode);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows == 0)
                        return "Ledger Group not found";
                }
            }

            return "Updated Successfully";
        }



        // DELETE LEDGER GROUP
        public async Task<bool> DeleteLedgerGroup(int ledgergroupcode, string tenantcode)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
        UPDATE ledger_group_master
        SET deleted = true
        WHERE ledgergroupcode = @ledgergroupcode
        AND tenantcode = @tenantcode
        AND deleted = false";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ledgergroupcode", ledgergroupcode);
                    cmd.Parameters.AddWithValue("@tenantcode", tenantcode);

                    int rows = await cmd.ExecuteNonQueryAsync();

                    return rows > 0;
                }
            }
        }
        public async Task<List<ledger_type_master>> GetLedgerTypes(string tenantcode)
        {
            List<ledger_type_master> list = new List<ledger_type_master>();
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();
                string query = @"SELECT * FROM ledger_type_master 
                          WHERE deleted = false AND tenantcode = @tenantcode
                          ORDER BY ledgertypecode";
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@tenantcode", tenantcode);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ledger_type_master
                            {
                                ledgertypecode = Convert.ToInt32(reader["ledgertypecode"]),
                                ledgertypename = reader["ledgertypename"].ToString(),
                                shortname = reader["shortname"].ToString(),
                                description = reader["description"].ToString(),
                                naturetype = Convert.ToInt32(reader["naturetype"]),
                                isactive = Convert.ToBoolean(reader["isactive"]),
                                createddate = Convert.ToDateTime(reader["createddate"]),
                                tenantcode = reader["tenantcode"].ToString(),
                                isgstapplicable = Convert.ToBoolean(reader["isgstapplicable"]),
                                isvatapplicable = Convert.ToBoolean(reader["isvatapplicable"]),
                                sgstpercentage = Convert.ToDecimal(reader["sgstpercentage"]),
                                cgstpercentage = Convert.ToDecimal(reader["cgstpercentage"]),
                                igstpercentage = Convert.ToDecimal(reader["igstpercentage"]),
                                deleted = Convert.ToBoolean(reader["deleted"])
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task<List<ledger_group_master>> GetLedgerGroups(string tenantcode)
        {
            List<ledger_group_master> list = new List<ledger_group_master>();
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();
                string query = @"SELECT * FROM ledger_group_master 
                          WHERE deleted = false AND tenantcode = @tenantcode
                          ORDER BY ledgergroupcode";
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@tenantcode", tenantcode);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ledger_group_master
                            {
                                ledgergroupcode = Convert.ToInt32(reader["ledgergroupcode"]),
                                ledgergroupname = reader["ledgergroupname"].ToString(),
                                shortname = reader["shortname"].ToString(),
                                ledgertypecode = Convert.ToInt32(reader["ledgertypecode"]),
                                description = reader["description"].ToString(),
                                isactive = Convert.ToBoolean(reader["isactive"]),
                                createddate = Convert.ToDateTime(reader["createddate"]),
                                tenantcode = reader["tenantcode"].ToString(),
                                deleted = Convert.ToBoolean(reader["deleted"])
                            });
                        }
                    }
                }
            }
            return list;
        }
        private async Task<string> GetNextSalesBillNo(IDbConnection db, IDbTransaction transaction, string tenantcode)
        {
            int year = DateTime.Now.Year;
            string yearPrefix = year.ToString();   // e.g. 2026 (stored format has no "BILL" prefix)

            // Serialize concurrent inserts for the same tenant+year (released automatically at tx end)
            await db.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtext(@lockkey));",
                new { lockkey = "SALES_" + tenantcode + "_" + year },
                transaction);

            string query = @"
        SELECT MAX(CAST(SUBSTRING(t.b FROM LENGTH(@yearPrefix::text) + 1) AS bigint))
        FROM (
            SELECT REGEXP_REPLACE(billno, '^BILL', '') AS b
            FROM public.sales_master
            WHERE tenantcode = @tenantcode
              AND billno IS NOT NULL
        ) t
        WHERE t.b LIKE @yearPrefix::text || '%'
          AND SUBSTRING(t.b FROM LENGTH(@yearPrefix::text) + 1) ~ '^[0-9]+$';";

            long? maxSeq = await db.ExecuteScalarAsync<long?>(
                query,
                new { tenantcode, yearPrefix },
                transaction);

            long nextSeq = maxSeq.HasValue ? maxSeq.Value + 1 : 1;

            return yearPrefix + nextSeq.ToString("D4");   // e.g. 20260001, 20260002 ...
        }
        public async Task<string> InsertSales(sales_request request)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                using (var trans = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        //========================================
                        // Generate Bill Number
                        //========================================

                        string generatedBillNo = await GetNextSalesBillNo(
                            conn,
                            trans,
                            request.master.tenantcode);

                        // Remove BILL prefix
                        // Example:
                        // BILL202602 -> 202602
                        string billno = generatedBillNo.StartsWith(
                            "BILL",
                            StringComparison.OrdinalIgnoreCase)
                            ? generatedBillNo.Substring(4)
                            : generatedBillNo;

                        request.master.billno = billno;


                        //========================================
                        // Insert Sales Master
                        //========================================

                        string masterQuery = @"
INSERT INTO sales_master
(
    billno,
    billdate,
    invoiceno,
    invoicedate,
    customercode,
    salestype,
    warehousefield,
    patientid,
    patientname,
    mobileno,
    address,
    consultant,
    grossamount,
    discountamount,
    taxamount,
    roundoff,
    netamount,
    paymentmode,
    paymentstatus,
    currencycode,
    isactive,
    deleted,
    remarks,
    createddate,
    modifieddate,
    usercode,
    tenantcode,
    branchcode,
    companycode,
    ordercode,
    cntcode,
    bh_code
)
VALUES
(
    @billno,
    @billdate,
    @invoiceno,
    @invoicedate,
    @customercode,
    @salestype,
    @warehousefield,
    @patientid,
    @patientname,
    @mobileno,
    @address,
    @consultant,
    @grossamount,
    @discountamount,
    @taxamount,
    @roundoff,
    @netamount,
    @paymentmode,
    @paymentstatus,
    @currencycode,
    @isactive,
    @deleted,
    @remarks,
    @createddate,
    @modifieddate,
    @usercode,
    @tenantcode,
    @branchcode,
    @companycode,
    @ordercode,
    @cntcode,
    @bh_code
)
RETURNING salescode;";

                        long salescode = await conn.ExecuteScalarAsync<long>(
                            masterQuery,
                            request.master,
                            trans);


                        string detailQuery = @"
INSERT INTO sales_detail
(
    salescode,
    itemcode,
    quantity,
    freequantity,
    uomcode,
    rate,
    discountpercentage,
    discountamount,
    taxpercentage,
    taxamount,
    amount,
    totalamount,
    batchno,
    manufacturingdate,
    expirydate,
    soldqty,
    warehousecode,
    tenantcode
)
VALUES
(
    @salescode,
    @itemcode,
    @quantity,
    @freequantity,
    @uomcode,
    @rate,
    @discountpercentage,
    @discountamount,
    @taxpercentage,
    @taxamount,
    @amount,
    @totalamount,
    @batchno,
    @manufacturingdate,
    @expirydate,
    @soldqty,
    @warehousecode,
    @tenantcode
);";

                        foreach (var item in request.details)
                        {
                            item.salescode = salescode;

                            await conn.ExecuteAsync(
                                detailQuery,
                                item,
                                trans);

                            //---------------------------------------
                            // Check Stock
                            //---------------------------------------

                            string stockQuery = @"
SELECT stockcode, closingstock
FROM stock_master
WHERE itemcode=@itemcode
AND warehousecode=@warehousecode
AND batchno=@batchno;";

                            var stock = await conn.QueryFirstOrDefaultAsync(stockQuery,
                                new
                                {
                                    item.itemcode,
                                    item.warehousecode,
                                    item.batchno
                                },
                                trans);

                            if (stock == null)
                            {
                                throw new Exception($"Stock not found for Item : {item.itemcode}");
                            }

                            decimal closingStock = stock.closingstock;

                            if (closingStock < item.quantity)
                            {
                                throw new Exception($"Insufficient stock for Item : {item.itemcode}");
                            }

                            //---------------------------------------
                            // Update Stock
                            //---------------------------------------

                            string updateStock = @"
UPDATE stock_master
SET
    soldqty = COALESCE(soldqty,0) + @qty,
    closingstock = COALESCE(closingstock,0) - @qty,
    stockvalue = (COALESCE(closingstock,0)-@qty) * unitcost,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode=@stockcode;";

                            await conn.ExecuteAsync(
                                updateStock,
                                new
                                {
                                    qty = item.quantity,
                                    stockcode = stock.stockcode
                                },
                                trans);
                        }

                        await trans.CommitAsync();

                        return "Sales Inserted Successfully";
                    }
                    catch (Exception ex)
                    {
                        await trans.RollbackAsync();
                        return ex.Message;
                    }
                }
            }
        }
        public async Task<string> UpdateSales(sales_request request)
        {
            if (request?.master == null || request.master.salescode == 0)
                return "salescode is required";

            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();
                using (var trans = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        string updateMaster = @"
                    UPDATE sales_master SET
                        billno=@billno, billdate=@billdate, invoiceno=@invoiceno,
                        invoicedate=@invoicedate, customercode=@customercode,
                        salestype=@salestype, warehousefield=@warehousefield,
                        patientid=@patientid, patientname=@patientname,
                        mobileno=@mobileno,
                        address=@address, consultant=@consultant,
                        prescribeddays=@prescribeddays,   
                        converteddays=@converteddays,     
                        grossamount=@grossamount,discountpercentage=@discountpercentage, discountamount=@discountamount,
                        taxamount=@taxamount,roundoff=@roundoff, netamount=@netamount,
                        paymentmode=@paymentmode, paymentstatus=@paymentstatus,
                        currencycode=@currencycode, remarks=@remarks,
                        modifieddate=NOW(), usercode=@usercode,
                        branchcode=@branchcode, companycode=@companycode,
                        ordercode=@ordercode,cntcode=@cntcode,
                        bh_code=@bh_code
                    WHERE salescode = @salescode
                      AND tenantcode = @tenantcode
                      AND deleted = false";

                        int masterRows = await conn.ExecuteAsync(updateMaster, request.master, trans);

                        // STOP here if master didn't match — don't touch details
                        if (masterRows == 0)
                        {
                            await trans.RollbackAsync();
                            return $"Sales record not found (salescode={request.master.salescode}, tenantcode={request.master.tenantcode})";
                        }

                        await conn.ExecuteAsync(
                            "DELETE FROM sales_detail WHERE salescode=@salescode AND tenantcode=@tenantcode",
                            new { request.master.salescode, request.master.tenantcode },
                            trans);

                        // Do NOT pass salesdetailcode — let identity generate it
                        string detailQuery = @"
                    INSERT INTO sales_detail
                    (
                        salescode, itemcode, quantity,
                        freequantity, uomcode, rate,
                        discountpercentage, discountamount,
                        taxpercentage, taxamount,
                        amount, totalamount, batchno,
                        manufacturingdate, expirydate,
                        soldqty, returnedqty,days, warehousecode,
                        tenantcode
                    )
                    VALUES
                    (
                        @salescode, @itemcode, @quantity,
                        @freequantity, @uomcode, @rate,
                        @discountpercentage, @discountamount,
                        @taxpercentage, @taxamount,
                        @amount, @totalamount, @batchno,
                        @manufacturingdate, @expirydate,
                        @soldqty, @returnedqty,@days, @warehousecode,
                        @tenantcode
                    )";

                        foreach (var item in request.details)
                        {
                            item.salescode = request.master.salescode;
                            item.tenantcode = request.master.tenantcode;
                            await conn.ExecuteAsync(detailQuery, item, trans);
                        }

                        await trans.CommitAsync();
                        return "Sales Updated Successfully";
                    }
                    catch
                    {
                        await trans.RollbackAsync();
                        throw;
                    }
                }
            }
        }
        public async Task<IEnumerable<sales_request>> GetSales(string tenantcode)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string masterQuery = @"
            SELECT * FROM sales_master
            WHERE deleted = false AND tenantcode = @tenantcode
            ORDER BY salescode DESC;";
                var masters = (await conn.QueryAsync<sales_master>(masterQuery, new { tenantcode })).ToList();

                string detailQuery = @"
            SELECT * FROM sales_detail
            WHERE tenantcode = @tenantcode
            ORDER BY salesdetailcode;";
                var details = (await conn.QueryAsync<sales_detail>(detailQuery, new { tenantcode })).ToList();

                return masters.Select(master => new sales_request
                {
                    master = master,
                    details = details.Where(x => x.salescode == master.salescode).ToList()
                });
            }
        }

        public async Task<string> DeleteSales(long salescode, string tenantcode)
        {
            using (var conn = new NpgsqlConnection(con))
            {
                await conn.OpenAsync();

                string query = @"
            UPDATE sales_master
            SET deleted = true, isactive = false, modifieddate = NOW()
            WHERE salescode = @salescode AND tenantcode = @tenantcode";

                await conn.ExecuteAsync(query, new { salescode, tenantcode });
                return "Sales Deleted Successfully";
            }
        }

        public async Task<string> UpsertWarehouse(warehouse_master warehouse)
        {
            try
            {
                using IDbConnection db = new NpgsqlConnection(con);
                db.Open();

                if (warehouse.warehousecode == null || warehouse.warehousecode == 0)
                {
                    string insertQuery = @"
                INSERT INTO warehouse_master
                (
                    orderno, warehousename, shortname, description, location,
                    tenantcode, isactive, isdeleted, createddate,
                    purchaseallow, salesallow, isdoctor_room,
                    druglicenseno, pharmacyregistrationno, gstno, msmeno
                )
                VALUES
                (
                    @orderno, @warehousename, @shortname, @description, @location,
                    @tenantcode, @isactive, @isdeleted, CURRENT_TIMESTAMP,
                    @purchaseallow, @salesallow, @isdoctor_room,
                    @druglicenseno, @pharmacyregistrationno, @gstno, @msmeno
                )
                RETURNING warehousecode;";

                    int newcode = await db.ExecuteScalarAsync<int>(insertQuery, warehouse);
                    return $"Warehouse Inserted Successfully|WarehouseCode:{newcode}";
                }
                else
                {
                    string updateQuery = @"
                UPDATE warehouse_master
                SET
                    orderno                 = @orderno,
                    warehousename           = @warehousename,
                    shortname               = @shortname,
                    description             = @description,
                    location                = @location,
                    isactive                = @isactive,
                    isdeleted               = @isdeleted,
                    purchaseallow           = @purchaseallow,
                    salesallow              = @salesallow,
                    isdoctor_room           = @isdoctor_room,
                    druglicenseno           = @druglicenseno,
                    pharmacyregistrationno  = @pharmacyregistrationno,
                    gstno                   = @gstno,
                    msmeno                  = @msmeno,
                    modifieddate            = CURRENT_TIMESTAMP
                WHERE warehousecode = @warehousecode
                  AND tenantcode::text = @tenantcode;";

                    int rows = await db.ExecuteAsync(updateQuery, warehouse);
                    return rows > 0 ? "Warehouse Updated Successfully" : "Warehouse not found";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> DeleteWarehouse(int warehousecode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"UPDATE warehouse_master
                   SET isdeleted = true
                   WHERE warehousecode = @warehousecode
                     AND tenantcode = @tenantcode";

                    await db.ExecuteAsync(query, new { warehousecode, tenantcode });

                    return "Warehouse Deleted Successfully";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public async Task<IEnumerable<warehouse_master>> GetWarehouseList(string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"SELECT *
                   FROM warehouse_master
                   WHERE isdeleted = false
                     AND tenantcode = @tenantcode
                   ORDER BY warehousecode";

                    return await db.QueryAsync<warehouse_master>(query, new { tenantcode });
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public async Task<string> UpsertManufacturer(manufacturer_master manufacturer)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query;

                    if (manufacturer.manufacturercode == 0)
                    {
                        query = @"
           INSERT INTO manufacturer_master
           (
               manufacturername,
               shortname,
               description,
               contactperson,
               phoneno,
               email,
               address,
               gstno,
               isactive,
               deleted,
               createddate,
               usercode,
               tenantcode
           )
           VALUES
           (
               @manufacturername,
               @shortname,
               @description,
               @contactperson,
               @phoneno,
               @email,
               @address,
               @gstno,
               @isactive,
               @deleted,
               @createddate,
               @usercode,
               @tenantcode
           );";

                        await db.ExecuteAsync(query, manufacturer);

                        return "Manufacturer Created Successfully";
                    }
                    else
                    {
                        query = @"
           UPDATE manufacturer_master
           SET
               manufacturername = @manufacturername,
               shortname = @shortname,
               description = @description,
               contactperson = @contactperson,
               phoneno = @phoneno,
               email = @email,
               address = @address,
               gstno = @gstno,
               isactive = @isactive,
               usercode = @usercode,
               tenantcode = @tenantcode
           WHERE manufacturercode = @manufacturercode;";

                        await db.ExecuteAsync(query, manufacturer);

                        return "Manufacturer Updated Successfully";
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        // In UpsertManufacturer's UPDATE branch, change WHERE clause to:

        public async Task<IEnumerable<manufacturer_master>> GetManufacturerList(string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                SELECT * FROM manufacturer_master
                WHERE deleted = false AND tenantcode = @tenantcode
                ORDER BY manufacturercode";

                    return await db.QueryAsync<manufacturer_master>(query, new { tenantcode });
                }
            }
            catch (Exception) { throw; }
        }

        public async Task<string> DeleteManufacturer(long manufacturercode, string tenantcode)
        {
            try
            {
                using (IDbConnection db = new NpgsqlConnection(con))
                {
                    string query = @"
                UPDATE manufacturer_master
                SET deleted = true
                WHERE manufacturercode = @manufacturercode AND tenantcode = @tenantcode";

                    await db.ExecuteAsync(query, new { manufacturercode, tenantcode });
                    return "Manufacturer Deleted Successfully";
                }
            }
            catch (Exception ex) { return ex.Message; }
        }
        // ─── PURCHASE RETURN ──────────────────────────────────────────────────────────

        public async Task<IEnumerable<purchase_return_lookup_result>> GetPurchaseReturnLookup(
     long itemcode, string? batchno, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            string query = @"
    SELECT
        pd.purchasedetailcode,
        pd.purchasecode,
        pd.itemcode,
        im.itemname,
        pd.batchno,
        pm.vendorcode,
        v.vendorname,
        v.contactperson,
        v.phonenumber,
        v.gstnumber,
        pd.rate,
        pd.receivedqty,
        COALESCE(pd.returnedqty, 0) AS returnedqty,
        COALESCE(stk.closingstock, 0) AS availableqty,
        im.packsize,
        pd.warehousecode
    FROM public.purchase_detail pd
    JOIN public.purchase_master pm ON pm.purchasecode = pd.purchasecode
    LEFT JOIN public.item_master im ON im.itemcode = pd.itemcode
    LEFT JOIN public.vendor_master v ON v.vendorcode = pm.vendorcode
    LEFT JOIN public.stock_master stk
        ON stk.itemcode      = pd.itemcode
       AND stk.batchno       = pd.batchno
       AND stk.warehousecode = pd.warehousecode
       AND stk.tenantcode    = pd.tenantcode
       AND stk.deleted       = false
    WHERE pd.itemcode = @itemcode
      AND (@batchno IS NULL OR pd.batchno = @batchno)
      AND pm.deleted = false
      AND pd.tenantcode = @tenantcode
    ORDER BY pd.purchasedetailcode DESC;";

            return await db.QueryAsync<purchase_return_lookup_result>(
                query, new { itemcode, batchno, tenantcode });
        }

        public async Task<List<long>> InsertPurchaseReturn(purchase_return_request request)
        {
            if (request.items == null || !request.items.Any())
                throw new Exception("At least one return item is required.");

            using IDbConnection db = new NpgsqlConnection(con);
            db.Open();

            using var transaction = db.BeginTransaction();
            var returncodes = new List<long>();

            try
            {
                foreach (var item in request.items)
                {
                    // ==========================
                    // 1) Pull purchase_detail row (for rate / audit fields)
                    // ==========================
                    var detail = await db.QueryFirstOrDefaultAsync<purchase_detail>(@"
    SELECT
        purchasedetailcode, purchasecode, itemcode, quantity, freequantity,
        uomcode, rate, discountpercentage, discountamount, taxpercentage, taxamount,
        amount, totalamount, batchno,
        manufacturingdate::timestamp AS manufacturingdate,
        expirydate::timestamp AS expirydate,
        orderedqty, receivedqty, rejectedqty,
        COALESCE(returnedqty, 0) AS returnedqty,
        warehousecode, packaging, manufacturercode, tenantcode
    FROM public.purchase_detail
    WHERE purchasedetailcode = @purchasedetailcode
      AND tenantcode = @tenantcode;",
                        new { item.purchasedetailcode, request.tenantcode }, transaction);

                    if (detail == null)
                        throw new Exception($"Purchase detail not found for purchasedetailcode={item.purchasedetailcode}");

                    decimal totalqty = item.returnqty * item.packsize;

                    // ==========================
                    // 1b) Validate against ACTUAL stock
                    // ==========================
                    var stock = await db.QueryFirstOrDefaultAsync<stock_master>(@"
    SELECT stockcode, closingstock, unitcost
    FROM public.stock_master
    WHERE itemcode = @itemcode
      AND batchno = @batchno
      AND tenantcode = @tenantcode
      AND deleted = false
      AND (@warehousecode IS NULL OR warehousecode = @warehousecode);",
                        new
                        {
                            item.itemcode,
                            item.batchno,
                            request.tenantcode,
                            item.warehousecode
                        }, transaction);

                    if (stock == null)
                        throw new Exception($"Stock not found for item {item.itemcode}, batch {item.batchno}");

                    if (totalqty > stock.closingstock)
                        throw new Exception($"Cannot return {totalqty} for item {item.itemcode}; only {stock.closingstock} available");

                    decimal amount = totalqty * detail.rate;

                    // ==========================
                    // 2) Update purchase_detail.returnedqty
                    // ==========================
                    await db.ExecuteAsync(@"
            UPDATE public.purchase_detail
            SET returnedqty = COALESCE(returnedqty,0) + @totalqty
            WHERE purchasedetailcode = @purchasedetailcode;",
                        new { totalqty, item.purchasedetailcode }, transaction);

                    // ==========================
                    // 3) Update stock_master by stockcode
                    // ==========================
                    await db.ExecuteAsync(@"
            UPDATE public.stock_master
            SET returnqty    = COALESCE(returnqty,0) + @totalqty,
                closingstock = closingstock - @totalqty,
                stockvalue   = (closingstock - @totalqty) * unitcost,
                modifieddate = CURRENT_TIMESTAMP
            WHERE stockcode = @stockcode;",
                        new { totalqty, stockcode = stock.stockcode }, transaction);

                    // ==========================
                    // 4) Insert the return record for THIS item
                    // ==========================
                    long returncode = await db.ExecuteScalarAsync<long>(@"
            INSERT INTO public.purchase_return_master
            (
                purchasedetailcode, purchasecode, itemcode, vendorcode, batchno,
                returnqty, packsize, totalqty, rate, amount,
                warehousecode, remarks,
                isactive, deleted, createddate, usercode, tenantcode
            )
            VALUES
            (
                @purchasedetailcode, @purchasecode, @itemcode, @vendorcode, @batchno,
                @returnqty, @packsize, @totalqty, @rate, @amount,
                @warehousecode, @remarks,
                true, false, CURRENT_TIMESTAMP, @usercode, @tenantcode
            )
            RETURNING purchasereturncode;",
                        new
                        {
                            item.purchasedetailcode,
                            item.purchasecode,
                            item.itemcode,
                            request.vendorcode,
                            item.batchno,
                            item.returnqty,
                            item.packsize,
                            totalqty,
                            rate = detail.rate,
                            amount,
                            item.warehousecode,
                            remarks = item.remarks ?? request.remarks,
                            request.usercode,
                            request.tenantcode
                        }, transaction);

                    returncodes.Add(returncode);
                }

                transaction.Commit();
                return returncodes;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception("Purchase return failed: " + ex.Message);
            }
        }
        public async Task<List<long>> InsertSalesReturn(sales_return_request request)
        {
            if (request.items == null || !request.items.Any())
                throw new Exception("At least one return item is required.");

            using IDbConnection db = new NpgsqlConnection(con);
            db.Open();

            using var transaction = db.BeginTransaction();
            var returncodes = new List<long>();

            try
            {
                foreach (var item in request.items)
                {
                    // ==========================
                    // 1) Pull sales_detail row (for rate / audit fields)
                    // ==========================
                    var detail = await db.QueryFirstOrDefaultAsync<sales_detail>(@"
SELECT
    salesdetailcode, salescode, itemcode, quantity, freequantity, uomcode, rate,
    discountpercentage, discountamount, taxpercentage, taxamount,
    amount, totalamount, batchno, manufacturingdate, expirydate,
    soldqty, COALESCE(returnedqty,0) AS returnedqty,
    warehousecode, tenantcode
FROM public.sales_detail
WHERE salesdetailcode = @salesdetailcode
  AND tenantcode = @tenantcode;",
                        new { item.salesdetailcode, request.tenantcode }, transaction);

                    if (detail == null)
                        throw new Exception($"Sales detail not found for salesdetailcode={item.salesdetailcode}");

                    decimal totalqty = item.returnqty * item.packsize;

                    // ==========================
                    // 1b) Validate against ACTUAL stock
                    // ==========================
                    var stock = await db.QueryFirstOrDefaultAsync<stock_master>(@"
    SELECT stockcode, closingstock, unitcost
    FROM public.stock_master
    WHERE itemcode = @itemcode
      AND batchno = @batchno
      AND tenantcode = @tenantcode
      AND deleted = false
      AND (@warehousecode IS NULL OR warehousecode = @warehousecode);",
                        new
                        {
                            item.itemcode,
                            item.batchno,
                            request.tenantcode,
                            item.warehousecode
                        }, transaction);

                    if (stock == null)
                        throw new Exception($"Stock not found for item {item.itemcode}, batch {item.batchno}");

                    if (totalqty > stock.closingstock)
                        throw new Exception($"Cannot return {totalqty} for item {item.itemcode}; only {stock.closingstock} available");

                    decimal amount = totalqty * detail.rate;

                    // ==========================
                    // 2) Update sales_detail.returnedqty
                    // ==========================
                    await db.ExecuteAsync(@"
UPDATE public.sales_detail
SET returnedqty = COALESCE(returnedqty,0) + @totalqty
WHERE salesdetailcode = @salesdetailcode;",
                        new { totalqty, item.salesdetailcode }, transaction);

                    // ==========================
                    // 3) Update stock_master by stockcode
                    // ==========================
                    await db.ExecuteAsync(@"
UPDATE public.stock_master
SET
    damagedqty   = COALESCE(damagedqty,0) + @totalqty,
    closingstock = closingstock + @totalqty,
    stockvalue   = (closingstock + @totalqty) * unitcost,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode = @stockcode;",
                        new { totalqty, stockcode = stock.stockcode }, transaction);

                    // ==========================
                    // 4) Insert the return record for THIS item
                    // ==========================
                    long returncode = await db.ExecuteScalarAsync<long>(@"
INSERT INTO public.sales_return_master
(
    salesdetailcode, salescode, itemcode, customercode, batchno,
    returnqty, packsize, totalqty, rate, amount,
    warehousecode, remarks,
    isactive, deleted, createddate, usercode, tenantcode
)
VALUES
(
    @salesdetailcode, @salescode, @itemcode, @customercode, @batchno,
    @returnqty, @packsize, @totalqty, @rate, @amount,
    @warehousecode, @remarks,
    true, false, CURRENT_TIMESTAMP, @usercode, @tenantcode
)
RETURNING salesreturncode;",
                        new
                        {
                            item.salesdetailcode,
                            item.salescode,
                            item.itemcode,
                            request.customercode,
                            item.batchno,
                            item.returnqty,
                            item.packsize,
                            totalqty,
                            rate = detail.rate,
                            amount,
                            item.warehousecode,
                            remarks = item.remarks ?? request.remarks,
                            request.usercode,
                            request.tenantcode
                        }, transaction);

                    returncodes.Add(returncode);
                }

                transaction.Commit();
                return returncodes;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception("Sales return failed: " + ex.Message);
            }
        }
        public async Task<IEnumerable<sales_return_lookup_result>> GetSalesReturnLookup(
    long itemcode, string? batchno, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            string query = @"
SELECT
    sd.salesdetailcode,
    sd.salescode,
    sd.itemcode,
    im.itemname,
    sd.batchno,
    sm.customercode,
    NULL AS customername,
    sm.patientid,
    sm.patientname,
    sd.rate,
    sd.quantity AS deliveredqty,
    COALESCE(sd.returnedqty, 0) AS returnedqty,
    COALESCE(stk.closingstock, 0) AS availableqty,
    im.packsize,
    sd.warehousecode
FROM public.sales_detail sd
INNER JOIN public.sales_master sm
    ON sm.salescode = sd.salescode
LEFT JOIN public.item_master im
    ON im.itemcode = sd.itemcode
LEFT JOIN public.stock_master stk
    ON stk.itemcode      = sd.itemcode
   AND stk.batchno       = sd.batchno
   AND stk.warehousecode = sd.warehousecode
   AND stk.tenantcode    = sd.tenantcode
   AND stk.deleted       = false
WHERE sd.itemcode = @itemcode
  AND (@batchno IS NULL OR sd.batchno = @batchno)
  AND sm.deleted = false
  AND sd.tenantcode = @tenantcode
ORDER BY sd.salesdetailcode DESC;";

            return await db.QueryAsync<sales_return_lookup_result>(
                query, new { itemcode, batchno, tenantcode });
        }
        public async Task<string> InsertStockAdjustment(stock_adjustment_request request)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();

            try
            {
                // Check Stock Exists
                string checkStockQuery = @"
SELECT stockcode, closingstock, unitcost
FROM stock_master
WHERE stockcode = @stockcode
AND tenantcode = @tenantcode
AND deleted = false;";

                var stock = await conn.QueryFirstOrDefaultAsync<stock_master>(
                    checkStockQuery,
                    new
                    {
                        stockcode = request.stock.stockcode,
                        tenantcode = request.stock.tenantcode
                    },
                    tran);

                if (stock == null)
                {
                    tran.Rollback();
                    return "Stock record not found.";
                }

                decimal afterQty = request.adjustmentlog.afterqty;

                decimal stockValue = afterQty * stock.unitcost;

                // Update Stock Master
                string updateStockQuery = @"
UPDATE stock_master
SET
    closingstock = @closingstock,
    stockvalue = @stockvalue,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode = @stockcode;";

                await conn.ExecuteAsync(
                    updateStockQuery,
                    new
                    {
                        closingstock = afterQty,
                        stockvalue = stockValue,
                        stockcode = request.stock.stockcode
                    },
                    tran);

                // Insert Adjustment Log
                string insertLogQuery = @"
INSERT INTO stock_adjustment_log
(
    stockcode,
    itemcode,
    warehousecode,
    branchcode,
    locationcode,
    batchno,
    beforeqty,
    adjustedqty,
    afterqty,
    unitcost,
    stockvalue,
    adjustmenttype,
    adjustmentreason,
    remarks,
    adjustmentdate,
    adjustedby,
    isactive,
    deleted,
    createddate,
    tenantcode,
    companycode
)
VALUES
(
    @stockcode,
    @itemcode,
    @warehousecode,
    @branchcode,
    @locationcode,
    @batchno,
    @beforeqty,
    @adjustedqty,
    @afterqty,
    @unitcost,
    @stockvalue,
    @adjustmenttype,
    @adjustmentreason,
    @remarks,
    CURRENT_TIMESTAMP,
    @adjustedby,
    @isactive,
    @deleted,
    CURRENT_TIMESTAMP,
    @tenantcode,
    @companycode
);";

                await conn.ExecuteAsync(
                    insertLogQuery,
                    new
                    {
                        stockcode = request.stock.stockcode,
                        itemcode = request.adjustmentlog.itemcode,
                        warehousecode = request.adjustmentlog.warehousecode,
                        branchcode = request.adjustmentlog.branchcode,
                        locationcode = request.adjustmentlog.locationcode,
                        batchno = request.adjustmentlog.batchno,

                        beforeqty = request.adjustmentlog.beforeqty,
                        adjustedqty = request.adjustmentlog.adjustedqty,
                        afterqty = afterQty,

                        unitcost = stock.unitcost,
                        stockvalue = stockValue,

                        adjustmenttype = request.adjustmentlog.adjustmenttype,
                        adjustmentreason = request.adjustmentlog.adjustmentreason,
                        remarks = request.adjustmentlog.remarks,

                        adjustedby = request.adjustmentlog.adjustedby,

                        isactive = true,
                        deleted = false,

                        tenantcode = request.stock.tenantcode,
                        companycode = request.stock.companycode
                    },
                    tran);

                tran.Commit();

                return "Stock Adjustment Saved Successfully.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                return $"Error : {ex.Message}";
            }
        }
        public async Task<string> UpdateStockAdjustment(stock_adjustment_request request)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();

            try
            {
                //=================================================
                // 1. Check Stock Exists - Tenant Wise
                //=================================================

                string checkStockQuery = @"
SELECT
    stockcode,
    closingstock,
    unitcost
FROM stock_master
WHERE stockcode = @stockcode
AND tenantcode = @tenantcode
AND deleted = false;";

                var stock = await conn.QueryFirstOrDefaultAsync<stock_master>(
                    checkStockQuery,
                    new
                    {
                        stockcode = request.stock.stockcode,
                        tenantcode = request.stock.tenantcode
                    },
                    tran);

                if (stock == null)
                {
                    await tran.RollbackAsync();
                    return "Stock record not found.";
                }

                //=================================================
                // 2. Check Adjustment Log Exists - Tenant Wise
                //=================================================

                string checkLogQuery = @"
SELECT
    adjustmentlogcode,
    stockcode,
    beforeqty,
    adjustedqty,
    afterqty
FROM stock_adjustment_log
WHERE adjustmentlogcode = @adjustmentlogcode
AND tenantcode = @tenantcode
AND deleted = false;";

                var adjustmentLog = await conn.QueryFirstOrDefaultAsync<stock_adjustment_log>(
                    checkLogQuery,
                    new
                    {
                        adjustmentlogcode = request.adjustmentlog.adjustmentlogcode,
                        tenantcode = request.stock.tenantcode
                    },
                    tran);

                if (adjustmentLog == null)
                {
                    await tran.RollbackAsync();
                    return "Stock adjustment record not found.";
                }

                //=================================================
                // 3. Calculate New Stock
                //=================================================

                decimal beforeQty = adjustmentLog.beforeqty;

                decimal newAfterQty;

                if (request.adjustmentlog.adjustmenttype == "Increase")
                {
                    newAfterQty = beforeQty + request.adjustmentlog.adjustedqty;
                }
                else if (request.adjustmentlog.adjustmenttype == "Decrease")
                {
                    newAfterQty = beforeQty - request.adjustmentlog.adjustedqty;
                }
                else
                {
                    await tran.RollbackAsync();
                    return "Invalid adjustment type. Use Increase or Decrease.";
                }

                if (newAfterQty < 0)
                {
                    await tran.RollbackAsync();
                    return "Stock quantity cannot be negative.";
                }

                decimal stockValue = newAfterQty * stock.unitcost;

                //=================================================
                // 4. Update Stock Master
                //=================================================

                string updateStockQuery = @"
UPDATE stock_master
SET
    adjustedqty = @adjustedqty,
    adjusteddate = CURRENT_TIMESTAMP,
    adjustedby = @adjustedby,
    adjustmentreason = @adjustmentreason,
    adjustmenttype = @adjustmenttype,
    closingstock = @closingstock,
    stockvalue = @stockvalue,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode = @stockcode
AND tenantcode = @tenantcode
AND deleted = false;";

                await conn.ExecuteAsync(
                    updateStockQuery,
                    new
                    {
                        adjustedqty = request.adjustmentlog.adjustedqty,
                        adjustedby = request.adjustmentlog.adjustedby,
                        adjustmentreason = request.adjustmentlog.adjustmentreason,
                        adjustmenttype = request.adjustmentlog.adjustmenttype,
                        closingstock = newAfterQty,
                        stockvalue = stockValue,
                        stockcode = request.stock.stockcode,
                        tenantcode = request.stock.tenantcode
                    },
                    tran);

                //=================================================
                // 5. Update Adjustment Log
                //=================================================

                string updateLogQuery = @"
UPDATE stock_adjustment_log
SET
    itemcode = @itemcode,
    warehousecode = @warehousecode,
    branchcode = @branchcode,
    locationcode = @locationcode,
    batchno = @batchno,
    beforeqty = @beforeqty,
    adjustedqty = @adjustedqty,
    afterqty = @afterqty,
    unitcost = @unitcost,
    stockvalue = @stockvalue,
    adjustmenttype = @adjustmenttype,
    adjustmentreason = @adjustmentreason,
    remarks = @remarks,
    adjustedby = @adjustedby,
    modifieddate = CURRENT_TIMESTAMP,
    companycode = @companycode
WHERE adjustmentlogcode = @adjustmentlogcode
AND tenantcode = @tenantcode
AND deleted = false;";

                int rows = await conn.ExecuteAsync(
                    updateLogQuery,
                    new
                    {
                        adjustmentlogcode = request.adjustmentlog.adjustmentlogcode,

                        itemcode = request.adjustmentlog.itemcode,
                        warehousecode = request.adjustmentlog.warehousecode,
                        branchcode = request.adjustmentlog.branchcode,
                        locationcode = request.adjustmentlog.locationcode,
                        batchno = request.adjustmentlog.batchno,

                        beforeqty = beforeQty,
                        adjustedqty = request.adjustmentlog.adjustedqty,
                        afterqty = newAfterQty,

                        unitcost = stock.unitcost,
                        stockvalue = stockValue,

                        adjustmenttype = request.adjustmentlog.adjustmenttype,
                        adjustmentreason = request.adjustmentlog.adjustmentreason,
                        remarks = request.adjustmentlog.remarks,

                        adjustedby = request.adjustmentlog.adjustedby,

                        companycode = request.stock.companycode,
                        tenantcode = request.stock.tenantcode
                    },
                    tran);

                if (rows == 0)
                {
                    await tran.RollbackAsync();
                    return "Stock adjustment record not found.";
                }

                //=================================================
                // 6. Commit
                //=================================================

                await tran.CommitAsync();

                return "Stock Adjustment Updated Successfully.";
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                return $"Error : {ex.Message}";
            }
        }
        public async Task<string> DeleteStockAdjustment(
    long adjustmentlogcode,
    string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();

            try
            {
                //=================================================
                // 1. Get Adjustment Log
                //=================================================

                string getLogQuery = @"
SELECT
    adjustmentlogcode,
    stockcode,
    beforeqty,
    adjustedqty,
    afterqty
FROM stock_adjustment_log
WHERE adjustmentlogcode = @adjustmentlogcode
AND tenantcode = @tenantcode
AND deleted = false;";

                var log = await conn.QueryFirstOrDefaultAsync<stock_adjustment_log>(
                    getLogQuery,
                    new
                    {
                        adjustmentlogcode,
                        tenantcode
                    },
                    tran);

                if (log == null)
                {
                    await tran.RollbackAsync();
                    return "Stock adjustment record not found.";
                }

                //=================================================
                // 2. Restore Previous Stock Quantity
                //=================================================

                string updateStockQuery = @"
UPDATE stock_master
SET
    closingstock = @beforeqty,
    stockvalue = @stockvalue,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode = @stockcode
AND tenantcode = @tenantcode
AND deleted = false;";

                // Get unit cost
                string unitCostQuery = @"
SELECT unitcost
FROM stock_master
WHERE stockcode = @stockcode
AND tenantcode = @tenantcode
AND deleted = false;";

                decimal unitcost = await conn.ExecuteScalarAsync<decimal>(
                    unitCostQuery,
                    new
                    {
                        stockcode = log.stockcode,
                        tenantcode
                    },
                    tran);

                decimal stockValue = log.beforeqty * unitcost;

                await conn.ExecuteAsync(
                    updateStockQuery,
                    new
                    {
                        beforeqty = log.beforeqty,
                        stockvalue = stockValue,
                        stockcode = log.stockcode,
                        tenantcode
                    },
                    tran);

                //=================================================
                // 3. Soft Delete Adjustment Log
                //=================================================

                string deleteQuery = @"
UPDATE stock_adjustment_log
SET
    deleted = true,
    isactive = false,
    modifieddate = CURRENT_TIMESTAMP
WHERE adjustmentlogcode = @adjustmentlogcode
AND tenantcode = @tenantcode;";

                int rows = await conn.ExecuteAsync(
                    deleteQuery,
                    new
                    {
                        adjustmentlogcode,
                        tenantcode
                    },
                    tran);

                if (rows == 0)
                {
                    await tran.RollbackAsync();
                    return "Stock adjustment record not found.";
                }

                //=================================================
                // 4. Commit
                //=================================================

                await tran.CommitAsync();

                return "Stock Adjustment Deleted Successfully.";
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                return $"Error : {ex.Message}";
            }
        }
        public async Task<stock_adjustment_log?> GetStockAdjustment(
            long adjustmentlogcode,
            string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            string query = @"
        SELECT
            adjustmentlogcode,
            stockcode,
            itemcode,
            warehousecode,
            branchcode,
            locationcode,
            batchno,
            beforeqty,
            adjustedqty,
            afterqty,
            unitcost,
            stockvalue,
            adjustmenttype,
            adjustmentreason,
            remarks,
            adjustmentdate,
            adjustedby,
            isactive,
            deleted,
            createddate,
            modifieddate,
            tenantcode,
            companycode
        FROM public.stock_adjustment_log
        WHERE adjustmentlogcode = @adjustmentlogcode
          AND tenantcode = @tenantcode
          AND deleted = false;";

            return await db.QueryFirstOrDefaultAsync<stock_adjustment_log>(
                query,
                new
                {
                    adjustmentlogcode,
                    tenantcode
                }
            );
        }
        public async Task<long> InsertStockTransfer(
stock_transfer_request request,
string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ----------------------------------------------------
                // Serialize concurrent inserts for the same tenant so
                // the indentcode MAX()+1 calculation below doesn't race.
                // ----------------------------------------------------

                await conn.ExecuteAsync(
                    "SELECT pg_advisory_xact_lock(hashtext(@lockkey));",
                    new { lockkey = "STOCKTRANSFER_" + tenantcode },
                    transaction);

                // ----------------------------------------------------
                // Compute indentcode BEFORE the insert — the column is
                // NOT NULL, so it must be supplied in the INSERT itself,
                // not patched in afterwards.
                // ----------------------------------------------------

                long indentcode = await conn.ExecuteScalarAsync<long>(
                    @"
            SELECT COALESCE(MAX(indentcode), 0) + 1
            FROM stock_transfer_master
            WHERE tenantcode = @tenantcode
            ",
                    new { tenantcode },
                    transaction);

                // ----------------------------------------------------
                // Insert Master — let the IDENTITY column generate
                // transfercode, but supply indentcode up front.
                // ----------------------------------------------------

                long transfercode = await conn.ExecuteScalarAsync<long>(
                    @"
            INSERT INTO stock_transfer_master
            (
                transferdate,
                transfertype,
                fromwarehousecode,
                towarehousecode,
                branchcode,
                reason,
                remarks,
                status,
                createdby,
                createddate,
                tenantcode,
                indentcode
            )
            VALUES
            (
                @transferdate,
                @transfertype,
                @fromwarehousecode,
                @towarehousecode,
                @branchcode,
                @reason,
                @remarks,
                'PENDING',
                @createdby,
                CURRENT_TIMESTAMP,
                @tenantcode,
                @indentcode
            )
            RETURNING transfercode;",
                    new
                    {
                        transferdate = request.master.transferdate,
                        transfertype = request.master.transfertype,
                        fromwarehousecode = request.master.fromwarehousecode,
                        towarehousecode = request.master.towarehousecode,
                        branchcode = request.master.branchcode,
                        reason = request.master.reason,
                        remarks = request.master.remarks,
                        createdby = request.master.createdby,
                        tenantcode,
                        indentcode
                    },
                    transaction);

                // ----------------------------------------------------
                // Now that we have the real transfercode, build the
                // human-readable transferno and store it.
                // ----------------------------------------------------

                string transferno = $"TRF-{transfercode:D6}";

                await conn.ExecuteAsync(
                    @"
            UPDATE stock_transfer_master
            SET transferno = @transferno
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode;",
                    new { transferno, transfercode, tenantcode },
                    transaction);

                // ----------------------------------------------------
                // Insert Details — let the IDENTITY column generate
                // transferdetailcode. No manual MAX()+1 needed.
                // ----------------------------------------------------

                foreach (var detail in request.details)
                {
                    if (detail.transferqty <= 0)
                    {
                        throw new Exception(
                            $"Transfer quantity must be greater than zero for Item {detail.itemcode}");
                    }

                    var sourceStock = await conn.QueryFirstOrDefaultAsync<stock_master>(
                        @"
        SELECT *
        FROM stock_master
        WHERE warehousecode = @warehousecode
          AND itemcode = @itemcode
          AND batchno = @batchno
          AND tenantcode = @tenantcode
          AND deleted = false
          AND isactive = true
        FOR UPDATE
        ",
                        new
                        {
                            warehousecode = request.master.fromwarehousecode,
                            itemcode = detail.itemcode,
                            batchno = detail.batchno,
                            tenantcode
                        },
                        transaction);

                    if (sourceStock == null)
                    {
                        throw new Exception(
                            $"Stock not found. Warehouse: {request.master.fromwarehousecode}, " +
                            $"Item: {detail.itemcode}, Batch: {detail.batchno}");
                    }

                    if (sourceStock.closingstock < detail.transferqty)
                    {
                        throw new Exception(
                            $"Insufficient stock for Item {detail.itemcode}, Batch {detail.batchno}. " +
                            $"Available: {sourceStock.closingstock}, Transfer: {detail.transferqty}");
                    }

                    await conn.ExecuteAsync(
                        @"
        INSERT INTO stock_transfer_detail
        (
            transfercode, itemcode, batchno,
            manufacturingdate, expirydate, uomcode,
            availableqty, requestedqty, approvedqty, transferqty,
            unitcost, stockvalue, remarks, tenantcode
        )
        VALUES
        (
            @transfercode, @itemcode, @batchno,
            @manufacturingdate, @expirydate, @uomcode,
            @availableqty, @requestedqty, 0, @transferqty,
            @unitcost, @stockvalue, @remarks, @tenantcode
        )
        ",
                        new
                        {
                            transfercode,
                            itemcode = detail.itemcode,
                            batchno = detail.batchno,
                            manufacturingdate = detail.manufacturingdate,
                            expirydate = detail.expirydate,
                            uomcode = detail.uomcode,
                            availableqty = sourceStock.closingstock,
                            requestedqty = detail.requestedqty,
                            transferqty = detail.transferqty,
                            unitcost = detail.unitcost,
                            stockvalue = detail.transferqty * detail.unitcost,
                            remarks = detail.remarks,
                            tenantcode
                        },
                        transaction);
                }

                await transaction.CommitAsync();
                return transfercode;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ============================================================
        // UPDATE
        // Only PENDING transfers can be updated
        // ============================================================

        public async Task<bool> UpdateStockTransfer(
        stock_transfer_request request,
        string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);

            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                long transfercode =
                    request.master.transfercode;


                // ----------------------------------------------------
                // Check Master
                // ----------------------------------------------------

                var master =
                    await conn.QueryFirstOrDefaultAsync<stock_transfer_master>(
                        @"
                SELECT *
                FROM stock_transfer_master
                WHERE transfercode = @transfercode
                  AND tenantcode = @tenantcode
                FOR UPDATE
                ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);


                if (master == null)
                {
                    return false;
                }


                if (master.status != "PENDING")
                {
                    throw new Exception(
                        "Only PENDING transfer can be updated.");
                }


                // ----------------------------------------------------
                // Update Master
                // ----------------------------------------------------

                await conn.ExecuteAsync(
                    @"
            UPDATE stock_transfer_master
            SET
                transferdate = @transferdate,
                transfertype = @transfertype,
                fromwarehousecode = @fromwarehousecode,
                towarehousecode = @towarehousecode,
                branchcode = @branchcode,
                reason = @reason,
                remarks = @remarks
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                    new
                    {
                        transfercode,
                        transferdate =
                            request.master.transferdate,
                        transfertype =
                            request.master.transfertype,
                        fromwarehousecode =
                            request.master.fromwarehousecode,
                        towarehousecode =
                            request.master.towarehousecode,
                        branchcode =
                            request.master.branchcode,
                        reason =
                            request.master.reason,
                        remarks =
                            request.master.remarks,
                        tenantcode
                    },
                    transaction);


                // ----------------------------------------------------
                // Delete old details
                // ----------------------------------------------------

                await conn.ExecuteAsync(
                    @"
            DELETE FROM stock_transfer_detail
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                    new
                    {
                        transfercode,
                        tenantcode
                    },
                    transaction);


                // ----------------------------------------------------
                // Generate Detail Code
                // ----------------------------------------------------

                long detailcode =
                    await conn.ExecuteScalarAsync<long>(
                        @"
                SELECT COALESCE(MAX(transferdetailcode), 0)
                FROM stock_transfer_detail
                WHERE tenantcode = @tenantcode
                ",
                        new { tenantcode },
                        transaction);


                // ----------------------------------------------------
                // Insert new details
                // ----------------------------------------------------

                foreach (var detail in request.details)
                {
                    detailcode++;


                    var sourceStock =
                        await conn.QueryFirstOrDefaultAsync<stock_master>(
                            @"
                    SELECT *
                    FROM stock_master
                    WHERE warehousecode = @warehousecode
                      AND itemcode = @itemcode
                      AND batchno = @batchno
                      AND tenantcode = @tenantcode
                      AND deleted = false
                      AND isactive = true
                    FOR UPDATE
                    ",
                            new
                            {
                                warehousecode =
                                    request.master.fromwarehousecode,
                                itemcode =
                                    detail.itemcode,
                                batchno =
                                    detail.batchno,
                                tenantcode
                            },
                            transaction);


                    if (sourceStock == null)
                    {
                        throw new Exception(
                            $"Stock not found for Item {detail.itemcode}, Batch {detail.batchno}");
                    }


                    if (sourceStock.closingstock < detail.transferqty)
                    {
                        throw new Exception(
                            $"Insufficient stock for Item {detail.itemcode}");
                    }


                    await conn.ExecuteAsync(
                        @"
                INSERT INTO stock_transfer_detail
                (
                    transferdetailcode,
                    transfercode,
                    itemcode,
                    batchno,
                    manufacturingdate,
                    expirydate,
                    uomcode,
                    availableqty,
                    requestedqty,
                    approvedqty,
                    transferqty,
                    unitcost,
                    stockvalue,
                    remarks,
                    tenantcode
                )
                VALUES
                (
                    @transferdetailcode,
                    @transfercode,
                    @itemcode,
                    @batchno,
                    @manufacturingdate,
                    @expirydate,
                    @uomcode,
                    @availableqty,
                    @requestedqty,
                    0,
                    @transferqty,
                    @unitcost,
                    @stockvalue,
                    @remarks,
                    @tenantcode
                )
                ",
                        new
                        {
                            transferdetailcode = detailcode,
                            transfercode,
                            itemcode =
                                detail.itemcode,
                            batchno =
                                detail.batchno,
                            manufacturingdate =
                                detail.manufacturingdate,
                            expirydate =
                                detail.expirydate,
                            uomcode =
                                detail.uomcode,
                            availableqty =
                                sourceStock.closingstock,
                            requestedqty =
                                detail.requestedqty,
                            transferqty =
                                detail.transferqty,
                            unitcost =
                                detail.unitcost,
                            stockvalue =
                                detail.transferqty *
                                detail.unitcost,
                            remarks =
                                detail.remarks,
                            tenantcode
                        },
                        transaction);
                }


                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ============================================================
        // DELETE
        // Only PENDING transfers
        // ============================================================

        public async Task<bool> DeleteStockTransfer(
            long transfercode,
            string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);

            await conn.OpenAsync();

            using var transaction =
                await conn.BeginTransactionAsync();

            try
            {
                var status =
                    await conn.ExecuteScalarAsync<string?>(
                        @"
                SELECT status
                FROM stock_transfer_master
                WHERE transfercode = @transfercode
                  AND tenantcode = @tenantcode
                ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);


                if (status == null)
                {
                    return false;
                }


                if (status != "PENDING")
                {
                    throw new Exception(
                        "Only PENDING transfer can be deleted.");
                }


                await conn.ExecuteAsync(
                    @"
            DELETE FROM stock_transfer_detail
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                    new
                    {
                        transfercode,
                        tenantcode
                    },
                    transaction);


                int rows =
                    await conn.ExecuteAsync(
                        @"
                DELETE FROM stock_transfer_master
                WHERE transfercode = @transfercode
                  AND tenantcode = @tenantcode
                ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);


                await transaction.CommitAsync();

                return rows > 0;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<IEnumerable<stock_transfer_request>> GetStockTransferList(string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);

            var masters = (await conn.QueryAsync<stock_transfer_master>(
                @"
        SELECT
            transfercode,
            transferno,
            transferdate,
            transfertype,
            indentcode,
            fromwarehousecode,
            towarehousecode,
            branchcode,
            reason,
            remarks,
            status,
            createdby,
            createddate,
            dispatchedby,
            dispatcheddate,
            receivedby,
            receiveddate,
            rejectedby,
            rejecteddate,
            tenantcode
        FROM stock_transfer_master
        WHERE tenantcode = @tenantcode
        ORDER BY transfercode DESC
        ",
                new { tenantcode })).ToList();

            var details = (await conn.QueryAsync<stock_transfer_detail>(
                @"
        SELECT *
        FROM stock_transfer_detail
        WHERE tenantcode = @tenantcode
        ",
                new { tenantcode })).ToList();

            return masters.Select(m => new stock_transfer_request
            {
                master = m,
                details = details
                    .Where(d => d.transfercode == m.transfercode)
                    .ToList()
            });
        }


        // ============================================================
        // GET BY CODE
        // ============================================================

        public async Task<stock_transfer_request?>
        GetStockTransferByCode(
            long transfercode,
            string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);

            var master =
                await conn.QueryFirstOrDefaultAsync<stock_transfer_master>(
                    @"
            SELECT *
            FROM stock_transfer_master
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                    new
                    {
                        transfercode,
                        tenantcode
                    });


            if (master == null)
            {
                return null;
            }


            var details =
                await conn.QueryAsync<stock_transfer_detail>(
                    @"
            SELECT *
            FROM stock_transfer_detail
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ORDER BY transferdetailcode
            ",
                    new
                    {
                        transfercode,
                        tenantcode
                    });


            return new stock_transfer_request
            {
                master = master,
                details = details.ToList()
            };
        }


        // ============================================================
        // DISPATCH
        //
        // PENDING → IN_TRANSIT
        //
        // Source stock is reduced here.
        // Destination stock is NOT added yet.
        // ============================================================

        public async Task<bool> DispatchStockTransfer(
            long transfercode,
            long usercode,
            string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);

            await conn.OpenAsync();

            using var transaction =
                await conn.BeginTransactionAsync();

            try
            {
                // ----------------------------------------------------
                // Lock transfer
                // ----------------------------------------------------

                var master =
                    await conn.QueryFirstOrDefaultAsync<stock_transfer_master>(
                        @"
                SELECT *
                FROM stock_transfer_master
                WHERE transfercode = @transfercode
                  AND tenantcode = @tenantcode
                FOR UPDATE
                ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);


                if (master == null)
                {
                    return false;
                }


                if (master.status != "PENDING")
                {
                    throw new Exception(
                        $"Transfer cannot be dispatched because current status is {master.status}");
                }


                // ----------------------------------------------------
                // Get Details
                // ----------------------------------------------------

                var details =
                    await conn.QueryAsync<stock_transfer_detail>(
                        @"
                SELECT *
                FROM stock_transfer_detail
                WHERE transfercode = @transfercode
                  AND tenantcode = @tenantcode
                ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);


                foreach (var detail in details)
                {
                    // ------------------------------------------------
                    // Find source stock
                    //
                    // warehouse + item + batch + tenant
                    // ------------------------------------------------

                    var sourceStock =
                        await conn.QueryFirstOrDefaultAsync<stock_master>(
                            @"
                    SELECT *
                    FROM stock_master
                    WHERE warehousecode = @warehousecode
                      AND itemcode = @itemcode
                      AND batchno = @batchno
                      AND tenantcode = @tenantcode
                      AND deleted = false
                      AND isactive = true
                    FOR UPDATE
                    ",
                            new
                            {
                                warehousecode =
                                    master.fromwarehousecode,

                                itemcode =
                                    detail.itemcode,

                                batchno =
                                    detail.batchno,

                                tenantcode
                            },
                            transaction);


                    if (sourceStock == null)
                    {
                        throw new Exception(
                            $"Source stock not found. " +
                            $"Warehouse: {master.fromwarehousecode}, " +
                            $"Item: {detail.itemcode}, " +
                            $"Batch: {detail.batchno}");
                    }


                    // ------------------------------------------------
                    // Check stock
                    // ------------------------------------------------

                    if (sourceStock.closingstock <
                        detail.transferqty)
                    {
                        throw new Exception(
                            $"Insufficient stock for Item {detail.itemcode}, " +
                            $"Batch {detail.batchno}. " +
                            $"Available: {sourceStock.closingstock}, " +
                            $"Required: {detail.transferqty}");
                    }


                    // ------------------------------------------------
                    // Deduct source stock
                    // ------------------------------------------------

                    decimal newClosingStock =
                        sourceStock.closingstock -
                        detail.transferqty;


                    await conn.ExecuteAsync(
                        @"
                UPDATE stock_master
                SET
                    closingstock = @closingstock
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode
                ",
                        new
                        {
                            stockcode =
                                sourceStock.stockcode,

                            closingstock =
                                newClosingStock,

                            tenantcode
                        },
                        transaction);
                }


                // ----------------------------------------------------
                // Update transfer status
                // ----------------------------------------------------

                await conn.ExecuteAsync(
                    @"
            UPDATE stock_transfer_master
            SET
                status = 'IN_TRANSIT',
                dispatchedby = @usercode,
                dispatcheddate = CURRENT_TIMESTAMP
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                    new
                    {
                        transfercode,
                        usercode,
                        tenantcode
                    },
                    transaction);


                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ============================================================
        // RECEIVE
        //
        // IN_TRANSIT → RECEIVED
        //
        // Add stock to destination warehouse.
        // ============================================================

        public async Task<bool> ReceiveStockTransfer(
    long transfercode,
    long usercode,
    string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);

            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ----------------------------------------------------
                // 1. Get Transfer Master
                // ----------------------------------------------------

                var master =
                    await conn.QueryFirstOrDefaultAsync<stock_transfer_master>(
                        @"
            SELECT *
            FROM stock_transfer_master
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            FOR UPDATE
            ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);

                if (master == null)
                {
                    return false;
                }

                if (master.status != "IN_TRANSIT")
                {
                    throw new Exception(
                        $"Transfer cannot be received because current status is {master.status}");
                }

                // ----------------------------------------------------
                // 2. Get Transfer Details
                // ----------------------------------------------------

                var details =
                    await conn.QueryAsync<stock_transfer_detail>(
                        @"
            SELECT *
            FROM stock_transfer_detail
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);

                if (!details.Any())
                {
                    throw new Exception("No transfer details found.");
                }

                // ----------------------------------------------------
                // 3. Process each item
                // ----------------------------------------------------

                foreach (var detail in details)
                {
                    if (detail.transferqty <= 0)
                    {
                        throw new Exception(
                            $"Invalid transfer quantity for item {detail.itemcode}.");
                    }

                    // ------------------------------------------------
                    // Find destination stock
                    // warehouse + item + batch + tenant
                    // ------------------------------------------------

                    var destinationStock =
                        await conn.QueryFirstOrDefaultAsync<stock_master>(
                            @"
                SELECT *
                FROM stock_master
                WHERE warehousecode = @warehousecode
                  AND itemcode = @itemcode
                  AND batchno = @batchno
                  AND tenantcode = @tenantcode
                  AND deleted = false
                  AND isactive = true
                FOR UPDATE
                ",
                            new
                            {
                                warehousecode = master.towarehousecode,
                                itemcode = detail.itemcode,
                                batchno = detail.batchno,
                                tenantcode
                            },
                            transaction);

                    // ------------------------------------------------
                    // 4. Destination stock already exists
                    // ------------------------------------------------

                    if (destinationStock != null)
                    {
                        decimal newClosingStock =
                            destinationStock.closingstock +
                            detail.transferqty;

                        decimal newStockValue =
                            newClosingStock *
                            destinationStock.unitcost;

                        await conn.ExecuteAsync(
                            @"
                UPDATE stock_master
                SET
                    closingstock = @closingstock,
                    stockvalue = @stockvalue,
                    modifieddate = CURRENT_TIMESTAMP,
                    usercode = @usercode
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode
                ",
                            new
                            {
                                stockcode = destinationStock.stockcode,
                                closingstock = newClosingStock,
                                stockvalue = newStockValue,
                                usercode,
                                tenantcode
                            },
                            transaction);
                    }

                    // ------------------------------------------------
                    // 5. Destination stock does not exist
                    // ------------------------------------------------

                    else
                    {


                        await conn.ExecuteAsync(
                        @"
                INSERT INTO stock_master
                (
                    
                    itemcode,
                    warehousecode,
                    branchcode,
                    locationcode,
                    openingstock,
                    purchasedqty,
                    soldqty,
                    damagedqty,
                    returnqty,
                    adjustedqty,
                    adjusteddate,
                    adjustedby,
                    adjustmentreason,
                    adjustmenttype,
                    closingstock,
                    unitcost,
                    stockvalue,
                    batchno,
                    manufacturingdate,
                    expirydate,
                    isactive,
                    deleted,
                    createddate,
                    modifieddate,
                    usercode,
                    tenantcode,
                    companycode
                )
                VALUES
                (
                    @itemcode,
                    @warehousecode,
                    @branchcode,
                    NULL,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    @closingstock,
                    @unitcost,
                    @stockvalue,
                    @batchno,
                    @manufacturingdate,
                    @expirydate,
                    true,
                    false,
                    CURRENT_TIMESTAMP,
                    NULL,
                    @usercode,
                    @tenantcode,
                    @companycode
                )
                ",
                        new
                        {


                            itemcode = detail.itemcode,

                            warehousecode =
                                master.towarehousecode,

                            branchcode =
                                master.branchcode,

                            closingstock =
                                detail.transferqty,

                            unitcost =
                                detail.unitcost,

                            stockvalue =
                                detail.transferqty *
                                detail.unitcost,

                            batchno =
                                detail.batchno,

                            manufacturingdate =
                                detail.manufacturingdate,

                            expirydate =
                                detail.expirydate,

                            usercode,

                            tenantcode,

                            companycode = (string?)null
                        },
                        transaction);
                    }

                    // ------------------------------------------------
                    // 6. Update transfer detail
                    // ------------------------------------------------

                    await conn.ExecuteAsync(
                        @"
            UPDATE stock_transfer_detail
            SET
                approvedqty = @approvedqty
            WHERE transferdetailcode = @transferdetailcode
              AND tenantcode = @tenantcode
            ",
                        new
                        {
                            approvedqty = detail.transferqty,

                            transferdetailcode =
                                detail.transferdetailcode,

                            tenantcode
                        },
                        transaction);
                }

                // ----------------------------------------------------
                // 7. Update Transfer Master
                // ----------------------------------------------------

                await conn.ExecuteAsync(
                    @"
        UPDATE stock_transfer_master
        SET
            status = 'RECEIVED',
            receivedby = @usercode,
            receiveddate = CURRENT_TIMESTAMP
        WHERE transfercode = @transfercode
          AND tenantcode = @tenantcode
        ",
                    new
                    {
                        transfercode,
                        usercode,
                        tenantcode
                    },
                    transaction);

                // ----------------------------------------------------
                // 8. Commit
                // ----------------------------------------------------

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ============================================================
        // REJECT
        //
        // IN_TRANSIT → REJECTED
        //
        // Return stock to source warehouse.
        // ============================================================

        public async Task<bool> RejectStockTransfer(
            long transfercode,
            long usercode,
            string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);

            await conn.OpenAsync();

            using var transaction =
                await conn.BeginTransactionAsync();

            try
            {
                // ----------------------------------------------------
                // Get transfer
                // ----------------------------------------------------

                var master =
                    await conn.QueryFirstOrDefaultAsync<stock_transfer_master>(
                        @"
                SELECT *
                FROM stock_transfer_master
                WHERE transfercode = @transfercode
                  AND tenantcode = @tenantcode
                FOR UPDATE
                ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);


                if (master == null)
                {
                    return false;
                }


                if (master.status != "IN_TRANSIT")
                {
                    throw new Exception(
                        $"Transfer cannot be rejected because current status is {master.status}");
                }


                // ----------------------------------------------------
                // Get details
                // ----------------------------------------------------

                var details =
                    await conn.QueryAsync<stock_transfer_detail>(
                        @"
                SELECT *
                FROM stock_transfer_detail
                WHERE transfercode = @transfercode
                  AND tenantcode = @tenantcode
                ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);


                foreach (var detail in details)
                {
                    // ------------------------------------------------
                    // Find source stock
                    // ------------------------------------------------

                    var sourceStock =
                        await conn.QueryFirstOrDefaultAsync<stock_master>(
                            @"
                    SELECT *
                    FROM stock_master
                    WHERE warehousecode = @warehousecode
                      AND itemcode = @itemcode
                      AND batchno = @batchno
                      AND tenantcode = @tenantcode
                      AND deleted = false
                      AND isactive = true
                    FOR UPDATE
                    ",
                            new
                            {
                                warehousecode =
                                    master.fromwarehousecode,

                                itemcode =
                                    detail.itemcode,

                                batchno =
                                    detail.batchno,

                                tenantcode
                            },
                            transaction);


                    if (sourceStock == null)
                    {
                        throw new Exception(
                            $"Source stock not found while rejecting transfer. " +
                            $"Item: {detail.itemcode}, " +
                            $"Batch: {detail.batchno}");
                    }


                    // ------------------------------------------------
                    // Return stock to source
                    // ------------------------------------------------

                    decimal newClosingStock =
                        sourceStock.closingstock +
                        detail.transferqty;


                    await conn.ExecuteAsync(
                        @"
                UPDATE stock_master
                SET
                    closingstock = @closingstock
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode
                ",
                        new
                        {
                            stockcode =
                                sourceStock.stockcode,

                            closingstock =
                                newClosingStock,

                            tenantcode
                        },
                        transaction);
                }


                // ----------------------------------------------------
                // Update status
                // ----------------------------------------------------

                await conn.ExecuteAsync(
                    @"
            UPDATE stock_transfer_master
            SET
                status = 'REJECTED',
                rejectedby = @usercode,
                rejecteddate = CURRENT_TIMESTAMP
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                    new
                    {
                        transfercode,
                        usercode,
                        tenantcode
                    },
                    transaction);


                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> PartialRejectStockTransfer(
long transfercode,
long usercode,
string tenantcode,
stock_transfer_partial_reject_request request)
        {
            using var conn = new NpgsqlConnection(con);

            await conn.OpenAsync();

            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ====================================================
                // 1. GET TRANSFER MASTER
                // ====================================================

                var master =
                    await conn.QueryFirstOrDefaultAsync<stock_transfer_master>(
                        @"
            SELECT *
            FROM stock_transfer_master
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            FOR UPDATE
            ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction);

                if (master == null)
                {
                    return false;
                }

                // ====================================================
                // 2. CHECK TRANSFER STATUS
                // ====================================================

                if (master.status != "IN_TRANSIT")
                {
                    throw new Exception(
                        $"Transfer cannot be partially received because current status is {master.status}");
                }

                // ====================================================
                // 3. CHECK WAREHOUSES
                // ====================================================

                if (master.fromwarehousecode == null)
                {
                    throw new Exception("Source warehouse is missing.");
                }

                if (master.towarehousecode == null)
                {
                    throw new Exception("Destination warehouse is missing.");
                }

                // ====================================================
                // 4. CHECK REQUEST DETAILS
                // ====================================================

                if (request.details == null ||
                    request.details.Count == 0)
                {
                    throw new Exception(
                        "No partial rejection details provided.");
                }

                // ====================================================
                // 5. GET TRANSFER DETAILS
                // ====================================================

                var transferDetails =
                    (await conn.QueryAsync<stock_transfer_detail>(
                        @"
            SELECT *
            FROM stock_transfer_detail
            WHERE transfercode = @transfercode
              AND tenantcode = @tenantcode
            FOR UPDATE
            ",
                        new
                        {
                            transfercode,
                            tenantcode
                        },
                        transaction)).ToList();

                if (transferDetails.Count == 0)
                {
                    throw new Exception(
                        "No transfer details found.");
                }

                // ====================================================
                // 6. PROCESS EACH DETAIL
                // ====================================================

                foreach (var action in request.details)
                {
                    var detail =
                        transferDetails.FirstOrDefault(x =>
                            x.transferdetailcode ==
                            action.transferdetailcode);

                    if (detail == null)
                    {
                        throw new Exception(
                            $"Transfer detail {action.transferdetailcode} not found.");
                    }

                    // ------------------------------------------------
                    // Validate quantities
                    // ------------------------------------------------

                    if (action.acceptedqty < 0)
                    {
                        throw new Exception(
                            $"Accepted quantity cannot be negative for item {detail.itemcode}.");
                    }

                    if (action.rejectedqty < 0)
                    {
                        throw new Exception(
                            $"Rejected quantity cannot be negative for item {detail.itemcode}.");
                    }

                    decimal processedQty =
                        action.acceptedqty +
                        action.rejectedqty;

                    // ------------------------------------------------
                    // Example:
                    // Transfer Qty = 10
                    // Accepted     = 7
                    // Rejected     = 3
                    // Total        = 10
                    // ------------------------------------------------

                    if (processedQty != detail.transferqty)
                    {
                        throw new Exception(
                            $"Invalid quantity for item {detail.itemcode}. " +
                            $"Transfer quantity = {detail.transferqty}, " +
                            $"Accepted + Rejected = {processedQty}.");
                    }

                    // =================================================
                    // 7. ADD ACCEPTED QUANTITY TO DESTINATION
                    // =================================================

                    if (action.acceptedqty > 0)
                    {
                        var destinationStock =
                            await conn.QueryFirstOrDefaultAsync<stock_master>(
                                @"
                    SELECT *
                    FROM stock_master
                    WHERE warehousecode = @warehousecode
                      AND itemcode = @itemcode
                      AND batchno = @batchno
                      AND tenantcode = @tenantcode
                      AND deleted = false
                      AND isactive = true
                    FOR UPDATE
                    ",
                                new
                                {
                                    warehousecode =
                                        master.towarehousecode,

                                    itemcode =
                                        detail.itemcode,

                                    batchno =
                                        detail.batchno,

                                    tenantcode
                                },
                                transaction);

                        // ------------------------------------------------
                        // Destination stock exists
                        // ------------------------------------------------

                        if (destinationStock != null)
                        {
                            decimal newClosingStock =
                                destinationStock.closingstock +
                                action.acceptedqty;

                            decimal unitcost =
                                destinationStock.unitcost > 0
                                    ? destinationStock.unitcost
                                    : detail.unitcost;

                            decimal newStockValue =
                                newClosingStock * unitcost;

                            await conn.ExecuteAsync(
                                @"
                    UPDATE stock_master
                    SET
                        closingstock = @closingstock,
                        stockvalue = @stockvalue,
                        modifieddate = CURRENT_TIMESTAMP,
                        usercode = @usercode
                    WHERE stockcode = @stockcode
                      AND tenantcode = @tenantcode
                    ",
                                new
                                {
                                    stockcode =
                                        destinationStock.stockcode,

                                    closingstock =
                                        newClosingStock,

                                    stockvalue =
                                        newStockValue,

                                    usercode,
                                    tenantcode
                                },
                                transaction);
                        }

                        // ------------------------------------------------
                        // Destination stock does not exist
                        // ------------------------------------------------

                        else
                        {


                            decimal stockValue =
                                action.acceptedqty *
                                detail.unitcost;

                            await conn.ExecuteAsync(
                                @"
                    INSERT INTO stock_master
                    (
                        
                        itemcode,
                        warehousecode,
                        branchcode,
                        locationcode,
                        openingstock,
                        purchasedqty,
                        soldqty,
                        damagedqty,
                        returnqty,
                        closingstock,
                        unitcost,
                        stockvalue,
                        batchno,
                        manufacturingdate,
                        expirydate,
                        isactive,
                        deleted,
                        createddate,
                        modifieddate,
                        usercode,
                        tenantcode,
                        companycode
                    )
                    VALUES
                    (
                        
                        @itemcode,
                        @warehousecode,
                        @branchcode,
                        NULL,
                        0,
                        0,
                        0,
                        0,
                        0,
                        @closingstock,
                        @unitcost,
                        @stockvalue,
                        @batchno,
                        @manufacturingdate,
                        @expirydate,
                        true,
                        false,
                        CURRENT_TIMESTAMP,
                        NULL,
                        @usercode,
                        @tenantcode,
                        NULL
                    )
                    ",
                                new
                                {


                                    itemcode =
                                        detail.itemcode,

                                    warehousecode =
                                        master.towarehousecode,

                                    branchcode =
                                        master.branchcode,

                                    closingstock =
                                        action.acceptedqty,

                                    unitcost =
                                        detail.unitcost,

                                    stockvalue =
                                        stockValue,

                                    batchno =
                                        detail.batchno,

                                    manufacturingdate =
                                        detail.manufacturingdate,

                                    expirydate =
                                        detail.expirydate,

                                    usercode,
                                    tenantcode
                                },
                                transaction);
                        }
                    }

                    // =================================================
                    // 8. RETURN REJECTED QUANTITY TO SOURCE
                    // =================================================

                    if (action.rejectedqty > 0)
                    {
                        var sourceStock =
                            await conn.QueryFirstOrDefaultAsync<stock_master>(
                                @"
                    SELECT *
                    FROM stock_master
                    WHERE warehousecode = @warehousecode
                      AND itemcode = @itemcode
                      AND batchno = @batchno
                      AND tenantcode = @tenantcode
                      AND deleted = false
                      AND isactive = true
                    FOR UPDATE
                    ",
                                new
                                {
                                    warehousecode =
                                        master.fromwarehousecode,

                                    itemcode =
                                        detail.itemcode,

                                    batchno =
                                        detail.batchno,

                                    tenantcode
                                },
                                transaction);

                        if (sourceStock == null)
                        {
                            throw new Exception(
                                $"Source stock not found. " +
                                $"Warehouse: {master.fromwarehousecode}, " +
                                $"Item: {detail.itemcode}, " +
                                $"Batch: {detail.batchno}");
                        }

                        // ------------------------------------------------
                        // Add rejected quantity back to source
                        // ------------------------------------------------

                        decimal newSourceClosingStock =
                            sourceStock.closingstock +
                            action.rejectedqty;

                        decimal newSourceStockValue =
                            newSourceClosingStock *
                            sourceStock.unitcost;

                        await conn.ExecuteAsync(
                            @"
                UPDATE stock_master
                SET
                    closingstock = @closingstock,
                    stockvalue = @stockvalue,
                    modifieddate = CURRENT_TIMESTAMP,
                    usercode = @usercode
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode
                ",
                            new
                            {
                                stockcode =
                                    sourceStock.stockcode,

                                closingstock =
                                    newSourceClosingStock,

                                stockvalue =
                                    newSourceStockValue,

                                usercode,
                                tenantcode
                            },
                            transaction);
                    }

                    // =================================================
                    // 9. UPDATE TRANSFER DETAIL
                    // =================================================
                    //
                    // NOTE:
                    // Your current stock_transfer_detail class does
                    // not contain receivedqty/rejectedqty.
                    //
                    // These columns must exist in PostgreSQL.
                    // =================================================

                    await conn.ExecuteAsync(
                        @"
            UPDATE stock_transfer_detail
            SET
                approvedqty = @approvedqty,
                remarks = @remarks
            WHERE transferdetailcode = @transferdetailcode
              AND transfercode = @transfercode
              AND tenantcode = @tenantcode
            ",
                        new
                        {
                            approvedqty =
                                action.acceptedqty,

                            remarks =
                                action.remarks,

                            transferdetailcode =
                                action.transferdetailcode,

                            transfercode,
                            tenantcode
                        },
                        transaction);
                }

                // ====================================================
                // 10. UPDATE MASTER STATUS
                // ====================================================

                await conn.ExecuteAsync(
                    @"
        UPDATE stock_transfer_master
        SET
            status = 'PARTIALLY_RECEIVED',
            receivedby = @usercode,
            receiveddate = CURRENT_TIMESTAMP
        WHERE transfercode = @transfercode
          AND tenantcode = @tenantcode
        ",
                    new
                    {
                        transfercode,
                        usercode,
                        tenantcode
                    },
                    transaction);

                // ====================================================
                // 11. COMMIT
                // ====================================================

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        // ─── PHARMACY QUEUE (prescription → pharmacy dispensing) ───────────────────────

        public async Task<string> ReceivePrescription(ReceivePrescriptionRequest req, string tenant_code)
        {
            if (req.items == null || !req.items.Any())
                return "Success|Inserted:0";

            try
            {
                using IDbConnection db = new NpgsqlConnection(con);
                db.Open();
                using var tx = db.BeginTransaction();

                int inserted = 0;

                foreach (var item in req.items)
                {
                    var match = await db.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT itemcode, itemname FROM item_master
                  WHERE deleted = false AND tenantcode = @tenant_code
                  AND itemname ILIKE @drug_name
                  LIMIT 1",
                        new { tenant_code, drug_name = item.drug_name }, tx);

                    if (match == null)
                    {
                        match = await db.QueryFirstOrDefaultAsync<dynamic>(
                            @"SELECT itemcode, itemname FROM item_master
                      WHERE deleted = false AND tenantcode = @tenant_code
                      AND itemname ILIKE @pattern
                      LIMIT 1",
                            new { tenant_code, pattern = $"%{item.drug_name}%" }, tx);
                    }

                    var row = new PharmacyPrescriptionQueueRow
                    {
                        pr_det_id = string.IsNullOrWhiteSpace(item.pr_det_id) ? null : Guid.Parse(item.pr_det_id),
                        pr_code = req.pr_code,
                        sheet_id = string.IsNullOrWhiteSpace(req.sheet_id) ? null : Guid.Parse(req.sheet_id),
                        op_id = string.IsNullOrWhiteSpace(req.op_id) ? null : Guid.Parse(req.op_id),
                        ip_id = string.IsNullOrWhiteSpace(req.ip_id) ? null : Guid.Parse(req.ip_id),
                        custid = req.custid,
                        drug_name = item.drug_name,
                        matched_itemcode = match != null ? (long?)match.itemcode : null,
                        matched_itemname = match != null ? (string)match.itemname : null,
                        qty = item.qty,
                        morning = item.morning,
                        afternoon = item.afternoon,
                        evening = item.evening,
                        night = item.night,
                        before_food = item.before_food,
                        after_food = item.after_food,
                        days = item.days,
                        route = item.route,
                        notes = item.notes,
                        status = match != null ? "MATCHED" : "PENDING",
                        tenant_code = tenant_code
                    };

                    await db.InsertAsync(row, tx);
                    inserted++;
                }

                tx.Commit();
                return $"Success|Inserted:{inserted}";
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<List<PharmacyPrescriptionQueueRow>> GetPharmacyQueue(string tenant_code, string? status = null)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            var rows = await db.QueryAsync<PharmacyPrescriptionQueueRow>(
                @"SELECT * FROM pharmacy_prescription_queue
          WHERE tenant_code = @tenant_code
          AND (@status IS NULL OR status = @status)
          ORDER BY created_at DESC",
                new { tenant_code, status });
            return rows.ToList();
        }

        // Groups all queue rows for a patient by prescription, so a 2-3 drug prescription
        // shows as ONE list, not 2-3 separate rows the pharmacist has to piece together.
        public async Task<List<PharmacyQueueGroup>> GetPharmacyQueueByCustId(
            decimal custid, string tenant_code, string? status = null)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            var rows = (await db.QueryAsync<PharmacyPrescriptionQueueRow>(
                @"SELECT * FROM pharmacy_prescription_queue
          WHERE custid = @custid AND tenant_code = @tenant_code
          AND (@status IS NULL OR status = @status)
          ORDER BY created_at DESC",
                new { custid, tenant_code, status })).ToList();

            return rows
                .GroupBy(r => new { r.pr_code, r.sheet_id })
                .Select(g => new PharmacyQueueGroup
                {
                    custid = custid,
                    pr_code = g.Key.pr_code,
                    sheet_id = g.Key.sheet_id,
                    op_id = g.First().op_id,
                    ip_id = g.First().ip_id,
                    items = g.ToList()
                })
                .OrderByDescending(g => g.items.Max(i => i.created_at))
                .ToList();
        }

        // Generic status update — covers "buy 2 of 3" partial-dispense workflows too,
        // since you can flip individual queue rows independently (each drug is its own row).
        public async Task<string> UpdatePharmacyQueueStatus(Guid queue_id, string status, string tenant_code)
        {
            var allowed = new[] { "PENDING", "MATCHED", "DISPENSED", "CANCELLED" };
            if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
                return $"Invalid status. Allowed: {string.Join(", ", allowed)}";

            using IDbConnection db = new NpgsqlConnection(con);
            int rows = await db.ExecuteAsync(
                @"UPDATE pharmacy_prescription_queue
          SET status = @status, updated_at = NOW()
          WHERE queue_id = @queue_id AND tenant_code = @tenant_code",
                new { status = status.ToUpper(), queue_id, tenant_code });

            return rows > 0 ? "Success" : "Queue item not found";
        }
        // ─── GET ALL — STOCK ADJUSTMENT ───────────────────────────────────────────────
        public async Task<IEnumerable<stock_adjustment_log>> GetAllStockAdjustments(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            string query = @"
                SELECT
                    adjustmentlogcode,
                    stockcode,
                    itemcode,
                    warehousecode,
                    branchcode,
                    locationcode,
                    batchno,
                    beforeqty,
                    adjustedqty,
                    afterqty,
                    unitcost,
                    stockvalue,
                    adjustmenttype,
                    adjustmentreason,
                    remarks,
                    adjustmentdate,
                    adjustedby,
                    isactive,
                    deleted,
                    createddate,
                    modifieddate,
                    tenantcode,
                    companycode
                FROM public.stock_adjustment_log
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY adjustmentlogcode DESC;";

            return await db.QueryAsync<stock_adjustment_log>(query, new { tenantcode });
        }

        // ─── GET ALL — SALES RETURN ────────────────────────────────────────────────────
        public async Task<IEnumerable<sales_return_master>> GetAllSalesReturns(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            string query = @"
                SELECT
                    salesreturncode,
                    salesdetailcode,
                    salescode,
                    itemcode,
                    customercode,
                    batchno,
                    returnqty,
                    packsize,
                    totalqty,
                    rate,
                    amount,
                    warehousecode,
                    remarks,
                    isactive,
                    deleted,
                    createddate,
                    usercode,
                    tenantcode
                FROM public.sales_return_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY salesreturncode DESC;";

            return await db.QueryAsync<sales_return_master>(query, new { tenantcode });
        }

        // ─── GET ALL — PURCHASE RETURN ─────────────────────────────────────────────────
        public async Task<IEnumerable<purchase_return_master>> GetAllPurchaseReturns(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            string query = @"
                SELECT
                    purchasereturncode,
                    purchasedetailcode,
                    purchasecode,
                    itemcode,
                    vendorcode,
                    batchno,
                    returnqty,
                    packsize,
                    totalqty,
                    rate,
                    amount,
                    warehousecode,
                    remarks,
                    isactive,
                    deleted,
                    createddate,
                    usercode,
                    tenantcode
                FROM public.purchase_return_master
                WHERE deleted = false
                  AND tenantcode = @tenantcode
                ORDER BY purchasereturncode DESC;";

            return await db.QueryAsync<purchase_return_master>(query, new { tenantcode });
        }
        // ─── UPSERT — STOCK ADJUSTMENT ─────────────────────────────────────────────────
        // If adjustmentlog.adjustmentlogcode is 0/null -> Insert (same as InsertStockAdjustment)
        // If adjustmentlog.adjustmentlogcode has a value -> Update (same as UpdateStockAdjustment)
        public async Task<string> UpsertStockAdjustment(stock_adjustment_request request)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();

            try
            {
                bool isInsert = request.adjustmentlog.adjustmentlogcode == 0;

                //=================================================
                // 1. Check Stock Exists - Tenant Wise
                //=================================================

                string checkStockQuery = @"
SELECT stockcode, closingstock, unitcost
FROM stock_master
WHERE stockcode = @stockcode
AND tenantcode = @tenantcode
AND deleted = false;";

                var stock = await conn.QueryFirstOrDefaultAsync<stock_master>(
                    checkStockQuery,
                    new
                    {
                        stockcode = request.stock.stockcode,
                        tenantcode = request.stock.tenantcode
                    },
                    tran);

                if (stock == null)
                {
                    await tran.RollbackAsync();
                    return "Stock record not found.";
                }

                if (isInsert)
                {
                    //=================================================
                    // INSERT PATH
                    //=================================================

                    decimal afterQty = request.adjustmentlog.afterqty;
                    decimal stockValue = afterQty * stock.unitcost;

                    string updateStockQuery = @"
UPDATE stock_master
SET
    closingstock = @closingstock,
    stockvalue = @stockvalue,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode = @stockcode;";

                    await conn.ExecuteAsync(
                        updateStockQuery,
                        new
                        {
                            closingstock = afterQty,
                            stockvalue = stockValue,
                            stockcode = request.stock.stockcode
                        },
                        tran);

                    string insertLogQuery = @"
INSERT INTO stock_adjustment_log
(
    stockcode,
    itemcode,
    warehousecode,
    branchcode,
    locationcode,
    batchno,
    beforeqty,
    adjustedqty,
    afterqty,
    unitcost,
    stockvalue,
    adjustmenttype,
    adjustmentreason,
    remarks,
    adjustmentdate,
    adjustedby,
    isactive,
    deleted,
    createddate,
    tenantcode,
    companycode
)
VALUES
(
    @stockcode,
    @itemcode,
    @warehousecode,
    @branchcode,
    @locationcode,
    @batchno,
    @beforeqty,
    @adjustedqty,
    @afterqty,
    @unitcost,
    @stockvalue,
    @adjustmenttype,
    @adjustmentreason,
    @remarks,
    CURRENT_TIMESTAMP,
    @adjustedby,
    @isactive,
    @deleted,
    CURRENT_TIMESTAMP,
    @tenantcode,
    @companycode
);";

                    await conn.ExecuteAsync(
                        insertLogQuery,
                        new
                        {
                            stockcode = request.stock.stockcode,
                            itemcode = request.adjustmentlog.itemcode,
                            warehousecode = request.adjustmentlog.warehousecode,
                            branchcode = request.adjustmentlog.branchcode,
                            locationcode = request.adjustmentlog.locationcode,
                            batchno = request.adjustmentlog.batchno,

                            beforeqty = request.adjustmentlog.beforeqty,
                            adjustedqty = request.adjustmentlog.adjustedqty,
                            afterqty = afterQty,

                            unitcost = stock.unitcost,
                            stockvalue = stockValue,

                            adjustmenttype = request.adjustmentlog.adjustmenttype,
                            adjustmentreason = request.adjustmentlog.adjustmentreason,
                            remarks = request.adjustmentlog.remarks,

                            adjustedby = request.adjustmentlog.adjustedby,

                            isactive = true,
                            deleted = false,

                            tenantcode = request.stock.tenantcode,
                            companycode = request.stock.companycode
                        },
                        tran);

                    await tran.CommitAsync();
                    return "Stock Adjustment Saved Successfully.";
                }
                else
                {
                    //=================================================
                    // UPDATE PATH
                    //=================================================

                    string checkLogQuery = @"
SELECT
    adjustmentlogcode,
    stockcode,
    beforeqty,
    adjustedqty,
    afterqty
FROM stock_adjustment_log
WHERE adjustmentlogcode = @adjustmentlogcode
AND tenantcode = @tenantcode
AND deleted = false;";

                    var adjustmentLog = await conn.QueryFirstOrDefaultAsync<stock_adjustment_log>(
                        checkLogQuery,
                        new
                        {
                            adjustmentlogcode = request.adjustmentlog.adjustmentlogcode,
                            tenantcode = request.stock.tenantcode
                        },
                        tran);

                    if (adjustmentLog == null)
                    {
                        await tran.RollbackAsync();
                        return "Stock adjustment record not found.";
                    }

                    decimal beforeQty = adjustmentLog.beforeqty;
                    decimal newAfterQty;

                    if (request.adjustmentlog.adjustmenttype == "Increase")
                    {
                        newAfterQty = beforeQty + request.adjustmentlog.adjustedqty;
                    }
                    else if (request.adjustmentlog.adjustmenttype == "Decrease")
                    {
                        newAfterQty = beforeQty - request.adjustmentlog.adjustedqty;
                    }
                    else
                    {
                        await tran.RollbackAsync();
                        return "Invalid adjustment type. Use Increase or Decrease.";
                    }

                    if (newAfterQty < 0)
                    {
                        await tran.RollbackAsync();
                        return "Stock quantity cannot be negative.";
                    }

                    decimal stockValue = newAfterQty * stock.unitcost;

                    string updateStockQuery = @"
UPDATE stock_master
SET
    adjustedqty = @adjustedqty,
    adjusteddate = CURRENT_TIMESTAMP,
    adjustedby = @adjustedby,
    adjustmentreason = @adjustmentreason,
    adjustmenttype = @adjustmenttype,
    closingstock = @closingstock,
    stockvalue = @stockvalue,
    modifieddate = CURRENT_TIMESTAMP
WHERE stockcode = @stockcode
AND tenantcode = @tenantcode
AND deleted = false;";

                    await conn.ExecuteAsync(
                        updateStockQuery,
                        new
                        {
                            adjustedqty = request.adjustmentlog.adjustedqty,
                            adjustedby = request.adjustmentlog.adjustedby,
                            adjustmentreason = request.adjustmentlog.adjustmentreason,
                            adjustmenttype = request.adjustmentlog.adjustmenttype,
                            closingstock = newAfterQty,
                            stockvalue = stockValue,
                            stockcode = request.stock.stockcode,
                            tenantcode = request.stock.tenantcode
                        },
                        tran);

                    string updateLogQuery = @"
UPDATE stock_adjustment_log
SET
    itemcode = @itemcode,
    warehousecode = @warehousecode,
    branchcode = @branchcode,
    locationcode = @locationcode,
    batchno = @batchno,
    beforeqty = @beforeqty,
    adjustedqty = @adjustedqty,
    afterqty = @afterqty,
    unitcost = @unitcost,
    stockvalue = @stockvalue,
    adjustmenttype = @adjustmenttype,
    adjustmentreason = @adjustmentreason,
    remarks = @remarks,
    adjustedby = @adjustedby,
    modifieddate = CURRENT_TIMESTAMP,
    companycode = @companycode
WHERE adjustmentlogcode = @adjustmentlogcode
AND tenantcode = @tenantcode
AND deleted = false;";

                    int rows = await conn.ExecuteAsync(
                        updateLogQuery,
                        new
                        {
                            adjustmentlogcode = request.adjustmentlog.adjustmentlogcode,

                            itemcode = request.adjustmentlog.itemcode,
                            warehousecode = request.adjustmentlog.warehousecode,
                            branchcode = request.adjustmentlog.branchcode,
                            locationcode = request.adjustmentlog.locationcode,
                            batchno = request.adjustmentlog.batchno,

                            beforeqty = beforeQty,
                            adjustedqty = request.adjustmentlog.adjustedqty,
                            afterqty = newAfterQty,

                            unitcost = stock.unitcost,
                            stockvalue = stockValue,

                            adjustmenttype = request.adjustmentlog.adjustmenttype,
                            adjustmentreason = request.adjustmentlog.adjustmentreason,
                            remarks = request.adjustmentlog.remarks,

                            adjustedby = request.adjustmentlog.adjustedby,

                            companycode = request.stock.companycode,
                            tenantcode = request.stock.tenantcode
                        },
                        tran);

                    if (rows == 0)
                    {
                        await tran.RollbackAsync();
                        return "Stock adjustment record not found.";
                    }

                    await tran.CommitAsync();
                    return "Stock Adjustment Updated Successfully.";
                }
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                return $"Error : {ex.Message}";
            }
        }
        public async Task<IEnumerable<purchase_filter_result>> FilterPurchases(
    string? vendorname,
    string? billno,
    string? invoiceno,
    string? paymentstatus,
    DateTime? fromdate,
    DateTime? todate,
    string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            string query = @"
        SELECT
            m.purchasecode,
            m.billno,
            m.billdate::timestamp AS billdate,
            m.invoiceno,
            m.invoicedate::timestamp AS invoicedate,
            m.vendorcode,
            v.vendorname,
            m.grossamount,
            m.totalamount,
            m.taxamount,
            m.netamount,
            m.paymentmode,
            m.paymentstatus,
            m.chequeno,
            m.accountno,
            m.createddate::timestamp AS createddate
        FROM public.purchase_master m
        LEFT JOIN public.vendor_master v ON v.vendorcode = m.vendorcode
        WHERE m.deleted = false
          AND m.tenantcode = @tenantcode
          AND (@vendorname IS NULL OR v.vendorname ILIKE '%' || @vendorname || '%')
          AND (@billno IS NULL OR m.billno ILIKE '%' || @billno || '%')
          AND (@invoiceno IS NULL OR m.invoiceno ILIKE '%' || @invoiceno || '%')
          AND (@paymentstatus IS NULL OR m.paymentstatus = @paymentstatus)
          AND (@fromdate IS NULL OR m.createddate::date >= @fromdate)
          AND (@todate IS NULL OR m.createddate::date <= @todate)
        ORDER BY m.purchasecode DESC;";

            return await db.QueryAsync<purchase_filter_result>(query, new
            {
                tenantcode,
                vendorname,
                billno,
                invoiceno,
                paymentstatus,
                fromdate,
                todate
            });
        }

        // ─── PURCHASE BULK STATUS UPDATE ──────────────────────────────────────────────
        public async Task<int> UpdateMultiplePurchaseStatus(
    List<long> purchasecodes,
    string paymentstatus,
    string? paymentmode,
    string? chequeno,
    string? accountno,
    string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            db.Open();

            string query = @"
        UPDATE public.purchase_master
        SET paymentstatus = @paymentstatus,
            paymentmode   = COALESCE(@paymentmode, paymentmode),
            chequeno      = COALESCE(@chequeno, chequeno),
            accountno     = COALESCE(@accountno, accountno),
            modifieddate  = CURRENT_TIMESTAMP
        WHERE purchasecode = ANY(@purchasecodes)
          AND tenantcode = @tenantcode
          AND deleted = false;";

            int rows = await db.ExecuteAsync(query, new
            {
                paymentstatus,
                paymentmode,
                chequeno,
                accountno,
                purchasecodes = purchasecodes.ToArray(),
                tenantcode
            });

            return rows;
        }
        // ─── DOCTOR ROOM CONSUMPTION ────────────────────────────────────────────────────

        // List warehouses flagged as doctor rooms — for the "Doctor Room" dropdown
        public async Task<IEnumerable<warehouse_master>> GetDoctorRooms(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<warehouse_master>(@"
        SELECT * FROM public.warehouse_master
        WHERE isdoctor_room = true
          AND isdeleted = false
          AND tenantcode = @tenantcode
        ORDER BY warehousename;",
                new { tenantcode });
        }

        // Current stock sitting in a specific doctor room — populates the item/batch grid
        public async Task<IEnumerable<stock_master>> GetDoctorRoomStock(long roomwarehousecode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            return await db.QueryAsync<stock_master>(@"
        SELECT
            s.*,
            i.itemname
        FROM public.stock_master s
        LEFT JOIN public.item_master i ON i.itemcode = s.itemcode
        WHERE s.warehousecode = @roomwarehousecode
          AND s.tenantcode = @tenantcode
          AND s.deleted = false
          AND s.closingstock > 0
        ORDER BY s.stockcode DESC;",
                new { roomwarehousecode, tenantcode });
        }

        // Year-reset consumption number, same pattern as your GRN/Sales bill generators
        private async Task<string> GetNextConsumptionNo(IDbConnection db, IDbTransaction transaction, string tenantcode)
        {
            int year = DateTime.Now.Year;
            string yearPrefix = "DC" + year;

            await db.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtext(@lockkey));",
                new { lockkey = "CONSUMPTION_" + tenantcode + "_" + year },
                transaction);

            string query = @"
        SELECT MAX(
            CAST(SUBSTRING(consumptionno FROM LENGTH(@yearPrefix) + 1) AS bigint)
        )
        FROM public.consumption_master
        WHERE tenantcode = @tenantcode
          AND consumptionno LIKE @yearPrefix || '%'
          AND SUBSTRING(consumptionno FROM LENGTH(@yearPrefix) + 1) ~ '^[0-9]+$';";

            long? maxSeq = await db.ExecuteScalarAsync<long?>(
                query, new { tenantcode, yearPrefix }, transaction);

            long nextSeq = maxSeq.HasValue ? maxSeq.Value + 1 : 1;
            return yearPrefix + nextSeq.ToString("D4");   // e.g. DC20260001
        }

        public async Task<long> InsertConsumption(consumption_request request, string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();
            using var tran = await conn.BeginTransactionAsync();

            try
            {
                request.master.tenantcode = tenantcode;
                foreach (var d in request.details) d.tenantcode = tenantcode;

                if (request.details == null || request.details.Count == 0)
                    throw new Exception("At least one consumed item is required.");

                // ── Validate the selected location really is a doctor room ──
                var room = await conn.QueryFirstOrDefaultAsync<warehouse_master>(@"
            SELECT * FROM public.warehouse_master
            WHERE warehousecode = @roomwarehousecode
              AND tenantcode = @tenantcode
              AND isdeleted = false
            FOR UPDATE;",
                    new { request.master.roomwarehousecode, tenantcode }, tran);

                if (room == null)
                    throw new Exception("Doctor room / warehouse not found.");

                if (!room.isdoctor_room)
                    throw new Exception("Selected location is not marked as a Doctor Room.");

                // ── Generate consumption number ──
                request.master.consumptionno = await GetNextConsumptionNo(conn, tran, tenantcode);

                // ── Insert header (totals patched after processing lines) ──
                long consumptioncode = await conn.ExecuteScalarAsync<long>(@"
    INSERT INTO public.consumption_master
    (
        consumptionno, consumptiondate, roomwarehousecode,
        dcode, custid,
        departmentname, doctorname, remarks,
        totalitems, totalqty, totalvalue,
        isactive, deleted, createddate, usercode, tenantcode, branchcode
    )
    VALUES
    (
        @consumptionno, @consumptiondate, @roomwarehousecode,
        @dcode, @custid,
        @departmentname, @doctorname, @remarks,
        0, 0, 0,
        true, false, CURRENT_TIMESTAMP, @usercode, @tenantcode, @branchcode
    )
    RETURNING consumptioncode;",
    request.master, tran);

                decimal totalqty = 0, totalvalue = 0;

                foreach (var item in request.details)
                {
                    if (item.consumedqty <= 0)
                        throw new Exception($"Consumed quantity must be greater than zero (stock {item.stockcode}).");

                    // ── Lock the EXACT stock row the user selected ──
                    var stock = await conn.QueryFirstOrDefaultAsync<stock_master>(@"
                SELECT * FROM public.stock_master
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode
                  AND deleted = false
                FOR UPDATE;",
                        new { item.stockcode, tenantcode }, tran);

                    if (stock == null)
                        throw new Exception($"Stock record {item.stockcode} not found.");

                    if (stock.warehousecode != request.master.roomwarehousecode)
                        throw new Exception(
                            $"Stock {item.stockcode} does not belong to the selected doctor room.");

                    if (item.consumedqty > stock.closingstock)
                        throw new Exception(
                            $"Consumption quantity ({item.consumedqty}) exceeds available stock " +
                            $"({stock.closingstock}) for item {item.itemcode}.");

                    decimal newClosing = stock.closingstock - item.consumedqty;
                    decimal newValue = newClosing * stock.unitcost;

                    // ── Deduct from stock_master (single source of truth, same as your other flows) ──
                    await conn.ExecuteAsync(@"
                UPDATE public.stock_master
                SET closingstock = @closingstock,
                    stockvalue   = @stockvalue,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE stockcode = @stockcode;",
                        new { closingstock = newClosing, stockvalue = newValue, item.stockcode }, tran);

                    decimal lineValue = item.consumedqty * stock.unitcost;

                    await conn.ExecuteAsync(@"
                INSERT INTO public.consumption_detail
                (
                    consumptioncode, stockcode, itemcode, batchno,
                    availableqty, consumedqty, unitcost, stockvalue,
                    remarks, tenantcode
                )
                VALUES
                (
                    @consumptioncode, @stockcode, @itemcode, @batchno,
                    @availableqty, @consumedqty, @unitcost, @stockvalue,
                    @remarks, @tenantcode
                );",
                        new
                        {
                            consumptioncode,
                            item.stockcode,
                            item.itemcode,
                            batchno = stock.batchno,
                            availableqty = stock.closingstock,   // BEFORE deduction — audit snapshot
                            item.consumedqty,
                            unitcost = stock.unitcost,
                            stockvalue = lineValue,
                            item.remarks,
                            tenantcode
                        }, tran);

                    totalqty += item.consumedqty;
                    totalvalue += lineValue;
                }

                // ── Patch header totals ──
                await conn.ExecuteAsync(@"
            UPDATE public.consumption_master
            SET totalitems = @totalitems,
                totalqty   = @totalqty,
                totalvalue = @totalvalue
            WHERE consumptioncode = @consumptioncode;",
                    new { totalitems = request.details.Count, totalqty, totalvalue, consumptioncode }, tran);

                await tran.CommitAsync();
                return consumptioncode;
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<consumption_request>> GetAllConsumptions(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var masters = (await db.QueryAsync<consumption_master>(@"
        SELECT * FROM public.consumption_master
        WHERE deleted = false AND tenantcode = @tenantcode
        ORDER BY consumptioncode DESC;",
                new { tenantcode })).ToList();

            var details = (await db.QueryAsync<consumption_detail>(@"
        SELECT d.*, i.itemname
        FROM public.consumption_detail d
        LEFT JOIN public.item_master i ON i.itemcode = d.itemcode
        WHERE d.tenantcode = @tenantcode;",
                new { tenantcode })).ToList();

            return masters.Select(m => new consumption_request
            {
                master = m,
                details = details.Where(d => d.consumptioncode == m.consumptioncode).ToList()
            });
        }

        public async Task<consumption_request?> GetConsumptionByCode(long consumptioncode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<consumption_master>(@"
        SELECT * FROM public.consumption_master
        WHERE consumptioncode = @consumptioncode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { consumptioncode, tenantcode });

            if (master == null) return null;

            var details = await db.QueryAsync<consumption_detail>(@"
        SELECT * FROM public.consumption_detail
        WHERE consumptioncode = @consumptioncode;",
                new { consumptioncode });

            return new consumption_request { master = master, details = details.ToList() };
        }

        // Soft delete + restore stock (mirror of DeleteStockAdjustment's restore logic)
        public async Task<string> DeleteConsumption(long consumptioncode, string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();
            using var tran = await conn.BeginTransactionAsync();

            try
            {
                var master = await conn.QueryFirstOrDefaultAsync<consumption_master>(@"
            SELECT * FROM public.consumption_master
            WHERE consumptioncode = @consumptioncode
              AND tenantcode = @tenantcode
              AND deleted = false;",
                    new { consumptioncode, tenantcode }, tran);

                if (master == null)
                    return "Consumption record not found.";

                var details = await conn.QueryAsync<consumption_detail>(@"
            SELECT * FROM public.consumption_detail
            WHERE consumptioncode = @consumptioncode;",
                    new { consumptioncode }, tran);

                foreach (var d in details)
                {
                    // Give the consumed quantity back to stock
                    await conn.ExecuteAsync(@"
                UPDATE public.stock_master
                SET closingstock = closingstock + @consumedqty,
                    stockvalue   = (closingstock + @consumedqty) * unitcost,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode;",
                        new { d.consumedqty, d.stockcode, tenantcode }, tran);
                }

                await conn.ExecuteAsync(@"
            UPDATE public.consumption_master
            SET deleted = true, isactive = false, modifieddate = CURRENT_TIMESTAMP
            WHERE consumptioncode = @consumptioncode
              AND tenantcode = @tenantcode;",
                    new { consumptioncode, tenantcode }, tran);

                await tran.CommitAsync();
                return "Success";
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }
        public async Task<string> UpdateConsumption(consumption_request request, string tenantcode)
        {
            using var conn = new NpgsqlConnection(con);
            await conn.OpenAsync();
            using var tran = await conn.BeginTransactionAsync();

            try
            {
                request.master.tenantcode = tenantcode;
                foreach (var d in request.details) d.tenantcode = tenantcode;

                if (request.master.consumptioncode <= 0)
                    throw new Exception("Valid consumptioncode is required.");

                if (request.details == null || request.details.Count == 0)
                    throw new Exception("At least one consumed item is required.");

                // ── 1. Lock & validate header exists ──
                var existingMaster = await conn.QueryFirstOrDefaultAsync<consumption_master>(@"
            SELECT * FROM public.consumption_master
            WHERE consumptioncode = @consumptioncode
              AND tenantcode = @tenantcode
              AND deleted = false
            FOR UPDATE;",
                    new { request.master.consumptioncode, tenantcode }, tran);

                if (existingMaster == null)
                    return "Consumption record not found.";

                // ── 2. Validate the (possibly changed) room ──
                var room = await conn.QueryFirstOrDefaultAsync<warehouse_master>(@"
            SELECT * FROM public.warehouse_master
            WHERE warehousecode = @roomwarehousecode
              AND tenantcode = @tenantcode
              AND isdeleted = false
            FOR UPDATE;",
                    new { request.master.roomwarehousecode, tenantcode }, tran);

                if (room == null)
                    throw new Exception("Doctor room / warehouse not found.");

                if (!room.isdoctor_room)
                    throw new Exception("Selected location is not marked as a Doctor Room.");

                // ── 3. Reverse OLD detail lines — give qty back to stock ──
                var oldDetails = await conn.QueryAsync<consumption_detail>(@"
            SELECT * FROM public.consumption_detail
            WHERE consumptioncode = @consumptioncode;",
                    new { request.master.consumptioncode }, tran);

                foreach (var od in oldDetails)
                {
                    await conn.ExecuteAsync(@"
                UPDATE public.stock_master
                SET closingstock = closingstock + @consumedqty,
                    stockvalue   = (closingstock + @consumedqty) * unitcost,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode;",
                        new { od.consumedqty, od.stockcode, tenantcode }, tran);
                }

                // ── 4. Delete old detail rows ──
                await conn.ExecuteAsync(@"
            DELETE FROM public.consumption_detail
            WHERE consumptioncode = @consumptioncode;",
                    new { request.master.consumptioncode }, tran);

                // ── 5. Update header ──
                int masterRows = await conn.ExecuteAsync(@"
    UPDATE public.consumption_master
    SET
        consumptiondate   = @consumptiondate,
        roomwarehousecode = @roomwarehousecode,
        dcode             = @dcode,
        custid            = @custid,
        departmentname    = @departmentname,
        doctorname        = @doctorname,
        remarks           = @remarks,
        modifieddate      = CURRENT_TIMESTAMP,
        usercode          = @usercode,
        branchcode        = @branchcode
    WHERE consumptioncode = @consumptioncode
      AND tenantcode = @tenantcode;",
    request.master, tran);

                if (masterRows == 0)
                    throw new Exception("Failed to update consumption header.");

                // ── 6. Re-apply NEW detail lines (same validation as Insert) ──
                decimal totalqty = 0, totalvalue = 0;

                foreach (var item in request.details)
                {
                    if (item.consumedqty <= 0)
                        throw new Exception($"Consumed quantity must be greater than zero (stock {item.stockcode}).");

                    var stock = await conn.QueryFirstOrDefaultAsync<stock_master>(@"
                SELECT * FROM public.stock_master
                WHERE stockcode = @stockcode
                  AND tenantcode = @tenantcode
                  AND deleted = false
                FOR UPDATE;",
                        new { item.stockcode, tenantcode }, tran);

                    if (stock == null)
                        throw new Exception($"Stock record {item.stockcode} not found.");

                    if (stock.warehousecode != request.master.roomwarehousecode)
                        throw new Exception(
                            $"Stock {item.stockcode} does not belong to the selected doctor room.");

                    if (item.consumedqty > stock.closingstock)
                        throw new Exception(
                            $"Consumption quantity ({item.consumedqty}) exceeds available stock " +
                            $"({stock.closingstock}) for item {item.itemcode}.");

                    decimal newClosing = stock.closingstock - item.consumedqty;
                    decimal newValue = newClosing * stock.unitcost;

                    await conn.ExecuteAsync(@"
                UPDATE public.stock_master
                SET closingstock = @closingstock,
                    stockvalue   = @stockvalue,
                    modifieddate = CURRENT_TIMESTAMP
                WHERE stockcode = @stockcode;",
                        new { closingstock = newClosing, stockvalue = newValue, item.stockcode }, tran);

                    decimal lineValue = item.consumedqty * stock.unitcost;

                    await conn.ExecuteAsync(@"
                INSERT INTO public.consumption_detail
                (
                    consumptioncode, stockcode, itemcode, batchno,
                    availableqty, consumedqty, unitcost, stockvalue,
                    remarks, tenantcode
                )
                VALUES
                (
                    @consumptioncode, @stockcode, @itemcode, @batchno,
                    @availableqty, @consumedqty, @unitcost, @stockvalue,
                    @remarks, @tenantcode
                );",
                        new
                        {
                            request.master.consumptioncode,
                            item.stockcode,
                            item.itemcode,
                            batchno = stock.batchno,
                            availableqty = stock.closingstock,   // snapshot BEFORE this deduction
                            item.consumedqty,
                            unitcost = stock.unitcost,
                            stockvalue = lineValue,
                            item.remarks,
                            tenantcode
                        }, tran);

                    totalqty += item.consumedqty;
                    totalvalue += lineValue;
                }

                // ── 7. Patch header totals ──
                await conn.ExecuteAsync(@"
            UPDATE public.consumption_master
            SET totalitems = @totalitems,
                totalqty   = @totalqty,
                totalvalue = @totalvalue
            WHERE consumptioncode = @consumptioncode;",
                    new
                    {
                        totalitems = request.details.Count,
                        totalqty,
                        totalvalue,
                        request.master.consumptioncode
                    }, tran);

                await tran.CommitAsync();
                return "Success";
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }
        public async Task<string> CancelPharmacyQueueByPrDetId(Guid pr_det_id, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(con);
            int rows = await db.ExecuteAsync(
                @"UPDATE pharmacy_prescription_queue
          SET status = 'CANCELLED', updated_at = NOW()
          WHERE pr_det_id = @pr_det_id AND tenant_code = @tenant_code
          AND status <> 'DISPENSED'",
                new { pr_det_id, tenant_code });
            return rows > 0 ? "Success" : "Queue item not found";
        }
        // ═══════════════════════════════════════════════════════════════════════════
        //  PASTE THESE METHODS INTO YOUR EXISTING  ItemMasterClass  (InventoryClass namespace)
        //  Add "using medico_backend.InventoryModel;" if not already present (it already is).
        // ═══════════════════════════════════════════════════════════════════════════

        // ─── PURCHASE ORDER — number generator (same pattern as GetNextGrnCode / GetNextSalesBillNo) ──
        private async Task<string> GetNextPurchaseOrderNo(IDbConnection db, IDbTransaction transaction, string tenantcode)
        {
            int year = DateTime.Now.Year;
            string yearPrefix = "PO" + year;   // e.g. PO2026

            await db.ExecuteAsync(
                "SELECT pg_advisory_xact_lock(hashtext(@lockkey));",
                new { lockkey = "PURCHASEORDER_" + tenantcode + "_" + year },
                transaction);

            string query = @"
        SELECT MAX(
            CAST(SUBSTRING(orderno FROM LENGTH(@yearPrefix) + 1) AS bigint)
        )
        FROM public.purchase_order_master
        WHERE tenantcode = @tenantcode
          AND orderno LIKE @yearPrefix || '%'
          AND SUBSTRING(orderno FROM LENGTH(@yearPrefix) + 1) ~ '^[0-9]+$';";

            long? maxSeq = await db.ExecuteScalarAsync<long?>(
                query, new { tenantcode, yearPrefix }, transaction);

            long nextSeq = maxSeq.HasValue ? maxSeq.Value + 1 : 1;

            return yearPrefix + nextSeq.ToString("D4");   // e.g. PO20260001
        }

        // ─── INSERT ─────────────────────────────────────────────────────────────────
        public async Task<long> InsertPurchaseOrder(purchase_order_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        if (request.details == null || !request.details.Any())
                            throw new Exception("At least one item is required.");

                        request.master.orderno = await GetNextPurchaseOrderNo(
                            db, transaction, request.master.tenantcode);

                        request.master.status = string.IsNullOrWhiteSpace(request.master.status)
                            ? "PENDING"
                            : request.master.status;

                        string masterQuery = @"
                    INSERT INTO public.purchase_order_master
                    (
                        orderno, orderdate, llrcode, vendorcode,
                        overalltax, taxpercentage,
                        grossamount, discountamount, taxamount, roundoff, grandtotal,
                        status, remarks,
                        isactive, deleted, createddate,
                        usercode, tenantcode, branchcode, companycode
                    )
                    VALUES
                    (
                        @orderno, @orderdate, @llrcode, @vendorcode,
                        @overalltax, @taxpercentage,
                        @grossamount, @discountamount, @taxamount, @roundoff, @grandtotal,
                        @status, @remarks,
                        @isactive, @deleted, CURRENT_TIMESTAMP,
                        @usercode, @tenantcode, @branchcode, @companycode
                    )
                    RETURNING ordercode;";

                        long ordercode = await db.ExecuteScalarAsync<long>(masterQuery, request.master, transaction);

                        string detailQuery = @"
                    INSERT INTO public.purchase_order_detail
                    (
                        ordercode, itemcode, itemgroupcode,
                        quantity, packing, freeqty, mrp, orderrate,
                        discountpercentage, discountamount, amount,
                        tenantcode
                    )
                    VALUES
                    (
                        @ordercode, @itemcode, @itemgroupcode,
                        @quantity, @packing, @freeqty, @mrp, @orderrate,
                        @discountpercentage, @discountamount, @amount,
                        @tenantcode
                    );";

                        foreach (var item in request.details)
                        {
                            item.ordercode = ordercode;
                            item.tenantcode = request.master.tenantcode;

                            // amount = (qty * rate) - discountamount, guard against unset values
                            item.amount = (item.quantity * item.orderrate) - item.discountamount;

                            await db.ExecuteAsync(detailQuery, item, transaction);
                        }

                        if (request.terms != null && request.terms.Any())
                        {
                            string termQuery = @"
                        INSERT INTO public.purchase_order_terms
                        (ordercode, slno, termcondition, value, tenantcode)
                        VALUES
                        (@ordercode, @slno, @termcondition, @value, @tenantcode);";

                            foreach (var term in request.terms)
                            {
                                term.ordercode = ordercode;
                                term.tenantcode = request.master.tenantcode;
                                await db.ExecuteAsync(termQuery, term, transaction);
                            }
                        }

                        transaction.Commit();
                        return ordercode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Purchase Order insert failed: " + ex.Message);
                    }
                }
            }
        }

        // ─── UPDATE ─────────────────────────────────────────────────────────────────
        public async Task<long> UpdatePurchaseOrder(purchase_order_request request)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        if (request.master.ordercode <= 0)
                            throw new Exception("Valid ordercode is required.");

                        string masterQuery = @"
                    UPDATE public.purchase_order_master
                    SET
                        orderdate       = @orderdate,
                        llrcode         = @llrcode,
                        vendorcode      = @vendorcode,
                        overalltax      = @overalltax,
                        taxpercentage   = @taxpercentage,
                        grossamount     = @grossamount,
                        discountamount  = @discountamount,
                        taxamount       = @taxamount,
                        roundoff        = @roundoff,
                        grandtotal      = @grandtotal,
                        status          = @status,
                        remarks         = @remarks,
                        isactive        = @isactive,
                        deleted         = @deleted,
                        modifieddate    = CURRENT_TIMESTAMP,
                        usercode        = @usercode,
                        branchcode      = @branchcode,
                        companycode     = @companycode
                    WHERE ordercode = @ordercode
                      AND tenantcode = @tenantcode;";

                        int masterRows = await db.ExecuteAsync(masterQuery, request.master, transaction);

                        if (masterRows == 0)
                            throw new Exception("Purchase order not found.");

                        await db.ExecuteAsync(
                            "DELETE FROM public.purchase_order_detail WHERE ordercode = @ordercode;",
                            new { ordercode = request.master.ordercode }, transaction);

                        await db.ExecuteAsync(
                            "DELETE FROM public.purchase_order_terms WHERE ordercode = @ordercode;",
                            new { ordercode = request.master.ordercode }, transaction);

                        string detailQuery = @"
                    INSERT INTO public.purchase_order_detail
                    (
                        ordercode, itemcode, itemgroupcode,
                        quantity, packing, freeqty, mrp, orderrate,
                        discountpercentage, discountamount, amount,
                        tenantcode
                    )
                    VALUES
                    (
                        @ordercode, @itemcode, @itemgroupcode,
                        @quantity, @packing, @freeqty, @mrp, @orderrate,
                        @discountpercentage, @discountamount, @amount,
                        @tenantcode
                    );";

                        foreach (var item in request.details)
                        {
                            item.ordercode = request.master.ordercode;
                            item.tenantcode = request.master.tenantcode;
                            item.amount = (item.quantity * item.orderrate) - item.discountamount;

                            await db.ExecuteAsync(detailQuery, item, transaction);
                        }

                        if (request.terms != null && request.terms.Any())
                        {
                            string termQuery = @"
                        INSERT INTO public.purchase_order_terms
                        (ordercode, slno, termcondition, value, tenantcode)
                        VALUES
                        (@ordercode, @slno, @termcondition, @value, @tenantcode);";

                            foreach (var term in request.terms)
                            {
                                term.ordercode = request.master.ordercode;
                                term.tenantcode = request.master.tenantcode;
                                await db.ExecuteAsync(termQuery, term, transaction);
                            }
                        }

                        transaction.Commit();
                        return request.master.ordercode;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Purchase Order update failed: " + ex.Message);
                    }
                }
            }
        }

        // ─── DELETE (soft delete) ─────────────────────────────────────────────────
        public async Task<string> DeletePurchaseOrder(long ordercode, string tenantcode)
        {
            using (IDbConnection db = new NpgsqlConnection(con))
            {
                db.Open();

                string query = @"
            UPDATE public.purchase_order_master
            SET deleted = true, isactive = false, modifieddate = CURRENT_TIMESTAMP
            WHERE ordercode = @ordercode
              AND tenantcode = @tenantcode;";

                int rows = await db.ExecuteAsync(query, new { ordercode, tenantcode });
                return rows > 0 ? "Success" : "Failed";
            }
        }

        // ─── GET ALL ────────────────────────────────────────────────────────────────
        public async Task<IEnumerable<purchase_order_request>> GetAllPurchaseOrders(string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var masters = (await db.QueryAsync<purchase_order_master>(@"
        SELECT * FROM public.purchase_order_master
        WHERE deleted = false
          AND tenantcode = @tenantcode
        ORDER BY ordercode DESC;",
                new { tenantcode })).ToList();

            var details = (await db.QueryAsync<purchase_order_detail>(@"
        SELECT * FROM public.purchase_order_detail
        WHERE tenantcode = @tenantcode;",
                new { tenantcode })).ToList();

            var terms = (await db.QueryAsync<purchase_order_terms>(@"
        SELECT * FROM public.purchase_order_terms
        WHERE tenantcode = @tenantcode;",
                new { tenantcode })).ToList();

            return masters.Select(m => new purchase_order_request
            {
                master = m,
                details = details.Where(d => d.ordercode == m.ordercode).ToList(),
                terms = terms.Where(t => t.ordercode == m.ordercode).ToList()
            });
        }

        // ─── GET BY CODE ─────────────────────────────────────────────────────────────
        public async Task<purchase_order_request?> GetPurchaseOrderByCode(long ordercode, string tenantcode)
        {
            using IDbConnection db = new NpgsqlConnection(con);

            var master = await db.QueryFirstOrDefaultAsync<purchase_order_master>(@"
        SELECT * FROM public.purchase_order_master
        WHERE ordercode = @ordercode
          AND tenantcode = @tenantcode
          AND deleted = false;",
                new { ordercode, tenantcode });

            if (master == null) return null;

            var details = await db.QueryAsync<purchase_order_detail>(@"
        SELECT * FROM public.purchase_order_detail
        WHERE ordercode = @ordercode;",
                new { ordercode });

            var terms = await db.QueryAsync<purchase_order_terms>(@"
        SELECT * FROM public.purchase_order_terms
        WHERE ordercode = @ordercode
        ORDER BY slno;",
                new { ordercode });

            return new purchase_order_request
            {
                master = master,
                details = details.ToList(),
                terms = terms.ToList()
            };
        }
    }
}

