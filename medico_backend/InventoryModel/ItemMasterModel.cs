using Dapper.Contrib.Extensions;

namespace medico_backend.InventoryModel
{
    [Table("item_master")]
    public class item_master

    {
        [Key]
        public long itemcode { get; set; }
        public string itemname { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }

        public int categorycode { get; set; }
        public int subcategorycode { get; set; }

        public int hsncode { get; set; }

        public string itemtype { get; set; }

        public decimal gstpercentage { get; set; }

        public int uomcode { get; set; }

        public decimal purchaserate { get; set; }

        public decimal salesrate { get; set; }

        public decimal mrp { get; set; }

        public decimal currentstock { get; set; }

        public decimal minstock { get; set; }

        public decimal reorderlevel { get; set; }
        public decimal packsize { get; set; } = 1;
        public bool isexpiry { get; set; }          // Missing
        public int expiryalertdays { get; set; }    // Missing
        public bool batchrequired { get; set; }

        public bool expiryrequired { get; set; }

        public bool serialrequired { get; set; }

        public int brandcode { get; set; }

        public int manufacturercode { get; set; }

        public int taxcode { get; set; }

        // New Fields

        public int naturetype { get; set; }

        // Asset = 1
        // Liability = 2
        // Expense = 3
        // Income = 4

        public string? manufacturername { get; set; }
        public int ledgergroupcode { get; set; }

        public string? drugname { get; set; }

        public string? packaging { get; set; }

        public bool isactive { get; set; }

        public bool deleted { get; set; }

        public DateTime createddate { get; set; }

        public int usercode { get; set; }

        public string tenantcode { get; set; }
        public string? schedule { get; set; }

        public bool isnarcoticdrug { get; set; } = false;
    }
    [Table("vendor_master")]

    public class vendor_master
 {
        [Key]
     public long vendorcode { get; set; }

     public string vendorname { get; set; }

     public string shortname { get; set; }

     public string vendortype { get; set; }

     public string contactperson { get; set; }

     public string phonenumber { get; set; }

     public string alternatephonenumber { get; set; }

     public string emailid { get; set; }

     public string website { get; set; }

     public string gstnumber { get; set; }

     public string pannumber { get; set; }

     public string taxid { get; set; }

     public string registrationnumber { get; set; }

     // New Fields

     public string? druglicenseno { get; set; }

     public string? fssaino { get; set; }

     public decimal? vendorrating { get; set; }

     public string addressline1 { get; set; }

     public string addressline2 { get; set; }

     public string landmark { get; set; }

     public string city { get; set; }

     public string district { get; set; }

     public string state { get; set; }

     public string postalcode { get; set; }

     public string countrycode { get; set; }

     public string countryname { get; set; }

     public string currencycode { get; set; }

     public string paymentterms { get; set; }

     public string creditperiod { get; set; }

     public string bankname { get; set; }

     public string accountnumber { get; set; }

     public string ifsccode { get; set; }

     public string swiftcode { get; set; }

     public string ibannumber { get; set; }

     public bool isactive { get; set; }

     public bool deleted { get; set; }

     public DateTime createddate { get; set; }

     public DateTime? modifieddate { get; set; }

     public long usercode { get; set; }

     public string tenantcode { get; set; }

     public string branchcode { get; set; }
 }
   public class purchase_master
{
    public long purchasecode { get; set; }

    public string? billno { get; set; }

    public DateTime billdate { get; set; }

    public string? invoiceno { get; set; }

    public DateTime? invoicedate { get; set; }

    public long? vendorcode { get; set; }

    public decimal grossamount { get; set; }

    public decimal overalldiscount { get; set; }     // overall discount % applied on grossamount
    public decimal discountamount { get; set; }       // already existed — overall discount value
    public decimal totalamount { get; set; }

    public decimal taxamount { get; set; }

    public decimal netamount { get; set; }

    // New Fields
    public decimal transportationcharges { get; set; }

    public decimal roundoff { get; set; }

    public string? paymentmode { get; set; }

    public string? paymentstatus { get; set; }

    public string? currencycode { get; set; }

    public bool isactive { get; set; }

    public bool deleted { get; set; }

    public string? remarks { get; set; }

    public DateTime createddate { get; set; }

    public DateTime? modifieddate { get; set; }

    public long? usercode { get; set; }

    public string? tenantcode { get; set; }

    public string? branchcode { get; set; }

    public string? companycode { get; set; }

        public long grncode { get; set; }
        public string? chequeno { get; set; }     // NEW
        public string? accountno { get; set; }
    }
    public class purchase_detail
{
    public long purchasedetailcode { get; set; }

    public long purchasecode { get; set; }

    public long itemcode { get; set; }

    public decimal quantity { get; set; }

        public decimal freequantity { get; set; }

        public long? uomcode { get; set; }

    public decimal rate { get; set; }

    public decimal discountpercentage { get; set; }

    public decimal discountamount { get; set; }

    public decimal taxpercentage { get; set; }

    public decimal taxamount { get; set; }

    public decimal amount { get; set; }

    public decimal totalamount { get; set; }

    public string? batchno { get; set; }

    public DateTime? manufacturingdate { get; set; }

    public DateTime? expirydate { get; set; }

    // New Fields

    public decimal orderedqty { get; set; }

    public decimal receivedqty { get; set; }

        public decimal rejectedqty { get; set; }
        public decimal returnedqty { get; set; }
        public decimal issuedqty { get; set; }

        // Warehouse / Store
        public long warehousecode { get; set; }

    // Packaging
    public string? packaging { get; set; }

    // Manufacturer
    public long manufacturercode { get; set; }

        public string? tenantcode { get; set; }
        public int packsize { get; set; }
        public decimal packg { get; set; }
        public decimal mrp { get; set; }
    }

    public class purchase_request
    {
        public purchase_master master { get; set; }
        public List<purchase_detail> details { get; set; }
    }
   public class stock_master
{
    public long stockcode { get; set; }

    public long itemcode { get; set; }

    public long? warehousecode { get; set; }
    public string? branchcode { get; set; }
    public string? locationcode { get; set; }

    public decimal openingstock { get; set; }
    public decimal openingstrip { get; set; }     // NEW
    public decimal openingloose { get; set; }
    public decimal purchasedqty { get; set; }
    public decimal soldqty { get; set; }
    public decimal damagedqty { get; set; }
    public decimal returnqty { get; set; }

    // New fields for Stock Adjustment
    public decimal adjustedqty { get; set; }           // Latest adjustment quantity
    public DateTime? adjusteddate { get; set; }        // Latest adjustment date
    public long? adjustedby { get; set; }              // User who adjusted
    public string? adjustmentreason { get; set; }      // Physical Count, Damage, Expiry...
    public string? adjustmenttype { get; set; }        // Increase / Decrease

    public decimal closingstock { get; set; }

    public decimal unitcost { get; set; }
    public decimal stockvalue { get; set; }
    public decimal mrpunit { get; set; }

    public string? batchno { get; set; }
    public DateTime? manufacturingdate { get; set; }
    public DateTime? expirydate { get; set; }

    public bool isactive { get; set; }
    public bool deleted { get; set; }

    public DateTime createddate { get; set; }
    public DateTime? modifieddate { get; set; }

    public long? usercode { get; set; }

    public string? tenantcode { get; set; }
    public string? companycode { get; set; }
}
    public class indent_master
    {
        public long indentcode { get; set; }

        // Indent Info
        public string? indentno { get; set; }
        public DateTime indentdate { get; set; }

        // Request Info
        public long? requestedby { get; set; }
        public long? departmentcode { get; set; }
        public string? branchcode { get; set; }
        public long purchasedetailcode { get; set; }

        public decimal issuedqty { get; set; }
        // Remarks
        public string? remarks { get; set; }

        // Approval
        public string? approvalstatus { get; set; }   // PENDING / APPROVED / REJECTED
        public long? approvedby { get; set; }
        public DateTime? approveddate { get; set; }
        public string? approvalremarks { get; set; }

        // Status
        public bool isactive { get; set; }
        public bool deleted { get; set; }

        // Audit
        public DateTime createddate { get; set; }

        // Multi Tenant
        public string? tenantcode { get; set; }
    }
    public class indent_detail
    {
        public long indentdetailcode { get; set; }

        // Parent Indent
        public long indentcode { get; set; }

        // Item Reference
        public long itemcode { get; set; }
        public long purchasedetailcode { get; set; }
            
        // Quantity
        public decimal requestedqty { get; set; }
        public decimal approvedqty { get; set; }
        public decimal issuedqty { get; set; }

        // Remarks
        public string? remarks { get; set; }
    }
    public class indent_request
    {
        public indent_master master { get; set; }
        public List<indent_detail> details { get; set; }
    }
    public class purchase_entry_master
    {
        public long purchaseentrycode { get; set; }

        // GRN
        public string? grnno { get; set; }
        public DateTime grndate { get; set; }
        public long? receivedby { get; set; }

        // Purchase
        public string? billno { get; set; }
        public DateTime? billdate { get; set; }
        public string? invoiceno { get; set; }
        public DateTime? invoicedate { get; set; }

        public long vendorcode { get; set; }

        public decimal totalqty { get; set; }
        public decimal receivedqty { get; set; }

        public decimal grossamount { get; set; }
        public decimal discountamount { get; set; }
        public decimal taxamount { get; set; }
        public decimal othercharges { get; set; }
        public decimal netamount { get; set; }

        public string? paymentmode { get; set; }
        public string? paymentstatus { get; set; }

        public string? approvalstatus { get; set; }
        public bool posted { get; set; }

        public string? remarks { get; set; }

        public bool isactive { get; set; }
        public bool deleted { get; set; }

        public DateTime createddate { get; set; }
        public DateTime? modifieddate { get; set; }
        public long? usercode { get; set; }

        public string? tenantcode { get; set; }
        public string? branchcode { get; set; }
        public string? companycode { get; set; }
    }
    public class purchase_entry_detail
    {
        public long purchaseentrydetailcode { get; set; }

        public long purchaseentrycode { get; set; }
        public long itemcode { get; set; }

        public decimal orderedqty { get; set; }
        public decimal receivedqty { get; set; }
        public decimal rejectedqty { get; set; }
        public decimal quantity { get; set; }

        public decimal rate { get; set; }
        public decimal discountpercentage { get; set; }
        public decimal discountamount { get; set; }
        public decimal taxpercentage { get; set; }
        public decimal taxamount { get; set; }

        public decimal amount { get; set; }
        public decimal totalamount { get; set; }

        public string? batchno { get; set; }
        public DateTime? manufacturingdate { get; set; }
        public DateTime? expirydate { get; set; }

        public long? warehousecode { get; set; }

        public string? tenantcode { get; set; }
        
    }
    public class purchase_entry_request
    {
        public purchase_entry_master master { get; set; }
        public List<purchase_entry_detail> details { get; set; }
    }
    public class category_master
    {
        public long categorycode { get; set; }
        public string categoryname { get; set; }
        public string shortname { get; set; }
        public string? description { get; set; }
        public int parentcategorycode { get; set; }
        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public int usercode { get; set; }
        public string? tenantcode { get; set; }
    }
    public class uom_master
    {
        public long ucode { get; set; }

        public int orderno { get; set; }

        public string? name { get; set; }

        public string? shortname { get; set; }

        public string? description { get; set; }

        public bool? deleted { get; set; } = false;

        public int? usercode { get; set; }

        public int? computercode { get; set; }

        public DateTime? entereddate { get; set; }

        public DateTime? ibsdate { get; set; }

        public int? packsize { get; set; }

        public string? tenant_code { get; set; }
    }
    public class parent_category_master
    {
        public int parentcategorycode { get; set; }
        public string parentcategoryname { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }
        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public string? tenantcode { get; set; }
    }

    public class ledger_master
    {
        public int ledgercode { get; set; }
        public string ledgername { get; set; }
        public string lcode { get; set; }
        public string ldgcode { get; set; }

        public string taxtype { get; set; }
        public string taxsubtype { get; set; }
        public decimal taxpercentage { get; set; }

        public decimal gstpercentage { get; set; }
        public string hsncode { get; set; }

        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public string? tenantcode { get; set; }
    }
    [Table("ledger_type_master")]
    public class ledger_type_master
    {
        [Key]
        public int? ledgertypecode { get; set; }
        public string ledgertypename { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }
        public int naturetype { get; set; }
        public bool isactive { get; set; } = true;
        public DateTime createddate { get; set; } = DateTime.UtcNow;
        public string tenantcode { get; set; }
        public bool isgstapplicable { get; set; } = false;
        public bool isvatapplicable { get; set; } = false;
        public decimal sgstpercentage { get; set; } = 0;
        public decimal cgstpercentage { get; set; } = 0;
        public decimal igstpercentage { get; set; } = 0;
        public bool deleted { get; set; } = false;
    }
    [Table("ledger_group_master")]
    public class ledger_group_master
    {
        [Key]
        public int? ledgergroupcode { get; set; }

        public string ledgergroupname { get; set; }

        public string shortname { get; set; }

        public int ledgertypecode { get; set; }

        public string description { get; set; }

        public bool isactive { get; set; } = true;

        public DateTime createddate { get; set; } = DateTime.Now;

        public string tenantcode { get; set; }

        public bool deleted { get; set; } = false;
    }
    public class sales_master
 {
     public long salescode { get; set; }

     public string? billno { get; set; }

     public DateTime billdate { get; set; }

     public string? invoiceno { get; set; }

     public DateTime? invoicedate { get; set; }

     public long? customercode { get; set; }

     // NEW FIELDS
     public string? salestype { get; set; }      // IP/OP or Counter Sales

     public string? warehousefield { get; set; }  // Warehouse

     public string? patientid { get; set; }      // Patient ID

     public string? patientname { get; set; }    // Patient Name
     public string? mobileno { get; set; }

     public string? address { get; set; }        // Patient Address

     public string? consultant { get; set; }     // Consultant Name
     public int? prescribeddays { get; set; }   // total days prescribed by doctor
     public int? converteddays { get; set; }

     public decimal grossamount { get; set; }
     public decimal discountpercentage { get; set; }
     public decimal discountamount { get; set; }

     public decimal taxamount { get; set; }
     public decimal roundoff { get; set; }

     public decimal netamount { get; set; }

     public string? paymentmode { get; set; }

     public string? paymentstatus { get; set; }

     public string? currencycode { get; set; }

     public bool isactive { get; set; }

     public bool deleted { get; set; }

     public string? remarks { get; set; }

     public DateTime createddate { get; set; }

     public DateTime? modifieddate { get; set; }

     public long? usercode { get; set; }

     public string? tenantcode { get; set; }

     public string? branchcode { get; set; }

     public string? companycode { get; set; }

     public long? ordercode { get; set; }
 }
 public class sales_detail
 {
     public long salesdetailcode { get; set; }

     public long salescode { get; set; }

     public long itemcode { get; set; }

     public decimal quantity { get; set; }

     public decimal freequantity { get; set; }

     public long? uomcode { get; set; }

     public decimal rate { get; set; }

     public decimal discountpercentage { get; set; }

     public decimal discountamount { get; set; }

     public decimal taxpercentage { get; set; }

     public decimal taxamount { get; set; }

     public decimal amount { get; set; }

     public decimal totalamount { get; set; }

     public string? batchno { get; set; }

     public DateTime? manufacturingdate { get; set; }

     public DateTime? expirydate { get; set; }

     // Sales Specific

     public decimal soldqty { get; set; }
      public decimal returnedqty { get; set; }
        public int days { get; set; }


        public long? warehousecode { get; set; }

     public string? tenantcode { get; set; }
 }
 public class sales_request
 {
     public sales_master master { get; set; }
     public List<sales_detail> details { get; set; }
 }
    [Table("warehouse_master")]
    public class warehouse_master
 {
        [Key]
     public int? warehousecode { get; set; }
     public int orderno { get; set; }

     public string warehousename { get; set; }
     public string shortname { get; set; }
     public string description { get; set; }
     public string location { get; set; }

     public string? tenantcode { get; set; }

     public bool isactive { get; set; }
     public bool isdeleted { get; set; }

     public DateTime createddate { get; set; }
     public bool purchaseallow { get; set; } = false;

     public bool salesallow { get; set; } = false;
     public bool isdoctor_room { get; set; } = false;
     public string? druglicenseno { get; set; }
     public string? pharmacyregistrationno { get; set; }
     public string? gstno { get; set; }
     public string? msmeno { get; set; }
    }
    public class manufacturer_master
    {
        public long manufacturercode { get; set; }
        public string manufacturername { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }
        public string contactperson { get; set; }
        public string phoneno { get; set; }
        public string email { get; set; }
        public string address { get; set; }
        public string gstno { get; set; }
        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public int usercode { get; set; }
        public string tenantcode { get; set; }
    }
    // ─── PURCHASE RETURN ──────────────────────────────────────────────────────────

    // Step 1 input: user types item name + batch no
    public class purchase_return_lookup_request
    {
        public long itemcode { get; set; }
        public string? batchno { get; set; }   // optional now — filters results if provided
    }

    // Step 1 output: vendor + rate + available qty for that item/batch
    public class purchase_return_lookup_result
    {
        public long purchasedetailcode { get; set; }
        public long purchasecode { get; set; }
        public long itemcode { get; set; }
        public string itemname { get; set; }
        public string batchno { get; set; }

        public long vendorcode { get; set; }
        public string vendorname { get; set; }
        public string contactperson { get; set; }
        public string phonenumber { get; set; }
        public string gstnumber { get; set; }

        public decimal rate { get; set; }
        public decimal receivedqty { get; set; }
        public decimal returnedqty { get; set; }
        public decimal availableqty { get; set; }   // receivedqty - returnedqty
        public decimal packsize { get; set; }

        public long? warehousecode { get; set; }
    }

    // Step 2 input: ONE line item inside a multi-item return submission
    public class purchase_return_item_request
    {
        public long purchasedetailcode { get; set; }
        public long purchasecode { get; set; }
        public long itemcode { get; set; }
        public string batchno { get; set; }

        public decimal returnqty { get; set; }
        public decimal packsize { get; set; }

        public long? warehousecode { get; set; }
        public string? remarks { get; set; }
    }

    // Step 2 input: the whole submission — one vendor, many items
    public class purchase_return_request
    {
        public long vendorcode { get; set; }
        public long? usercode { get; set; }
        public string? tenantcode { get; set; }
        public string? remarks { get; set; }   // header-level remark, optional

        public List<purchase_return_item_request> items { get; set; } = new();
    }
    // Persisted return record
    [Table("purchase_return_master")]
    public class purchase_return_master
    {
        [Key]
        public long purchasereturncode { get; set; }

        public long purchasedetailcode { get; set; }
        public long purchasecode { get; set; }
        public long itemcode { get; set; }
        public long vendorcode { get; set; }
        public string? batchno { get; set; }

        public decimal returnqty { get; set; }
        public decimal packsize { get; set; }
        public decimal totalqty { get; set; }   // returnqty * packsize
        public decimal rate { get; set; }
        public decimal amount { get; set; }     // totalqty * rate

        public long? warehousecode { get; set; }
        public string? remarks { get; set; }

        public bool isactive { get; set; }
        public bool deleted { get; set; }
        public DateTime createddate { get; set; }
        public long? usercode { get; set; }
        public string? tenantcode { get; set; }
    }

       public class sales_return_lookup_request
   {
       public long itemcode { get; set; }

       public string? batchno { get; set; }   // Optional
   }
   public class sales_return_lookup_result
   {
       public long salesdetailcode { get; set; }

       public long salescode { get; set; }

       public long itemcode { get; set; }

       public string? itemname { get; set; }

       public string? batchno { get; set; }

public long? customercode { get; set; }

     public string? customername { get; set; }

       public string? patientid { get; set; }

       public string? patientname { get; set; }

       public decimal rate { get; set; }

       public decimal deliveredqty { get; set; }

       public decimal returnedqty { get; set; }

       public decimal availableqty { get; set; }

       public decimal packsize { get; set; }

       public long? warehousecode { get; set; }
   }
   
    // Step 2 input: ONE line item inside a multi-item sales return submission
    public class sales_return_item_request
    {
        public long salesdetailcode { get; set; }
        public long salescode { get; set; }
        public long itemcode { get; set; }
        public string? batchno { get; set; }

        public decimal returnqty { get; set; }
        public decimal packsize { get; set; }

        public long? warehousecode { get; set; }
        public string? remarks { get; set; }
    }

    // Step 2 input: the whole submission — one customer, many items
    public class sales_return_request
    {
        public long? customercode { get; set; }
        public long? usercode { get; set; }
        public string? tenantcode { get; set; }
        public string? remarks { get; set; }   // header-level remark, optional

        public List<sales_return_item_request> items { get; set; } = new();
    }
    public class sales_return_master
    {

        public long salesreturncode { get; set; }

        public long salesdetailcode { get; set; }

        public long salescode { get; set; }

        public long itemcode { get; set; }

        public long? customercode { get; set; }

        public string? batchno { get; set; }

        public decimal returnqty { get; set; }

        public decimal packsize { get; set; }

        public decimal totalqty { get; set; }

        public decimal rate { get; set; }

        public decimal amount { get; set; }

        public long? warehousecode { get; set; }

        public string? remarks { get; set; }

        public bool isactive { get; set; }

        public bool deleted { get; set; }

        public DateTime createddate { get; set; }

        public long? usercode { get; set; }

        public string? tenantcode { get; set; }
    }
     public class stock_adjustment_log
 {
     public long adjustmentlogcode { get; set; }

     public long stockcode { get; set; }

     public long itemcode { get; set; }

     public long? warehousecode { get; set; }

     public string? branchcode { get; set; }

     public string? locationcode { get; set; }

     public string? batchno { get; set; }

     // Stock before adjustment
     public decimal beforeqty { get; set; }

     // Adjustment quantity (+/-)
     public decimal adjustedqty { get; set; }

     // Stock after adjustment
     public decimal afterqty { get; set; }

     public decimal unitcost { get; set; }

     public decimal stockvalue { get; set; }

     // Increase / Decrease
     public string adjustmenttype { get; set; } = string.Empty;

     // Physical Count, Damage, Expiry, Theft, etc.
     public string adjustmentreason { get; set; } = string.Empty;

     public string? remarks { get; set; }

     public DateTime adjustmentdate { get; set; }

     // User who performed the adjustment
     public long? adjustedby { get; set; }

     public bool isactive { get; set; } = true;

     public bool deleted { get; set; } = false;

     public DateTime createddate { get; set; }

     public DateTime? modifieddate { get; set; }

     public string? tenantcode { get; set; }

     public string? companycode { get; set; }
 }
 public class stock_adjustment_request
 {
     public stock_master stock { get; set; } = new();

     public stock_adjustment_log adjustmentlog { get; set; } = new();
 }
    public class stock_transfer_master
{
    public long transfercode { get; set; }
    public string? transferno { get; set; }
    public DateTime transferdate { get; set; }
    public string? transfertype { get; set; }
    public long? indentcode { get; set; }
    public long? fromwarehousecode { get; set; }
    public long? towarehousecode { get; set; }
    public string? branchcode { get; set; }
    public string? reason { get; set; }
    public string? remarks { get; set; }
    public string? status { get; set; }
    public long? createdby { get; set; }
    public DateTime? createddate { get; set; }
    public string? tenantcode { get; set; }

    public List<stock_transfer_detail>? details { get; set; }
}
 public class stock_transfer_detail
 {
     public long transferdetailcode { get; set; }

     public long transfercode { get; set; }

     public long itemcode { get; set; }

     public string? batchno { get; set; }

     public DateTime? manufacturingdate { get; set; }

     public DateTime? expirydate { get; set; }

     public long? uomcode { get; set; }

     public decimal availableqty { get; set; }

     public decimal requestedqty { get; set; }

     public decimal approvedqty { get; set; }

     public decimal transferqty { get; set; }

     // Partial receiving / rejection
     public decimal receivedqty { get; set; }

     public decimal rejectedqty { get; set; }

     public decimal unitcost { get; set; }

     public decimal stockvalue { get; set; }

     public string? remarks { get; set; }

     public string? tenantcode { get; set; }
 }
 public class stock_transfer_request
 {
     public stock_transfer_master master { get; set; }
         = new stock_transfer_master();

     public List<stock_transfer_detail> details { get; set; }
         = new List<stock_transfer_detail>();
 }
 public class stock_transfer_action_request
 {
     public long transfercode { get; set; }

     public long usercode { get; set; }

     public string? remarks { get; set; }
 }
 public class stock_transfer_partial_reject_detail
 {
     public long transferdetailcode { get; set; }

     public decimal acceptedqty { get; set; }

     public decimal rejectedqty { get; set; }

     public string? remarks { get; set; }
 }

 public class stock_transfer_partial_reject_request
 {
     public long transfercode { get; set; }

     public long usercode { get; set; }

     public string? remarks { get; set; }

     public List<stock_transfer_partial_reject_detail> details { get; set; }
         = new List<stock_transfer_partial_reject_detail>();
 }



    // ─── PHARMACY QUEUE ──────────────────────────────────────────────────────────
    [Table("pharmacy_prescription_queue")]
    public class PharmacyPrescriptionQueueRow
    {
        [ExplicitKey] public Guid queue_id { get; set; } = Guid.NewGuid();
        public Guid? pr_det_id { get; set; }
        public string? pr_code { get; set; }
        public Guid? sheet_id { get; set; }
        public Guid? op_id { get; set; }
        public Guid? ip_id { get; set; }
        public decimal custid { get; set; }

        public string drug_name { get; set; } = string.Empty;
        public long? matched_itemcode { get; set; }
        public string? matched_itemname { get; set; }

        public decimal? qty { get; set; }
        public string? morning { get; set; }
        public string? afternoon { get; set; }
        public string? evening { get; set; }
        public string? night { get; set; }
        public bool before_food { get; set; }
        public bool after_food { get; set; }
        public int? days { get; set; }
        public string? route { get; set; }
        public string? notes { get; set; }

        public string status { get; set; } = "PENDING";
        public string? tenant_code { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
    }

    public class ReceivePrescriptionRequest
    {
        public decimal custid { get; set; }
        public string? op_id { get; set; }
        public string? ip_id { get; set; }
        public string? sheet_id { get; set; }
        public string? pr_code { get; set; }
        public List<PrescriptionQueueItem> items { get; set; } = new();
    }

    public class PrescriptionQueueItem
    {
        public string? pr_det_id { get; set; }
        public string drug_name { get; set; } = string.Empty;
        public decimal qty { get; set; }
        public string? morning { get; set; }
        public string? afternoon { get; set; }
        public string? evening { get; set; }
        public string? night { get; set; }
        public bool before_food { get; set; }
        public bool after_food { get; set; }
        public int? days { get; set; }
        public string? route { get; set; }
        public string? notes { get; set; }
    }
    public class MatchQueueItemRequest
    {
        public Guid queue_id { get; set; }
        public long itemcode { get; set; }
    }

    public class DispenseQueueItemRequest
    {
        public Guid queue_id { get; set; }
    }
    public class PharmacyQueueGroup
    {
        public decimal custid { get; set; }
        public string? pr_code { get; set; }
        public Guid? sheet_id { get; set; }
        public Guid? op_id { get; set; }
        public Guid? ip_id { get; set; }
        public List<PharmacyPrescriptionQueueRow> items { get; set; } = new();
    }

    public class UpdateQueueStatusRequest
    {
        public Guid queue_id { get; set; }
        public string status { get; set; } = string.Empty;   // PENDING / MATCHED / DISPENSED / CANCELLED
    }
    // ─── PURCHASE FILTER ──────────────────────────────────────────────────────────
    public class purchase_filter_result
    {
        public long purchasecode { get; set; }
        public string? billno { get; set; }
        public DateTime billdate { get; set; }
        public string? invoiceno { get; set; }
        public DateTime? invoicedate { get; set; }
        public long? vendorcode { get; set; }
        public string? vendorname { get; set; }
        public decimal grossamount { get; set; }
        public decimal totalamount { get; set; }
        public decimal taxamount { get; set; }
        public decimal netamount { get; set; }
        public string? paymentmode { get; set; }
        public string? paymentstatus { get; set; }
        public string? chequeno { get; set; }     // NEW
        public string? accountno { get; set; }
        public DateTime createddate { get; set; }
    }

    // ─── PURCHASE BULK STATUS UPDATE ──────────────────────────────────────────────
    public class purchase_bulk_status_request
    {
        public List<long> purchasecodes { get; set; } = new();
        public string paymentstatus { get; set; } = string.Empty;
        public string? paymentmode { get; set; }   // NEW
        public string? chequeno { get; set; }      // NEW
        public string? accountno { get; set; }     // NEW
        public long? usercode { get; set; }
    }
    public class consumption_master
    {
        public long consumptioncode { get; set; }
        public string? consumptionno { get; set; }
        public DateTime consumptiondate { get; set; }

        public long roomwarehousecode { get; set; }   // must be a warehouse_master row with isdoctor_room = true
        public string? departmentname { get; set; }
        public string? doctorname { get; set; }
        public string? remarks { get; set; }

        public decimal totalitems { get; set; }
        public decimal totalqty { get; set; }
        public decimal totalvalue { get; set; }

        public bool isactive { get; set; } = true;
        public bool deleted { get; set; } = false;
        public DateTime createddate { get; set; }
        public DateTime? modifieddate { get; set; }
        public long? usercode { get; set; }
        public string? tenantcode { get; set; }
        public string? branchcode { get; set; }
    }

    public class consumption_detail
    {
        public long consumptiondetailcode { get; set; }
        public long consumptioncode { get; set; }

        public long stockcode { get; set; }     // exact batch/stock row selected by the user
        public long itemcode { get; set; }
        public string? batchno { get; set; }

        public decimal availableqty { get; set; }   // system-filled, snapshot before deduction
        public decimal consumedqty { get; set; }    // user input
        public decimal unitcost { get; set; }       // system-filled
        public decimal stockvalue { get; set; }     // system-filled

        public string? remarks { get; set; }
        public string? tenantcode { get; set; }
    }

    public class consumption_request
    {
        public consumption_master master { get; set; } = new();
        public List<consumption_detail> details { get; set; } = new();
    }
    

    
        // ─── PURCHASE ORDER MASTER ─────────────────────────────────────────────
        public class purchase_order_master
        {
            public long ordercode { get; set; }

            public string? orderno { get; set; }
            public DateTime orderdate { get; set; }
            public string? llrcode { get; set; }
            public long? vendorcode { get; set; }

            public bool overalltax { get; set; }
            public decimal taxpercentage { get; set; }

            public decimal grossamount { get; set; }
            public decimal discountamount { get; set; }
            public decimal taxamount { get; set; }
            public decimal roundoff { get; set; }
            public decimal grandtotal { get; set; }

            public string? status { get; set; }   // PENDING / APPROVED / CLOSED / CANCELLED / CONVERTED

            public string? remarks { get; set; }

            public bool isactive { get; set; }
            public bool deleted { get; set; }

            public DateTime createddate { get; set; }
            public DateTime? modifieddate { get; set; }
            public long? usercode { get; set; }

            public string? tenantcode { get; set; }
            public string? branchcode { get; set; }
            public string? companycode { get; set; }
        }

        // ─── PURCHASE ORDER DETAIL (item lines) ────────────────────────────────
        public class purchase_order_detail
        {
            public long orderdetailcode { get; set; }
            public long ordercode { get; set; }

            public long itemcode { get; set; }
            public int? itemgroupcode { get; set; }

            public decimal quantity { get; set; }
            public decimal packing { get; set; }
            public decimal freeqty { get; set; }
            public decimal mrp { get; set; }
            public decimal orderrate { get; set; }

            public decimal discountpercentage { get; set; }
            public decimal discountamount { get; set; }

            public decimal amount { get; set; }

            public string? tenantcode { get; set; }
        }

        // ─── PURCHASE ORDER TERMS & CONDITIONS ─────────────────────────────────
        public class purchase_order_terms
        {
            public long termcode { get; set; }
            public long ordercode { get; set; }
            public int slno { get; set; }
            public string? termcondition { get; set; }
            public string? value { get; set; }
            public string? tenantcode { get; set; }
        }

        // ─── COMPOSITE REQUEST (what the controller receives) ─────────────────
        public class purchase_order_request
        {
            public purchase_order_master master { get; set; } = new();
            public List<purchase_order_detail> details { get; set; } = new();
            public List<purchase_order_terms>? terms { get; set; }
        }
        
}

