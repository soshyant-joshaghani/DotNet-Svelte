using DotnetSvelte.Core.Config;
using Npgsql;

namespace DotnetSvelte.Core.Db;

public sealed class DbMigrationService(Settings settings, IConfiguration config, ILogger<DbMigrationService> log)
    : IHostedService
{
    private const long AdvisoryLockKey = 727_274_001;

    public async Task StartAsync(CancellationToken ct)
    {
        var dir = FindMigrationsDir()
            ?? throw new DirectoryNotFoundException("migrations directory not found; set MIGRATIONS_DIR");

        await using var conn = await OpenAsync(ct);
        await Run(conn, $"SELECT pg_advisory_lock({AdvisoryLockKey})", ct);
        try
        {
            await Run(conn,
                "CREATE TABLE IF NOT EXISTS schema_migrations (name text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())",
                ct);

            var applied = new HashSet<string>();
            await using (var cmd = new NpgsqlCommand("SELECT name FROM schema_migrations", conn))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct)) applied.Add(reader.GetString(0));

            var files = Directory.GetFiles(dir, "*.sql").OrderBy(Path.GetFileName, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (applied.Contains(name)) continue;

                log.LogInformation("applying migration {Name}", name);
                await using var tx = await conn.BeginTransactionAsync(ct);
                await using (var cmd = new NpgsqlCommand(await File.ReadAllTextAsync(file, ct), conn, tx))
                    await cmd.ExecuteNonQueryAsync(ct);
                await using (var cmd = new NpgsqlCommand(
                    "INSERT INTO schema_migrations (name) VALUES (@n) ON CONFLICT (name) DO NOTHING", conn, tx))
                {
                    cmd.Parameters.AddWithValue("n", name);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
            }
        }
        finally
        {
            await Run(conn, $"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var conn = new NpgsqlConnection(settings.ConnectionString);
            try
            {
                await conn.OpenAsync(ct);
                return conn;
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                await conn.DisposeAsync();
                log.LogWarning("database not ready ({Message}); retry {Attempt}/30", ex.Message, attempt);
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }

    private static async Task Run(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private string? FindMigrationsDir()
    {
        var configured = config["MIGRATIONS_DIR"];
        if (!string.IsNullOrWhiteSpace(configured)) return Directory.Exists(configured) ? configured : null;

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 4 && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var candidate in new[] { "migrations", Path.Combine("backend", "migrations") })
            {
                var path = Path.Combine(dir.FullName, candidate);
                if (Directory.Exists(path)) return path;
            }
        }
        return null;
    }
}
