using Dapper.Contrib.Extensions;
using System.Text.Json.Serialization;

namespace medico_backend.Model
{
    [Table("op_status_master")]
    public class OpStatusMasterModel
    {
        [Key]
        public int op_status_id { get; set; }
        public string status_name { get; set; } = string.Empty;   // display name
        public string? tenant_code { get; set; }
        public bool is_active { get; set; } = true;
        public bool deleted { get; set; } = false;
        public int? usercode { get; set; }

        public bool is_fixed { get; set; } = false;
        public string? status_key { get; set; }                   // status code (never changes)
        public int sort_order { get; set; } = 0;

        public DateTime created_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        public DateTime updated_at { get; set; } =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }

    // POST save
    public class OpStatusSaveRequest
    {
        public string status_code { get; set; } = string.Empty;
        public string display_status_name { get; set; } = string.Empty;
        public int? usercode { get; set; }
    }

    // POST bulk-save
    public class OpStatusBulkSaveRequest
    {
        public List<OpStatusSaveRequest> statuses { get; set; } = new();
        public int? usercode { get; set; }
    }

    // One status for a tenant
    public class OpStatusResponse
    {
        public string status_code { get; set; } = string.Empty;
        public string default_status_name { get; set; } = string.Empty;
        public string display_status_name { get; set; } = string.Empty;
        public bool is_customized { get; set; }
    }

    // Response envelope
    public class OpStatusApiResponse<T>
    {
        public string status { get; set; } = "Success";
        public string message { get; set; } = string.Empty;
        public T? data { get; set; }

        [JsonIgnore]
        public int http_code { get; set; } = 200;

        public static OpStatusApiResponse<T> Success(T data, string message) =>
            new() { status = "Success", message = message, data = data, http_code = 200 };

        public static OpStatusApiResponse<T> Error(int http_code, string message) =>
            new() { status = "Error", message = message, data = default, http_code = http_code };
    }
}