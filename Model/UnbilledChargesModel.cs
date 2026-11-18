using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("unbilledcharges")]
    public class UnbilledChargeRow
    {
        [ExplicitKey] public string? unbilledid { get; set; }
        public string? entrytype { get; set; }      // "CONSULTATION" | "INVESTIGATION"
        public string? entryid { get; set; }         // op_id (consultation) or inv_det_id (investigation)
        public DateTime? chargedate { get; set; }
        public decimal? custid { get; set; }
        public string? opvisitid { get; set; }        // op_id
        public Guid? ip_id { get; set; }
        public int? bedcode { get; set; }
        public int? tcode { get; set; }
        public double? quantity { get; set; }
        public double? rate { get; set; }
        public double? amount { get; set; }
        public double? discount { get; set; }
        public double? charityamount { get; set; }
        public bool? billedstatus { get; set; }
        public string? billno { get; set; }
        public string? billid { get; set; }
        public DateTime? billeddate { get; set; }
        public double? billedquantity { get; set; }
        public double? billedamount { get; set; }
        public string? particulars { get; set; }
        public string? tenant_code { get; set; }
        [Write(false)]
        public bool? paid_status { get; set; }
    }

    // ── Request DTOs ─────────────────────────────────────────────
    public class AddUnbilledConsultationRequest
    {
        public string op_id { get; set; } = string.Empty;
        public decimal custid { get; set; }
        public int? tcode { get; set; }          
        public double rate { get; set; }
        public double amount { get; set; }
        public double quantity { get; set; } = 1;
        public string? particulars { get; set; }   // NEW
    }

    public class UnbilledChargeSummary
    {
        public string? unbilledid { get; set; }
        public string? entrytype { get; set; }
        public string? entryid { get; set; }
        public DateTime? chargedate { get; set; }
        public int? tcode { get; set; }
        public double? quantity { get; set; }
        public double? rate { get; set; }
        public double? amount { get; set; }
        public string? item_name { get; set; }
    }
    // ── Request DTO for updating an unbilled charge ──────────────
    public class UpdateUnbilledChargeRequest
    {
        public string unbilledid { get; set; } = string.Empty;
        public double? rate { get; set; }
        public double? quantity { get; set; }
        public double? amount { get; set; }        // if omitted, recalculated from rate * quantity when both are supplied
        public double? discount { get; set; }
        public double? charityamount { get; set; }
    }
    public class AddUnbilledIpRegistrationRequest
    {
        public Guid ip_id { get; set; }
        public decimal custid { get; set; }
        public int? tcode { get; set; }
        public double rate { get; set; }
        public double amount { get; set; }
        public double quantity { get; set; } = 1;
        public string? particulars { get; set; }
    }
    public class DiscardUnbilledChargesRequest
    {
        // Send one id or many. A single id is just a list of one.
        public List<string> unbilledids { get; set; } = new();
    }

    public class DiscardUnbilledResult
    {
        public int requested { get; set; }
        public int discarded { get; set; }
        public List<string> discarded_ids { get; set; } = new();
        public List<DiscardSkipped> skipped { get; set; } = new();
    }

    public class DiscardSkipped
    {
        public string unbilledid { get; set; } = string.Empty;
        public string reason { get; set; } = string.Empty;
    }
}