using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Application.Signup;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class SignupServiceTests
{
    private static (SignupService svc, AppDbContext db) CreateSut(StripeOptions? stripeOptions = null)
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var opts = Options.Create(stripeOptions ?? new StripeOptions());
        var billing = new BillingService(
            db,
            tenantSvc,
            new DineOS.Application.Billing.CreateCheckoutSessionRequestValidator(),
            opts,
            NullLogger<BillingService>.Instance);

        var svc = new SignupService(
            db,
            billing,
            new SignupRequestValidator(),
            opts,
            NullLogger<SignupService>.Instance);

        return (svc, db);
    }

    private static SignupRequest ValidRequest(string email = "owner@example.com") => new()
    {
        RestaurantName = "Test Bistro",
        OwnerName      = "Test Owner",
        OwnerEmail     = email,
        Phone          = "+38344123456",
        City           = "Prishtina",
    };

    [Fact]
    public async Task StartSignupAsync_InvalidEmail_ReturnsValidationFailed()
    {
        var (svc, _) = CreateSut();

        var result = await svc.StartSignupAsync(new SignupRequest
        {
            RestaurantName = "Bistro",
            OwnerName      = "O",
            OwnerEmail     = "not-an-email",
            Phone          = "1",
            City           = "C",
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.ValidationFailed, result.Error);
    }

    [Fact]
    public async Task StartSignupAsync_StripeNotConfigured_ReturnsUnprocessableEntity()
    {
        var (svc, _) = CreateSut(); // empty StripeOptions → IsConfigured == false

        var result = await svc.StartSignupAsync(ValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.UnprocessableEntity, result.Error);
        Assert.Equal("Billing provider is unavailable. Please try again later.", result.Message);
    }

    [Fact]
    public async Task GetStatusAsync_UnknownSession_ReturnsNotFound()
    {
        var (svc, _) = CreateSut();

        var result = await svc.GetStatusAsync("cs_unknown");

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.Error);
    }

    [Fact]
    public async Task GetStatusAsync_EmptySessionId_ReturnsBadRequest()
    {
        var (svc, _) = CreateSut();

        var result = await svc.GetStatusAsync("   ");

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.BadRequest, result.Error);
    }

    [Theory]
    [InlineData(BillingStatus.Incomplete, "PendingPayment")]
    [InlineData(BillingStatus.Active, "Active")]
    [InlineData(BillingStatus.Trialing, "Active")]
    [InlineData(BillingStatus.Canceled, "Failed")]
    [InlineData(BillingStatus.PastDue, "Failed")]
    [InlineData(BillingStatus.None, "Failed")]
    public async Task GetStatusAsync_MapsBillingStatusToContractString(BillingStatus billingStatus, string expected)
    {
        var (svc, db) = CreateSut();
        var tenant = new Tenant
        {
            Name             = "Bistro",
            Slug             = "bistro",
            OwnerName        = "O",
            OwnerEmail       = "o@example.com",
            Phone            = "1",
            City             = "C",
            Plan             = SubscriptionPlan.Pro,
            BillingStatus    = billingStatus,
            StripeSessionId  = "cs_test_abc123",
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var result = await svc.GetStatusAsync("cs_test_abc123");

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value!.Status);
    }
}
