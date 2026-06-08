using DineOS.Application.Interfaces.Services;
using StackExchange.Redis;

namespace DineOS.Infrastructure.Services;

public class TokenBlacklistService(IConnectionMultiplexer redis) : ITokenBlacklistService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public Task BlacklistAsync(string jti, TimeSpan ttl)
        // A non-positive TTL means the token has already expired — JWT lifetime
        // validation rejects it regardless, so there is nothing to revoke. Skip
        // the write: StackExchange.Redis rejects a non-positive expiry (SETEX 0)
        // with a server error, so writing it would throw for no benefit.
        => ttl <= TimeSpan.Zero
            ? Task.CompletedTask
            : _db.StringSetAsync($"blacklist:{jti}", "1", ttl);

    public async Task<bool> IsBlacklistedAsync(string jti)
        => (await _db.StringGetAsync($"blacklist:{jti}")).HasValue;
}
