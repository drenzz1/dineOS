using DineOS.Application.Common;
using DineOS.Application.DTOs;
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
/// Integration tests for /api/v1/staff — real PostgreSQL via Testcontainers.
/// Each test uses a unique tenant ID to avoid inter-test state coupling; xUnit
/// does not guarantee intra-class ordering, so no test assumes another has run first.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class StaffIntegrationTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── 1. GET returns 200 with empty list (tenant 701 has no staff) ─────────
    // Staff management is OwnerOnly (account-level), so an Owner token is used.
    [Fact]
    public async Task GetStaff_AuthenticatedOwner_Returns200_WithEmptyList()
    {
        var client = ClientWith(Jwt(Roles.Owner, "701"));

        var response = await client.GetAsync("/api/v1/staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<ApiResponse<List<StaffMemberDto>>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    // ── 2. POST valid body → 201, new member appears in subsequent GET ───────
    [Fact]
    public async Task PostStaff_ValidRequest_Returns201_AndAppearsInGet()
    {
        var client = ClientWith(Jwt(Roles.Owner, "702"));

        var postResponse = await client.PostAsync("/api/v1/staff", Json(new
        {
            fullName = "Alice Smith",
            email = "alice@dineos.dev",
            role = Roles.Cashier,
            pin = "1234"
        }));

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var created = UnwrapDto(await postResponse.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        Assert.Equal("Alice Smith", created.FullName);
        Assert.Equal("alice@dineos.dev", created.Email);
        Assert.Equal(Roles.Cashier, created.Role);
        Assert.True(created.IsActive);
        Assert.Equal(702, created.TenantId);

        // Verify the new member appears in a fresh GET
        var list = UnwrapList(await (await client.GetAsync("/api/v1/staff")).Content.ReadAsStringAsync());
        Assert.NotNull(list);
        Assert.Contains(list, s => s.Email == "alice@dineos.dev");
    }

    // ── 3. POST with missing FullName → 400 with FluentValidation body ───────
    [Fact]
    public async Task PostStaff_MissingFullName_Returns400_WithValidationErrors()
    {
        var client = ClientWith(Jwt(Roles.Owner, "703"));

        var response = await client.PostAsync("/api/v1/staff", Json(new
        {
            // fullName intentionally omitted — FluentValidation must catch it
            email = "noname@dineos.dev",
            role = Roles.Cashier,
            pin = "1234"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var result = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(
            await response.Content.ReadAsStringAsync(), JsonOpts);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Validation failed", result.Message);
        Assert.NotNull(result.Errors);
        Assert.NotEmpty(result.Errors);
    }

    // ── 4. PUT updates FullName and Role, leaves other fields unchanged ──────
    [Fact]
    public async Task PutStaff_ValidRequest_UpdatesRecord()
    {
        var client = ClientWith(Jwt(Roles.Owner, "704"));
        var created = await CreateStaffAsync(client, "Bob Jones", "bob@dineos.dev", Roles.Cashier, "5678");

        var putResponse = await client.PutAsync($"/api/v1/staff/{created.Id}", Json(new
        {
            fullName = "Bob Updated",
            role = Roles.Manager
        }));

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updated = UnwrapDto(await putResponse.Content.ReadAsStringAsync());
        Assert.NotNull(updated);
        Assert.Equal("Bob Updated", updated.FullName);
        Assert.Equal(Roles.Manager, updated.Role);
        Assert.Equal("bob@dineos.dev", updated.Email); // null in request → unchanged
    }

    // ── 5. PATCH /active sets IsActive = false, visible in GET list ──────────
    [Fact]
    public async Task PatchStaffActive_SetsFalse_ReflectedInGetList()
    {
        var client = ClientWith(Jwt(Roles.Owner, "705"));
        var created = await CreateStaffAsync(client, "Carol White", "carol@dineos.dev", Roles.KitchenStaff, "9012");

        var patchResponse = await client.PatchAsync(
            $"/api/v1/staff/{created.Id}/active", Json(new { isActive = false }));

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var patched = UnwrapDto(await patchResponse.Content.ReadAsStringAsync());
        Assert.NotNull(patched);
        Assert.False(patched.IsActive);

        // Confirm GET reflects the change (IsActive=false is returned, record still visible)
        var list = UnwrapList(await (await client.GetAsync("/api/v1/staff")).Content.ReadAsStringAsync());
        Assert.NotNull(list);
        var inList = list.FirstOrDefault(s => s.Id == created.Id);
        Assert.NotNull(inList);
        Assert.False(inList.IsActive);
    }

    // ── 6. Tenant 707 cannot see or mutate tenant 706's staff ────────────────
    [Fact]
    public async Task TenantIsolation_CrossTenantToken_CannotSeeOrModifyOtherTenantStaff()
    {
        var t706Client = ClientWith(Jwt(Roles.Owner, "706"));
        var t707Client = ClientWith(Jwt(Roles.Owner, "707"));

        // Create a staff member belonging to tenant 706
        var created = await CreateStaffAsync(t706Client, "Dave T706", "dave@dineos.dev", Roles.Cashier, "1111");

        // GET by tenant 707 — must not include tenant 706's staff
        var list = UnwrapList(
            await (await t707Client.GetAsync("/api/v1/staff")).Content.ReadAsStringAsync());
        Assert.NotNull(list);
        Assert.DoesNotContain(list, s => s.Id == created.Id);

        // PUT by tenant 707 on tenant 706's staff → 404 (query filter excludes it)
        var putResponse = await t707Client.PutAsync(
            $"/api/v1/staff/{created.Id}", Json(new { fullName = "Hacked" }));
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);

        // PATCH by tenant 707 on tenant 706's staff → 404
        var patchResponse = await t707Client.PatchAsync(
            $"/api/v1/staff/{created.Id}/active", Json(new { isActive = false }));
        Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);
    }

    // ── 7. PUT non-existent id → 404 ─────────────────────────────────────────
    [Fact]
    public async Task PutStaff_NonExistentId_Returns404()
    {
        var client = ClientWith(Jwt(Roles.Owner, "709"));

        var response = await client.PutAsync("/api/v1/staff/99999", Json(new { fullName = "Ghost" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 8. Unauthenticated → 401 on staff endpoints ───────────────────────────
    [Theory]
    [InlineData("GET",   "/api/v1/staff")]
    [InlineData("POST",  "/api/v1/staff")]
    [InlineData("PUT",   "/api/v1/staff/1")]
    [InlineData("PATCH", "/api/v1/staff/1/active")]
    public async Task StaffEndpoints_NoToken_Returns401(string method, string path)
    {
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = Json(new { })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 9–11. Cashier → 403 on all staff write endpoints ─────────────────────
    [Theory]
    [InlineData("POST",  "/api/v1/staff")]
    [InlineData("PUT",   "/api/v1/staff/99999")]
    [InlineData("PATCH", "/api/v1/staff/99999/active")]
    public async Task StaffWriteEndpoints_CashierRole_Returns403(string method, string path)
    {
        // Authorization policy check fires before the action, so staff ID 99999 need not exist
        var client = ClientWith(Jwt(Roles.Cashier, "708"));

        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = Json(new { })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<StaffMemberDto> CreateStaffAsync(
        HttpClient client, string fullName, string email, string role, string pin)
    {
        var response = await client.PostAsync("/api/v1/staff", Json(new
        {
            fullName, email, role, pin
        }));
        response.EnsureSuccessStatusCode();
        var dto = UnwrapDto(await response.Content.ReadAsStringAsync());
        return dto!;
    }

    private StaffMemberDto? UnwrapDto(string body) =>
        JsonSerializer.Deserialize<ApiResponse<StaffMemberDto>>(body, JsonOpts)?.Data;

    private List<StaffMemberDto>? UnwrapList(string body) =>
        JsonSerializer.Deserialize<ApiResponse<List<StaffMemberDto>>>(body, JsonOpts)?.Data;

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
}
