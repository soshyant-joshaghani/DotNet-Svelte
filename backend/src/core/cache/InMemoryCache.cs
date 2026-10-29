using System.Collections.Concurrent;
using System.Text.Json;
using DotnetSvelte.Core.Config;

namespace DotnetSvelte.Core.Cache;

/// <summary>Process-local cache with the same JSON round-trip as Redis; meant for tests and Redis-less runs.</summary>
public sealed class InMemoryCache : ICache
{
    private readonly ConcurrentDictionary<string, (string Json, TimeSpan Ttl)> _items = new();

    public IReadOnlyCollection<string> Keys => [.. _items.Keys];

    public TimeSpan? TtlOf(string key) => _items.TryGetValue(key, out var item) ? item.Ttl : null;

    public Task<T?> GetAsync<T>(string key) where T : class =>
        Task.FromResult(_items.TryGetValue(key, out var item)
            ? JsonSerializer.Deserialize<T>(item.Json, Json.Options)
            : null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
    {
        _items[key] = (JsonSerializer.Serialize(value, Json.Options), ttl);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(params string[] keys)
    {
        foreach (var key in keys) _items.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task DeletePrefixAsync(string prefix)
    {
        foreach (var key in _items.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            _items.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
