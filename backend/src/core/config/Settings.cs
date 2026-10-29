using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DotnetSvelte.Core.Config;

public sealed class Settings
{
    public string ApiV1Str { get; init; } = "/api/v1";
    public string SecretKey { get; init; } = "changethis";
    public int AccessTokenExpireMinutes { get; init; } = 60 * 24 * 8;
    public string FrontendHost { get; init; } = "http://dashboard.localhost";
    public string Environment { get; init; } = "local";
    public IReadOnlyList<string> BackendCorsOrigins { get; init; } = [];
    public string ProjectName { get; init; } = "dotnet-svelte";

    public string PostgresServer { get; init; } = "localhost";
    public int PostgresPort { get; init; } = 5432;
    public string PostgresUser { get; init; } = "postgres";
    public string PostgresPassword { get; init; } = "";
    public string PostgresDb { get; init; } = "";

    public string RedisHost { get; init; } = "localhost";
    public int RedisPort { get; init; } = 6379;
    public int RedisDb { get; init; }
    public string RedisPassword { get; init; } = "";

    public string FirstSuperuser { get; init; } = "admin@example.com";
    public string FirstSuperuserPassword { get; init; } = "changethis";

    public string AppHost { get; init; } = "0.0.0.0";
    public int AppPort { get; init; } = 8000;

    public bool IsLocal => Environment == "local";

    public IReadOnlyList<string> AllCorsOrigins =>
        [.. BackendCorsOrigins.Select(o => o.TrimEnd('/')), FrontendHost.TrimEnd('/')];

    public string ConnectionString => new NpgsqlConnectionStringBuilder
    {
        Host = PostgresServer,
        Port = PostgresPort,
        Username = PostgresUser,
        Password = PostgresPassword,
        Database = PostgresDb,
    }.ConnectionString;

    public static Settings From(IConfiguration c)
    {
        string Str(string key, string fallback) =>
            string.IsNullOrWhiteSpace(c[key]) ? fallback : c[key]!;
        int Int(string key, int fallback) =>
            int.TryParse(c[key], out var v) ? v : fallback;

        return new Settings
        {
            ApiV1Str = Str("API_V1_STR", "/api/v1"),
            SecretKey = Str("SECRET_KEY", "changethis"),
            AccessTokenExpireMinutes = Int("ACCESS_TOKEN_EXPIRE_MINUTES", 60 * 24 * 8),
            FrontendHost = Str("FRONTEND_HOST", "http://dashboard.localhost"),
            Environment = Str("ENVIRONMENT", "local"),
            BackendCorsOrigins = Str("BACKEND_CORS_ORIGINS", "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ProjectName = Str("PROJECT_NAME", "dotnet-svelte"),
            PostgresServer = Str("POSTGRES_SERVER", "localhost"),
            PostgresPort = Int("POSTGRES_PORT", 5432),
            PostgresUser = Str("POSTGRES_USER", "postgres"),
            PostgresPassword = Str("POSTGRES_PASSWORD", ""),
            PostgresDb = Str("POSTGRES_DB", ""),
            RedisHost = Str("REDIS_HOST", "localhost"),
            RedisPort = Int("REDIS_PORT", 6379),
            RedisDb = Int("REDIS_DB", 0),
            RedisPassword = Str("REDIS_PASSWORD", ""),
            FirstSuperuser = Str("FIRST_SUPERUSER", "admin@example.com"),
            FirstSuperuserPassword = Str("FIRST_SUPERUSER_PASSWORD", "changethis"),
            AppHost = Str("APP_HOST", "0.0.0.0"),
            AppPort = Int("APP_PORT", 8000),
        };
    }

    public void EnsureSecure()
    {
        if (IsLocal) return;
        if (SecretKey == "changethis")
            throw new InvalidOperationException("SECRET_KEY must be set outside ENVIRONMENT=local");
        if (FirstSuperuserPassword == "changethis")
            throw new InvalidOperationException("FIRST_SUPERUSER_PASSWORD must be set outside ENVIRONMENT=local");
    }
}
