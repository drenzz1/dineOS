using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DineOS.Tests.Unit;

/// <summary>
/// Regression tests for the post-checkout owner provisioning flow (#205).
/// The original bug: owners were created with a non-temporary password, no
/// UPDATE_PASSWORD action, and an "Owner" role that the FE's getPrimaryRole
/// didn't recognise — the emailed password became permanent and FE role
/// gating broke. The password/UPDATE_PASSWORD invariants below still lock that
/// in.
///
/// #staff-pin-auth Phase 2 deliberately reintroduces the account-level
/// <c>Owner</c> role — but safely: it is a composite over <c>Manager</c> in
/// Keycloak, so the token still carries <c>Manager</c> (FE gating works) while
/// <c>Owner</c> gates staff/billing. The password handling is unchanged. So we
/// now assert the owner is assigned <c>Owner</c> (see
/// <see cref="RunAsync_AssignsOwnerRealmRole"/>).
/// </summary>
public class OwnerProvisioningJobTests
{
    private static (OwnerProvisioningJob job, AppDbContext db, IKeycloakAdminClient admin, IBackgroundJobClient bg)
        CreateSut()
    {
        var tenantSvc = Substitute.For<ITenantService>();
        tenantSvc.TenantId.Returns((long?)null);

        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            tenantSvc);

        var admin = Substitute.For<IKeycloakAdminClient>();
        admin.CreateUserAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
             .Returns("kc-user-id");

        var bg = Substitute.For<IBackgroundJobClient>();

        var job = new OwnerProvisioningJob(
            db, admin, bg, NullLogger<OwnerProvisioningJob>.Instance);

        return (job, db, admin, bg);
    }

    private static Tenant SeedTenant(AppDbContext db)
    {
        var tenant = new Tenant
        {
            Name       = "Olio & Sale",
            Slug       = "olio-and-sale",
            OwnerName  = "Jane Doe",
            OwnerEmail = "jane@example.com",
            IsActive   = true,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    [Fact]
    public async Task RunAsync_CreatesUserWithTemporaryPasswordAndUpdatePasswordAction()
    {
        var (job, db, admin, _) = CreateSut();
        var tenant = SeedTenant(db);

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await admin.Received(1).CreateUserAsync(
            email:              "jane@example.com",
            firstName:          "Jane",
            lastName:           "Doe",
            tempPassword:       "TempPass!23",
            requiredActions:    Arg.Is<IReadOnlyList<string>>(a =>
                                    a.Count == 1 && a.Contains("UPDATE_PASSWORD")),
            temporaryPassword:  true,
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AssignsOwnerRealmRole()
    {
        var (job, db, admin, _) = CreateSut();
        var tenant = SeedTenant(db);

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        // #staff-pin-auth Phase 2: the business account is the account-level
        // Owner (composite over Manager), not a bare Manager. Operational
        // access still flows via the Owner->Manager composite in Keycloak.
        await admin.Received(1).AssignRealmRoleAsync(
            "kc-user-id", "Owner", Arg.Any<CancellationToken>());
        await admin.DidNotReceive().AssignRealmRoleAsync(
            Arg.Any<string>(), "Manager", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_SetsTenantIdAttributeForTokenClaim()
    {
        // Regression: the owner user was created without the tenant_id attribute
        // that the Keycloak protocol mapper turns into the tenant_id token claim.
        // Its absence made TenantIsolationMiddleware reject every authenticated
        // request as "Tenant context is required." — the owner could change the
        // first-login password but was then stranded, unable to load any data.
        var (job, db, admin, _) = CreateSut();
        var tenant = SeedTenant(db);

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await admin.Received(1).SetUserAttributeAsync(
            "kc-user-id", "tenant_id", tenant.Id.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_PersistsKeycloakUserIdAndEnqueuesWelcomeEmail()
    {
        var (job, db, _, bg) = CreateSut();
        var tenant = SeedTenant(db);

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        var refreshed = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenant.Id);
        Assert.Equal("kc-user-id", refreshed.KeycloakUserId);

        bg.Received(1).Create(
            Arg.Any<Hangfire.Common.Job>(),
            Arg.Any<Hangfire.States.IState>());
    }

    // Sentinel exposed by KeycloakProfileDefaults; duplicated here as a literal
    // on purpose so a silent rename of the constant would break this test.
    private const string Placeholder = "—";

    [Theory]
    // ── Empty / whitespace-only input falls back to the placeholder sentinel.
    [InlineData("",          Placeholder, Placeholder)]
    [InlineData("   ",       Placeholder, Placeholder)]
    [InlineData("\t",        Placeholder, Placeholder)]
    [InlineData("\n",        Placeholder, Placeholder)]
    // ── Single-word names: placeholder for lastName (no mirroring).
    [InlineData("test",      "test",      Placeholder)]
    [InlineData("Jane  ",    "Jane",      Placeholder)]
    [InlineData("  test  ",  "test",      Placeholder)]
    [InlineData("\tDren\t",  "Dren",      Placeholder)]
    // ── Two-word names: standard split.
    [InlineData("Jane Doe",  "Jane",      "Doe")]
    // ── Internal whitespace runs (double space, tabs) collapse cleanly.
    [InlineData("Jane  Doe", "Jane",      "Doe")]
    [InlineData("Jane\tDoe", "Jane",      "Doe")]
    // ── Three+ tokens: everything after the first becomes the lastName,
    //    joined by a single space (no token loss).
    [InlineData("Mary Anne Smith",   "Mary",   "Anne Smith")]
    [InlineData("José  de la Cruz",  "José",   "de la Cruz")]
    public async Task RunAsync_AlwaysSendsNonEmptyLastNameToKeycloak(
        string ownerName, string expectedFirst, string expectedLast)
    {
        // Regression: Keycloak's declarative user-profile (realm export sets
        // `unmanagedAttributePolicy: ENABLED`) rejects direct-grant logins
        // with "Account is not fully set up" when lastName is empty — even
        // after UPDATE_PASSWORD is cleared. Owners signing up with a single
        // word ("test") used to hit this and could never complete first-login.
        // The current contract: emit a visible placeholder ("—") in any field
        // the signup payload did not provide, rather than mirror the first
        // token (which would store fabricated surname data).
        var (job, db, admin, _) = CreateSut();
        var tenant = new Tenant
        {
            Name       = "Solo",
            Slug       = $"solo-{Guid.NewGuid():N}",
            OwnerName  = ownerName,
            OwnerEmail = $"solo-{Guid.NewGuid():N}@example.com",
            IsActive   = true,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await admin.Received(1).CreateUserAsync(
            email:              tenant.OwnerEmail,
            firstName:          expectedFirst,
            lastName:           expectedLast,
            tempPassword:       "TempPass!23",
            requiredActions:    Arg.Any<IReadOnlyList<string>>(),
            temporaryPassword:  true,
            ct:                 Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_SkipsWhenTenantAlreadyHasKeycloakUserId()
    {
        var (job, db, admin, _) = CreateSut();
        var tenant = SeedTenant(db);
        tenant.KeycloakUserId = "pre-existing";
        await db.SaveChangesAsync();

        await job.RunAsync(tenant.Id, "TempPass!23", CancellationToken.None);

        await admin.DidNotReceive().CreateUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await admin.DidNotReceive().AssignRealmRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
