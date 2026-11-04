using System;
using System.Collections.Generic;
using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  HMS DUE COLLECTION — MODELS & DTOs
    //
    //  receipt_advances conventions:
    //    Deposit row : requestguid = NULL          → unallocated advance credit
    //    Usage row   : requestguid = bill guid      → advance used against a bill
    //    Refund row  : requestguid = refund receipt guid → advance refunded to patient
    //
    //  Available advance balance = SUM(deposit rows) − SUM(usage + refund rows)
    //  Cancel: soft-delete usage rows → balance auto-restored (no manual math needed)
    //
    //  receipt_master.receipttype values:
    //    'DUE'            → due payment (cash / card / UPI / cheque / bank)
    //    'ADVANCE'        → advance deposit from patient
    //    'ADVANCE_REFUND' → excess advance refunded back at discharge
    // ═══════════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────────
    //  TABLE MODELS
    // ─────────────────────────────────────────────────────────────────────────

    [Table("receipt_master")]
    public class HmsDueReceiptMaster
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
        public long? payledgercode { get; set; }
        public string? bankname { get; set; }
        public string? paymentreference { get; set; }
        public DateTime? chequedate { get; set; }
        public string? cardno { get; set; }
        public DateTime? carddate { get; set; }
        public double? amountpaid { get; set; }
        public double? amountadjusted { get; set; }
        public double? amounttotal { get; set; }
        public bool? deleted { get; set; }
        public bool? isdeleted { get; set; }
        public int? enteredbhcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public string? receipttype { get; set; }
        public int? custid { get; set; }
        public string? opvisitid { get; set; }
        public string? tenant_code { get; set; }
        public bool? isbill { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefund { get; set; }
        public bool? isrefferal { get; set; }
        public bool? ismonthly { get; set; }
    }

    [Table("receipt_details")]
    public class HmsDueReceiptDetail
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
        public int? custid { get; set; }
        public string? opvisitid { get; set; }
        public int? enteredbhcode { get; set; }
        public bool? isbill { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefund { get; set; }
    }

    /// <summary>
    /// Maps to: public.receipt_advances
    ///
    /// DEPOSIT : requestguid IS NULL        → credit (advance in)
    /// USAGE   : requestguid = bill guid    → debit  (advance applied to bill)
    /// REFUND  : requestguid = refund guid  → debit  (advance returned to patient)
    ///
    /// Available balance = SUM(deposits) − SUM(usage + refund rows)
    /// Cancelling a collection soft-deletes usage rows → balance auto-restores.
    /// </summary>
    [Table("receipt_advances")]
    public class HmsDueReceiptAdvance
    {
        [ExplicitKey] public string? receiptadvanceid { get; set; }
        public string? receiptguid { get; set; }
        public string? requestguid { get; set; }
        public double? receiptamount { get; set; }
        public bool? deleted { get; set; }
        public string? tenant_code { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
    }

    [Table("balancecollectionby")]
    public class HmsDueBalanceCollectionBy
    {
        [ExplicitKey] public string? balancecollectionbyid { get; set; }
        public int? bhcode { get; set; }
        // Legacy columns
        public DateTime? collecteddate { get; set; }
        public string? collectiontype { get; set; }
        public string? receiptguid { get; set; }
        public string? requestguid { get; set; }
        // New-style columns
        public DateTime? collected_date { get; set; }
        public string? collection_type { get; set; }
        public string? receipt_guid { get; set; }
        public string? request_guid { get; set; }
        // Common
        public double? collectedamount { get; set; }
        public bool? deleted { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public DateTime? entereddate { get; set; }
        public DateTime? ibsdate { get; set; }
        public decimal? tmcode { get; set; }
        public decimal? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public string? tenant_code { get; set; }
    }

    [Table("balancecollectionbytest")]
    public class HmsDueBalanceCollectionByTest
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

    // ─────────────────────────────────────────────────────────────────────────
    //  REQUEST DTOs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Single due collection request.
    ///   Scenario 1 — Cash only     : advance_to_use=0,    cash_collected>0
    ///   Scenario 2 — Advance only  : advance_to_use>0,    cash_collected=0
    ///   Scenario 3 — Both          : advance_to_use>0,    cash_collected>0
    /// </summary>
    public class HmsDueCollectionRequest
    {
        public string requestguid { get; set; } = string.Empty;
        public double advance_to_use { get; set; } = 0;
        public double cash_collected { get; set; } = 0;
        public string collection_type { get; set; } = "CASH";
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? tmcode { get; set; }
        public string? remarks { get; set; }

        // ── NEW: same provision flags as LIMS DueCollectionClass.Save ──────────
        // All optional — sensible defaults applied in the service if omitted.
        public bool? isbill { get; set; }
        public bool? isrefferal { get; set; }
        public bool? ismonthly { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefund { get; set; }
        public long? payledgercode { get; set; }
        public DateTime? card_date { get; set; }
    }

    /// <summary>
    /// Per-bill line item inside a bulk collection request.
    /// Mirrors HmsDueCollectionRequest field-for-field so each bill can be
    /// settled with its own payment mode, reference, and ledger codes.
    /// Any field left null falls back to the batch-level value on
    /// HmsBulkDueCollectionRequest.
    /// </summary>
    public class HmsBulkDueCollectionItem
    {
        public string requestguid { get; set; } = string.Empty;
        public double advance_to_use { get; set; } = 0;
        public double cash_collected { get; set; } = 0;
        public string? collection_type { get; set; }
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? tmcode { get; set; }
        public string? remarks { get; set; }

        // ── NEW: per-item provision flags (fallback: batch-level value) ────────
        public bool? isbill { get; set; }
        public bool? isrefferal { get; set; }
        public bool? ismonthly { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefund { get; set; }
        public long? payledgercode { get; set; }
        public DateTime? card_date { get; set; }
    }

    public class HmsBulkDueCollectionRequest
    {
        public List<HmsBulkDueCollectionItem> items { get; set; } = new();
        public string collection_type { get; set; } = "CASH";
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? tmcode { get; set; }
        public string? remarks { get; set; }

        // ── NEW: batch-level default provision flags ────────────────────────────
        public bool? isbill { get; set; }
        public bool? isrefferal { get; set; }
        public bool? ismonthly { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefund { get; set; }
        public long? payledgercode { get; set; }
        public DateTime? card_date { get; set; }
    }

    public class HmsAdvanceDepositRequest
    {
        public decimal custid { get; set; }
        public string? patient_name { get; set; }
        public string? opvisitid { get; set; }
        public double amount { get; set; }
        public string collection_type { get; set; } = "CASH";
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? tmcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? remarks { get; set; }
    }

    public class HmsAdvanceRefundRequest
    {
        public decimal custid { get; set; }
        public string? opvisitid { get; set; }
        public double refund_amount { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? remarks { get; set; }
    }

    public class HmsDueCollectionFilterRequest
    {
        public decimal? custid { get; set; }
        public string? requestguid { get; set; }
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? search { get; set; }
        public bool? pending_only { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RESPONSE DTOs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dry-run preview — shown before confirming collection. No DB writes.
    /// UI shows: current_due, advance_available, advance_to_use (clamped),
    ///           amount_to_collect (cash needed), due_after.
    /// </summary>
    public class HmsDuePreviewResponse
    {
        public string requestguid { get; set; } = string.Empty;
        public string? bill_no { get; set; }
        public DateTime? bill_date { get; set; }
        public string? bill_type { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? opvisitid { get; set; }
        public string? doctor_name { get; set; }
        public int? dcode { get; set; }
        public double total_bill_amount { get; set; }
        public double previous_paid { get; set; }
        public double current_due { get; set; }
        public double advance_available { get; set; }
        /// <summary>Server-clamped: min(requested, min(available, current_due)).</summary>
        public double advance_to_use { get; set; }
        public double amount_to_collect { get; set; }
        public double due_after { get; set; }
    }

    /// <summary>Returned after a successful single due collection.</summary>
    public class HmsDueCollectionResponse
    {
        public string? receipt_guid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public string? advance_receipt_guid { get; set; }
        public string requestguid { get; set; } = string.Empty;
        public string? bill_no { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public double total_bill_amount { get; set; }
        public double due_before { get; set; }
        public double advance_used { get; set; }
        public double cash_collected { get; set; }
        public double total_settled { get; set; }
        public double due_after { get; set; }
        public bool is_fully_settled { get; set; }
        public string collection_type { get; set; } = string.Empty;
        public string? reference_no { get; set; }
        public DateTime collected_date { get; set; }
        public string? collected_by { get; set; }
        public string? remarks { get; set; }
    }

    /// <summary>Result for a single bill inside a bulk collection response.</summary>
    public class HmsBulkDueItemResult
    {
        public string requestguid { get; set; } = string.Empty;
        public string? bill_no { get; set; }
        public string? patient_name { get; set; }
        public double advance_used { get; set; }
        public double cash_collected { get; set; }
        public double total_settled { get; set; }
        public double due_before { get; set; }
        public double due_after { get; set; }
        public bool is_fully_settled { get; set; }
        public string? advance_receipt_guid { get; set; }
        public string? receipt_guid { get; set; }
        public string? receipt_no { get; set; }

        // ── NEW: echo back the effective payment details used for this item ───
        public string? collection_type { get; set; }
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public string? remarks { get; set; }

        /// <summary>collected / skipped</summary>
        public string status { get; set; } = string.Empty;
        public string? message { get; set; }
    }

    /// <summary>
    /// Returned after a successful bulk due collection.
    /// One shared receipt covers all bills in the batch.
    /// </summary>
    public class HmsBulkDueCollectionResponse
    {
        /// <summary>Shared receipt guid for the entire batch.</summary>
        public string batch_receipt_guid { get; set; } = string.Empty;
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public string collection_type { get; set; } = string.Empty;
        public double total_advance_used { get; set; }
        public double total_cash_collected { get; set; }
        public double total_settled { get; set; }
        public int items_processed { get; set; }
        public int items_skipped { get; set; }
        public DateTime collected_date { get; set; }
        public List<HmsBulkDueItemResult> items { get; set; } = new();
    }

    public class HmsAdvanceReceiptResponse
    {
        public string? receipt_guid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public string? receipt_type { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public double amount { get; set; }
        public string? collection_type { get; set; }
        public DateTime receipt_date { get; set; }
    }

    public class HmsPatientAdvanceSummary
    {
        public decimal custid { get; set; }
        public string? patient_name { get; set; }
        public double total_advance_deposited { get; set; }
        public double total_advance_used { get; set; }
        public double total_advance_refunded { get; set; }
        public double available_balance { get; set; }
        public List<HmsAdvanceLedgerRow> ledger { get; set; } = new();
    }

    public class HmsAdvanceLedgerRow
    {
        public string? receiptadvanceid { get; set; }
        public string? receiptguid { get; set; }
        public string? requestguid { get; set; }
        public double receiptamount { get; set; }
        public DateTime? receiptdate { get; set; }
        public string? receiptsnoprint { get; set; }
        public string? transaction_type { get; set; }
        public string? bill_no { get; set; }
    }

    public class HmsDueCollectionSummary
    {
        public string? receipt_guid { get; set; }
        public string? receipt_no { get; set; }
        public int? custid { get; set; }
        public string? patient_id { get; set; }
        public string? bill_guid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? bill_no { get; set; }
        public int? dcode { get; set; }   // add this near doctor_name
        public string? doctor_name { get; set; }
        public double amount_paid { get; set; }
        public double advance_used { get; set; }
        public string? collection_type { get; set; }
        public string? receipt_type { get; set; }
        public DateTime receipt_date { get; set; }
        public double total_bill_amount { get; set; }
        public double current_due { get; set; }
        public bool is_fully_settled { get; set; }
        public string? branch_name { get; set; }
        public string? collected_by { get; set; }
    }

    // ── All Due Bills ──────────────────────────────────────────────────────

    public class HmsAllDueFilterRequest
    {
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public decimal? custid { get; set; }
        public int? dcode { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public double? min_due { get; set; }
        public string? search { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsAllDueBillRow
    {
        public string? requestguid { get; set; }
        public string? bill_no { get; set; }
        public DateTime? bill_date { get; set; }
        public string? bill_type { get; set; }
        public decimal? custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? opvisitid { get; set; }
        public int? dcode { get; set; }
        public string? doctor_name { get; set; }
        public int? enteredbhcode { get; set; }
        public string? branch_name { get; set; }
        public double total_bill_amount { get; set; }
        public double paid_amount { get; set; }
        public double due_amount { get; set; }
        public double advance_available { get; set; }
        public DateTime? last_paid_date { get; set; }
    }

    public class HmsAllDueSummary
    {
        public int total_bills { get; set; }
        public double total_billed { get; set; }
        public double total_paid { get; set; }
        public double total_due { get; set; }
    }

    // ── Paid History ───────────────────────────────────────────────────────

    public class HmsPaidHistoryFilterRequest
    {
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public decimal? custid { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? receipt_type { get; set; }
        public string? collection_type { get; set; }
        public string? search { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsPaidHistoryRow
    {
        public string? receiptguid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public string? receipt_type { get; set; }
        public DateTime? receiptdate { get; set; }
        public double? amount { get; set; }
        public int? custid { get; set; }
        public string? patient_id { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? bill_no { get; set; }
        public string? bill_guid { get; set; }
        public DateTime? bill_date { get; set; }
        public string? doctor_name { get; set; }
        public string? branch_name { get; set; }
        public string? collection_type { get; set; }
        public string? bank_name { get; set; }
        public string? reference_no { get; set; }
        public DateTime? chequedate { get; set; }
        public double? remaining_due { get; set; }
        public bool? is_bill_settled { get; set; }
    }

    public class HmsPaidHistorySummary
    {
        public int total_receipts { get; set; }
        public double total_due_collected { get; set; }
        public double total_advance_deposited { get; set; }
        public double total_advance_refunded { get; set; }
        public double net_collected { get; set; }
    }
    public class HmsPaidHistoryAdvancedFilterRequest
    {
        // ── Flag filters ──────────────────────────────────────────
        public bool? isbill { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefferal { get; set; }
        public bool? ismonthly { get; set; }

        // ── Location filters ──────────────────────────────────────
        public int? bhcode { get; set; }          // Branch
        public int? cntcode { get; set; }         // Counter

        // ── Number filters ────────────────────────────────────────
        public int? year { get; set; }            // Year (e.g. 2026)
        public string? bill_no { get; set; }      // Bill No (requestsnoprint)
        public string? sample_no { get; set; }    // Sample No
        public string? receipt_no { get; set; }   // Receipt No (receiptsnoprint)

        // ── Person filters ────────────────────────────────────────
        public string? patient_name { get; set; } // Patient name search
        public int? dcode { get; set; }           // Doctor
        public decimal? custid { get; set; }      // Patient ID

        // ── Date + Time range ─────────────────────────────────────
        public DateTime? receipt_date_from { get; set; }   // Receipt Date From
        public DateTime? receipt_date_to { get; set; }     // Receipt Date To
        public TimeSpan? time_from { get; set; }            // Time From
        public TimeSpan? time_to { get; set; }              // Time To

        // ── Receipt type filter ───────────────────────────────────
        public string? receipt_type { get; set; }  // DUE / ADVANCE / ADVANCE_REFUND / ALL
        public string? collection_type { get; set; } // CASH / CARD / UPI / CHEQUE

        // ── Pagination ────────────────────────────────────────────
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }
    public class HmsDailyCollectionReportFilterRequest
    {
        public DateTime? fromdate { get; set; }          // FROM date
        public DateTime? todate { get; set; }            // TO date
        public int? bhcode { get; set; }                 // BRANCH
        public string? customer_search { get; set; }     // CUSTOMER — search by ID, Name, Mobile
        public decimal? custid { get; set; }             // resolved after customer search
        public string? doctor_search { get; set; }       // DOCTOR — search by ID, Name
        public int? dcode { get; set; }                  // resolved after doctor search
        public string? ismonthly { get; set; }           // MONTHLY — "true" / "false" / null = all
        public string? report_type { get; set; }         // SELECT REPORT — "DUE"/"ADVANCE"/"ALL" etc.
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsDailyCollectionReportRow
    {
        public string? receiptguid { get; set; }
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public string? receipt_type { get; set; }
        public DateTime? receipt_date { get; set; }
        public int? custid { get; set; }
        public string? patient_id { get; set; }
        public string? patient_name { get; set; }
        public string? mobileno { get; set; }
        public string? bill_no { get; set; }
        public string? bill_guid { get; set; }
        public string? doctor_name { get; set; }
        public string? branch_name { get; set; }
        public double? amount_paid { get; set; }
        public string? collection_type { get; set; }
        public string? bank_name { get; set; }
        public string? reference_no { get; set; }
        public bool? ismonthly { get; set; }
        public bool? isbill { get; set; }
        public bool? ispatient { get; set; }
        public bool? isrefferal { get; set; }
        public double? remaining_due { get; set; }
        public bool? is_bill_settled { get; set; }
    }

    public class HmsDailyCollectionReportSummary
    {
        public int total_receipts { get; set; }
        public double total_cash { get; set; }
        public double total_card { get; set; }
        public double total_upi { get; set; }
        public double total_cheque { get; set; }
        public double total_advance { get; set; }
        public double total_advance_refund { get; set; }
        public double grand_total { get; set; }
    }
    public class HmsAdvanceUseRequest
    {
        public decimal custid { get; set; }
        public double amount_to_use { get; set; }
        /// <summary>Optional — settle only this bill. If omitted, applies FIFO across all due bills for the patient (oldest first).</summary>
        public string? requestguid { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? remarks { get; set; }
    }

    public class HmsAdvanceUseItemResult
    {
        public string requestguid { get; set; } = string.Empty;
        public string? bill_no { get; set; }
        public double due_before { get; set; }
        public double advance_used { get; set; }
        public double due_after { get; set; }
        public bool is_fully_settled { get; set; }
    }

    public class HmsAdvanceUseResponse
    {
        public decimal custid { get; set; }
        public string? patient_name { get; set; }
        public double advance_balance_before { get; set; }
        public double total_advance_used { get; set; }
        public double advance_balance_after { get; set; }
        public int bills_settled { get; set; }
        public List<HmsAdvanceUseItemResult> items { get; set; } = new();
    }
    public class HmsAdvanceCancelRequest
    {
        public string receiptguid { get; set; } = string.Empty;
        public int? usercode { get; set; }
        public string? reason { get; set; }
    }

    public class HmsAdvanceCancelResponse
    {
        public string receipt_guid { get; set; } = string.Empty;
        public string? receipt_no { get; set; }
        public decimal? custid { get; set; }
        public double cancelled_amount { get; set; }
        public double advance_balance_before { get; set; }
        public double advance_balance_after { get; set; }
        public DateTime cancelled_date { get; set; }
    }
    // ═════════════════════════════════════════════════════════════════════════
    //  ADVANCE REQUEST & PATIENT DEPOSIT / REFUND QUEUE MODELS
    // ═════════════════════════════════════════════════════════════════════════

    [Table("hms_advance_request")]
    public class HmsAdvanceRequestRow
    {
        [ExplicitKey] public Guid request_id { get; set; } = Guid.NewGuid();
        public string request_no { get; set; } = string.Empty;
        public DateTime request_date { get; set; } = DateTime.UtcNow;
        public decimal custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobile_no { get; set; }
        public string? opvisitid { get; set; }
        public Guid? ip_id { get; set; }
        public double amount { get; set; }
        public string? purpose { get; set; }
        public string? remarks { get; set; }
        public string status { get; set; } = "PENDING"; // PENDING, COLLECTED, CANCELLED
        public int? requested_by { get; set; }
        public string? requested_by_name { get; set; }
        public int? target_cntcode { get; set; }
        public int? target_bhcode { get; set; }
        public int? collected_by { get; set; }
        public string? collected_by_name { get; set; }
        public DateTime? collected_date { get; set; }
        public string? receiptguid { get; set; }
        public string? receiptsnoprint { get; set; }
        public string? cancel_reason { get; set; }
        public int? cancelled_by { get; set; }
        public DateTime? cancelled_date { get; set; }
        public string tenant_code { get; set; } = string.Empty;
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public DateTime updated_at { get; set; } = DateTime.UtcNow;
    }

    public class HmsCreateAdvanceRequestDto
    {
        public decimal custid { get; set; }
        public string? patient_name { get; set; }
        public string? mobile_no { get; set; }
        public string? opvisitid { get; set; }
        public Guid? ip_id { get; set; }
        public double amount { get; set; }
        public string? purpose { get; set; } // e.g. "IP Admission Deposit", "Surgery Advance"
        public string? remarks { get; set; }
        public int? target_cntcode { get; set; }
        public int? target_bhcode { get; set; }
        public int? usercode { get; set; }
        public string? user_name { get; set; }
    }

    public class HmsAdvanceRequestFilterDto
    {
        public string? status { get; set; } // "PENDING", "COLLECTED", "CANCELLED", or null for all
        public decimal? custid { get; set; }
        public int? cntcode { get; set; }
        public int? bhcode { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? search { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsAdvanceRequestListSummary
    {
        public int total_requests { get; set; }
        public int pending_count { get; set; }
        public double pending_amount { get; set; }
        public int collected_count { get; set; }
        public double collected_amount { get; set; }
        public int cancelled_count { get; set; }
        public double cancelled_amount { get; set; }
    }

    public class HmsCollectAdvanceRequestDto
    {
        public Guid request_id { get; set; }
        public double? amount { get; set; } // allows updating/confirming the actual received amount
        public string collection_type { get; set; } = "CASH"; // CASH, CARD, UPI, CHEQUE, etc.
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public string? cnttid { get; set; }
        public decimal? pmcode { get; set; }
        public decimal? ctcode { get; set; }
        public decimal? tmcode { get; set; }
        public int? usercode { get; set; }
        public string? user_name { get; set; }
        public int? computercode { get; set; }
        public string? remarks { get; set; }
    }

    public class HmsCancelAdvanceRequestDto
    {
        public Guid request_id { get; set; }
        public string? cancel_reason { get; set; }
        public int? usercode { get; set; }
    }

    public class HmsPatientAdvanceDepositFilterRequest
    {
        public string? search { get; set; } // patient name, mobile, custcode, custid
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? balance_status { get; set; } = "ALL"; // "ALL", "HAS_BALANCE", "ZERO_BALANCE"
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsPatientAdvanceDepositRow
    {
        public decimal custid { get; set; }
        public string custcode { get; set; } = string.Empty;
        public string patient_name { get; set; } = string.Empty;
        public string mobile_no { get; set; } = string.Empty;
        public string gender { get; set; } = string.Empty;
        public int age { get; set; }
        public double total_deposited { get; set; }
        public double total_used { get; set; }
        public double total_refunded { get; set; }
        public double available_balance { get; set; }
        public DateTime? first_deposit_date { get; set; }
        public DateTime? last_deposit_date { get; set; }
        public string? last_receipt_no { get; set; }
        public int deposit_count { get; set; }
        public string status { get; set; } = "ACTIVE"; // "HAS_BALANCE" | "EXHAUSTED"
    }

    public class HmsPatientAdvanceDepositSummary
    {
        public int total_patients { get; set; }
        public double total_deposited { get; set; }
        public double total_used { get; set; }
        public double total_refunded { get; set; }
        public double total_available_balance { get; set; }
    }

    public class HmsAdvanceRefundFilterRequest
    {
        public decimal? custid { get; set; }
        public string? search { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public int? bhcode { get; set; }
        public int? cntcode { get; set; }
        public int page { get; set; } = 1;
        public int pagesize { get; set; } = 20;
    }

    public class HmsAdvanceRefundHistoryRow
    {
        public string receiptguid { get; set; } = string.Empty;
        public string? receipt_no { get; set; }
        public string? receiptbarcode { get; set; }
        public DateTime receiptdate { get; set; }
        public decimal custid { get; set; }
        public string custcode { get; set; } = string.Empty;
        public string patient_name { get; set; } = string.Empty;
        public string mobile_no { get; set; } = string.Empty;
        public double refund_amount { get; set; }
        public int? usercode { get; set; }
        public string? entered_by { get; set; }
        public int? cntcode { get; set; }
        public string? counter_name { get; set; }
        public int? enteredbhcode { get; set; }
        public string? branch_name { get; set; }
        public string? remarks { get; set; }
    }

    public class HmsAdvanceRefundSummary
    {
        public int total_refund_count { get; set; }
        public double total_refund_amount { get; set; }
    }
}
