using Dapper;
using Npgsql;

public class ShiftAutoCloseService : BackgroundService
{
    private readonly string _conn;
    public ShiftAutoCloseService(IConfiguration cfg) => _conn = cfg.GetConnectionString("conn")!;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var db = new NpgsqlConnection(_conn);
                await db.ExecuteAsync(
                    @"UPDATE counter_timing SET todate = planned_to, close_type = 'AUTO'
                      WHERE todate IS NULL AND planned_to IS NOT NULL AND planned_to <= @now",
                    new { now = DateTime.UtcNow });
            }
            catch (Exception ex) { Console.WriteLine("ShiftAutoClose: " + ex.Message); }
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}