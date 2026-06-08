using System.Net;
using System.Text;
using System.Text.Json;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using DineOS.Tests.Common;
using DineOS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DineOS.Tests.Integration.LiveKeycloak;

/// <summary>
/// Regression coverage for the "Account is not fully set up" first-login bug
/// (single-word owner names produced an empty <c>lastName</c>, which
/// Keycloak's declarative user profile rejects on direct-grant login even
/// after the <c>UPDATE_PASSWORD</c> required action is cleared).
///
/// Unit tests only verified what <c>OwnerProvisioningJob</c> sends to a
/// mocked admin client — they could not catch the case where Keycloak
/// itself rejects the payload. This test runs the full provisioning →
/// first-login → login flow against a real Keycloak Testcontainer.
/// </summary>
[Collection("LiveAuth")]
[Trait(Traits.Category, Traits.LiveAuth)]
public class LiveOwnerProvisioningTests : IAsyncLifetime
{
    private readonly LiveKeycloakWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public LiveOwnerProvisioningTests(KeycloakContainerFixture keycloak)
    {
        _factory = new LiveKeycloakWebApplicationFactory(keycloak);
    }

    public Task InitializeAsync() => ((IAsyncLifetime)_factory).InitializeAsync();
    public Task DisposeAsync()    => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task SingleWordOwnerName_CompletesFirstLoginAndAuthenticates()
    {
        // Use a unique email per run so the test is deterministic regardless of
        // whether a previous run left a user behind in the realm.
        var email = $"single-word-{Guid.NewGuid():N}@example.com";
        const string tempPassword = "TempPass!23456";
        const string newPassword  = "NewPermanent!9876";

        // ── 1) Seed a tenant with a single-word OwnerName ────────────────────
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant
            {
                Name       = "Solo",
                Slug       = $"solo-{Guid.NewGuid():N}",
                OwnerName  = "test",
                OwnerEmail = email,
                IsActive   = true,
            });
            await db.SaveChangesAsync();
        }

        // ── 2) Run OwnerProvisioningJob against the live Keycloak ────────────
        long tenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .FirstAsync(t => t.OwnerEmail == email);
            tenantId = tenant.Id;

            var job = scope.ServiceProvider.GetRequiredService<OwnerProvisioningJob>();
            await job.RunAsync(tenantId, tempPassword, CancellationToken.None);

            var refreshed = await db.Tenants
                .IgnoreQueryFilters()
                .FirstAsync(t => t.Id == tenantId);
            Assert.False(string.IsNullOrEmpty(refreshed.KeycloakUserId),
                "OwnerProvisioningJob must persist the Keycloak user id.");
        }

        var client = _factory.CreateClient();

        // ── 3) Direct login before first-login completes must be rejected ────
        // Keycloak returns "Account is not fully set up" because of the
        // pending UPDATE_PASSWORD required action; the auth service maps it
        // to a non-success result.
        var earlyLogin = await client.PostAsync(
            "/api/v1/auth/login",
            Json(new { username = email, password = tempPassword }));
        Assert.NotEqual(HttpStatusCode.OK, earlyLogin.StatusCode);

        // ── 4) Complete first-login: this clears UPDATE_PASSWORD and resets ──
        var firstLogin = await client.PostAsync(
            "/api/v1/auth/first-login-password-change",
            Json(new
            {
                email,
                currentPassword = tempPassword,
                newPassword,
            }));

        Assert.True(
            firstLogin.IsSuccessStatusCode,
            $"first-login-password-change must succeed. Status={firstLogin.StatusCode} " +
            $"Body={await firstLogin.Content.ReadAsStringAsync()}");

        using (var doc = JsonDocument.Parse(await firstLogin.Content.ReadAsStringAsync()))
        {
            var data = doc.RootElement.GetProperty("data");
            Assert.False(string.IsNullOrEmpty(data.GetProperty("accessToken").GetString()));
            Assert.False(string.IsNullOrEmpty(data.GetProperty("refreshToken").GetString()));
        }

        // ── 5) A subsequent standard login must also succeed ─────────────────
        // This is what failed in the regression: even with UPDATE_PASSWORD
        // cleared, Keycloak rejected the login with "Account is not fully
        // set up" because lastName was empty.
        var login = await client.PostAsync(
            "/api/v1/auth/login",
            Json(new { username = email, password = newPassword }));

        Assert.True(
            login.IsSuccessStatusCode,
            $"Standard login after first-login must succeed (regression: empty " +
            $"lastName caused 'Account is not fully set up'). Status={login.StatusCode} " +
            $"Body={await login.Content.ReadAsStringAsync()}");
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
}
