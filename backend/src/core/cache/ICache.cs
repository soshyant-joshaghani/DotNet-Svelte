namespace DotnetSvelte.Core.Cache;

public interface ICache
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class;
    Task DeleteAsync(params string[] keys);
    Task DeletePrefixAsync(string prefix);
}
