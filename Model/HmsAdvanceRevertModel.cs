using System;

namespace medico_backend.Model
{
    public class HmsAdvanceRevertRequest
    {
        public string requestguid { get; set; } = string.Empty;
        /// <summary>Null/0 = revert ALL advance used on this bill.</summary>
        public double? revert_amount { get; set; }
        public decimal pmcode { get; set; }              // from paymode_master
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? reason { get; set; }
        public string? remarks { get; set; }
    }

    public class HmsAdvanceRevertInput
    {
        public double? amount { get; set; }
        public decimal pmcode { get; set; }
        public string? reference_no { get; set; }
        public string? bank_name { get; set; }
        public string? card_no { get; set; }
        public DateTime? cheque_date { get; set; }
        public int? enteredbhcode { get; set; }
        public int? cntcode { get; set; }
        public int? usercode { get; set; }
        public int? computercode { get; set; }
        public string? reason { get; set; }
        public string? remarks { get; set; }
    }

    public class HmsAdvanceRevertResult
    {
        public string requestguid { get; set; } = string.Empty;
        public string? bill_no { get; set; }
        public decimal? custid { get; set; }
        public double reverted_amount { get; set; }
        public decimal pmcode { get; set; }
        public string paymode { get; set; } = string.Empty;
        public string receipt_guid { get; set; } = string.Empty;
        public string? receipt_no { get; set; }
        public string? receipt_barcode { get; set; }
        public double advance_balance_before { get; set; }
        public double advance_balance_after { get; set; }
        public double bill_paid_amount { get; set; }
        public DateTime reverted_date { get; set; }
    }
}

