using DotnetSvelte.Core.Config;
using StackExchange.Redis;

namespace DotnetSvelte.Core.Cache;

public sealed class RedisConnection(Settings settings, int commandTimeoutMs = 2000) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ConnectionMultiplexer? _mux;

    public async Task<ConnectionMultiplexer> GetAsync()
    {
        if (_mux is not null) return _mux;

        await _gate.WaitAsync();
        try
        {
            return _mux ??= await ConnectionMultiplexer.ConnectAsync(Options());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IDatabase> GetDatabaseAsync() => (await GetAsync()).GetDatabase();

    private ConfigurationOptions Options()
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 500,
            SyncTimeout = commandTimeoutMs,
            AsyncTimeout = commandTimeoutMs,
            DefaultDatabase = settings.RedisDb,
            BacklogPolicy = BacklogPolicy.FailFast,
        };
        options.EndPoints.Add(settings.RedisHost, settings.RedisPort);
        if (!string.IsNullOrEmpty(settings.RedisPassword)) options.Password = settings.RedisPassword;
        return options;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mux is not null) await _mux.DisposeAsync();
    }
}
