using DineOS.Application.Authorization;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.StaffMembers;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class StaffServiceTests
{
    private static (StaffService svc, AppDbContext db, ITenantService tenantSvc, IPinHasher pinHasher) CreateSut(
        long? tenantId = 1L,
        IValidator<CreateStaffMemberRequest>? createValidator = null,
        IValidator<UpdateStaffMemberRequest>? updateValidator = null)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(tenantId);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns("test-user");

        var pinHasher = Substitute.For<IPinHasher>();
        pinHasher.Hash(Arg.Any<string>()).Returns(c => $"hashed:{c.Arg<string>()}");

        createValidator ??= AlwaysValid<CreateStaffMemberRequest>();
        updateValidator ??= AlwaysValid<UpdateStaffMemberRequest>();

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var svc = new StaffService(
            db,
            tenantSvc,
            currentUser,
            pinHasher,
            createValidator,
            updateValidator,
            NullLogger<StaffService>.Instance);

        return (svc, db, tenantSvc, pinHasher);
    }

    private static IValidator<T> AlwaysValid<T>()
    {
        var v = Substitute.For<IValidator<T>>();
        v.ValidateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        return v;
    }

    private static IValidator<T> AlwaysInvalid<T>(string error)
    {
        var v = Substitute.For<IValidator<T>>();
        v.ValidateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Field", error) }));
        return v;
    }

    [Fact]
    public async Task CreateStaffAsync_ValidRequest_ReturnsCreated()
    {
        var (svc, _, _, _) = CreateSut();

        var result = await svc.CreateStaffAsync(new CreateStaffMemberRequest
        {
            FullName = "Alice",
            Email    = "alice@x.com",
            Role     = Roles.Cashier,
            Pin      = "1234"
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsCreated);
        Assert.NotNull(result.Value);
        Assert.Equal("Alice", result.Value!.FullName);
    }

    [Fact]
    public async Task CreateStaffAsync_ValidationFails_ReturnsValidationFailed()
    {
        var (svc, _, _, _) = CreateSut(
            createValidator: AlwaysInvalid<CreateStaffMemberRequest>("FullName required"));

        var result = await svc.CreateStaffAsync(new CreateStaffMemberRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.ValidationFailed, result.Error);
        Assert.Equal("Validation failed", result.Message);
        Assert.NotNull(result.Errors);
        Assert.Contains("FullName required", result.Errors!);
    }

    [Fact]
    public async Task CreateStaffAsync_NoTenantContext_ReturnsBadRequest()
    {
        var (svc, _, _, _) = CreateSut(tenantId: null);

        var result = await svc.CreateStaffAsync(new CreateStaffMemberRequest
        {
            FullName = "A", Email = "a@x.com", Role = Roles.Cashier, Pin = "1234"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.BadRequest, result.Error);
    }

    [Fact]
    public async Task UpdateStaffAsync_MissingId_ReturnsNotFound()
    {
        var (svc, _, _, _) = CreateSut();

        var result = await svc.UpdateStaffAsync(9999, new UpdateStaffMemberRequest { FullName = "X" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.Error);
    }

    [Fact]
    public async Task SetStaffActiveAsync_TogglesActiveFlag()
    {
        var (svc, _, _, _) = CreateSut();

        var created = await svc.CreateStaffAsync(new CreateStaffMemberRequest
        {
            FullName = "Bob", Email = "bob@x.com", Role = Roles.Cashier, Pin = "1234"
        });

        var result = await svc.SetStaffActiveAsync(created.Value!.Id, new SetStaffActiveRequest { IsActive = false });

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }
}
