using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Auth;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Provisions the tenant owner's Keycloak account after a Stripe checkout
/// completes (#205): creates the user with a temporary password gated by
/// the <c>UPDATE_PASSWORD</c> required action, persists
/// <c>KeycloakUserId</c>, assigns the account-level <c>Owner</c> realm role,
/// then enqueues the welcome email. <c>Owner</c> is a composite over
/// <c>Manager</c> in Keycloak, so the JWT still carries <c>Manager</c> for the
/// operational <c>[Authorize]</c> policies and the FE role enum, while
/// <c>Owner</c> gates account-level capabilities (staff, billing).
/// </summary>
/// <remarks>
/// Lives in Hangfire (not inline in the webhook) because the webhook
/// commits a Stripe-event dedupe row before dispatching. If provisioning
/// failed in the request path, the retry would be no-op'd by the dedupe
/// and the user would never be created. Hangfire owns the retry budget.
/// Idempotent: short-circuits if <c>KeycloakUserId</c> is already set.
/// </remarks>
public sealed class OwnerProvisioningJob(
    AppDbContext db,
    IKeycloakAdminClient keycloakAdmin,
    IBackgroundJobClient backgroundJobs,
    ILogger<OwnerProvisioningJob> logger)
{
    // The business account gets the account-level Owner role (#staff-pin-auth
    // Phase 2): it gates staff + billing. Owner is a composite over Manager in
    // Keycloak, so the token also carries Manager — the owner login keeps full
    // operational access and the FE's getPrimaryRole resolves to Manager. This
    // composite is permanent by design (decided 2026-05-31): owners operate
    // directly, and PIN-issued staff sessions provide role-scoped switching on
    // shared terminals. (It also sidesteps the historical "Owner broke FE role
    // gating" bug, which was the empty role claim, not the role name.)
    private const string OwnerRoleName = "Owner";

    // Emailed password must be rotated on first login. UPDATE_PASSWORD is
    // attached as a Keycloak required action so the credential cannot be
    // used permanently. /api/v1/auth/login surfaces the resulting
    // "Account is not fully set up" error so the FE can route the owner
    // through the dedicated first-login password-change flow.
    private static readonly IReadOnlyList<string> OwnerRequiredActions =
        new[] { "UPDATE_PASSWORD" };

    [AutomaticRetry(
        Attempts = 5,
        DelaysInSeconds = new[] { 10, 30, 90, 300, 900 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(long tenantId, string tempPassword, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
        {
            logger.LogWarning(
                "Owner provisioning skipped — tenant not found. TenantId={TenantId}",
                tenantId);
            return;
        }

        if (tenant.KeycloakUserId is not null)
        {
            logger.LogInformation(
                "Owner provisioning skipped — tenant already has a Keycloak user. TenantId={TenantId} KeycloakUserId={KeycloakUserId}",
                tenant.Id, tenant.KeycloakUserId);
            return;
        }

        // Delegated to the shared helper so this job and DemoProvisioningJob
        // share one source of truth for "Keycloak requires non-empty
        // firstName/lastName". See KeycloakProfileDefaults for the constraint
        // rationale and the sentinel-vs-mirror decision.
        var (firstName, lastName) = KeycloakProfileDefaults.SplitDisplayName(tenant.OwnerName);

        // The emailed password is created as TEMPORARY with the
        // UPDATE_PASSWORD required action. The owner cannot use it as a
        // permanent credential — they must complete the first-login
        // password-change flow (/api/v1/auth/first-login-password-change)
        // before standard login works.
        var userId = await keycloakAdmin.CreateUserAsync(
            email:              tenant.OwnerEmail,
            firstName:          firstName,
            lastName:           lastName,
            tempPassword:       tempPassword,
            requiredActions:    OwnerRequiredActions,
            temporaryPassword:  true,
            ct);

        // CreateUserAsync recovers from a 409 (email already in Keycloak) by
        // returning the existing user's id without touching their password.
        // Explicitly reset here so the welcome email's temp password always
        // matches the Keycloak credential — for both new users and the
        // conflict-recovery path.
        await keycloakAdmin.ResetPasswordAsync(userId, tempPassword, temporary: true, ct);
        await keycloakAdmin.SetRequiredActionsAsync(userId, OwnerRequiredActions, ct);

        tenant.KeycloakUserId = userId;
        await db.SaveChangesAsync(ct);

        // The tenant_id user attribute is mapped to the tenant_id token claim by
        // a Keycloak protocol mapper (realm-export.json). Without it the owner's
        // JWT carries no tenant_id, and TenantIsolationMiddleware rejects every
        // authenticated request with "Tenant context is required." — including
        // the auto-login that follows the first-login password change, which
        // stranded the owner on the set-password screen. DemoProvisioningJob
        // already does this; owner provisioning must too.
        await keycloakAdmin.SetUserAttributeAsync(
            userId, "tenant_id", tenant.Id.ToString(), ct);

        await keycloakAdmin.AssignRealmRoleAsync(userId, OwnerRoleName, ct);

        // If this email previously held a demo account, retire it now.
        // DemoCleanupJob would otherwise call SetUserEnabledAsync(false)
        // on the same Keycloak user once the demo TTL expires, locking out
        // the paid owner.
        var demoUser = await db.DemoUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                d => d.Email == tenant.OwnerEmail
                  && d.Status == DemoUserStatus.Active,
                ct);

        if (demoUser is not null)
        {
            demoUser.Status = DemoUserStatus.Expired;
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Demo record expired on paid conversion. DemoUserId={DemoUserId} Email={OwnerEmail}",
                demoUser.Id, tenant.OwnerEmail);
        }

        backgroundJobs.Enqueue<OwnerWelcomeEmailJob>(
            job => job.SendAsync(tenant.Id, tempPassword, CancellationToken.None));

        logger.LogInformation(
            "Owner provisioned: TenantId={TenantId} KeycloakUserId={KeycloakUserId} Email={OwnerEmail}",
            tenant.Id, userId, tenant.OwnerEmail);
    }
}
