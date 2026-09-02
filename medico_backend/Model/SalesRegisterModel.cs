using System;
using System.Collections.Generic;
using System.Linq;

namespace medico_backend.Model
{
    public class SalesRegisterHeader
    {
        public string? CompanyName { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyContactNo { get; set; }
        public string? CompanyEmail { get; set; }
        public byte[]? LogoImage { get; set; }

        public string? PharmacyName { get; set; }
        public string? LicenseNo { get; set; }
        public string? PharmacyRCNo { get; set; }
        public string? GSTNo { get; set; }
        public string? MSMENo { get; set; }
        public string? Address { get; set; }
        public string? Branch { get; set; }
        public long? WarehouseCode { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string? RegisterStatus { get; set; } = "Verified";

        // Signatures & Signees
        public string? PreparedByName { get; set; }
        public string? PreparedByDescription { get; set; }
        public string? PreparedBySignature { get; set; }
        public string? InspectedByName { get; set; }
    }

    public class SalesRegisterDetailRow
    {
        public int SNo { get; set; }
        public DateTime Date { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public string Expiry { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal Mrp { get; set; }
        public decimal SaleRate { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public decimal NetAmount { get; set; }
    }

    public class SalesRegisterReportModel
    {
        public SalesRegisterHeader Header { get; set; } = new SalesRegisterHeader();
        public List<SalesRegisterDetailRow> Details { get; set; } = new List<SalesRegisterDetailRow>();
        public decimal TotalQty => Details?.Sum(d => d.Qty) ?? 0;
        public decimal TotalMrp => Details?.Sum(d => d.Mrp) ?? 0;
        public decimal TotalSaleRate => Details?.Sum(d => d.SaleRate) ?? 0;
        public decimal TotalDiscount => Details?.Sum(d => d.Discount) ?? 0;
        public decimal TotalTax => Details?.Sum(d => d.Tax) ?? 0;
        public decimal TotalNetAmount => Details?.Sum(d => d.NetAmount) ?? 0;
    }
}
