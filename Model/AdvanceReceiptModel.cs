using System;
using System.Collections.Generic;

namespace medico_backend.Model
{
    public class AdvanceReceiptPdfModel
    {
        // Hospital / Company Details
        public string? CompanyName { get; set; }
        public string? BranchName { get; set; }
        public string? HospitalName { get; set; }
        public string? HospitalAddress { get; set; }
        public string? HospitalPhone { get; set; }
        public string? HospitalEmail { get; set; }
        public string? HospitalWebsite { get; set; }
        public string? HospitalRegNo { get; set; }

        // Patient Details
        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? Age { get; set; }
        public string? Gender { get; set; }
        public string? MobileNo { get; set; }
        public string? PatientAddress { get; set; }
        public string? ReferralDoctor { get; set; }
        public string? ReferralName { get; set; }

        // Admission Details
        public string? AdmissionNo { get; set; }
        public DateTime? AdmissionDate { get; set; }
        public DateTime? DischargeDate { get; set; }
        public string? BedNo { get; set; }
        public bool IsInpatient { get; set; }

        // Receipt Details
        public string? ReceiptGuid { get; set; }
        public string? ReceiptNo { get; set; }
        public DateTime ReceiptDate { get; set; }
        public string? ReceiptType { get; set; } // "ADVANCE" or "ADVANCE_REFUND"
        public decimal Amount { get; set; }
        public string? AmountInWords { get; set; }
        public string? Description { get; set; }

        // Payment Details
        public string? PaymentMode { get; set; }
        public string? PaymentReference { get; set; }
        public string? BankName { get; set; }
        public string? CounterName { get; set; }
        public string? EnteredBy { get; set; }
        public string? Remarks { get; set; }

        // Reconciliation with receipt_advances table
        public decimal ThisDepositAmount { get; set; }
        public decimal ThisAdjustedAmount { get; set; }
        public decimal ThisRefundedAmount { get; set; }
        public decimal ThisBalanceAmount { get; set; }

        public decimal TotalAdvanceDeposited { get; set; }
        public decimal TotalAdvanceAdjusted { get; set; }
        public decimal TotalAdvanceRefunded { get; set; }
        public decimal CurrentAvailableBalance { get; set; }

        // Refund Specific Details
        public string? OriginalReceiptNo { get; set; }
        public DateTime? OriginalReceiptDate { get; set; }
        public decimal OriginalDepositAmount { get; set; }
        public decimal RefundedFromThisAdvance { get; set; }

        public List<AdvanceReceiptLedgerItem> PatientLedgerHistory { get; set; } = new();
    }

    public class AdvanceReceiptLedgerItem
    {
        public string? ReceiptNo { get; set; }
        public DateTime? Date { get; set; }
        public string? Type { get; set; }
        public string? Particulars { get; set; }
        public decimal Amount { get; set; }
        public string? Mode { get; set; }
    }

    public class AdvanceReceiptRequest
    {
        public AdvanceReceiptPdfModel? ReceiptData { get; set; }
        public List<AdvanceReceiptPdfModel> Receipts { get; set; } = new();
        public string? TenantId { get; set; }
        public bool IsLetterhead { get; set; } = false;
        public byte[]? HeaderImage { get; set; }
        public byte[]? FooterImage { get; set; }
        public byte[]? LogoImage { get; set; }
        public string? PageFormat { get; set; } = "A4";
    }

    public static class AmountToWordsConverter
    {
        public static string Convert(decimal amount)
        {
            if (amount == 0) return "Zero Rupees Only";

            var ones = new[] { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
                "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tens = new[] { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            int num = (int)Math.Floor(amount);
            string words = "";

            if (num / 10000000 > 0) { words += ConvertGroup(num / 10000000, ones, tens) + " Crore "; num %= 10000000; }
            if (num / 100000 > 0) { words += ConvertGroup(num / 100000, ones, tens) + " Lakh "; num %= 100000; }
            if (num / 1000 > 0) { words += ConvertGroup(num / 1000, ones, tens) + " Thousand "; num %= 1000; }
            if (num / 100 > 0) { words += ones[num / 100] + " Hundred "; num %= 100; }
            if (num > 0)
            {
                if (words != "") words += "and ";
                if (num < 20) words += ones[num];
                else words += tens[num / 10] + (num % 10 > 0 ? " " + ones[num % 10] : "");
            }

            return words.Trim() + " Rupees Only";
        }

        private static string ConvertGroup(int num, string[] ones, string[] tens)
        {
            string result = "";
            if (num / 100 > 0) { result += ones[num / 100] + " Hundred "; num %= 100; }
            if (num > 0)
            {
                if (result != "") result += "and ";
                if (num < 20) result += ones[num];
                else result += tens[num / 10] + (num % 10 > 0 ? " " + ones[num % 10] : "");
            }
            return result.Trim();
        }
    }
}
