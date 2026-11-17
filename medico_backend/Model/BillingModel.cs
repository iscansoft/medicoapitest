using System;
using System.Collections.Generic;
using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    // ═══════════════════════════════════════════════════════════════
    // DB TABLE MODELS — mapped to existing PostgreSQL tables
    // ═══════════════════════════════════════════════════════════════

    [Table("lab_request_master")]
    public class HmsLabRequestMaster
    {
        [ExplicitKey] public string? requestguid { get; set; }
        public int? requestsno { get; set; }
        public string? requestsnoprint { get; set; }
        public string? requestbarcode { get; set; }
        public string? requestconvertedbarcode { get; set; }
        public DateTime? requestdatetime { get; set; }
        public DateTime? requesteddatetime { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public decimal? bncode { get; set; }
        public decimal? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? tmcode { get; set; }
        public decimal? custid { get; set; }
        public string? name { get; set; }
        public string? gender { get; set; }
        public string? dateofbirth { get; set; }
        public string? ageyears { get; set; }
        public string? agemonths { get; set; }
        public string? agedays { get; set; }
        public string? address { get; set; }
        public int? areacode { get; set; }
        public string? mobileno { get; set; }
        public decimal? dcode { get; set; }
        public int? consultantdcode { get; set; }
        public decimal? ftcode { get; set; }
        public decimal? ricode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? dlcode { get; set; }
        public decimal? icode { get; set; }
        public double? requestamount { get; set; }
        public double? discountper { get; set; }
        public double? discountamount { get; set; }
        public double? specialdiscount { get; set; }
        public double? ourdispercentage { get; set; }
        public double? ourdiscount { get; set; }
        public double? totalamount { get; set; }
        public double? paidamount { get; set; }
        public double? paidviareceipt { get; set; }
        public double? newamount { get; set; }
        public double? taxamount { get; set; }
        public double? advanceamount { get; set; }
        public double? pmc1 { get; set; }
        public double? pmc2 { get; set; }
        public double? pmc3 { get; set; }
        public double? pmc1_amount { get; set; }
        public double? pmc2_amount { get; set; }
        public double? pmc3_amount { get; set; }
        public bool? iscashbill { get; set; }
        public bool? iscreditbill { get; set; }
        public bool? isinvestigation { get; set; }
        public bool? requeststatus { get; set; }
        public bool? resultstatus { get; set; }
        public bool? deleted { get; set; }
        public bool? isverified { get; set; }
        public bool? isinsurancepatient { get; set; }
        public bool? isdeleted { get; set; }
        public string? policyno { get; set; }
        public string? authorisationno { get; set; }
        public string? billtype { get; set; }
        public string? chargetype { get; set; }
        public string? bill_category { get; set; }
        public string? sheet_id { get; set; }
        public string? opvisitid { get; set; }
        public Guid? ip_id { get; set; }
        public string? concessionreason { get; set; }
        public string? card_refno { get; set; }
        public string? bank_app { get; set; }
        public int? seconddcode { get; set; }
        public string? seconddoctorname { get; set; }
        public string? doc1_dmid { get; set; }
        public string? doc1_dmvalue { get; set; }
        public string? doc1_path { get; set; }
        public string? doc2_dmid { get; set; }
        public string? doc2_dmvalue { get; set; }
        public string? doc2_path { get; set; }
        public int? enteredbhcode { get; set; }
        public int? alteredbhcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? tenant_code { get; set; }
        public string? orderguid { get; set; }
    }

    [Table("lab_request_details")]
    public class HmsLabRequestDetail
    {
        [ExplicitKey] public string? requestdetailsid { get; set; }
        public string? requestguid { get; set; }
        public int? testsno { get; set; }
        public decimal? tcode { get; set; }
        public int? packcode { get; set; }
        public int? ttid { get; set; }
        public string? chargetype { get; set; }
        public string? item_name { get; set; }
        public string? item_ref_id { get; set; }
        public double? testamount { get; set; }
        public double? testrate { get; set; }
        public double? standardprice { get; set; }
        public double? discount { get; set; }
        public double? specialdiscount { get; set; }
        public double? newamount { get; set; }
        public double? gstper { get; set; }
        public double? gstamount { get; set; }
        public double? collection { get; set; }
        public double? qty { get; set; }
        public bool ispack { get; set; } = false;
        public bool? resultstatus { get; set; }
        public bool? requeststatus { get; set; }
        public bool? islockresult { get; set; }
        public bool? istesthavingresult { get; set; }
        public bool? isprinted { get; set; }
        public bool? isauthorized1 { get; set; }
        public bool? isauthorized2 { get; set; }
        public bool? isworklistprinted { get; set; }
        public bool? isdeleted { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("receipt_master")]
    public class HmsReceiptMaster
    {
        [ExplicitKey] public string? receiptguid { get; set; }
        public DateTime? receiptdate { get; set; }
        public int? receiptsno { get; set; }
        public string? receiptsnoprint { get; set; }
        public string? receiptbarcode { get; set; }
        public string? receiptcovertedbarcode { get; set; }
        public decimal? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? tmcode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public string? bankname { get; set; }
        public string? paymentreference { get; set; }
        public string? cardno { get; set; }
        public double? amountpaid { get; set; }
        public double? amountadjusted { get; set; }
        public double? amounttotal { get; set; }
        public bool? deleted { get; set; }
        public bool? isdeleted { get; set; }
        public bool? isbill { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefund { get; set; }
        public bool? isrefferal { get; set; }
        public bool? ismonthly { get; set; }
        public string? receipttype { get; set; }
        public int? custid { get; set; }
        public string? opvisitid { get; set; }
        public int? enteredbhcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("receipt_details")]
    public class HmsReceiptDetail
    {
        [ExplicitKey] public string? receiptdetailsid { get; set; }
        public string? receiptguid { get; set; }
        public string? requestguid { get; set; }
        public double? receiptamount { get; set; }
        public double? discount_amount { get; set; }
        public double? refund_amount { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("balancecollectionby")]
    public class HmsBalanceCollectionBy
    {
        [ExplicitKey] public string? balancecollectionbyid { get; set; }
        public int? bhcode { get; set; }
        public DateTime? collected_date { get; set; }
        public string? collection_type { get; set; }
        public string? receipt_guid { get; set; }
        public string? request_guid { get; set; }
        public double? collectedamount { get; set; }
        public decimal? tmcode { get; set; }
        public decimal? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? pmcode { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("balancecollectionbytest")]
    public class HmsBalanceCollectionByTest
    {
        [ExplicitKey] public string? balancecollectionbytestid { get; set; }
        public decimal? tcode { get; set; }
        public string? balancecollectionbyid { get; set; }
        public double? collectedamount { get; set; }
        public bool? requeststatus { get; set; }
        public string? tenant_code { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
    }

    [Table("billno_master")]
    public class HmsBillNoMaster
    {
        [ExplicitKey] public decimal bncode { get; set; }
        public int orderno { get; set; }
        public string? name { get; set; }
        public string? shortname { get; set; }
        public decimal? bhcode { get; set; }
        public int? cntcode { get; set; }
        public bool? isdefault { get; set; }
        public bool? allbranch { get; set; }
        public bool? allcounter { get; set; }
        public bool? restartfinancialyear { get; set; }
        public bool? restartcalendaryear { get; set; }
        public bool? restartmonthly { get; set; }
        public bool? restartdaily { get; set; }
        public bool? issampleno { get; set; }
        public bool? isreceiptno { get; set; }
        public bool deleted { get; set; }
        public string? tenant_code { get; set; }
        public int usercode { get; set; }
        public int computercode { get; set; }
        public DateTime entereddate { get; set; }
        public DateTime ibsdate { get; set; }
    }

    [Table("billno_sequence")]
    public class HmsBillNoSequence
    {
        [Key] public long seq_id { get; set; }
        public decimal? bncode { get; set; }
        public int? bhcode { get; set; }
        public decimal? cntcode { get; set; }
        public int orderno { get; set; }
        public DateTime? last_used_date { get; set; }
        public string? tenant_code { get; set; }
        public string? snoprint { get; set; }
    }

    [Table("counter_timing")]
    public class HmsCounterTiming
    {
        [ExplicitKey] public string? cnttid { get; set; }
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public int shiftsno { get; set; }
        public DateTime? counterdate { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? shift_mode { get; set; }
        public DateTime? planned_to { get; set; }
        public string? close_type { get; set; }
        public string? tenant_code { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // REQUEST / RESPONSE DTOs
    // ═══════════════════════════════════════════════════════════════

    public class CreateHmsBillRequest
    {
        public string? requestguid { get; set; }
        public string? op_id { get; set; } = string.Empty;
        public Guid? ip_id { get; set; }
        public string? sheet_id { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? gender { get; set; }
        public string? dateofbirth { get; set; }
        public string? ageyears { get; set; }
        public string? agemonths { get; set; }
        public string? agedays { get; set; }
        public string? mobileno { get; set; }
        public string? address { get; set; }
        public int? areacode { get; set; }
        public int? dcode { get; set; }
        public int? consultantdcode { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public decimal? ftcode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? ricode { get; set; }
        public double? discountper { get; set; }
        public double? discountamount { get; set; }
        public double? specialdiscount { get; set; }
        public double? ourdispercentage { get; set; }     // referral concession %   <-- ADD
        public double? ourdiscount { get; set; }
        public double? paidamount { get; set; }
        public double? pmc1 { get; set; }
        public double? pmc2 { get; set; }
        public double? pmc3 { get; set; }
        public double? pmc1_amount { get; set; }
        public double? pmc2_amount { get; set; }
        public double? pmc3_amount { get; set; }
        public string collection_type { get; set; } = "CASH";
        public bool? iscashbill { get; set; }
        public bool? iscreditbill { get; set; }
        public bool? isinsurancepatient { get; set; }
        public string? policyno { get; set; }
        public string? authorisationno { get; set; }
        public string? concessionreason { get; set; }
        public string? card_refno { get; set; }
        public string? bank_app { get; set; }
        public List<HmsBillLineItemDto> items { get; set; } = new();
        public List<string>? unbilled_charge_ids { get; set; }
    }

    public class HmsBillLineItemDto
    {
        public int sno { get; set; }
        public string charge_type { get; set; } = "INVESTIGATION";
        public string? item_name { get; set; }
        public decimal? tcode { get; set; }
        public string? item_ref_id { get; set; }
        public double? unit_rate { get; set; }
        public double? amount { get; set; }
        public double qty { get; set; } = 1;
        public double? discount { get; set; }
        public double? gst_per { get; set; }
        public int? ttid { get; set; } = 1;
    }

    public class AddHmsPaymentRequest
    {
        public string requestguid { get; set; } = string.Empty;
        public double amount { get; set; }
        public string collection_type { get; set; } = "CASH";
        public decimal? pmcode { get; set; }
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public double? pmc1 { get; set; }
        public double? pmc2 { get; set; }
        public double? pmc3 { get; set; }
        public double? pmc1_amount { get; set; }
        public double? pmc2_amount { get; set; }
        public double? pmc3_amount { get; set; }
    }

    public class CancelHmsBillRequest
    {
        public string requestguid { get; set; } = string.Empty;
        public int? usercode { get; set; }
        public string? reason { get; set; }
    }

    public class HmsBillFilterRequest
    {
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public Guid? ip_id { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public decimal? custid { get; set; }
        public int? dcode { get; set; }
        public bool? pendingonly { get; set; }
        public bool? iscashbill { get; set; }
        public bool? iscreditbill { get; set; }
        public string? search { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class CloseCounterRequest
    {
        public string cnttid { get; set; } = string.Empty;
        public int? usercode { get; set; }
    }

    public class HmsBillResponse
    {
        public string? requestguid { get; set; }
        public string? op_id { get; set; }
        public Guid? ip_id { get; set; }
        public string? bill_no { get; set; }
        public string? barcode { get; set; }
        public DateTime? bill_date { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? gender { get; set; }
        public string? mobileno { get; set; }
        public string? dateofbirth { get; set; }
        public string? ageyears { get; set; }
        public int? dcode { get; set; }
        public string? doctor_name { get; set; }
        public string? fee_type { get; set; }
        public string? pay_mode { get; set; }
        public string? counter_name { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? tmcode { get; set; }
        public double? gross_amount { get; set; }
        public double? discount_amount { get; set; }
        public double? general_concession_per { get; set; }    // <-- ADD (= discountper)
        public double? general_concession_amount { get; set; } // <-- ADD (= discountamount)
        public double? referral_concession_per { get; set; }    // <-- ADD (= ourdispercentage)
        public double? referral_concession_amount { get; set; }
        public double? tax_amount { get; set; }
        public double? net_amount { get; set; }
        public double? paid_amount { get; set; }
        public double? balance_amount { get; set; }
        public bool is_settled { get; set; }
        public double? pmc1 { get; set; }
        public double? pmc2 { get; set; }
        public double? pmc3 { get; set; }
        public double? pmc1_amount { get; set; }
        public double? pmc2_amount { get; set; }
        public double? pmc3_amount { get; set; }
        public string? receiptguid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public List<HmsBillLineResponse> items { get; set; } = new();
    }

    public class HmsBillLineResponse
    {
        public string? requestdetailsid { get; set; }
        public string? charge_type { get; set; }
        public string? item_name { get; set; }
        public decimal? tcode { get; set; }
        public string? item_ref_id { get; set; }
        public double? unit_rate { get; set; }
        public double? amount { get; set; }
        public double? discount { get; set; }
        public double? final_amount { get; set; }
        public double? qty { get; set; }
        public double? gst_per { get; set; }
        public double? gst_amount { get; set; }
    }

    public class HmsBillSummary
    {
        public string? requestguid { get; set; }
        public Guid? ip_id { get; set; }
        public string? bill_no { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public DateTime? bill_date { get; set; }
        public string? doctor_name { get; set; }
        public double? gross_amount { get; set; }
        public double? discount_amount { get; set; }
        public double? net_amount { get; set; }
        public double? paid_amount { get; set; }
        public double? balance_amount { get; set; }
        public bool is_settled { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? opvisitid { get; set; }
        public string? dateofbirth { get; set; }
        public int? dcode { get; set; }
        public int? usercode { get; set; }

        public string? type { get; set; }   // "IP" | "OP" | "LAB" — NEW

    }

    public class HmsUnbilledChargesResponse
    {
        public string op_id { get; set; } = string.Empty;
        public decimal? custid { get; set; }
        public int? dcode { get; set; }
        public string? doctor_name { get; set; }
        public double consultation_fee { get; set; }
        public List<HmsBillLineItemDto> items { get; set; } = new();
        public double total_amount { get; set; }
    }

    public class HmsCounterTimingDto
    {
        public string? cnttid { get; set; }
        public int? cntcode { get; set; }
        public string? counter_name { get; set; }
        public int? bhcode { get; set; }
        public int shiftsno { get; set; }
        public DateTime? counterdate { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public bool is_open { get; set; }
        public bool is_closed { get; set; }
    }

    public class HmsDailyCollectionDto
    {
        public DateTime date { get; set; }
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public string? counter_name { get; set; }
        public int total_bills { get; set; }
        public double gross_amount { get; set; }
        public double discount_amount { get; set; }
        public double discount { get; set; }
        public double gross { get; set; }
        public double net { get; set; }
        public double collected { get; set; }
        public double due { get; set; }
        public double net_amount { get; set; }
        public double collected_cash { get; set; }
        public double collected_card { get; set; }
        public double collected_upi { get; set; }
        public double total_collected { get; set; }
        public double pending_amount { get; set; }
    }

    public class HmsNumberResult
    {
        public int sno { get; set; }
        public string snoprint { get; set; } = string.Empty;
        public string barcode { get; set; } = string.Empty;
        public decimal used_bncode { get; set; }
    }

    public class HmsReceiptInserted
    {
        public string guid { get; set; } = string.Empty;
        public string barcode { get; set; } = string.Empty;
        public string snoprint { get; set; } = string.Empty;
    }
    [Table("branch_master")]
    public class BranchMasterRow
    {
        [ExplicitKey] public int bhcode { get; set; }
        public int orderno { get; set; }
        public string? shortname { get; set; }
        public string? name { get; set; }
        public string? address { get; set; }
        public string? city { get; set; }
        public string? pincode { get; set; }
        public string? state { get; set; }
        public string? country { get; set; }
        public string? phone { get; set; }
        public string? mobile { get; set; }
        public string? fax { get; set; }
        public string? email { get; set; }
        public string? website { get; set; }
        public string? description { get; set; }
        public int? areacode { get; set; }
        public bool deleted { get; set; }
        public int usercode { get; set; }
        public int computercode { get; set; }
        public DateTime entereddate { get; set; }
        public DateTime ibsdate { get; set; }
        public bool? ismainbranch { get; set; }
        public bool? isbranch { get; set; }
        public bool? iscollectioncentre { get; set; }
        public string? pharmacyname { get; set; }
        public string? labname { get; set; }
    }

    [Table("counter_master")]
    public class CounterMasterRow
    {
        [ExplicitKey] public decimal cntcode { get; set; }
        public int orderno { get; set; }
        public string? shortname { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public int? bhcode { get; set; }
        public string? cashlcode { get; set; }
        public int? patientbilllcode { get; set; }
        public int? referabillllcode { get; set; }
        public int? patientsaleslcode { get; set; }
        public int? referalsaleslcode { get; set; }
        public int? commissionbilllcode { get; set; }
        public int? commissionsaleslcode { get; set; }
        public bool? timingcurrent { get; set; }
        public bool? timingvariable { get; set; }
        public DateTime? timingfrom { get; set; }
        public DateTime? timingto { get; set; }
        public bool deleted { get; set; }
        public int usercode { get; set; }
        public int computercode { get; set; }
        public DateTime entereddate { get; set; }
        public DateTime ibsdate { get; set; }
        public string? expenselcode { get; set; }
        public int? hdcode { get; set; }
        public bool? timingfixed { get; set; }
        public bool? isinsurance { get; set; }
    }
    // ── BILLNO MASTER CONFIG DTOs ──────────────────────────────────────────────

    public class CreateBillNoMasterRequest
    {
        public string name { get; set; } = string.Empty;
        public string shortname { get; set; } = string.Empty;
        public int orderno { get; set; } = 0;
        public decimal? bhcode { get; set; }
        public int? cntcode { get; set; }
        public bool isdefault { get; set; } = false;
        public bool allbranch { get; set; } = true;
        public bool allcounter { get; set; } = true;
        public bool restartfinancialyear { get; set; } = false;
        public bool restartcalendaryear { get; set; } = false;
        public bool restartmonthly { get; set; } = true;
        public bool restartdaily { get; set; } = false;
        public bool issampleno { get; set; } = false;
        public bool isreceiptno { get; set; } = false;
        public int? usercode { get; set; }
        public int? computercode { get; set; }
    }

    public class UpdateBillNoMasterRequest
    {
        public decimal bncode { get; set; }
        public string? name { get; set; }
        public string? shortname { get; set; }
        public int? orderno { get; set; }
        public decimal? bhcode { get; set; }
        public int? cntcode { get; set; }
        public bool? isdefault { get; set; }
        public bool? allbranch { get; set; }
        public bool? allcounter { get; set; }
        public bool? restartfinancialyear { get; set; }
        public bool? restartcalendaryear { get; set; }
        public bool? restartmonthly { get; set; }
        public bool? restartdaily { get; set; }
        public bool? issampleno { get; set; }
        public bool? isreceiptno { get; set; }
        public int? usercode { get; set; }
    }

    public class DeleteBillNoMasterRequest
    {
        public decimal bncode { get; set; }
        public int? usercode { get; set; }
    }

    public class BillNoMasterFilterRequest
    {
        public bool? isreceiptno { get; set; }
        public bool? issampleno { get; set; }
        public bool? includeDeleted { get; set; } = false;
        public string? search { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class BillNoMasterResponse
    {
        public decimal bncode { get; set; }
        public string? name { get; set; }
        public string? shortname { get; set; }
        public int orderno { get; set; }
        public decimal? bhcode { get; set; }
        public int? cntcode { get; set; }
        public bool? isdefault { get; set; }
        public bool? allbranch { get; set; }
        public bool? allcounter { get; set; }
        public bool? restartfinancialyear { get; set; }
        public bool? restartcalendaryear { get; set; }
        public bool? restartmonthly { get; set; }
        public bool? restartdaily { get; set; }
        public bool? issampleno { get; set; }
        public bool? isreceiptno { get; set; }
        public bool deleted { get; set; }
        public string? tenant_code { get; set; }
        public DateTime entereddate { get; set; }
        /// <summary>Live count of how many sequence rows currently use this config — helps the UI warn before delete.</summary>
        public int sequence_rows_in_use { get; set; }
    }
    public class UpdateHmsBillRequest
    {
        public string requestguid { get; set; } = string.Empty;  // required
        public string? op_id { get; set; }
        public Guid? ip_id { get; set; }
        public string? sheet_id { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? gender { get; set; }
        public string? dateofbirth { get; set; }
        public string? ageyears { get; set; }
        public string? agemonths { get; set; }
        public string? agedays { get; set; }
        public string? mobileno { get; set; }
        public string? address { get; set; }
        public int? areacode { get; set; }
        public int? dcode { get; set; }
        public int? consultantdcode { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public decimal? ftcode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? ricode { get; set; }
        public double? discountper { get; set; }
        public double? discountamount { get; set; }
        public double? specialdiscount { get; set; }
        public double? ourdispercentage { get; set; }     // referral concession %   <-- ADD
        public double? ourdiscount { get; set; }
        public double? pmc1 { get; set; }
        public double? pmc2 { get; set; }
        public double? pmc3 { get; set; }
        public double? pmc1_amount { get; set; }
        public double? pmc2_amount { get; set; }
        public double? pmc3_amount { get; set; }
        public bool? iscashbill { get; set; }
        public bool? iscreditbill { get; set; }
        public bool? isinsurancepatient { get; set; }
        public string? policyno { get; set; }
        public string? authorisationno { get; set; }
        public string? concessionreason { get; set; }
        public string? card_refno { get; set; }
        public string? bank_app { get; set; }

        // ── Optional: revert advance that was used on this bill into a paymode ──
        public bool? revert_all_advance { get; set; }        // true = revert everything used
        public double? revert_advance_amount { get; set; }   // or a specific amount
        public decimal? revert_pmcode { get; set; }          // paymode_master.pmcode (required when reverting)
        public string? revert_reference_no { get; set; }
        public string? revert_bank_name { get; set; }
        public string? revert_card_no { get; set; }
        public DateTime? revert_cheque_date { get; set; }
        public string? revert_reason { get; set; }

        public List<HmsBillLineItemDto> items { get; set; } = new();
    }

    public class UpdateHmsBillResponse
    {
        public string requestguid { get; set; } = string.Empty;
        public string? op_id { get; set; }
        public Guid? ip_id { get; set; }
        public string? bill_no { get; set; }
        public string? barcode { get; set; }
        public DateTime? bill_date { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? gender { get; set; }
        public string? mobileno { get; set; }
        public string? ageyears { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public double? gross_amount { get; set; }
        public double? discount_amount { get; set; }
        public double? general_concession_per { get; set; }    // <-- ADD (= discountper)
        public double? general_concession_amount { get; set; } // <-- ADD (= discountamount)
        public double? referral_concession_per { get; set; }    // <-- ADD (= ourdispercentage)
        public double? referral_concession_amount { get; set; }
        public double? net_amount { get; set; }
        public double? paid_amount { get; set; }
        public double? balance_amount { get; set; }
        public bool is_settled { get; set; }
        public string? message { get; set; }
        public double? pmc1_amount { get; set; }
        public double? pmc2_amount { get; set; }
        public double? pmc3_amount { get; set; }
        public double? advance_reverted_amount { get; set; }
        public string? revert_receipt_guid { get; set; }
        public string? revert_receipt_no { get; set; }
        public double? advance_balance_after { get; set; }
        public List<HmsBillLineResponse> items { get; set; } = new();
    }
    public class AutoBillOpRequest
    {
        public string op_id { get; set; } = string.Empty;   // op_registration.op_id (GUID text)
        public int? enteredbhcode { get; set; }              // branch of the cash counter
        public int? cntcode { get; set; }                    // cash counter code (must have an open shift)
    }
    public class HmsPayModeSourceRow
    {
        public string source { get; set; } = "";        // BILL | DUE | ADVANCE
        public string paymode { get; set; } = "";       // CASH | CARD | UPI | CHEQUE | BANK | MIXED
        public int receipt_count { get; set; }
        public int bill_count { get; set; }
        public double total_collected { get; set; }
    }

    public class HmsPayModeTotalRow
    {
        public string paymode { get; set; } = "";
        public double bill_amount { get; set; }
        public double due_amount { get; set; }
        public double advance_amount { get; set; }
        public double total { get; set; }
    }

    public class HmsPayModeCollectionResponse
    {
        public DateTime fromdate { get; set; }
        public DateTime todate { get; set; }
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }

        public double bill_total { get; set; }          // collected at billing
        public double due_total { get; set; }           // collected through due collection
        public double advance_total { get; set; }       // advance deposits
        public double grand_total { get; set; }         // bill + due + advance
        public double advance_refunded { get; set; }    // refunds paid out
        public double net_total { get; set; }           // grand_total - advance_refunded

        public List<HmsPayModeTotalRow> by_paymode { get; set; } = new();     // one row per paymode
        public List<HmsPayModeSourceRow> details { get; set; } = new();       // paymode x source
    }
    public class HmsOpPaymodeAmount
    {
        public string paymode { get; set; } = "";
        public decimal? pmcode { get; set; }
        public double amount { get; set; }
    }

    public class HmsOpCategoryAmountRow
    {
        public string category { get; set; } = "";
        public int visit_count { get; set; }
        public int bill_count { get; set; }
        public int item_count { get; set; }
        public double gross_amount { get; set; }
        public double discount_amount { get; set; }
        public double discount { get; set; }
        public double gross { get; set; }
        public double net { get; set; }
        public double collected { get; set; }
        public double due { get; set; }
        public double net_amount { get; set; }
        public double collected_amount { get; set; }
        public double due_amount { get; set; }
        public double advance_adjusted { get; set; }
        public List<HmsOpPaymodeAmount> paymodes { get; set; } = new();
    }

    public class HmsOpServiceAmountRow
    {
        public int? service_id { get; set; }
        public string service_name { get; set; } = "";
        public int visit_count { get; set; }
        public int item_count { get; set; }
        public string tokens { get; set; } = "";
        public double gross_amount { get; set; }
        public double discount_amount { get; set; }
        public double discount { get; set; }
        public double gross { get; set; }
        public double net { get; set; }
        public double collected { get; set; }
        public double due { get; set; }
        public double net_amount { get; set; }
        public double collected_amount { get; set; }
        public double due_amount { get; set; }
        public double advance_adjusted { get; set; }
        public List<HmsOpPaymodeAmount> paymodes { get; set; } = new();
    }

    public class HmsOpVisitCategoryRow
    {
        public string op_id { get; set; } = "";
        public string? op_no { get; set; }
        public string? token_no { get; set; }
        public DateTime? visit_date { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string bill_nos { get; set; } = "";
        public Dictionary<string, double> net_by_category { get; set; } = new();
        public Dictionary<string, double> collected_by_category { get; set; } = new();
        public Dictionary<string, double> collected_by_paymode { get; set; } = new();
        public double gross_amount { get; set; }
        public double discount_amount { get; set; }
        public double discount { get; set; }
        public double gross { get; set; }
        public double net { get; set; }
        public double collected { get; set; }
        public double due { get; set; }
        public double net_amount { get; set; }
        public double collected_amount { get; set; }
        public double due_amount { get; set; }
        public double advance_adjusted { get; set; }
    }

    public class HmsOldDueClearedBillRow
    {
        public string receipt_guid { get; set; } = "";
        public string? receipt_no { get; set; }
        public DateTime? receipt_date { get; set; }
        public string requestguid { get; set; } = "";
        public string? bill_no { get; set; }
        public DateTime? bill_date { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public double bill_total { get; set; }
        public double amount_cleared { get; set; }
        public string paymode { get; set; } = "";
        public decimal? pmcode { get; set; }
        public string? reference_no { get; set; }
        public string? doctor_name { get; set; }
        public string? counter_name { get; set; }
        public string? user_name { get; set; }
    }

    public class HmsOldDueClearedDateSummary
    {
        public DateTime bill_date { get; set; }
        public string bill_date_str { get; set; } = "";
        public int bill_count { get; set; }
        public double total_cleared { get; set; }
        public List<HmsOpPaymodeAmount> paymodes { get; set; } = new();
    }

    public class HmsCancelledBillSummaryRow
    {
        public string requestguid { get; set; } = "";
        public string? bill_no { get; set; }
        public DateTime? bill_date { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public double total_amount { get; set; }
        public double gross_amount { get; set; }
        public double discount_amount { get; set; }
        public string? doctor_name { get; set; }
        public string? cancellation_reason { get; set; }
        public string? cancelled_by { get; set; }
    }

    public class HmsOpCategoryCollectionResponse
    {
        public DateTime fromdate { get; set; }
        public DateTime todate { get; set; }
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public int? dcode { get; set; }
        public int total_visits { get; set; }
        public double total_gross { get; set; }
        public double total_discount { get; set; }
        public double total_net { get; set; }
        public double total_collected { get; set; }
        public double total_due { get; set; }
        public double total_ip_cost { get; set; }
        public double total_pharmacy_cost { get; set; }

        // ── IP Summary ────────────────────────────────────────────────
        public double total_ip_amount { get; set; }
        public double ip_advance_used { get; set; }
        public List<HmsOpPaymodeAmount> ip_paymodes { get; set; } = new();

        // ── Advance summary ───────────────────────────────────────────
        public double advance_collected { get; set; }
        public double total_advance_collected { get; set; }
        public double advance_used { get; set; }
        public double total_advance_used { get; set; }
        public double advance_refunded { get; set; }
        public double total_advance_refunded { get; set; }
        public double total_advance_adjusted { get; set; }
        public List<HmsOpPaymodeAmount> advance_collected_by_paymode { get; set; } = new();

        // ── Old due cleared today ─────────────────────────────────────
        public double total_due_cleared { get; set; }
        public double due_cleared_today { get; set; }
        public List<HmsOpPaymodeAmount> due_cleared_by_paymode { get; set; } = new();
        public List<HmsOldDueClearedDateSummary> due_cleared_by_bill_date { get; set; } = new();
        public List<HmsOldDueClearedBillRow> due_cleared_bills { get; set; } = new();

        // ── Cancelled bills ───────────────────────────────────────────
        public int cancelled_bill_count { get; set; }
        public double cancelled_bill_total_amount { get; set; }
        public List<HmsCancelledBillSummaryRow> cancelled_bills { get; set; } = new();

        // ── Net Counter Cash Closing ──────────────────────────────────
        public double net_counter_collection { get; set; }

        public List<HmsOpPaymodeAmount> paymode_totals { get; set; } = new();
        public List<HmsOpCategoryAmountRow> categories { get; set; } = new();
        public List<HmsOpServiceAmountRow> services { get; set; } = new();
        public List<HmsOpVisitCategoryRow> visits { get; set; } = new();
    }

    // internal rows used by the query
    public class HmsOpCategoryLine
    {
        public string op_id { get; set; } = "";
        public string? op_no { get; set; }
        public string? token_no { get; set; }
        public DateTime? visit_date { get; set; }
        public string? patient_name { get; set; }
        public decimal? custid { get; set; }
        public string requestguid { get; set; } = "";
        public string? bill_no { get; set; }
        public string? item_name { get; set; }
        public int? service_id { get; set; }
        public string? service_name { get; set; }
        public string category { get; set; } = "";
        public double gross { get; set; }
        public double discount { get; set; }
        public double net { get; set; }
        public double paid { get; set; }
        public double advance_adjusted { get; set; }
        public double ratio { get; set; }
        public double bill_paid { get; set; }
        public double bill_advance { get; set; }
    }

    public class HmsOpBillPayRow
    {
        public string requestguid { get; set; } = "";
        public string paymode { get; set; } = "";
        public decimal? pmcode { get; set; }
        public double amount { get; set; }
    }
}