using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Application.Authorization;
using DineOS.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Integration;

/// <summary>
/// Integration tests for /api/v1/reports — real PostgreSQL via Testcontainers.
/// Each test uses a unique tenant ID (521–529) to avoid inter-test coupling.
/// The order-history test guards the payment-method grouping, which previously
/// used an EF projection (GroupBy → g.First().Method) that does not translate
/// to SQL and threw at runtime.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class ReportsIntegrationTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── Order history returns the seeded order with item count + payment method ─
    [Fact]
    public async Task GetOrderHistory_Manager_Returns200_WithItemCountAndPaymentMethod()
    {
        var client = ClientWith(Jwt(Roles.Manager, "521"));
        var orderId = await SeedOrderWithItemsAndPaymentAsync(
            tenantId: 521,
            total: 40.00m,
            status: OrderStatus.Delivered,
            itemCount: 2,
            method: PaymentMethod.Card);

        var response = await client.GetAsync("/api/v1/reports/orders/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<OrderHistoryReportDto>>(response);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var order = Assert.Single(result.Data.Orders, o => o.Id == orderId);
        Assert.Equal(2, order.ItemCount);
        Assert.Equal(40.00m, order.Total);
        Assert.Equal("Card", order.PaymentMethod);
        Assert.Equal("Delivered", order.Status);
    }

    // ── Order history for a fresh tenant is empty (no crash, no leakage) ────────
    [Fact]
    public async Task GetOrderHistory_EmptyTenant_Returns200_EmptyList()
    {
        var client = ClientWith(Jwt(Roles.Manager, "522"));

        var response = await client.GetAsync("/api/v1/reports/orders/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<OrderHistoryReportDto>>(response);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Orders);
        Assert.Equal(0, result.Data.TotalCount);
    }

    // ── Items report ranks ordered items by quantity sold ──────────────────────
    [Fact]
    public async Task GetItemsReport_Manager_Returns200_RankedByQuantity()
    {
        var client = ClientWith(Jwt(Roles.Manager, "523"));
        await SeedOrderWithNamedItemsAsync(523, ("Burger", 5), ("Fries", 2));

        var response = await client.GetAsync("/api/v1/reports/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<ItemsReportDto>>(response);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var burger = Assert.Single(result.Data.TopItems, i => i.Name == "Burger");
        var fries = Assert.Single(result.Data.TopItems, i => i.Name == "Fries");
        Assert.Equal(5, burger.Quantity);
        Assert.Equal(2, fries.Quantity);
        // Burger (5) must outrank Fries (2) in the descending-by-quantity list.
        Assert.True(
            result.Data.TopItems.FindIndex(i => i.Name == "Burger") <
            result.Data.TopItems.FindIndex(i => i.Name == "Fries"));
    }

    // ── KitchenStaff is below Manager → 403 on reports ──────────────────────────
    [Fact]
    public async Task GetOrderHistory_KitchenStaff_Returns403()
    {
        var client = ClientWith(Jwt(Roles.KitchenStaff, "524"));

        var response = await client.GetAsync("/api/v1/reports/orders/history");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<long> SeedOrderWithItemsAndPaymentAsync(
        long tenantId, decimal total, OrderStatus status, int itemCount, PaymentMethod method)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order
        {
            TenantId = tenantId,
            OrderType = "dine-in",
            TableNumber = 3,
            Status = status,
            Total = total,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        for (var i = 0; i < itemCount; i++)
        {
            db.OrderItems.Add(new OrderItem
            {
                TenantId = tenantId,
                OrderId = order.Id,
                Name = $"Item {i}",
                Quantity = 1,
                UnitPrice = total / itemCount,
            });
        }

        db.Payments.Add(new Payment
        {
            TenantId = tenantId,
            OrderId = order.Id,
            Amount = total,
            Method = method,
            Status = PaymentStatus.Completed,
        });

        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task SeedOrderWithNamedItemsAsync(
        long tenantId, params (string Name, int Quantity)[] items)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order
        {
            TenantId = tenantId,
            OrderType = "dine-in",
            Status = OrderStatus.Delivered,
            Total = 0m,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        foreach (var (name, quantity) in items)
        {
            db.OrderItems.Add(new OrderItem
            {
                TenantId = tenantId,
                OrderId = order.Id,
                Name = name,
                Quantity = quantity,
                UnitPrice = 10.00m,
            });
        }
        await db.SaveChangesAsync();
    }

    private HttpClient ClientWith(string jwt)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    private static string Jwt(string role, string tenantId)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim("sub",          $"test-{role.ToLower()}"),
                new Claim("email",        $"{role.ToLower()}@dineos.dev"),
                new Claim("tenant_id",    tenantId),
                new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } }))
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
}
