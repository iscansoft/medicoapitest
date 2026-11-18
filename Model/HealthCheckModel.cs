using System.Text.Json.Serialization;

namespace Medico_Backend.Model
{
    public class HealthCheckResponse
    {
        [JsonPropertyName("app")]
        public string App { get; set; } = "Medico API";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "healthy";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }
}