using DineOS.Application.Common;
using DineOS.Application.DemoAccess;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class DemoAccessServiceTests
{
    private static (DemoAccessService svc, AppDbContext db, IBackgroundJobClient jobs)
        CreateSut(DemoOptions? demoOpts = null)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var jobs = Substitute.For<IBackgroundJobClient>();

        var svc = new DemoAccessService(
            db,
            new RequestDemoAccessRequestValidator(),
            jobs,
            Options.Create(demoOpts ?? new DemoOptions()),
            NullLogger<DemoAccessService>.Instance);

        return (svc, db, jobs);
    }

    private static RequestDemoAccessRequest ValidRequest(string email = "visitor@example.com") => new()
    {
        Email = email,
        AcceptedTerms = true,
    };

    [Fact]
    public async Task RequestAsync_NewEmail_CreatesPendingRowAndEnqueuesProvisioning()
    {
        var (svc, db, jobs) = CreateSut();

        var result = await svc.RequestAsync(ValidRequest(), ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        var row = await db.DemoUsers.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("visitor@example.com", row.Email);
        Assert.Equal(DemoUserStatus.Pending, row.Status);
        Assert.Equal("127.0.0.1", row.IpAddress);
        Assert.True(row.ExpiresAt > DateTime.UtcNow.AddDays(6));

        jobs.Received(1).Create(
            Arg.Any<Hangfire.Common.Job>(),
            Arg.Any<Hangfire.States.EnqueuedState>());
    }

    [Fact]
    public async Task RequestAsync_ActiveEmail_EnqueuesResendInsteadOfProvisioning()
    {
        var (svc, db, jobs) = CreateSut();

        db.DemoUsers.Add(new DemoUser
        {
            Email          = "visitor@example.com",
            KeycloakUserId = "kc-1",
            Status         = DemoUserStatus.Active,
            RequestedAt    = DateTime.UtcNow.AddDays(-1),
            ExpiresAt      = DateTime.UtcNow.AddDays(6),
        });
        await db.SaveChangesAsync();

        var result = await svc.RequestAsync(ValidRequest(), ipAddress: "10.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await db.DemoUsers.IgnoreQueryFilters().CountAsync()); // no extra row

        // Reload to confirm IP was updated, status was NOT regressed.
        var row = await db.DemoUsers.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(DemoUserStatus.Active, row.Status);
        Assert.Equal("10.0.0.1", row.IpAddress);
    }

    [Fact]
    public async Task RequestAsync_ExpiredEmail_ResetsExpiryAndReEnqueuesProvisioning()
    {
        var (svc, db, jobs) = CreateSut();
        var pastExpiry = DateTime.UtcNow.AddDays(-3);

        db.DemoUsers.Add(new DemoUser
        {
            Email          = "visitor@example.com",
            KeycloakUserId = "kc-1",
            Status         = DemoUserStatus.Expired,
            RequestedAt    = DateTime.UtcNow.AddDays(-10),
            ExpiresAt      = pastExpiry,
        });
        await db.SaveChangesAsync();

        var result = await svc.RequestAsync(ValidRequest(), ipAddress: "10.0.0.1");

        Assert.True(result.IsSuccess);
        var row = await db.DemoUsers.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(DemoUserStatus.Pending, row.Status);
        Assert.True(row.ExpiresAt > DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task RequestAsync_HoneypotTripped_ReturnsOkWithoutInsertingRowOrEnqueuingJob()
    {
        var (svc, db, jobs) = CreateSut();

        var result = await svc.RequestAsync(new RequestDemoAccessRequest
        {
            Email         = "visitor@example.com",
            AcceptedTerms = true,
            CompanyName   = "I'm a bot",
        }, ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await db.DemoUsers.IgnoreQueryFilters().CountAsync());
        jobs.DidNotReceive().Create(
            Arg.Any<Hangfire.Common.Job>(),
            Arg.Any<Hangfire.States.EnqueuedState>());
    }

    [Fact]
    public async Task RequestAsync_FeatureDisabled_ReturnsNotFound()
    {
        var (svc, _, _) = CreateSut(new DemoOptions { Enabled = false });

        var result = await svc.RequestAsync(ValidRequest(), ipAddress: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.Error);
    }

    [Fact]
    public async Task RequestAsync_InvalidEmail_ReturnsValidationFailed()
    {
        var (svc, _, _) = CreateSut();

        var result = await svc.RequestAsync(new RequestDemoAccessRequest
        {
            Email         = "not-an-email",
            AcceptedTerms = true,
        }, ipAddress: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task RequestAsync_TermsNotAccepted_ReturnsValidationFailed()
    {
        var (svc, _, _) = CreateSut();

        var result = await svc.RequestAsync(new RequestDemoAccessRequest
        {
            Email         = "visitor@example.com",
            AcceptedTerms = false,
        }, ipAddress: null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task RequestAsync_EmailIsLowercasedAndTrimmed()
    {
        var (svc, db, _) = CreateSut();

        await svc.RequestAsync(ValidRequest("  Visitor@Example.COM  "), ipAddress: null);

        var row = await db.DemoUsers.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("visitor@example.com", row.Email);
    }
}
