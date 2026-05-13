using DineOS.Tests.Common;
using DineOS.Tests.Fixtures;
using System.Net;

namespace DineOS.Tests.Integration.LiveKeycloak;

/// <summary>
/// Live RBAC integration tests — every token is a real Keycloak JWT; no symmetric-key shortcuts.
///
/// Policy matrix verified against controller [Authorize] attributes:
///   SuperAdminOnly   = RequireRole("SuperAdmin")
///   ManagerAndAbove  = RequireRole("SuperAdmin", "Manager")
///   CashierAndAbove  = RequireRole("SuperAdmin", "Manager", "Cashier")
///   KitchenStaffOnly = RequireRole("KitchenStaff")
///
/// TenantIsolationMiddleware: SuperAdmin bypassed; all others use tenant_id JWT claim
/// (all seeded non-admin users have tenant_id=1 — no X-Tenant-ID header required).
/// </summary>
[Collection("LiveAuth")]
[Trait(Traits.Category, Traits.LiveAuth)]
public class LiveRbacTests : IAsyncLifetime
{
    private readonly LiveKeycloakWebApplicationFactory _factory;
    private readonly KeycloakTokenClient _tokenClient;

    public LiveRbacTests(KeycloakContainerFixture keycloak)
    {
        _factory     = new LiveKeycloakWebApplicationFactory(keycloak);
        _tokenClient = new KeycloakTokenClient(keycloak);
    }

    public Task InitializeAsync() => ((IAsyncLifetime)_factory).InitializeAsync();

    public async Task DisposeAsync()
    {
        _tokenClient.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    // ── SuperAdmin ────────────────────────────────────────────────────────────────

    /// <summary>
    /// AdminRestaurantsController carries [Authorize(Policy = "SuperAdminOnly")] at class level.
    /// SuperAdmin has no tenant_id claim; TenantIsolationMiddleware bypasses SuperAdmin entirely.
    /// Fresh DB → empty list → 200 OK.
    /// </summary>
    [Fact]
    public async Task SuperAdmin_SuperAdminOnlyEndpoint_Returns200()
    {
        var token  = await _tokenClient.GetSuperAdminTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/admin/restaurants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// StaffController carries [Authorize(Policy = "ManagerAndAbove")].
    /// SuperAdmin satisfies ManagerAndAbove (RequireRole includes "SuperAdmin").
    /// TenantIsolationMiddleware bypasses SuperAdmin, so no tenant header is needed.
    /// We assert not-403 — business-layer result may be 200 with an empty list.
    /// </summary>
    [Fact]
    public async Task SuperAdmin_ManagerAndAboveEndpoint_IsNotForbidden()
    {
        var token  = await _tokenClient.GetSuperAdminTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/staff");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Manager ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// MenuController.GetMenu carries [Authorize(Policy = "ManagerAndAbove")].
    /// manager@dineos.dev has the Manager role and tenant_id=1 (JWT claim).
    /// TenantIsolationMiddleware accepts the tenant_id claim without an explicit header.
    /// Fresh DB → empty list → 200 OK.
    /// </summary>
    [Fact]
    public async Task Manager_ManagerAndAboveEndpoint_Returns200()
    {
        var token  = await _tokenClient.GetManagerTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/menu");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// AdminRestaurantsController is SuperAdminOnly. Manager is not in that policy.
    /// Authorization middleware short-circuits with 403 before any service logic runs.
    /// </summary>
    [Fact]
    public async Task Manager_SuperAdminOnlyEndpoint_Returns403()
    {
        var token  = await _tokenClient.GetManagerTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/admin/restaurants");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Cashier ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// OrdersController carries [Authorize(Policy = "CashierAndAbove")] at class level.
    /// cashier@dineos.dev has the Cashier role and tenant_id=1 (JWT claim).
    /// Fresh DB → empty list → 200 OK.
    /// </summary>
    [Fact]
    public async Task Cashier_CashierAndAboveEndpoint_Returns200()
    {
        var token  = await _tokenClient.GetCashierTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// MenuController.GetMenu is ManagerAndAbove — Cashier is not in RequireRole("SuperAdmin","Manager").
    /// Authorization middleware short-circuits with 403.
    /// </summary>
    [Fact]
    public async Task Cashier_ManagerAndAboveEndpoint_Returns403()
    {
        var token  = await _tokenClient.GetCashierTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/menu");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── KitchenStaff ──────────────────────────────────────────────────────────────

    /// <summary>
    /// KitchenController carries [Authorize(Policy = "KitchenStaffOnly")] at class level.
    /// kitchen@dineos.dev has the KitchenStaff role and tenant_id=1 (JWT claim).
    /// Fresh DB → empty list → 200 OK.
    /// </summary>
    [Fact]
    public async Task KitchenStaff_KitchenStaffOnlyEndpoint_Returns200()
    {
        var token  = await _tokenClient.GetKitchenStaffTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.GetAsync("/api/v1/kitchen/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// MenuController.CreateMenuItem carries [Authorize(Policy = "ManagerAndAbove")].
    /// Authorization runs before model binding — an empty body is fine here, the 403
    /// is returned before FluentValidation or the service layer is reached.
    /// </summary>
    [Fact]
    public async Task KitchenStaff_ManagerAndAboveEndpoint_Returns403()
    {
        var token  = await _tokenClient.GetKitchenStaffTokenAsync();
        var client = _factory.CreateClient().WithBearer(token);

        var response = await client.PostAsync(
            "/api/v1/menu/items",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
