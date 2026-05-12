using System.Diagnostics;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace DineOS.Tests.Benchmarks;

// Manual perf benchmark for the GET /api/v1/menu/items cache-aside path.
//
// Default: no-op (returns immediately) so CI doesn't depend on a running
// Postgres + Redis. To reproduce locally:
//   docker compose -f docker-compose.yml -f /tmp/docker-compose.override.yml up postgres redis -d
//   dotnet ef database update ...
//   seed menu items (see docs/backend/redis-caching.md)
//   RUN_BENCH=1 dotnet test --filter ColdVsWarm --nologo
public class MenuCacheBenchmark(ITestOutputHelper output)
{
    [Fact]
    public async Task ColdVsWarm()
    {
        if (Environment.GetEnvironmentVariable("RUN_BENCH") != "1")
        {
            output.WriteLine("Skipping: set RUN_BENCH=1 to enable.");
            return;
        }

        var pgConn = Environment.GetEnvironmentVariable("BENCH_PG")
            ?? "Host=localhost;Port=5434;Database=dineos;Username=dineos;Password=dineos_dev";
        var redisConn = Environment.GetEnvironmentVariable("BENCH_REDIS")
            ?? "localhost:6380,abortConnect=false";

        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(1L);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("bench");

        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(pgConn)
                .Options,
            tenantSvc);

        var redis = await ConnectionMultiplexer.ConnectAsync(redisConn);
        var cache = new RedisCacheService(redis, NullLogger<RedisCacheService>.Instance);

        var svc = new MenuService(
            db,
            tenantSvc,
            currentUser,
            cache,
            Substitute.For<IFileStorageService>(),
            AlwaysValid<CreateMenuItemRequest>(),
            AlwaysValid<UpdateMenuItemRequest>(),
            AlwaysValid<CreateMenuCategoryRequest>(),
            AlwaysValid<UploadMenuItemImageRequest>(),
            NullLogger<MenuService>.Instance);

        const string key = "menu:items:tenant:1";

        // Warm up EF / Npgsql connection pool so the cold timing reflects only
        // the cache-miss penalty, not first-connection setup.
        await cache.RemoveAsync(key);
        _ = await db.MenuItems.AsNoTracking().Take(1).ToListAsync();

        // Cold: cache miss -> DB query + EF materialize + cache populate.
        await cache.RemoveAsync(key);
        var sw = Stopwatch.StartNew();
        var coldResult = await svc.GetMenuItemsAsync();
        sw.Stop();
        var coldMs = sw.Elapsed.TotalMilliseconds;
        output.WriteLine($"COLD: {coldMs:F2} ms  (rows: {coldResult.Value!.Count})");

        // Warm: cache hit each call.
        var warmTimes = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            sw.Restart();
            await svc.GetMenuItemsAsync();
            sw.Stop();
            warmTimes.Add(sw.Elapsed.TotalMilliseconds);
        }

        output.WriteLine(string.Empty);
        output.WriteLine("WARM (10 runs):");
        for (int i = 0; i < warmTimes.Count; i++)
            output.WriteLine($"  run {i + 1,2}: {warmTimes[i]:F2} ms");

        var avg = warmTimes.Average();
        var min = warmTimes.Min();
        var max = warmTimes.Max();

        output.WriteLine(string.Empty);
        output.WriteLine($"WARM avg={avg:F2} ms  min={min:F2}  max={max:F2}");
        output.WriteLine($"Speedup: {coldMs / avg:F1}x (cold {coldMs:F2} ms vs warm avg {avg:F2} ms)");

        Assert.True(avg < coldMs, $"Warm avg ({avg:F2} ms) should be < cold ({coldMs:F2} ms).");
    }

    private static IValidator<T> AlwaysValid<T>()
    {
        var v = Substitute.For<IValidator<T>>();
        v.ValidateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        return v;
    }
}
