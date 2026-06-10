using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Infrastructure.Persistence.Seed;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Provisions an isolated demo tenant + Keycloak user for a demo access
/// request (#216). Each requester gets their own tenant seeded with the full
/// demo data set, so users never share data. Idempotent on the Keycloak side:
/// if the user already exists the password is rotated and the tenant updated.
/// On re-provision (expired user re-requests), the previous tenant is
/// soft-deleted and a fresh one is created.
/// </summary>
public sealed class DemoProvisioningJob(
    AppDbContext db,
    IKeycloakAdminClient keycloakAdmin,
    IPinHasher pinHasher,
    IBackgroundJobClient backgroundJobs,
    IOptions<DemoOptions> demoOptions,
    ILogger<DemoProvisioningJob> logger)
{
    private const string DemoFirstName = "Demo";
    private const string DemoLastName  = "User";

    [AutomaticRetry(
        Attempts = 5,
        DelaysInSeconds = new[] { 10, 30, 90, 300, 900 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(long demoUserId, string tempPassword, CancellationToken ct)
    {
        var demoUser = await db.DemoUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == demoUserId && d.DeletedAt == null, ct);

        if (demoUser is null)
        {
            logger.LogWarning(
                "Demo provisioning skipped — DemoUser not found. DemoUserId={DemoUserId}",
                demoUserId);
            return;
        }

        var opts = demoOptions.Value;

        // Soft-delete the previous demo tenant so it doesn't accumulate across
        // re-provisions (expired user re-requesting demo access).
        if (demoUser.TenantId is not null)
        {
            var oldTenant = await db.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    t => t.Id == demoUser.TenantId && t.DeletedAt == null, ct);
            if (oldTenant is not null)
            {
                oldTenant.DeletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Demo re-provision: soft-deleted previous tenant. TenantId={TenantId} DemoUserId={DemoUserId}",
                    oldTenant.Id, demoUserId);
            }
        }

        // Create an isolated tenant for this demo user.
        var slug = $"demo-{Guid.NewGuid():N}"[..20];
        var tenant = new Tenant
        {
            Name       = "Demo Restaurant",
            Slug       = slug,
            IsActive   = true,
            OwnerName  = "Demo",
            // Placeholder owner email — not the requester's real email so that
            // SignupService and DemoAccessService guards don't treat this as a
            // paid owner or a pending-payment tenant.
            OwnerEmail = $"owner@{slug}.local",
            Phone      = "+1 555 000 0000",
            City       = "Tirana",
            Plan       = SubscriptionPlan.Pro,
            CreatedAt  = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        await DemoDataSeeder.SeedAsync(db, pinHasher, tenant.Id, ct);

        // Provision (or reset) the Keycloak user.
        string keycloakUserId;
        if (demoUser.KeycloakUserId is null)
        {
            System.Diagnostics.Debug.Assert(
                !string.IsNullOrWhiteSpace(DemoFirstName) &&
                !string.IsNullOrWhiteSpace(DemoLastName),
                "Demo first/last names must be non-empty.");

            keycloakUserId = await keycloakAdmin.CreateUserAsync(
                email:             demoUser.Email,
                firstName:         DemoFirstName,
                lastName:          DemoLastName,
                tempPassword:      tempPassword,
                requiredActions:   Array.Empty<string>(),
                temporaryPassword: false,
                ct);
            demoUser.KeycloakUserId = keycloakUserId;
        }
        else
        {
            keycloakUserId = demoUser.KeycloakUserId;
            await keycloakAdmin.SetUserEnabledAsync(keycloakUserId, enabled: true, ct);
            await keycloakAdmin.ResetPasswordAsync(keycloakUserId, tempPassword, temporary: false, ct);
        }

        await keycloakAdmin.SetUserAttributeAsync(
            keycloakUserId, "tenant_id", tenant.Id.ToString(), ct);

        await keycloakAdmin.AssignRealmRoleAsync(keycloakUserId, opts.RealmRole, ct);

        demoUser.TenantId = tenant.Id;
        demoUser.Status   = DemoUserStatus.Active;
        await db.SaveChangesAsync(ct);

        backgroundJobs.Enqueue<DemoWelcomeEmailJob>(
            job => job.SendAsync(demoUser.Id, tempPassword, isReissue: false, CancellationToken.None));

        logger.LogInformation(
            "Demo provisioned. DemoUserId={DemoUserId} TenantId={TenantId} KeycloakUserId={KeycloakUserId} Email={Email}",
            demoUser.Id, tenant.Id, keycloakUserId, demoUser.Email);
    }
}
