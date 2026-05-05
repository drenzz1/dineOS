using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Integration;

/// <summary>
/// Integration tests for /api/v1/admin/restaurants.
/// Uses the seeded Demo Restaurant (Id=1) for read and patch operations.
/// Create tests use unique names to avoid slug conflicts across runs.
/// </summary>
[Collection("IntegrationTests")]
public class AdminRestaurantsIntegrationTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── List ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRestaurants_SuperAdmin_Returns200WithPagedList()
    {
        var client = SuperAdminClient();

        var response = await client.GetAsync("/api/v1/admin/restaurants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<ApiResponse<PagedResponse<RestaurantDto>>>(response);
        Assert.True(body!.Success);
        Assert.True(body.Data!.TotalCount >= 1);
        Assert.NotEmpty(body.Data.Items);
    }

    [Fact]
    public async Task GetRestaurants_SearchByName_ReturnsMatchingResults()
    {
        var client = SuperAdminClient();

        var response = await client.GetAsync("/api/v1/admin/restaurants?search=Demo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<ApiResponse<PagedResponse<RestaurantDto>>>(response);
        Assert.True(body!.Data!.Items.All(r => r.Name.Contains("Demo", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GetRestaurants_NonExistentSearch_ReturnsEmptyList()
    {
        var client = SuperAdminClient();

        var response = await client.GetAsync("/api/v1/admin/restaurants?search=ZZZNORESULT999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<ApiResponse<PagedResponse<RestaurantDto>>>(response);
        Assert.Empty(body!.Data!.Items);
        Assert.Equal(0, body.Data.TotalCount);
    }

    [Fact]
    public async Task GetRestaurants_ManagerRole_Returns403()
    {
        var client = ClientWith(GenerateJwt("Manager", "1"));

        var response = await client.GetAsync("/api/v1/admin/restaurants");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRestaurants_Unauthenticated_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/restaurants");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Get by ID ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRestaurant_SeededId_Returns200WithCorrectDto()
    {
        var client = SuperAdminClient();

        var response = await client.GetAsync("/api/v1/admin/restaurants/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<ApiResponse<RestaurantDto>>(response);
        Assert.True(body!.Success);
        Assert.Equal(1L, body.Data!.Id);
        Assert.Equal("Demo Restaurant", body.Data.Name);
        Assert.Equal("Active", body.Data.Status);
    }

    [Fact]
    public async Task GetRestaurant_NonExistentId_Returns404()
    {
        var client = SuperAdminClient();

        var response = await client.GetAsync("/api/v1/admin/restaurants/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostRestaurant_ValidPayload_Returns201WithDto()
    {
        var client = SuperAdminClient();
        var name = $"Test Restaurant {Guid.NewGuid():N}";
        var payload = JsonContent(new
        {
            name,
            ownerName = "Test Owner",
            ownerEmail = "owner@test-integration.com",
            phone = "+355 69 111 2222",
            city = "Tirana",
            plan = "Free"
        });

        var response = await client.PostAsync("/api/v1/admin/restaurants", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await DeserializeAsync<ApiResponse<RestaurantDto>>(response);
        Assert.True(body!.Success);
        Assert.Equal(name, body.Data!.Name);
        Assert.Equal("Active", body.Data.Status);
        Assert.Equal("Free", body.Data.Plan);
    }

    [Fact]
    public async Task PostRestaurant_MissingName_Returns400()
    {
        var client = SuperAdminClient();
        var payload = JsonContent(new
        {
            ownerName = "Owner",
            ownerEmail = "o@example.com",
            phone = "+1 555 000",
            city = "City",
            plan = "Free"
        });

        var response = await client.PostAsync("/api/v1/admin/restaurants", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRestaurant_InvalidEmail_Returns400()
    {
        var client = SuperAdminClient();
        var payload = JsonContent(new
        {
            name = "Some Restaurant",
            ownerName = "Owner",
            ownerEmail = "not-an-email",
            phone = "+1 555 000",
            city = "City",
            plan = "Free"
        });

        var response = await client.PostAsync("/api/v1/admin/restaurants", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRestaurant_InvalidPlan_Returns400()
    {
        var client = SuperAdminClient();
        var payload = JsonContent(new
        {
            name = "Some Restaurant",
            ownerName = "Owner",
            ownerEmail = "o@example.com",
            phone = "+1 555 000",
            city = "City",
            plan = "Enterprise"
        });

        var response = await client.PostAsync("/api/v1/admin/restaurants", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Update status ─────────────────────────────────────────────────────

    [Fact]
    public async Task PatchStatus_SuspendAndReactivate_Returns200WithUpdatedStatus()
    {
        var client = SuperAdminClient();

        var suspend = await client.PatchAsync("/api/v1/admin/restaurants/1/status",
            JsonContent(new { status = "Suspended" }));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspendBody = await DeserializeAsync<ApiResponse<RestaurantDto>>(suspend);
        Assert.Equal("Suspended", suspendBody!.Data!.Status);

        var reactivate = await client.PatchAsync("/api/v1/admin/restaurants/1/status",
            JsonContent(new { status = "Active" }));
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        var reactivateBody = await DeserializeAsync<ApiResponse<RestaurantDto>>(reactivate);
        Assert.Equal("Active", reactivateBody!.Data!.Status);
    }

    [Fact]
    public async Task PatchStatus_InvalidStatus_Returns400()
    {
        var client = SuperAdminClient();

        var response = await client.PatchAsync("/api/v1/admin/restaurants/1/status",
            JsonContent(new { status = "Deleted" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_NonExistentId_Returns404()
    {
        var client = SuperAdminClient();

        var response = await client.PatchAsync("/api/v1/admin/restaurants/99999/status",
            JsonContent(new { status = "Active" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Update plan ───────────────────────────────────────────────────────

    [Fact]
    public async Task PatchPlan_ValidPlan_Returns200WithUpdatedPlan()
    {
        var client = SuperAdminClient();

        var response = await client.PatchAsync("/api/v1/admin/restaurants/1/plan",
            JsonContent(new { plan = "Free" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<RestaurantDto>>(response);
        Assert.Equal("Free", body!.Data!.Plan);
    }

    [Fact]
    public async Task PatchPlan_InvalidPlan_Returns400()
    {
        var client = SuperAdminClient();

        var response = await client.PatchAsync("/api/v1/admin/restaurants/1/plan",
            JsonContent(new { plan = "Enterprise" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchPlan_NonExistentId_Returns404()
    {
        var client = SuperAdminClient();

        var response = await client.PatchAsync("/api/v1/admin/restaurants/99999/plan",
            JsonContent(new { plan = "Pro" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private HttpClient SuperAdminClient() => ClientWith(GenerateJwt("SuperAdmin"));

    private HttpClient ClientWith(string jwt)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    private static string GenerateJwt(string role, string? tenantId = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));

        var claims = new List<Claim>
        {
            new("sub",   $"test-{role.ToLower()}"),
            new("email", $"{role.ToLower()}@dineos.dev"),
            new("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } }))
        };

        if (tenantId is not null)
            claims.Add(new Claim("tenant_id", tenantId));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
}
