using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Tests.Unit;

// Concrete entity only used in this test class
public class StubItem : BaseEntity
{
    public string Name { get; set; } = "";
}

// Extends AppDbContext so EF discovers StubItem and applies the soft-delete query filter
internal sealed class StubDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    public DbSet<StubItem> Items => Set<StubItem>();
}

public class GenericRepositoryTests
{
    private static StubDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task AddAsync_PersistsEntity_AndReturnsIt()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);

        var result = await repo.AddAsync(new StubItem { Name = "alpha" });

        Assert.Equal("alpha", result.Name);
        Assert.Single(await repo.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEntity()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);
        var item = await repo.AddAsync(new StubItem { Name = "beta" });

        var found = await repo.GetByIdAsync(item.Id);

        Assert.NotNull(found);
        Assert.Equal("beta", found.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ExcludesSoftDeletedEntities()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);
        var live = await repo.AddAsync(new StubItem { Name = "live" });
        var deleted = await repo.AddAsync(new StubItem { Name = "gone" });
        await repo.DeleteAsync(deleted.Id);

        var all = (await repo.GetAllAsync()).ToList();

        Assert.Single(all);
        Assert.Equal("live", all[0].Name);
    }

    [Fact]
    public async Task FindAsync_ReturnsOnlyMatchingEntities()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);
        await repo.AddAsync(new StubItem { Name = "foo" });
        await repo.AddAsync(new StubItem { Name = "bar" });

        var result = (await repo.FindAsync(x => x.Name == "foo")).ToList();

        Assert.Single(result);
        Assert.Equal("foo", result[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);
        var item = await repo.AddAsync(new StubItem { Name = "original" });

        item.Name = "updated";
        await repo.UpdateAsync(item);

        Assert.Equal("updated", (await repo.GetByIdAsync(item.Id))!.Name);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAtViaDbContextOverride()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);
        var item = await repo.AddAsync(new StubItem { Name = "x" });
        var createdAt = item.UpdatedAt;

        await Task.Delay(5);
        item.Name = "y";
        await repo.UpdateAsync(item);

        Assert.True(item.UpdatedAt >= createdAt);
    }

    [Fact]
    public async Task DeleteAsync_ExistingEntity_SoftDeletesIt()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);
        var item = await repo.AddAsync(new StubItem { Name = "to-delete" });

        await repo.DeleteAsync(item.Id);

        Assert.Null(await repo.GetByIdAsync(item.Id));
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        await using var ctx = CreateContext();
        var repo = new GenericRepository<StubItem>(ctx);

        var ex = await Record.ExceptionAsync(() => repo.DeleteAsync(Guid.NewGuid()));

        Assert.Null(ex);
    }
}
