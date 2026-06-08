using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Auth;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Provisions a Keycloak user for a demo access request (#216): creates the
/// user, stamps the demo tenant id attribute, assigns the configured realm
/// role, then enqueues the welcome email. Idempotent: short-circuits if the
/// <c>DemoUser</c> already has a <c>KeycloakUserId</c>; rotates the password
/// instead so the email can carry a fresh value.
/// </summary>
public sealed class DemoProvisioningJob(
    AppDbContext db,
    IKeycloakAdminClient keycloakAdmin,
    IBackgroundJobClient backgroundJobs,
    IOptions<DemoOptions> demoOptions,
    ILogger<DemoProvisioningJob> logger)
{
    // Display-name fields for demo users. Demo accounts have no per-user
    // identity collected at request time, so we use fixed literals — but we
    // intentionally route them through KeycloakProfileDefaults rather than
    // hard-coding string constants in the CreateUserAsync call so this job
    // cannot drift from OwnerProvisioningJob's "always non-empty" invariant.
    // If KeycloakProfileDefaults.MissingFieldPlaceholder changes (or the
    // realm tightens its user-profile validators), both jobs evolve together.
    private const string DemoFirstName = "Demo";
    private const string DemoLastName = "User";

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

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == opts.TenantSlug && t.DeletedAt == null, ct);

        if (tenant is null)
        {
            logger.LogError(
                "Demo provisioning aborted — demo tenant slug '{Slug}' not found. Configure Demo:TenantSlug or seed the tenant.",
                opts.TenantSlug);
            throw new InvalidOperationException(
                $"Demo tenant '{opts.TenantSlug}' is not seeded.");
        }

        string keycloakUserId;
        if (demoUser.KeycloakUserId is null)
        {
            // Guard: both fields must be non-empty per
            // KeycloakProfileDefaults.SplitDisplayName's contract — assert it
            // here too so a future "edit the constants" pass cannot silently
            // re-introduce the empty-lastName "Account is not fully set up"
            // bug that broke OwnerProvisioningJob for single-word owners.
            System.Diagnostics.Debug.Assert(
                !string.IsNullOrWhiteSpace(DemoFirstName) &&
                !string.IsNullOrWhiteSpace(DemoLastName),
                "Demo first/last names must be non-empty — Keycloak's declarative " +
                "user-profile rejects empty values with 'Account is not fully set up'.");

            keycloakUserId = await keycloakAdmin.CreateUserAsync(
                email:              demoUser.Email,
                firstName:          DemoFirstName,
                lastName:           DemoLastName,
                tempPassword:       tempPassword,
                requiredActions:    Array.Empty<string>(),
                // Demo: emailed creds ARE the credential — UPDATE_PASSWORD
                // would break the frontend's password-grant login.
                temporaryPassword:  false,
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

        demoUser.Status = DemoUserStatus.Active;
        await db.SaveChangesAsync(ct);

        backgroundJobs.Enqueue<DemoWelcomeEmailJob>(
            job => job.SendAsync(demoUser.Id, tempPassword, isReissue: false, CancellationToken.None));

        logger.LogInformation(
            "Demo provisioned. DemoUserId={DemoUserId} KeycloakUserId={KeycloakUserId} Email={Email}",
            demoUser.Id, keycloakUserId, demoUser.Email);
    }
}
