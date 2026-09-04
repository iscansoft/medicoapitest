using medico_backend.InventoryClass;
using medico_backend.InventoryModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.InventoryController
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class ItemMasterController : ControllerBase
    {
        private readonly ItemMasterClass itemclass;

        public ItemMasterController(ItemMasterClass _imc)
        {
            itemclass = _imc;
        }

        private string GetTenantCode() =>
            Request.Headers["tenantcode"].ToString();

        private IActionResult MissingTenantCode() =>
            BadRequest(new { Status = "Failed", Message = "Header 'tenantcode' is required." });

        // ─── ITEM MASTER ──────────────────────────────────────────────────────────────

        [HttpPost("insertitem")]
        public async Task<IActionResult> InsertItem([FromBody] item_master item)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                item.tenantcode = tenantcode; // always override from header

                var result = await itemclass.InsertItem(item);
                return Ok(new
                {
                    Status = "Success",
                    Message = item.itemcode == 0 ? "Item inserted successfully" : "Item updated successfully",
                    ItemCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updateitem")]
        public async Task<IActionResult> UpdateItem([FromBody] item_master item)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                item.tenantcode = tenantcode;

                var res = await itemclass.UpdateItem(item);
                return res == "Item Updated Successfully"
                    ? Ok(new { Status = "Success", Message = "Item updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to update item" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deleteitem")]
        public async Task<IActionResult> DeleteItem(long itemcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var res = await itemclass.DeleteItem(itemcode, tenantcode);
                return res == "Success"
                    ? Ok(new { Status = "Success", Message = "Item deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to delete item" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getallitems")]
        public async Task<IActionResult> GetAllItems()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllItems(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getitembycode")]
        public async Task<IActionResult> GetItemByCode(long itemcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetItemByCode(itemcode, tenantcode);
                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Item not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── VENDOR MASTER ────────────────────────────────────────────────────────────

        [HttpPost("upsertvendor")]
        public async Task<IActionResult> UpsertVendor([FromBody] vendor_master vendor)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                vendor.tenantcode = tenantcode;

                var result = await itemclass.UpsertVendor(vendor);
                return Ok(new
                {
                    Status = "Success",
                    Message = vendor.vendorcode == 0 ? "Vendor inserted successfully" : "Vendor updated successfully",
                    VendorCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updatevendor")]
        public async Task<IActionResult> UpdateVendor([FromBody] vendor_master vendor)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                vendor.tenantcode = tenantcode;

                var res = await itemclass.UpdateVendor(vendor);
                return res == "Success"
                    ? Ok(new { Status = "Success", Message = "Vendor updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to update vendor" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deletevendor")]
        public async Task<IActionResult> DeleteVendor(long vendorcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var res = await itemclass.DeleteVendor(vendorcode, tenantcode);
                return res == "Success"
                    ? Ok(new { Status = "Success", Message = "Vendor deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to delete vendor" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getallvendors")]
        public async Task<IActionResult> GetAllVendors()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllVendors(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getvendorbycode")]
        public async Task<IActionResult> GetVendorByCode(long vendorcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetVendorByCode(vendorcode, tenantcode);
                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Vendor not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── PURCHASE MASTER ──────────────────────────────────────────────────────────

        [HttpPost("insertpurchase")]
        public async Task<IActionResult> InsertPurchase([FromBody] purchase_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

                var result = await itemclass.InsertPurchase(request);
                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase inserted successfully",
                    PurchaseCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updatepurchase")]
        public async Task<IActionResult> UpdatePurchase([FromBody] purchase_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

                var result = await itemclass.UpdatePurchase(request);
                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase updated successfully",
                    PurchaseCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deletepurchase")]
        public async Task<IActionResult> DeletePurchase(long purchasecode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeletePurchase(purchasecode, tenantcode);
                return result == "Success"
                    ? Ok(new { Status = "Success", Message = "Purchase deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Purchase not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getallpurchases")]
        public async Task<IActionResult> GetAllPurchases()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetAllPurchases(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getpurchasebycode")]
        public async Task<IActionResult> GetPurchaseByCode(long purchasecode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetPurchaseByCode(purchasecode, tenantcode);
                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Purchase not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── STOCK MASTER ─────────────────────────────────────────────────────────────

        [HttpPost("insertstock")]
        public async Task<IActionResult> InsertStock([FromBody] stock_master stock)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                stock.tenantcode = tenantcode;

                var result = await itemclass.InsertStock(stock);
                return Ok(new { Status = "Success", StockCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updatestock")]
        public async Task<IActionResult> UpdateStock([FromBody] stock_master stock)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                stock.tenantcode = tenantcode;

                var result = await itemclass.UpdateStock(stock);
                return Ok(new { Status = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deletestock")]
        public async Task<IActionResult> DeleteStock(long stockcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeleteStock(stockcode, tenantcode);
                return Ok(new { Status = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getallstocks")]
        public async Task<IActionResult> GetAllStocks()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllStocks(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getstockbycode")]
        public async Task<IActionResult> GetStockByCode(long stockcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetStockByCode(stockcode, tenantcode);
                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Stock not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getstockbyitem")]
        public async Task<IActionResult> GetStockByItem(long itemcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetStockByItem(itemcode, tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── INDENT MASTER ────────────────────────────────────────────────────────────

        [HttpPost("insertindent")]
        public async Task<IActionResult> InsertIndent([FromBody] indent_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.master.tenantcode = tenantcode;

                var result = await itemclass.InsertIndent(request);
                return Ok(new { Status = "Success", Message = "Indent inserted successfully", IndentCode = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updateindent")]
 public async Task<IActionResult> UpdateIndent(
     [FromBody] indent_request request)
 {
     try
     {
         // ---------------------------------------------
         // Validate request
         // ---------------------------------------------
         if (request == null || request.master == null)
         {
             return BadRequest(new
             {
                 Status = "Failed",
                 Message = "Indent request is required."
             });
         }

         if (request.master.indentcode <= 0)
         {
             return BadRequest(new
             {
                 Status = "Failed",
                 Message = "Valid indent code is required."
             });
         }

         if (request.details == null || request.details.Count == 0)
         {
             return BadRequest(new
             {
                 Status = "Failed",
                 Message = "At least one item is required."
             });
         }

         // ---------------------------------------------
         // Get Tenant Code
         // ---------------------------------------------
         string tenantcode =
             HttpContext.Items["TenantCode"]?.ToString()
             ?? Request.Headers["tenantcode"].FirstOrDefault()
             ?? string.Empty;

         if (string.IsNullOrWhiteSpace(tenantcode))
         {
             return BadRequest(new
             {
                 Status = "Failed",
                 Message = "Tenant code not found."
             });
         }

         // ---------------------------------------------
         // Update Indent
         // ---------------------------------------------
         var result = await itemclass.UpdateIndent(
             request,
             tenantcode
         );

         return Ok(new
         {
             Status = "Success",
             Message = "Indent updated successfully",
             IndentCode = result
         });
     }
     catch (Exception ex)
     {
         return BadRequest(new
         {
             Status = "Failed",
             Message = ex.Message
         });
     }
 }
        [HttpGet("deleteindent")]
        public async Task<IActionResult> DeleteIndent(long indentcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeleteIndent(indentcode, tenantcode);
                return result == "Success"
                    ? Ok(new { Status = "Success", Message = "Indent deleted successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Indent not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getallindents")]
        public async Task<IActionResult> GetAllIndents()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllIndents(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getindentbycode")]
        public async Task<IActionResult> GetIndentByCode(long indentcode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetIndentByCode(indentcode, tenantcode);
                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Indent not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── PURCHASE ENTRY (GRN) ─────────────────────────────────────────────────────

        [HttpPost("insertpurchaseentry")]
        public async Task<IActionResult> InsertPurchaseEntry([FromBody] purchase_entry_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                if (request == null || request.master == null || request.details == null || !request.details.Any())
                    return BadRequest(new { Status = "Failed", Message = "Invalid request data" });

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

                var result = await itemclass.InsertPurchaseEntry(request);
                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase Entry + Purchase Master inserted successfully",
                    PurchaseEntryCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updatepurchaseentry")]
        public async Task<IActionResult> UpdatePurchaseEntry([FromBody] purchase_entry_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details)
                    d.tenantcode = tenantcode;

                var res = await itemclass.UpdatePurchaseEntry(request);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deletepurchaseentry")]
        public async Task<IActionResult> DeletePurchaseEntry(long id)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var res = await itemclass.DeletePurchaseEntry(id, tenantcode);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getallpurchaseentries")]
        public async Task<IActionResult> GetAllPurchaseEntries()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllPurchaseEntries(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getpurchaseentrybycode")]
        public async Task<IActionResult> GetPurchaseEntryByCode(long purchaseentrycode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetPurchaseEntryByCode(purchaseentrycode, tenantcode);
                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Purchase Entry not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── EXCEL UPLOAD ─────────────────────────────────────────────────────────────

        [HttpPost("upload-excel")]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, file.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                await itemclass.ProcessExcel(filePath, tenantcode);

                return Ok("Excel uploaded & data inserted successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost("insertcategory")]
        public async Task<IActionResult> Insert([FromBody] category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var id = await itemclass.InsertCategory(model);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Category inserted successfully",
                    CategoryCode = id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }


        // GET ALL CATEGORY
        [HttpGet("getcategory")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var data = await itemclass.GetCategories(tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Data = data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }


        // UPDATE CATEGORY
        [HttpPost("updatecategory")]
        public async Task<IActionResult> Update([FromBody] category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var result = await itemclass.UpdateCategory(model);

                return Ok(new
                {
                    Status = result ? "Success" : "Failed",
                    Message = result
                        ? "Category updated successfully"
                        : "Update failed"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }


        // DELETE CATEGORY
        [HttpGet("deletecategory")]
        public async Task<IActionResult> DeleteCategory(long id)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeleteCategory(id, tenantcode);

                return Ok(new
                {
                    Status = result ? "Success" : "Failed",
                    Message = result
                        ? "Category deleted successfully"
                        : "Delete failed"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
        // INSERT
        [HttpPost("insertuom")]
        public async Task<IActionResult> Insert(uom_master model)
        {
            try
            {
                var tenant = GetTenantCode();

                if (string.IsNullOrEmpty(tenant))
                    return BadRequest("tenant_code header required");

                model.tenant_code = tenant;

                var id = await itemclass.InsertUom(model);

                return Ok(new
                {
                    status = "Success",
                    ucode = id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET ALL
        [HttpGet("getalluom")]
        public async Task<IActionResult> Getuom()
        {
            try
            {
                var tenant = GetTenantCode();

                var data = await itemclass.GetAllUom(tenant);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET BY CODE
        [HttpGet("getuombycode")]
        public async Task<IActionResult> GetByCode(long ucode)
        {
            try
            {
                var tenant = GetTenantCode();

                var data = await itemclass.GetUomByCode(ucode, tenant);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // UPDATE
        [HttpPost("updateuom")]
        public async Task<IActionResult> Update(uom_master model)
        {
            try
            {
                var tenant = GetTenantCode();

                model.tenant_code = tenant;

                var result = await itemclass.UpdateUom(model);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE
        [HttpGet("deleteuom")]
        public async Task<IActionResult> Delete(long ucode)
        {
            try
            {
                var tenant = GetTenantCode();

                var result = await itemclass.DeleteUom(ucode, tenant);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPost("insertparentcategory")]
        public async Task<IActionResult> InsertParentCategory(parent_category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var result = await itemclass.InsertParentCategory(model);

                return Ok(new
                {
                    Status = "Success",
                    Message = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("getparentcategory")]
        public async Task<IActionResult> GetParentCategories()
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetParentCategories(tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        [HttpPost("updateparentcategory")]
        public async Task<IActionResult> UpdateParentCategory(parent_category_master model)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                model.tenantcode = tenantcode;

                var result = await itemclass.UpdateParentCategory(model);

                return Ok(new
                {
                    Status = "Success",
                    Message = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("deleteparentcategory")]
        public async Task<IActionResult> DeleteParentCategory(int parentcategorycode)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeleteParentCategory(
                    parentcategorycode,
                    tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Message = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
        [HttpPost("insertledger")]
        public async Task<IActionResult> InsertLedger([FromBody] ledger_master ledger)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                ledger.tenantcode = tenantcode;

                var result = await itemclass.InsertLedger(ledger);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Ledger inserted successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("updateledger")]
        public async Task<IActionResult> UpdateLedger([FromBody] ledger_master ledger)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                ledger.tenantcode = tenantcode;

                var res = await itemclass.UpdateLedger(ledger);

                return res == "Updated Successfully"
                    ? Ok(new { Status = "Success", Message = "Ledger updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = "Unable to update ledger" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getledger")]
        public async Task<IActionResult> GetLedger()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetLedger(tenantcode);

                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deleteledger")]
        public async Task<IActionResult> DeleteLedger(int ledgercode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeleteLedger(ledgercode, tenantcode);

                return result
                    ? Ok(new { Status = "Success", Message = "Ledger deleted successfully" })
                    : NotFound(new { Status = "Failed", Message = "Ledger not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
        [HttpPost("insertledgertype")]
        public async Task<IActionResult> InsertLedgerType([FromBody] ledger_type_master ledger)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode))
                    return BadRequest(new { Status = "Failed", Message = "Tenant code missing" });

                ledger.tenantcode = tenantcode;

                var result = await itemclass.InsertLedgerType(ledger);

                return result == "Inserted Successfully"
                    ? Ok(new { Status = "Success", Message = result })
                    : BadRequest(new { Status = "Failed", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }


        // UPDATE
        [HttpPost("updateledgertype")]
        public async Task<IActionResult> UpdateLedgerType([FromBody] ledger_type_master ledger)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            ledger.tenantcode = tenantcode;

            var result = await itemclass.UpdateLedgerType(ledger);

            return Ok(new
            {
                Status = "Success",
                Message = result
            });
        }


        // DELETE
        [HttpGet("deleteledgertype")]
        public async Task<IActionResult> DeleteLedgerType(int ledgertypecode)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            var result = await itemclass.DeleteLedgerType(ledgertypecode, tenantcode);

            if (result)
            {
                return Ok(new
                {
                    Status = "Success",
                    Message = "Ledger Type deleted successfully"
                });
            }

            return NotFound(new
            {
                Status = "Failed",
                Message = "Ledger Type not found"
            });
        }



        [HttpPost("insertledgergroup")]
        public async Task<IActionResult> InsertLedgerGroup([FromBody] ledger_group_master ledger)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            ledger.tenantcode = tenantcode;

            var result = await itemclass.InsertLedgerGroup(ledger);

            return Ok(new
            {
                Status = "Success",
                Message = result
            });
        }


        // UPDATE
        [HttpPost("updateledgergroup")]
        public async Task<IActionResult> UpdateLedgerGroup([FromBody] ledger_group_master ledger)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            ledger.tenantcode = tenantcode;

            var result = await itemclass.UpdateLedgerGroup(ledger);

            return Ok(new
            {
                Status = "Success",
                Message = result
            });
        }


        // DELETE
        [HttpDelete("deleteledgergroup")]
        public async Task<IActionResult> DeleteLedgerGroup(int ledgergroupcode)
        {
            var tenantcode = GetTenantCode();

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = "Tenant code missing"
                });
            }

            var result = await itemclass.DeleteLedgerGroup(ledgergroupcode, tenantcode);

            if (result)
            {
                return Ok(new
                {
                    Status = "Success",
                    Message = "Ledger Group deleted successfully"
                });
            }

            return NotFound(new
            {
                Status = "Failed",
                Message = "Ledger Group not found"
            });
        }
        [HttpGet("getledgertype")]
        public async Task<IActionResult> GetLedgerTypes()
        {
            var tenantcode = GetTenantCode();
            if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

            var result = await itemclass.GetLedgerTypes(tenantcode);
            return Ok(new { Status = "Success", Data = result });
        }

        [HttpGet("getledgergroup")]
        public async Task<IActionResult> GetLedgerGroups()
        {
            var tenantcode = GetTenantCode();
            if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

            var result = await itemclass.GetLedgerGroups(tenantcode);
            return Ok(new { Status = "Success", Data = result });
        }
        [HttpPost("insertsales")]
        public async Task<IActionResult> InsertSales([FromBody] sales_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details) d.tenantcode = tenantcode;

                var result = await itemclass.InsertSales(request);
                return Ok(new { Status = "Success", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Error", Message = ex.Message });
            }
        }

        [HttpPost("updatesales")]
        public async Task<IActionResult> UpdateSales([FromBody] sales_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.master.tenantcode = tenantcode;
                foreach (var d in request.details) d.tenantcode = tenantcode;

                var result = await itemclass.UpdateSales(request);
                return Ok(new { Status = "Success", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Error", Message = ex.Message });
            }
        }

        [HttpGet("getsales")]
        public async Task<IActionResult> GetSales()
        {
            var tenantcode = GetTenantCode();
            if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

            var data = await itemclass.GetSales(tenantcode);
            return Ok(new { status = "Success", data = data });
        }

        [HttpDelete("deletesales")]
        public async Task<IActionResult> DeleteSales(long salescode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeleteSales(salescode, tenantcode);
                return Ok(new { Status = "Success", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Error", Message = ex.Message });
            }
        }
        [HttpPost("upsertwarehouse")]
        public async Task<IActionResult> Upsert([FromBody] warehouse_master warehouse)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                warehouse.tenantcode = tenantcode;

                // TEMP DEBUG — remove once confirmed
                var branch = (warehouse.warehousecode == null || warehouse.warehousecode == 0) ? "INSERT" : "UPDATE";

                var result = await itemclass.UpsertWarehouse(warehouse);

                return Ok(new
                {
                    status = true,
                    message = result,
                    debug_branch = branch,
                    debug_warehousecode_received = warehouse.warehousecode
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("getallwarehouse")]
        public async Task<IActionResult> GetAllS()
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetWarehouseList(tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }



        [HttpGet("deletewarehouse")]
        public async Task<IActionResult> Delete(int warehousecode)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrEmpty(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.DeleteWarehouse(warehousecode, tenantcode);

                return Ok(new
                {
                    status = true,
                    message = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
        [HttpPost("upsertmanufacturer")]
        public async Task<IActionResult> UpsertManufacturer([FromBody] manufacturer_master manufacturer)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                manufacturer.tenantcode = tenantcode;

                var result = await itemclass.UpsertManufacturer(manufacturer);
                return Ok(result);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpGet("getlistmanufacturer")]
        public async Task<IActionResult> GetList()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetManufacturerList(tenantcode);
                return Ok(result);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpDelete("deletemanufacturer")]
        public async Task<IActionResult> Deletemanufacturer(long manufacturercode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeleteManufacturer(manufacturercode, tenantcode);
                return Ok(result);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }
        // ─── PURCHASE RETURN ──────────────────────────────────────────────────────────

        // Step 1: search by item name + batch no → returns vendor + rate + available qty
        [HttpGet("getpurchasereturnlookup")]
        public async Task<IActionResult> GetPurchaseReturnLookup(long itemcode, string? batchno = null)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetPurchaseReturnLookup(itemcode, batchno, tenantcode);

                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // Step 2: submit returnqty + packsize → computes amount, updates stock + purchase_detail
        [HttpPost("insertpurchasereturn")]
        public async Task<IActionResult> InsertPurchaseReturn([FromBody] purchase_return_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                request.tenantcode = tenantcode;

                var result = await itemclass.InsertPurchaseReturn(request);

                return Ok(new
                {
                    Status = "Success",
                    Message = "Purchase return processed successfully",
                    PurchaseReturnCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
         // Step 2: Submit Sales Return
 [HttpPost("insertsalesreturn")]
 public async Task<IActionResult> InsertSalesReturn([FromBody] sales_return_request request)
 {
     try
     {
         var tenantcode = GetTenantCode();
         if (string.IsNullOrEmpty(tenantcode))
             return MissingTenantCode();

         request.tenantcode = tenantcode;

         var result = await itemclass.InsertSalesReturn(request);

         return Ok(new
         {
             Status = "Success",
             Message = "Sales return processed successfully",
             SalesReturnCode = result
         });
     }
     catch (Exception ex)
     {
         return BadRequest(new
         {
             Status = "Failed",
             Message = ex.Message
         });
            }
        }
        [HttpGet("getsalesreturn")]
        public async Task<IActionResult> GetSalesReturnLookup(long itemcode, string? batchno = null)
        {
            try
            {
                var tenantcode = GetTenantCode();

                if (string.IsNullOrWhiteSpace(tenantcode))
                    return MissingTenantCode();

                var result = await itemclass.GetSalesReturnLookup(
                    itemcode,
                    batchno,
                    tenantcode);

                return Ok(new
                {
                    Status = "Success",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Failed",
                    Message = ex.Message
                });
            }
        }
           
        // ─── PHARMACY QUEUE ─────────────────────────────────────────────────────────

        [HttpPost("receive-prescription")]
        public async Task<IActionResult> ReceivePrescription([FromBody] ReceivePrescriptionRequest req)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();
                if (req.items == null || !req.items.Any())
                    return BadRequest(new { Status = "Failed", Message = "items is required" });

                var result = await itemclass.ReceivePrescription(req, tenantcode);
                return result.StartsWith("Success")
                    ? Ok(new { Status = "Success", Message = result })
                    : BadRequest(new { Status = "Failed", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("pharmacy-queue")]
        public async Task<IActionResult> GetPharmacyQueue(string? status = null)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetPharmacyQueue(tenantcode, status);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        
        [HttpGet("pharmacy-queue/by-custid")]
        public async Task<IActionResult> GetPharmacyQueueByCustId(decimal custid, string? status = null)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();
                if (custid <= 0) return BadRequest(new { Status = "Failed", Message = "custid is required" });

                var result = await itemclass.GetPharmacyQueueByCustId(custid, tenantcode, status);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("pharmacy-queue/update-status")]
        public async Task<IActionResult> UpdatePharmacyQueueStatus([FromBody] UpdateQueueStatusRequest req)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.UpdatePharmacyQueueStatus(req.queue_id, req.status, tenantcode);
                return result == "Success"
                    ? Ok(new { Status = "Success", Message = result })
                    : BadRequest(new { Status = "Failed", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
        [HttpPost("insertstockadjustment")]
        public async Task<IActionResult> InsertStockAdjustment([FromBody] stock_adjustment_request request)
        {
            try
            {
                string tenantcode =
                    HttpContext.Items["TenantCode"]?.ToString()
                    ?? Request.Headers["tenantcode"].FirstOrDefault()
                    ?? string.Empty;

                if (string.IsNullOrEmpty(tenantcode))
                    return BadRequest(new { status = "Failed", message = "Tenant code not found." });

                request.stock.tenantcode = tenantcode;
                request.adjustmentlog.tenantcode = tenantcode;

                var result = await itemclass.InsertStockAdjustment(request);
                return Ok(new { status = "Success", message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "Failed", message = ex.Message });
            }
        }

        [HttpPost("updatestockadjustment")]
        public async Task<IActionResult> UpdateStockAdjustment([FromBody] stock_adjustment_request request)
        {
            try
            {
                string tenantcode =
                    HttpContext.Items["TenantCode"]?.ToString()
                    ?? Request.Headers["tenantcode"].FirstOrDefault()
                    ?? string.Empty;

                if (string.IsNullOrEmpty(tenantcode))
                    return BadRequest(new { status = "Failed", message = "Tenant code not found." });

                request.stock.tenantcode = tenantcode;
                request.adjustmentlog.tenantcode = tenantcode;

                var result = await itemclass.UpdateStockAdjustment(request);
                return Ok(new { status = "Success", message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "Failed", message = ex.Message });
            }
        }

        [HttpGet("deletestockadjustment")]
        public async Task<IActionResult> DeleteStockAdjustment(long adjustmentlogcode)
        {
            try
            {
                string tenantcode =
                    HttpContext.Items["TenantCode"]?.ToString()
                    ?? Request.Headers["tenantcode"].FirstOrDefault()
                    ?? string.Empty;

                if (string.IsNullOrEmpty(tenantcode))
                    return BadRequest(new { status = "Failed", message = "Tenant code not found." });

                var result = await itemclass.DeleteStockAdjustment(adjustmentlogcode, tenantcode);
                return Ok(new { status = "Success", message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "Failed", message = ex.Message });
            }
        }
        [HttpGet("getstockadjustment")]
 public async Task<IActionResult> GetStockAdjustment(long adjustmentlogcode)
 {
     try
     {
         string tenantcode =
             Request.Headers["TenantCode"].FirstOrDefault()
             ?? string.Empty;

         if (string.IsNullOrEmpty(tenantcode))
         {
             return BadRequest(new
             {
                 status = false,
                 message = "Tenant code not found."
             });
         }

         var result = await itemclass.GetStockAdjustment(
             adjustmentlogcode,
             tenantcode
         );

         if (result == null)
         {
             return NotFound(new
             {
                 status = false,
                 message = "Stock adjustment record not found."
             });
         }

         return Ok(new
         {
             status = true,
             message = "Stock adjustment retrieved successfully.",
             data = result
         });
     }
     catch (Exception ex)
     {
         return StatusCode(500, new
         {
             status = false,
             message = ex.Message
         });
     }
 }
    [HttpPost("insertstocktransfer")]
   public async Task<IActionResult> InsertStockTransfer(
[FromBody] stock_transfer_request request)
   {
       try
       {
           string tenantcode =
               HttpContext.Items["TenantCode"]?.ToString()
               ?? Request.Headers["tenantcode"].FirstOrDefault()
               ?? string.Empty;

           if (string.IsNullOrEmpty(tenantcode))
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Tenant code not found."
               });
           }

           if (request == null || request.master == null)
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Invalid request."
               });
           }

           if (request.details == null ||
               request.details.Count == 0)
           {
               return BadRequest(new
               {
                   status = false,
                   message = "At least one item is required."
               });
           }

           long transfercode =
               await itemclass.InsertStockTransfer(
                   request,
                   tenantcode);

           return Ok(new
           {
               status = true,
               message = "Stock transfer created successfully.",
               transfercode = transfercode
           });
       }
       catch (Exception ex)
       {
           return StatusCode(500, new
           {
               status = false,
               message = ex.Message
           });
       }
   }

   // ============================================================
   // UPDATE
   // ============================================================

   [HttpPost("updatestocktransfer")]
   public async Task<IActionResult> UpdateStockTransfer(
       [FromBody] stock_transfer_request request)
   {
       try
       {
           string tenantcode =
              HttpContext.Items["TenantCode"]?.ToString()
              ?? Request.Headers["tenantcode"].FirstOrDefault()
              ?? string.Empty;

           if (string.IsNullOrEmpty(tenantcode))
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Tenant code not found."
               });
           }


           bool result =
               await itemclass.UpdateStockTransfer(
                   request,
                   tenantcode);


           if (!result)
           {
               return NotFound(new
               {
                   status = false,
                   message = "Stock transfer not found."
               });
           }


           return Ok(new
           {
               status = true,
               message =
                   "Stock transfer updated successfully."
           });
       }
       catch (Exception ex)
       {
           return StatusCode(500, new
           {
               status = false,
               message = ex.Message
           });
       }
   }


   // ============================================================
   // DELETE
   // ============================================================

   [HttpDelete("deletestocktransfer")]
   public async Task<IActionResult> DeleteStockTransfer(
       long transfercode)
   {
       try
       {
           string tenantcode =
               HttpContext.Items["TenantCode"]?.ToString()
               ?? Request.Headers["tenantcode"].FirstOrDefault()
               ?? string.Empty;

           if (string.IsNullOrEmpty(tenantcode))
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Tenant code not found."
               });
           }


           bool result =
               await 
               itemclass.DeleteStockTransfer(
                   transfercode,
                   tenantcode);


           if (!result)
           {
               return NotFound(new
               {
                   status = false,
                   message = "Stock transfer not found."
               });
           }


           return Ok(new
           {
               status = true,
               message =
                   "Stock transfer deleted successfully."
           });
       }
       catch (Exception ex)
       {
           return StatusCode(500, new
           {
               status = false,
               message = ex.Message
           });
       }
   }


        [HttpGet("getallstocktransfer")]
        public async Task<IActionResult> GetAllStockTransfer()
        {
            try
            {
                string tenantcode =
                    HttpContext.Items["TenantCode"]?.ToString()
                    ?? Request.Headers["tenantcode"].FirstOrDefault()
                    ?? string.Empty;

                if (string.IsNullOrEmpty(tenantcode))
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Tenant code not found."
                    });
                }

                var result = await itemclass.GetStockTransferList(tenantcode);

                return Ok(new
                {
                    status = true,
                    data = result   // now each item has { master, details }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }


        // ============================================================
        // GET BY CODE
        // ============================================================

        [HttpGet("getstocktransfer")]
   public async Task<IActionResult> GetStockTransfer(
       long transfercode)
   {
       try
       {
           string tenantcode =
               HttpContext.Items["TenantCode"]?.ToString()
               ?? Request.Headers["tenantcode"].FirstOrDefault()
               ?? string.Empty;


           if (string.IsNullOrEmpty(tenantcode))
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Tenant code not found."
               });
           }


           var result =
               await itemclass.GetStockTransferByCode(
                   transfercode,
                   tenantcode);


           if (result == null)
           {
               return NotFound(new
               {
                   status = false,
                   message = "Stock transfer not found."
               });
           }


           return Ok(new
           {
               status = true,
               data = result
           });
       }
       catch (Exception ex)
       {
           return StatusCode(500, new
           {
               status = false,
               message = ex.Message
           });
       }
   }


   // ============================================================
   // DISPATCH
   // PENDING → IN_TRANSIT
   // ============================================================

   [HttpPost("dispatchstocktransfer")]
   public async Task<IActionResult> DispatchStockTransfer(
       [FromBody] stock_transfer_action_request request)
   {
       try
       {
           string tenantcode =
               HttpContext.Items["TenantCode"]?.ToString()
               ?? Request.Headers["tenantcode"].FirstOrDefault()
               ?? string.Empty;

           if (string.IsNullOrEmpty(tenantcode))
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Tenant code not found."
               });
           }


           bool result =
               await itemclass.DispatchStockTransfer(
                   request.transfercode,
                   request.usercode,
                   tenantcode);


           if (!result)
           {
               return NotFound(new
               {
                   status = false,
                   message =
                       "Stock transfer not found."
               });
           }


           return Ok(new
           {
               status = true,
               message =
                   "Stock transfer dispatched successfully. " +
                   "Stock deducted from source warehouse."
           });
       }
       catch (Exception ex)
       {
           return StatusCode(500, new
           {
               status = false,
               message = ex.Message
           });
       }
   }


   // ============================================================
   // RECEIVE
   // IN_TRANSIT → RECEIVED
   // ============================================================

   [HttpPost("receivestocktransfer")]
   public async Task<IActionResult> ReceiveStockTransfer(
       [FromBody] stock_transfer_action_request request)
   {
       try
       {
           string tenantcode =
                HttpContext.Items["TenantCode"]?.ToString()
                ?? Request.Headers["tenantcode"].FirstOrDefault()
                ?? string.Empty;


           if (string.IsNullOrEmpty(tenantcode))
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Tenant code not found."
               });
           }


           bool result =
               await itemclass.ReceiveStockTransfer(
                   request.transfercode,
                   request.usercode,
                   tenantcode);


           if (!result)
           {
               return NotFound(new
               {
                   status = false,
                   message =
                       "Stock transfer not found."
               });
           }


           return Ok(new
           {
               status = true,
               message =
                   "Stock transfer received successfully. " +
                   "Stock added to destination warehouse."
           });
       }
       catch (Exception ex)
       {
           return StatusCode(500, new
           {
               status = false,
               message = ex.Message
           });
       }
   }


   // ============================================================
   // REJECT
   // IN_TRANSIT → REJECTED
   // ============================================================

   [HttpPost("rejectstocktransfer")]
   public async Task<IActionResult> RejectStockTransfer(
       [FromBody] stock_transfer_action_request request)
   {
       try
       {
           string tenantcode =
                HttpContext.Items["TenantCode"]?.ToString()
                ?? Request.Headers["tenantcode"].FirstOrDefault()
                ?? string.Empty;


           if (string.IsNullOrEmpty(tenantcode))
           {
               return BadRequest(new
               {
                   status = false,
                   message = "Tenant code not found."
               });
           }


           bool result =
               await itemclass.RejectStockTransfer(
                   request.transfercode,
                   request.usercode,
                   tenantcode);


           if (!result)
           {
               return NotFound(new
               {
                   status = false,
                   message =
                       "Stock transfer not found."
               });
           }


           return Ok(new
           {
               status = true,
               message =
                   "Stock transfer rejected successfully. " +
                   "Stock returned to source warehouse."
           });
       }
       catch (Exception ex)
       {
           return StatusCode(500, new
           {
               status = false,
               message = ex.Message
           });
       }
   }
       [HttpPost("partialrejectstocktransfer")]
    public async Task<IActionResult> PartialRejectStockTransfer(
[FromBody] stock_transfer_partial_reject_request request)
    {
        try
        {
            // ----------------------------------------------------
            // Get Tenant Code
            // ----------------------------------------------------

            string tenantcode =
                HttpContext.Items["TenantCode"]?.ToString()
                ?? Request.Headers["tenantcode"].FirstOrDefault()
                ?? string.Empty;

            // ----------------------------------------------------
            // Validate Tenant Code
            // ----------------------------------------------------

            if (string.IsNullOrEmpty(tenantcode))
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Tenant code not found."
                });
            }

            // ----------------------------------------------------
            // Validate Request
            // ----------------------------------------------------

            if (request == null)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Request cannot be null."
                });
            }

            if (request.transfercode <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid transfer code."
                });
            }

            if (request.usercode <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Invalid user code."
                });
            }

            if (request.details == null ||
                request.details.Count == 0)
            {
                return BadRequest(new
                {
                    status = false,
                    message = "No partial rejection details provided."
                });
            }

            // ----------------------------------------------------
            // Call Partial Reject Method
            // ----------------------------------------------------

            bool result =
                await itemclass.PartialRejectStockTransfer(
                    request.transfercode,
                    request.usercode,
                    tenantcode,
                    request);

            // ----------------------------------------------------
            // Transfer Not Found
            // ----------------------------------------------------

            if (!result)
            {
                return NotFound(new
                {
                    status = false,
                    message = "Stock transfer not found."
                });
            }

            // ----------------------------------------------------
            // Success
            // ----------------------------------------------------

            return Ok(new
            {
                status = true,
                message =
                    "Stock transfer partially received successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = false,
                message = ex.Message
            });
        }
    }
        // ─── GET ALL — STOCK ADJUSTMENT ───────────────────────────────────────────────
        [HttpGet("getallstockadjustment")]
        public async Task<IActionResult> GetAllStockAdjustments()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllStockAdjustments(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── GET ALL — SALES RETURN ────────────────────────────────────────────────────
        [HttpGet("getallsalesreturn")]
        public async Task<IActionResult> GetAllSalesReturns()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllSalesReturns(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── GET ALL — PURCHASE RETURN ─────────────────────────────────────────────────
        [HttpGet("getallpurchasereturn")]
        public async Task<IActionResult> GetAllPurchaseReturns()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllPurchaseReturns(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
        // ─── UPSERT — STOCK ADJUSTMENT (single endpoint: insert or update) ────────────
        [HttpPost("upsertstockadjustment")]
        public async Task<IActionResult> UpsertStockAdjustment([FromBody] stock_adjustment_request request)
        {
            try
            {
                string tenantcode =
                    HttpContext.Items["TenantCode"]?.ToString()
                    ?? Request.Headers["tenantcode"].FirstOrDefault()
                    ?? string.Empty;

                if (string.IsNullOrEmpty(tenantcode))
                    return BadRequest(new { status = "Failed", message = "Tenant code not found." });

                request.stock.tenantcode = tenantcode;
                request.adjustmentlog.tenantcode = tenantcode;

                var result = await itemclass.UpsertStockAdjustment(request);

                bool ok = result.StartsWith("Stock Adjustment Saved") || result.StartsWith("Stock Adjustment Updated");

                return ok
                    ? Ok(new { status = "Success", message = result })
                    : BadRequest(new { status = "Failed", message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "Failed", message = ex.Message });
            }
        }
        // ─── FILTER PURCHASES ─────────────────────────────────────────────────────────
        [HttpGet("filterpurchases")]
        public async Task<IActionResult> FilterPurchases(
            string? vendorname,
            string? billno,
            string? invoiceno,
            string? paymentstatus,
            DateTime? fromdate,
            DateTime? todate)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.FilterPurchases(
                    vendorname, billno, invoiceno, paymentstatus, fromdate, todate, tenantcode);

                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        // ─── BULK UPDATE PURCHASE STATUS ──────────────────────────────────────────────
        [HttpPost("updatemultiplepurchasestatus")]
        public async Task<IActionResult> UpdateMultiplePurchaseStatus([FromBody] purchase_bulk_status_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                if (request == null || request.purchasecodes == null || !request.purchasecodes.Any())
                    return BadRequest(new { Status = "Failed", Message = "purchasecodes is required" });

                if (string.IsNullOrWhiteSpace(request.paymentstatus))
                    return BadRequest(new { Status = "Failed", Message = "paymentstatus is required" });

                var updatedCount = await itemclass.UpdateMultiplePurchaseStatus(
                    request.purchasecodes, request.paymentstatus, tenantcode);

                return updatedCount > 0
                    ? Ok(new { Status = "Success", Message = $"{updatedCount} purchase(s) updated", UpdatedCount = updatedCount })
                    : BadRequest(new { Status = "Failed", Message = "No matching purchases found to update" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
        // ─── DOCTOR ROOM CONSUMPTION ────────────────────────────────────────────────────

        [HttpGet("getdoctorrooms")]
        public async Task<IActionResult> GetDoctorRooms()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetDoctorRooms(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getdoctorroomstock")]
        public async Task<IActionResult> GetDoctorRoomStock(long roomwarehousecode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetDoctorRoomStock(roomwarehousecode, tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpPost("insertconsumption")]
        public async Task<IActionResult> InsertConsumption([FromBody] consumption_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                if (request == null || request.master == null || request.details == null || !request.details.Any())
                    return BadRequest(new { Status = "Failed", Message = "Invalid request data" });

                var result = await itemclass.InsertConsumption(request, tenantcode);
                return Ok(new
                {
                    Status = "Success",
                    Message = "Consumption saved successfully",
                    ConsumptionCode = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getallconsumption")]
        public async Task<IActionResult> GetAllConsumptions()
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetAllConsumptions(tenantcode);
                return Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("getconsumptionbycode")]
        public async Task<IActionResult> GetConsumptionByCode(long consumptioncode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.GetConsumptionByCode(consumptioncode, tenantcode);
                return result == null
                    ? NotFound(new { Status = "Failed", Message = "Consumption record not found" })
                    : Ok(new { Status = "Success", Data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }

        [HttpGet("deleteconsumption")]
        public async Task<IActionResult> DeleteConsumption(long consumptioncode)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                var result = await itemclass.DeleteConsumption(consumptioncode, tenantcode);
                return result == "Success"
                    ? Ok(new { Status = "Success", Message = "Consumption deleted and stock restored" })
                    : BadRequest(new { Status = "Failed", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
        [HttpPost("updateconsumption")]
        public async Task<IActionResult> UpdateConsumption([FromBody] consumption_request request)
        {
            try
            {
                var tenantcode = GetTenantCode();
                if (string.IsNullOrEmpty(tenantcode)) return MissingTenantCode();

                if (request == null || request.master == null || request.master.consumptioncode <= 0)
                    return BadRequest(new { Status = "Failed", Message = "Valid consumptioncode is required." });

                if (request.details == null || !request.details.Any())
                    return BadRequest(new { Status = "Failed", Message = "At least one item is required." });

                var result = await itemclass.UpdateConsumption(request, tenantcode);

                return result == "Success"
                    ? Ok(new { Status = "Success", Message = "Consumption updated successfully" })
                    : BadRequest(new { Status = "Failed", Message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Status = "Failed", Message = ex.Message });
            }
        }
    }
}
    
        
    
    

