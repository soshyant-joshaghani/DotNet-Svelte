using System.Text.Json;
using DotnetSvelte.Core.Config;
using StackExchange.Redis;

namespace DotnetSvelte.Core.Cache;

/// <summary>JSON read-cache that never throws: any Redis failure degrades to a miss / no-op.</summary>
public sealed class RedisCache(RedisConnection redis, ILogger<RedisCache> log) : ICache
{
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var raw = await (await redis.GetDatabaseAsync()).StringGetAsync(key);
            return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>((string)raw!, Json.Options);
        }
        catch (Exception ex)
        {
            log.LogDebug("cache get skipped: {Message}", ex.Message);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, Json.Options);
            await (await redis.GetDatabaseAsync()).StringSetAsync(key, payload, ttl);
        }
        catch (Exception ex)
        {
            log.LogDebug("cache set skipped: {Message}", ex.Message);
        }
    }

    public async Task DeleteAsync(params string[] keys)
    {
        if (keys.Length == 0) return;
        try
        {
            await (await redis.GetDatabaseAsync()).KeyDeleteAsync(keys.Select(k => (RedisKey)k).ToArray());
        }
        catch (Exception ex)
        {
            log.LogDebug("cache delete skipped: {Message}", ex.Message);
        }
    }

    public async Task DeletePrefixAsync(string prefix)
    {
        try
        {
            var mux = await redis.GetAsync();
            var db = mux.GetDatabase();
            foreach (var endpoint in mux.GetEndPoints())
            {
                var server = mux.GetServer(endpoint);
                if (server.IsReplica) continue;
                await foreach (var key in server.KeysAsync(db.Database, $"{prefix}*", 200))
                    await db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            log.LogDebug("cache prefix delete skipped: {Message}", ex.Message);
        }
    }
}
