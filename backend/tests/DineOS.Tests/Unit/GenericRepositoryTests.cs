using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Common;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class StubItem : BaseAuditingEntity
{
    public string Name { get; set; } = "";
}

internal sealed class StubDbContext(DbContextOptions<AppDbContext> options, ITenantService tenantService)
    : AppDbContext(options, tenantService)
{
    public DbSet<StubItem> Items => Set<StubItem>();
}

public class GenericRepositoryTests
{
    private static (StubDbContext ctx, GenericRepository<StubItem> repo) CreateSut()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var currentUserSvc = Substitute.For<ICurrentUserService>();
        currentUserSvc.UserId.Returns("test-user");

        var ctx = new StubDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var repo = new GenericRepository<StubItem>(ctx, currentUserSvc);
        return (ctx, repo);
    }

    [Fact]
    public async Task AddAsync_PersistsEntity_AndReturnsIt()
    {
        var (_, repo) = CreateSut();

        var result = await repo.AddAsync(new StubItem { Name = "alpha" });

        Assert.Equal("alpha", result.Name);
        Assert.Single(await repo.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEntity()
    {
        var (_, repo) = CreateSut();
        var item = await repo.AddAsync(new StubItem { Name = "beta" });

        var found = await repo.GetByIdAsync(item.Id);

        Assert.NotNull(found);
        Assert.Equal("beta", found.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var (_, repo) = CreateSut();

        var result = await repo.GetByIdAsync(99999L);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ExcludesSoftDeletedEntities()
    {
        var (_, repo) = CreateSut();
        await repo.AddAsync(new StubItem { Name = "live" });
        var deleted = await repo.AddAsync(new StubItem { Name = "gone" });
        await repo.DeleteAsync(deleted.Id);

        var all = (await repo.GetAllAsync()).ToList();

        Assert.Single(all);
        Assert.Equal("live", all[0].Name);
    }

    [Fact]
    public async Task FindAsync_ReturnsOnlyMatchingEntities()
    {
        var (_, repo) = CreateSut();
        await repo.AddAsync(new StubItem { Name = "foo" });
        await repo.AddAsync(new StubItem { Name = "bar" });

        var result = (await repo.FindAsync(x => x.Name == "foo")).ToList();

        Assert.Single(result);
        Assert.Equal("foo", result[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var (_, repo) = CreateSut();
        var item = await repo.AddAsync(new StubItem { Name = "original" });

        item.Name = "updated";
        await repo.UpdateAsync(item);

        Assert.Equal("updated", (await repo.GetByIdAsync(item.Id))!.Name);
    }

    [Fact]
    public async Task DeleteAsync_ExistingEntity_SoftDeletesIt()
    {
        var (_, repo) = CreateSut();
        var item = await repo.AddAsync(new StubItem { Name = "to-delete" });

        await repo.DeleteAsync(item.Id);

        Assert.Null(await repo.GetByIdAsync(item.Id));
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_SetsDeletedAtAndDeletedBy()
    {
        var (ctx, repo) = CreateSut();
        var item = await repo.AddAsync(new StubItem { Name = "to-delete" });

        await repo.DeleteAsync(item.Id);

        var raw = await ctx.Items.IgnoreQueryFilters().FirstAsync(e => e.Id == item.Id);
        Assert.NotNull(raw.DeletedAt);
        Assert.Equal("test-user", raw.DeletedBy);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        var (_, repo) = CreateSut();

        var ex = await Record.ExceptionAsync(() => repo.DeleteAsync(99999L));

        Assert.Null(ex);
    }
}
