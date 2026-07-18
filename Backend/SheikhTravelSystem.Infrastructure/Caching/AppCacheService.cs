using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Caching;

public sealed class AppCacheService(
    IMemoryCache memoryCache,
    IDistributedCache distributedCache,
    IOptions<CacheOptions> options,
    ILogger<AppCacheService> logger) : IAppCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly bool _useDistributed = !string.IsNullOrWhiteSpace(options.Value.RedisConnectionString);

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        if (_useDistributed)
        {
            try
            {
                var bytes = await distributedCache.GetAsync(key, cancellationToken);
                if (bytes is { Length: > 0 })
                {
                    var fromRedis = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
                    if (fromRedis is not null)
                    {
                        memoryCache.Set(key, fromRedis, ttl);
                        _keys.TryAdd(key, 0);
                        return fromRedis;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Redis must never take down read paths (settings, dashboard, catalogs).
                logger.LogWarning(ex, "Distributed cache GET failed for key {CacheKey}; falling back to factory.", key);
            }
        }

        var value = await factory(cancellationToken);
        memoryCache.Set(key, value, ttl);
        _keys.TryAdd(key, 0);

        if (_useDistributed)
        {
            try
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
                await distributedCache.SetAsync(
                    key,
                    payload,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Distributed cache SET failed for key {CacheKey}; memory cache still populated.", key);
            }
        }

        return value;
    }

    public void Remove(string key)
    {
        memoryCache.Remove(key);
        _keys.TryRemove(key, out _);
        if (!_useDistributed) return;

        try
        {
            distributedCache.Remove(key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Distributed cache REMOVE failed for key {CacheKey}.", key);
        }
    }

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
            Remove(key);
    }
}
