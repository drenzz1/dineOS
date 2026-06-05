using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace DineOS.Tests.Unit;

public class AdminServiceTests
{
    private static (AdminService svc, AppDbContext db) CreateSut()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        return (new AdminService(db), db);
    }

    [Fact]
    public async Task GetAnalyticsAsync_AggregatesLivePlatformData()
    {
        var (svc, db) = CreateSut();
        var now = DateTime.UtcNow;
        var today = now.Date.AddHours(12);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var betaMonthTimestamp = now.Day > 1
            ? monthStart.AddHours(12)
            : today.AddHours(1);
        var betaCountsToday = betaMonthTimestamp.Date == today.Date;

        db.Tenants.AddRange(
            new Tenant
            {
                Id = 10,
                Name = "Alpha Kitchen",
                Slug = "alpha-kitchen",
                OwnerName = "Alpha Owner",
                OwnerEmail = "alpha@example.com",
                Phone = "1",
                City = "Tirana",
                IsActive = true,
                CreatedAt = now.AddDays(-2)
            },
            new Tenant
            {
                Id = 20,
                Name = "Beta Bistro",
                Slug = "beta-bistro",
                OwnerName = "Beta Owner",
                OwnerEmail = "beta@example.com",
                Phone = "2",
                City = "Prishtina",
                IsActive = false,
                Plan = SubscriptionPlan.Pro,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddHours(-3)
            },
            new Tenant
            {
                Id = 30,
                Name = "Deleted Diner",
                Slug = "deleted-diner",
                OwnerName = "Deleted Owner",
                OwnerEmail = "deleted@example.com",
                Phone = "3",
                City = "Peja",
                IsActive = true,
                CreatedAt = now.AddDays(-1),
                DeletedAt = now
            });

        db.Orders.AddRange(
            new Order
            {
                TenantId = 10,
                OrderType = "DineIn",
                Status = OrderStatus.New,
                Total = 20m,
                CreatedAt = today.AddMinutes(15)
            },
            new Order
            {
                TenantId = 10,
                OrderType = "Takeaway",
                Status = OrderStatus.Delivered,
                Total = 30m,
                CreatedAt = today.AddMinutes(30)
            },
            new Order
            {
                TenantId = 20,
                OrderType = "DineIn",
                Status = OrderStatus.Delivered,
                Total = 40m,
                CreatedAt = betaMonthTimestamp
            },
            new Order
            {
                TenantId = 30,
                OrderType = "DineIn",
                Status = OrderStatus.Delivered,
                Total = 99m,
                CreatedAt = today.AddMinutes(45)
            });

        db.Payments.AddRange(
            new Payment
            {
                TenantId = 10,
                OrderId = 1,
                Amount = 30m,
                Method = PaymentMethod.Card,
                Status = PaymentStatus.Completed,
                CreatedAt = today.AddMinutes(31)
            },
            new Payment
            {
                TenantId = 10,
                OrderId = 2,
                Amount = 99m,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Pending,
                CreatedAt = today.AddMinutes(32)
            },
            new Payment
            {
                TenantId = 20,
                OrderId = 3,
                Amount = 40m,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Completed,
                CreatedAt = betaMonthTimestamp
            },
            new Payment
            {
                TenantId = 30,
                OrderId = 4,
                Amount = 99m,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Completed,
                CreatedAt = today.AddMinutes(46)
            });
        await db.SaveChangesAsync();

        var result = await svc.GetAnalyticsAsync();

        Assert.True(result.IsSuccess);
        var analytics = result.Value!;
        Assert.Equal(2, analytics.TotalRestaurants);
        Assert.Equal(1, analytics.ActiveRestaurants);
        Assert.Equal(betaCountsToday ? 3 : 2, analytics.OrdersToday);
        Assert.Equal(betaCountsToday ? 70m : 30m, analytics.RevenueToday);
        Assert.Equal(8, analytics.WeeklyGrowth.Count);
        Assert.Equal(2, analytics.WeeklyGrowth.Sum(w => w.NewRestaurants));

        Assert.Equal("Beta Bistro", analytics.TopRestaurants[0].Name);
        Assert.Equal(1, analytics.TopRestaurants[0].Orders);
        Assert.Equal(40m, analytics.TopRestaurants[0].Revenue);
        Assert.Equal("Alpha Kitchen", analytics.TopRestaurants[1].Name);
        Assert.Equal(2, analytics.TopRestaurants[1].Orders);
        Assert.Equal(30m, analytics.TopRestaurants[1].Revenue);
        Assert.DoesNotContain(analytics.TopRestaurants, r => r.Name == "Deleted Diner");

        Assert.Contains(analytics.ActivityFeed, e =>
            e.Description == "Restaurant suspended: Beta Bistro");
        Assert.DoesNotContain(analytics.ActivityFeed, e =>
            e.Description.Contains("Deleted Diner", StringComparison.Ordinal));
    }
}
