using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Restaurants;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class AdminRestaurantServiceTests
{
    private static (AdminRestaurantService svc, AppDbContext db) CreateSut(
        IValidator<CreateRestaurantRequest>? createValidator = null,
        IValidator<UpdateRestaurantStatusRequest>? statusValidator = null,
        IValidator<UpdateRestaurantPlanRequest>? planValidator = null)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("super-admin");

        createValidator ??= AlwaysValid<CreateRestaurantRequest>();
        statusValidator ??= AlwaysValid<UpdateRestaurantStatusRequest>();
        planValidator   ??= AlwaysValid<UpdateRestaurantPlanRequest>();

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var svc = new AdminRestaurantService(
            db,
            currentUser,
            createValidator,
            statusValidator,
            planValidator,
            NullLogger<AdminRestaurantService>.Instance);

        return (svc, db);
    }

    private static IValidator<T> AlwaysValid<T>()
    {
        var v = Substitute.For<IValidator<T>>();
        v.ValidateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        return v;
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsCreated_AndGeneratesSlug()
    {
        var (svc, db) = CreateSut();

        var result = await svc.CreateAsync(new CreateRestaurantRequest
        {
            Name       = "Pizza Place 42",
            OwnerName  = "Alice",
            OwnerEmail = "alice@example.com",
            Phone      = "+1 555 0100",
            City       = "Tirana",
            Plan       = "Pro"
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsCreated);
        Assert.Equal("Pizza Place 42", result.Value!.Name);
        Assert.Equal("Pro", result.Value.Plan);

        var saved = await db.Tenants.FirstAsync(t => t.Name == "Pizza Place 42");
        Assert.Equal("pizza-place-42", saved.Slug);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNotFound()
    {
        var (svc, _) = CreateSut();

        var result = await svc.GetByIdAsync(9999);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.Error);
    }

    [Fact]
    public async Task UpdateStatusAsync_TogglesIsActive()
    {
        var (svc, db) = CreateSut();
        var tenant = new Tenant { Name = "X", Slug = "x", OwnerName = "O", OwnerEmail = "o@x.com", Phone = "1", City = "C", IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var result = await svc.UpdateStatusAsync(tenant.Id, new UpdateRestaurantStatusRequest { Status = "Suspended" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Suspended", result.Value!.Status);
    }

    [Fact]
    public async Task UpdatePlanAsync_ChangesPlan()
    {
        var (svc, db) = CreateSut();
        var tenant = new Tenant { Name = "X", Slug = "x", OwnerName = "O", OwnerEmail = "o@x.com", Phone = "1", City = "C", Plan = SubscriptionPlan.Free };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var result = await svc.UpdatePlanAsync(tenant.Id, new UpdateRestaurantPlanRequest { Plan = "Pro" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Pro", result.Value!.Plan);
    }
}
