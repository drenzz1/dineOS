using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DineOS.Tests.Unit;

public class AiMenuServiceTests
{
    private static (AiMenuService svc, AppDbContext db, IAiClient ai) CreateSut(long? tenantId = 1L)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(tenantId);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("manager-1");

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var ai = Substitute.For<IAiClient>();

        var svc = new AiMenuService(db, ai, currentUser, NullLogger<AiMenuService>.Instance);
        return (svc, db, ai);
    }

    private static async Task<MenuItem> SeedItemAsync(AppDbContext db, long tenantId = 1L)
    {
        var item = new MenuItem
        {
            Name        = "Margherita Pizza",
            Price       = 9.50m,
            Category    = new MenuCategory { TenantId = tenantId, Name = "Pizza" },
            TenantId    = tenantId,
            Description = null,
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task SuggestDescriptionAsync_HappyPath_ReturnsDtoAndForwardsItemContext()
    {
        var (svc, db, ai) = CreateSut();
        var item = await SeedItemAsync(db);

        ai.GenerateMenuDescriptionAsync(
                Arg.Is<MenuDescriptionAiRequest>(r =>
                    r.Name == "Margherita Pizza" && r.Category == "Pizza" && r.Price == 9.50m),
                Arg.Any<CancellationToken>())
          .Returns(new MenuDescriptionAiResult(
              Description: "Classic tomato, mozzarella, and basil pizza baked in a wood-fired oven.",
              Allergens:   new[] { "gluten", "dairy" },
              Usage:       new AiUsage(120, 60, "claude-sonnet-4-5")));

        var result = await svc.SuggestDescriptionAsync(item.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(item.Id, result.Value!.MenuItemId);
        Assert.Contains("tomato", result.Value.SuggestedDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "gluten", "dairy" }, result.Value.SuggestedAllergens);
        Assert.Equal("claude-sonnet-4-5", result.Value.Metadata.Model);
        Assert.Equal(120, result.Value.Metadata.InputTokens);
        Assert.Equal(60,  result.Value.Metadata.OutputTokens);
    }

    [Fact]
    public async Task SuggestDescriptionAsync_UnknownId_ReturnsNotFound()
    {
        var (svc, _, ai) = CreateSut();

        var result = await svc.SuggestDescriptionAsync(9999);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.Error);
        await ai.DidNotReceiveWithAnyArgs().GenerateMenuDescriptionAsync(default!, default);
    }

    [Fact]
    public async Task SuggestDescriptionAsync_ProviderUnavailable_FallsBackTo422()
    {
        var (svc, db, ai) = CreateSut();
        var item = await SeedItemAsync(db);

        ai.GenerateMenuDescriptionAsync(Arg.Any<MenuDescriptionAiRequest>(), Arg.Any<CancellationToken>())
          .Returns(Task.FromException<MenuDescriptionAiResult>(new AiUnavailableException("upstream down")));

        var result = await svc.SuggestDescriptionAsync(item.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.UnprocessableEntity, result.Error);
        Assert.Contains("temporarily unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestDescriptionAsync_RespectsTenantFilter()
    {
        // Item belongs to tenant 2, but the service runs under tenant 1.
        var (svc, db, _) = CreateSut(tenantId: 1L);
        await SeedItemAsync(db, tenantId: 2L);
        var foreign = await db.MenuItems.IgnoreQueryFilters().FirstAsync();

        var result = await svc.SuggestDescriptionAsync(foreign.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.Error);
    }
}
