using DineOS.Application.Billing;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class BillingServiceTests
{
    [Fact]
    public async Task CreateCheckoutSessionAsync_MissingPriceForRequestedCycle_ReturnsBadRequest()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns(1L);

        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var service = new BillingService(
            db,
            tenantSvc,
            new CreateCheckoutSessionRequestValidator(),
            Options.Create(new StripeOptions
            {
                SecretKey = "sk_test_local",
                ProMonthlyPriceId = "price_monthly"
            }),
            NullLogger<BillingService>.Instance);

        var result = await service.CreateCheckoutSessionAsync(BillingCycle.Annual);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.BadRequest, result.Error);
        Assert.Equal("Stripe price is not configured for Annual billing.", result.Message);
    }
}
