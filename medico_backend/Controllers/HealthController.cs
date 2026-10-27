using Medico_Backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Medico_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IConfiguration configuration, ILogger<HealthController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // GET api/Health/Check  (also works at api/Health)
        // No JWT and no tenant_code header required.
        [HttpGet("Check")]
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Check(CancellationToken cancellationToken)
        {
            var dbConn = _configuration.GetConnectionString("conn");

            if (string.IsNullOrWhiteSpace(dbConn))
            {
                return Ok(new HealthCheckResponse
                {
                    Status = "unhealthy",
                    Message = "Database connection string 'conn' is not configured."
                });
            }

            try
            {
                // Give up after 5 seconds so the check never hangs
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                await using var connection = new NpgsqlConnection(dbConn);
                await connection.OpenAsync(cts.Token);

                await using var cmd = new NpgsqlCommand("SELECT 1;", connection);
                cmd.CommandTimeout = 5;
                await cmd.ExecuteScalarAsync(cts.Token);

                return Ok(new HealthCheckResponse
                {
                    Status = "healthy",
                    Message = ""
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check DB connection failed");

                return Ok(new HealthCheckResponse
                {
                    Status = "unhealthy",
                    Message = $"Database connection failed: {ex.Message}"
                });
            }
        }
    }
}