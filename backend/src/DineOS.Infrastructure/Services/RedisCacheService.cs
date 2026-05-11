using System.Text.Json;
using DineOS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DineOS.Infrastructure.Services;

public class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Db => redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await Db.StringGetAsync(key);
            if (!value.HasValue) return default;
            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache GET failed for key {Key}; falling through to source", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await Db.StringSetAsync(key, json, ttl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache SET failed for key {Key}; continuing without caching", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await Db.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache DEL failed for key {Key}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory(ct);
        if (value is not null) await SetAsync(key, value, ttl, ct);
        return value;
    }
}
