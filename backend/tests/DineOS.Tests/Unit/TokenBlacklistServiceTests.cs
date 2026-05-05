using DineOS.Infrastructure.Services;
using NSubstitute;
using StackExchange.Redis;

namespace DineOS.Tests.Unit;

public class TokenBlacklistServiceTests
{
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly IConnectionMultiplexer _multiplexer = Substitute.For<IConnectionMultiplexer>();

    private TokenBlacklistService Build()
    {
        _multiplexer.GetDatabase().Returns(_db);
        return new TokenBlacklistService(_multiplexer);
    }

    [Fact]
    public async Task BlacklistAsync_StoresKeyInRedisWithCorrectTTL()
    {
        var jti = "abc-123";
        var ttl = TimeSpan.FromMinutes(30);
        var sut = Build();

        await sut.BlacklistAsync(jti, ttl);

        await _db.Received(1).StringSetAsync(
            $"blacklist:{jti}",
            (RedisValue)"1",
            ttl);
    }

    [Fact]
    public async Task IsBlacklistedAsync_KeyExists_ReturnsTrue()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)"1");
        var sut = Build();

        var result = await sut.IsBlacklistedAsync("abc-123");

        Assert.True(result);
    }

    [Fact]
    public async Task IsBlacklistedAsync_KeyDoesNotExist_ReturnsFalse()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);
        var sut = Build();

        var result = await sut.IsBlacklistedAsync("abc-123");

        Assert.False(result);
    }
}
