using System;
using System.Collections.Generic;

namespace medico_backend.Model
{
    public class GstSummaryRow
    {
        public decimal GstPercentage { get; set; }
        public decimal TaxableValue { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal TotalTax { get; set; }
        public decimal NettTotal { get; set; }
    }

    public class GstDetailRow
    {
        public int SNo { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string GstNo { get; set; } = string.Empty;
        public decimal GstPercentage { get; set; }
        public decimal TaxableValue { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal TotalTax { get; set; }
        public decimal NettTotal { get; set; }
    }

    public class GstReportRequestPayload
    {
        public byte[]? LogoImage { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public DateTime? fromdate { get; set; }
        public DateTime? todate { get; set; }
        public string? billtype { get; set; } = "all";
        public string? reporttype { get; set; } = "GST Return Summary Report";
        public List<GstSummaryRow> summary { get; set; } = new();
        public List<GstSummaryRow> inward_summary { get; set; } = new();
        public List<GstSummaryRow> outward_summary { get; set; } = new();
        public List<GstDetailRow> details { get; set; } = new();
    }
}
