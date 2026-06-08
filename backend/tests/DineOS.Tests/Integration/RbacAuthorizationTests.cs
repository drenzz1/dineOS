using DineOS.Application.Authorization;
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
/// End-to-end authorization tests covering:
///   - Role-based access (RBAC policies)
///   - Tenant isolation (TenantIsolationMiddleware)
///   - Structured 401/403 error bodies (UseStatusCodePages)
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class RbacAuthorizationTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── 1. SuperAdmin can access platform admin endpoint ──────────────────
    [Fact]
    public async Task SuperAdmin_AdminEndpoint_Returns200()
    {
        var client = ClientWith(GenerateTestJwt(Roles.SuperAdmin));

        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── 2. Manager rejected from SuperAdmin-only endpoint ─────────────────
    [Fact]
    public async Task Manager_SuperAdminOnlyEndpoint_Returns403_WithStructuredError()
    {
        var client = ClientWith(GenerateTestJwt(Roles.Manager, "1"));

        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Equal("Forbidden", body.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("correlationId").GetString()));
    }

    // ── 3a. Cashier can create an order ───────────────────────────────────
    [Fact]
    public async Task Cashier_CreateOrder_Returns201()
    {
        var client = ClientWith(GenerateTestJwt(Roles.Cashier, "1"));
        client.DefaultRequestHeaders.Add("X-Tenant-ID", "1");

        var payload = JsonSerializer.Serialize(new
        {
            orderType = "pickup",
            notes = (string?)null,
            items = new[]
            {
                new { name = "Burger", quantity = 1, unitPrice = 9.99m, notes = (string?)null }
            }
        });
        var response = await client.PostAsync("/api/v1/orders",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── 3b. Cashier rejected from staff management ────────────────────────
    [Fact]
    public async Task Cashier_StaffManagementEndpoint_Returns403()
    {
        var client = ClientWith(GenerateTestJwt(Roles.Cashier, "1"));

        var response = await client.GetAsync("/api/v1/staff");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── 4a. KitchenStaff can access kitchen workflows ─────────────────────
    [Fact]
    public async Task KitchenStaff_KitchenOrders_Returns200()
    {
        var client = ClientWith(GenerateTestJwt(Roles.KitchenStaff, "1"));

        var response = await client.GetAsync("/api/v1/kitchen/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    public async Task OperationalRole_KitchenOrders_Returns200(string role)
    {
        var client = ClientWith(GenerateTestJwt(role, "1"));

        var response = await client.GetAsync("/api/v1/kitchen/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task KitchenStaff_ShiftsRead_Returns200()
    {
        var client = ClientWith(GenerateTestJwt(Roles.KitchenStaff, "1"));

        var response = await client.GetAsync("/api/v1/shifts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task KitchenStaff_ShiftsWrite_Returns403()
    {
        var client = ClientWith(GenerateTestJwt(Roles.KitchenStaff, "1"));

        var response = await client.PostAsync(
            "/api/v1/shifts",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── 4b. KitchenStaff rejected from other role-scoped endpoints ────────
    [Theory]
    [InlineData("GET",  "/api/v1/admin/users")]     // SuperAdminOnly
    [InlineData("GET",  "/api/v1/staff")]            // OwnerOnly
    [InlineData("GET",  "/api/v1/menu")]             // ManagerAndAbove
    [InlineData("GET",  "/api/v1/reports/sales")]    // ManagerAndAbove
    [InlineData("GET",  "/api/v1/orders")]           // CashierAndAbove
    public async Task KitchenStaff_OtherRoleEndpoints_Returns403(string method, string path)
    {
        var client = ClientWith(GenerateTestJwt(Roles.KitchenStaff, "1"));

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── 5. Cross-tenant: valid role, mismatched tenant header → 403 ───────
    [Fact]
    public async Task Manager_XTenantIdHeaderMismatch_Returns403_CrossTenantIsolation()
    {
        // JWT claims tenant_id=1 but header asserts tenant 2 — middleware must reject.
        // Use a ManagerAndAbove endpoint (/api/v1/menu) so the request clears the role
        // gate and the TenantIsolationMiddleware mismatch check is what produces the 403
        // (staff endpoints are OwnerOnly and would 403 a Manager at the role gate first).
        var client = ClientWith(GenerateTestJwt(Roles.Manager, "1"));
        client.DefaultRequestHeaders.Add("X-Tenant-ID", "2");

        var response = await client.GetAsync("/api/v1/menu");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("mismatch", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── 6. Unauthenticated → 401 with standardized JSON error body ────────
    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/admin/tenants")]
    [InlineData("/api/v1/orders")]
    [InlineData("/api/v1/kitchen/orders")]
    public async Task Unauthenticated_AnyProtectedEndpoint_Returns401_WithStructuredError(string path)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.Equal("Unauthorized", body.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("timestamp").GetString()));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a signed JWT for the given role.
    /// SuperAdmin does not need a tenantId — TenantIsolationMiddleware bypasses it.
    /// All other roles must supply tenantId or they will be rejected by the middleware.
    /// </summary>
    private static string GenerateTestJwt(string role, string? tenantId = null)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));

        // realm_access claim is parsed by KeycloakRolesTransformation into ClaimTypes.Role
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

    private HttpClient ClientWith(string jwt)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }
}
