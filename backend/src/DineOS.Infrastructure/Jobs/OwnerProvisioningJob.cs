using DineOS.Application.Interfaces.Services;
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
    // Phase 2): it can manage staff + billing, while operational work is done
    // per-shift via PIN-issued staff sessions. Owner is a composite over
    // Manager in Keycloak, so the token still carries Manager — operational
    // policies (ManagerAndAbove/CashierAndAbove) keep passing during the
    // transition and the FE's getPrimaryRole still resolves to Manager. This is
    // what makes assigning Owner safe now: the historical "Owner broke FE role
    // gating" bug was the empty role claim, not the role name itself. The final
    // tightening (drop the Owner->Manager composite) lands with the PIN UI.
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

        tenant.KeycloakUserId = userId;
        await db.SaveChangesAsync(ct);

        await keycloakAdmin.AssignRealmRoleAsync(userId, OwnerRoleName, ct);

        backgroundJobs.Enqueue<OwnerWelcomeEmailJob>(
            job => job.SendAsync(tenant.Id, tempPassword, CancellationToken.None));

        logger.LogInformation(
            "Owner provisioned: TenantId={TenantId} KeycloakUserId={KeycloakUserId} Email={OwnerEmail}",
            tenant.Id, userId, tenant.OwnerEmail);
    }
}
