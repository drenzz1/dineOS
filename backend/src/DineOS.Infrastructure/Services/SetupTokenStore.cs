using System.Security.Cryptography;
using DineOS.Application.Interfaces.Services;
using StackExchange.Redis;

namespace DineOS.Infrastructure.Services;

public sealed class SetupTokenStore(IConnectionMultiplexer redis) : ISetupTokenStore
{
    private const string KeyPrefix = "signup:setup-token:";
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<string> IssueAsync(long tenantId, TimeSpan ttl, CancellationToken ct = default)
    {
        var token = GenerateToken();
        await _db.StringSetAsync(KeyPrefix + token, tenantId, ttl);
        return token;
    }

    public async Task<long?> PeekAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var value = await _db.StringGetAsync(KeyPrefix + token);
        return value.TryParse(out long tenantId) ? tenantId : null;
    }

    public async Task<long?> ConsumeAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        // StringGetDelete is atomic — guarantees single-use even under concurrent submits.
        var value = await _db.StringGetDeleteAsync(KeyPrefix + token);
        return value.TryParse(out long tenantId) ? tenantId : null;
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
