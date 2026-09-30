using Dapper;
using Npgsql;

public class ShiftAutoCloseService : BackgroundService
{
    private readonly string _conn;
    private readonly ILogger<ShiftAutoCloseService> _log;

    public ShiftAutoCloseService(IConfiguration cfg, ILogger<ShiftAutoCloseService> log)
    {
        _conn = cfg.GetConnectionString("conn")!;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var db = new NpgsqlConnection(_conn);
                int n = await db.ExecuteAsync(
                    @"UPDATE counter_timing
                      SET todate = planned_to, close_type = 'AUTO'
                      WHERE todate IS NULL AND planned_to IS NOT NULL AND planned_to <= @now",
                    new { now = DateTime.UtcNow });
                if (n > 0) _log.LogInformation("Auto-closed {n} fixed shift(s).", n);
            }
            catch (Exception ex) { _log.LogError(ex, "Shift auto-close sweep failed."); }

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
}