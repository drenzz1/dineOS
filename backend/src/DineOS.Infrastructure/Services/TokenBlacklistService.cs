using DineOS.Application.Interfaces.Services;
using StackExchange.Redis;

namespace DineOS.Infrastructure.Services;

public class TokenBlacklistService(IConnectionMultiplexer redis) : ITokenBlacklistService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public Task BlacklistAsync(string jti, TimeSpan ttl)
        => _db.StringSetAsync($"blacklist:{jti}", "1", ttl);

    public async Task<bool> IsBlacklistedAsync(string jti)
        => (await _db.StringGetAsync($"blacklist:{jti}")).HasValue;
}
