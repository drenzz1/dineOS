using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Provisions the tenant owner's Keycloak account after a Stripe checkout
/// completes (#205): creates the user, persists <c>KeycloakUserId</c>,
/// assigns the Owner realm role, then enqueues the welcome email.
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
    private const string OwnerRoleName = "Owner";

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

        var userId = await keycloakAdmin.CreateUserAsync(
            email:           tenant.OwnerEmail,
            firstName:       firstName,
            lastName:        lastName,
            tempPassword:    tempPassword,
            requiredActions: new[] { "UPDATE_PASSWORD", "VERIFY_EMAIL" },
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
