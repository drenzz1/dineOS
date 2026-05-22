using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Provisions the tenant owner's Keycloak account after a Stripe checkout
/// completes (#205): creates the user with a temporary password gated by
/// the <c>UPDATE_PASSWORD</c> required action, persists
/// <c>KeycloakUserId</c>, assigns the <c>Manager</c> realm role, then
/// enqueues the welcome email. Owners are stamped as <c>Manager</c>
/// directly so the JWT's <c>realm_access.roles</c> claim aligns with
/// backend <c>[Authorize(Roles="Manager")]</c> checks and the FE role enum.
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
    // Tenant owners are functionally Managers of their own restaurant.
    // Assigning the Manager realm role directly keeps the JWT's
    // realm_access.roles aligned with backend [Authorize(Roles="Manager")]
    // checks and the frontend role enum — no role aliasing required.
    private const string OwnerRoleName = "Manager";

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

        var (firstName, lastName) = SplitOwnerName(tenant.OwnerName);

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

    private static (string First, string Last) SplitOwnerName(string ownerName)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
            return ("Owner", "");

        var trimmed = ownerName.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex < 0
            ? (trimmed, "")
            : (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].Trim());
    }
}
