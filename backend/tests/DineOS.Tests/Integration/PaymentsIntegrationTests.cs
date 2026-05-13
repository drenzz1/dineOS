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
/// Integration tests for /api/v1/payments — real PostgreSQL via Testcontainers.
/// Each test uses a unique tenant ID (801–809) to avoid inter-test state coupling.
/// Orders are seeded directly via the factory's service scope since there is no
/// Orders creation API yet.
/// </summary>
[Collection("IntegrationTests")]
public class PaymentsIntegrationTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── 1. GET open-orders returns the seeded open order ─────────────────────
    [Fact]
    public async Task GetOpenOrders_Cashier_Returns200_WithOpenOrder()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "801"));
        await SeedOrderAsync("801", total: 25.50m, status: OrderStatus.Ready);

        var response = await client.GetAsync("/api/v1/payments/open-orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<List<OrderDto>>>(response);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data, o => o.TenantId == 801 && o.Total == 25.50m);
    }

    // ── 2. GET open-orders for a fresh tenant returns empty list ─────────────
    [Fact]
    public async Task GetOpenOrders_EmptyTenant_Returns200_EmptyList()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "802"));

        var response = await client.GetAsync("/api/v1/payments/open-orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<List<OrderDto>>>(response);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.DoesNotContain(result.Data, o => o.TenantId == 802);
    }

    // ── 3. Delivered and Cancelled orders are excluded from open-orders ───────
    [Fact]
    public async Task GetOpenOrders_ExcludesDeliveredAndCancelledOrders()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "803"));
        await SeedOrderAsync("803", total: 10.00m, status: OrderStatus.Delivered);
        await SeedOrderAsync("803", total: 10.00m, status: OrderStatus.Cancelled);

        var response = await client.GetAsync("/api/v1/payments/open-orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<List<OrderDto>>>(response);
        Assert.NotNull(result);
        Assert.DoesNotContain(result.Data!, o => o.TenantId == 803);
    }

    // ── 4. POST valid cash payment → 201, order no longer in open-orders ─────
    [Fact]
    public async Task ProcessPayment_CashPayment_Returns201_AndOrderMarkedDelivered()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "804"));
        var orderId = await SeedOrderAsync("804", total: 32.00m, status: OrderStatus.Ready);

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId,
            amount = 32.00m,
            method = "Cash"
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<PaymentDto>>(response);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(orderId, result.Data.OrderId);
        Assert.Equal(32.00m, result.Data.Amount);
        Assert.Equal("Cash", result.Data.Method);
        Assert.Equal("Completed", result.Data.Status);
        Assert.Equal(804, result.Data.TenantId);

        // The order must no longer appear in open-orders
        var openOrders = await DeserializeAsync<ApiResponse<List<OrderDto>>>(
            await client.GetAsync("/api/v1/payments/open-orders"));
        Assert.DoesNotContain(openOrders!.Data!, o => o.Id == orderId);
    }

    // ── 5. POST valid card payment → 201 ─────────────────────────────────────
    [Fact]
    public async Task ProcessPayment_CardPayment_Returns201()
    {
        var client = ClientWith(Jwt(Roles.Manager, "805"));
        var orderId = await SeedOrderAsync("805", total: 15.75m, status: OrderStatus.New);

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId,
            amount = 15.75m,
            method = "Card"
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<PaymentDto>>(response);
        Assert.Equal("Card", result!.Data!.Method);
    }

    // ── 6. Amount mismatch → 422 ──────────────────────────────────────────────
    [Fact]
    public async Task ProcessPayment_AmountMismatch_Returns422()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "806"));
        var orderId = await SeedOrderAsync("806", total: 20.00m, status: OrderStatus.Ready);

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId,
            amount = 19.99m,
            method = "Cash"
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    // ── 7. Paying a Delivered order → 422 ─────────────────────────────────────
    [Fact]
    public async Task ProcessPayment_OrderAlreadyDelivered_Returns422()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "807"));
        var orderId = await SeedOrderAsync("807", total: 18.00m, status: OrderStatus.Delivered);

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId,
            amount = 18.00m,
            method = "Cash"
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── 8. Paying a Cancelled order → 422 ─────────────────────────────────────
    [Fact]
    public async Task ProcessPayment_OrderCancelled_Returns422()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "808"));
        var orderId = await SeedOrderAsync("808", total: 12.00m, status: OrderStatus.Cancelled);

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId,
            amount = 12.00m,
            method = "Card"
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── 9. Order not found → 404 ──────────────────────────────────────────────
    [Fact]
    public async Task ProcessPayment_OrderNotFound_Returns404()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "809"));

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId = 99999L,
            amount = 10.00m,
            method = "Cash"
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 10. Invalid payment method → 400 ──────────────────────────────────────
    [Fact]
    public async Task ProcessPayment_InvalidMethod_Returns400()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "810"));

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId = 1L,
            amount = 10.00m,
            method = "Bitcoin"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(result!.Success);
        Assert.Equal("Validation failed", result.Message);
    }

    // ── 11. Zero amount → 400 ─────────────────────────────────────────────────
    [Fact]
    public async Task ProcessPayment_ZeroAmount_Returns400()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "811"));

        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId = 1L,
            amount = 0m,
            method = "Cash"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(result!.Success);
        Assert.Equal("Validation failed", result.Message);
    }

    // ── 12. Tenant 812 cannot pay tenant 813's order ──────────────────────────
    [Fact]
    public async Task TenantIsolation_CrossTenantOrder_Returns404()
    {
        // Seed an order belonging to tenant 813
        var orderId = await SeedOrderAsync("813", total: 50.00m, status: OrderStatus.Ready);

        // Cashier of tenant 812 attempts to pay it
        var client = ClientWith(Jwt(Roles.Cashier, "812"));
        var response = await client.PostAsync("/api/v1/payments", Json(new
        {
            orderId,
            amount = 50.00m,
            method = "Cash"
        }));

        // The EF tenant query filter hides tenant 813's order from tenant 812
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 13. No token → 401 on all payment endpoints ───────────────────────────
    [Theory]
    [InlineData("GET",  "/api/v1/payments/open-orders")]
    [InlineData("POST", "/api/v1/payments")]
    public async Task PaymentEndpoints_NoToken_Returns401(string method, string path)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = Json(new { })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 14. KitchenStaff → 403 on payment endpoints ───────────────────────────
    [Theory]
    [InlineData("GET",  "/api/v1/payments/open-orders")]
    [InlineData("POST", "/api/v1/payments")]
    public async Task PaymentEndpoints_KitchenStaff_Returns403(string method, string path)
    {
        var client = ClientWith(Jwt(Roles.KitchenStaff, "814"));
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = Json(new { })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<long> SeedOrderAsync(string tenantId, decimal total, OrderStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order
        {
            TenantId = long.Parse(tenantId),
            OrderType = "dine-in",
            TableNumber = 1,
            Status = status,
            Total = total,
            Notes = null
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
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

    private static StringContent Json(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
}
