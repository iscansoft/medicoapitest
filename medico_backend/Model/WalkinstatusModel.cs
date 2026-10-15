using Dapper.Contrib.Extensions;

namespace medico_backend.Model
{
    [Table("op_status_master")]
    public class OpStatusMasterModel
    {
        [Key]
        public int op_status_id { get; set; }
        public string status_name { get; set; } = string.Empty;
        public string? tenant_code { get; set; }
        public bool is_active { get; set; } = true;
        public bool deleted { get; set; } = false;
        public int? usercode { get; set; }
        public DateTime created_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }
}