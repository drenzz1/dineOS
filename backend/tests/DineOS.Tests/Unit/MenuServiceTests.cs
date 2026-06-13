using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class MenuServiceTests
{
    private static (MenuService svc, AppDbContext db, FakeCache cache) CreateSut(
        long? tenantId = 1L,
        IValidator<CreateMenuItemRequest>? createValidator = null,
        IValidator<UpdateMenuItemRequest>? updateValidator = null)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(tenantId);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("test-user");

        createValidator ??= AlwaysValid<CreateMenuItemRequest>();
        updateValidator ??= AlwaysValid<UpdateMenuItemRequest>();
        var createCategoryValidator = AlwaysValid<CreateMenuCategoryRequest>();

        var cache = new FakeCache();

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var svc = new MenuService(
            db,
            tenantSvc,
            currentUser,
            cache,
            Substitute.For<IFileStorageService>(),
            Substitute.For<IEmbeddingsClient>(),
            createValidator,
            updateValidator,
            createCategoryValidator,
            Substitute.For<IValidator<UploadMenuItemImageRequest>>(),
            NullLogger<MenuService>.Instance);

        return (svc, db, cache);
    }

    private static IValidator<T> AlwaysValid<T>()
    {
        var v = Substitute.For<IValidator<T>>();
        v.ValidateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        return v;
    }

    [Fact]
    public async Task GetMenuItemsAsync_CacheMiss_LoadsFromDbAndPopulatesCache()
    {
        var (svc, db, cache) = CreateSut(tenantId: 1L);
        db.MenuItems.Add(new MenuItem { TenantId = 1, Name = "Pizza", Price = 10m, Category = new MenuCategory { TenantId = 1, Name = "Mains" } });
        await db.SaveChangesAsync();

        var result = await svc.GetMenuItemsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(1, cache.Gets);
        Assert.Equal(1, cache.Sets);
        Assert.True(cache.Store.ContainsKey("menu:items:tenant:1"));
    }

    [Fact]
    public async Task GetMenuItemsAsync_CacheHit_ReturnsCachedValueWithoutDb()
    {
        var (svc, db, cache) = CreateSut(tenantId: 1L);
        cache.Store["menu:items:tenant:1"] = new List<MenuItemDto>
        {
            new() { Id = 99, Name = "Cached Pizza", Price = 12m, Category = "Mains", TenantId = 1 }
        };

        // DB has a *different* row to prove the service did not hit it
        db.MenuItems.Add(new MenuItem { TenantId = 1, Name = "Db Pizza", Price = 99m, Category = new MenuCategory { TenantId = 1, Name = "Mains" } });
        await db.SaveChangesAsync();

        var result = await svc.GetMenuItemsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("Cached Pizza", result.Value![0].Name);
        Assert.Equal(1, cache.Gets);
        Assert.Equal(0, cache.Sets);
    }

    [Fact]
    public async Task CreateMenuItemAsync_Success_InvalidatesCache()
    {
        var (svc, _, cache) = CreateSut(tenantId: 1L);
        cache.Store["menu:items:tenant:1"] = new List<MenuItemDto>();

        var result = await svc.CreateMenuItemAsync(new CreateMenuItemRequest
        {
            Name = "New Item", Price = 5m, Category = "Drinks"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, cache.Removes);
        Assert.False(cache.Store.ContainsKey("menu:items:tenant:1"));
    }

    [Fact]
    public async Task UpdateMenuItemAsync_Success_InvalidatesCache()
    {
        var (svc, db, cache) = CreateSut(tenantId: 1L);
        var item = new MenuItem { TenantId = 1, Name = "Old", Price = 5m, Category = new MenuCategory { TenantId = 1, Name = "X" } };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        cache.Store["menu:items:tenant:1"] = new List<MenuItemDto>();

        var result = await svc.UpdateMenuItemAsync(item.Id, new UpdateMenuItemRequest
        {
            Name = "New", Price = 7m, Category = "Y"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, cache.Removes);
        Assert.False(cache.Store.ContainsKey("menu:items:tenant:1"));
    }

    [Fact]
    public async Task DeleteMenuItemAsync_Success_InvalidatesCache()
    {
        var (svc, db, cache) = CreateSut(tenantId: 1L);
        var item = new MenuItem { TenantId = 1, Name = "X", Price = 1m, Category = new MenuCategory { TenantId = 1, Name = "Y" } };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        cache.Store["menu:items:tenant:1"] = new List<MenuItemDto>();

        var result = await svc.DeleteMenuItemAsync(item.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, cache.Removes);
        Assert.False(cache.Store.ContainsKey("menu:items:tenant:1"));
    }

    private sealed class FakeCache : ICacheService
    {
        public Dictionary<string, object?> Store { get; } = new();
        public int Gets { get; private set; }
        public int Sets { get; private set; }
        public int Removes { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            Gets++;
            return Task.FromResult(Store.TryGetValue(key, out var v) ? (T?)v : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        {
            Sets++;
            Store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            Removes++;
            Store.Remove(key);
            return Task.CompletedTask;
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
}
