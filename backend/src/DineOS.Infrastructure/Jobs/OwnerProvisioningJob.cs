using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    ISetupTokenStore setupTokens,
    IOptions<FrontendOptions> frontendOptions,
    IBackgroundJobClient backgroundJobs,
    ILogger<OwnerProvisioningJob> logger)
{
    private const string OwnerRoleName = "Owner";
    private static readonly TimeSpan SetupTokenTtl = TimeSpan.FromHours(24);

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

        var token = await setupTokens.IssueAsync(tenant.Id, SetupTokenTtl, ct);
        var baseUrl = frontendOptions.Value.BaseUrl.TrimEnd('/');
        var setPasswordUrl = $"{baseUrl}/set-password?token={token}";

        backgroundJobs.Enqueue<OwnerWelcomeEmailJob>(
            job => job.SendAsync(tenant.Id, setPasswordUrl, CancellationToken.None));

        logger.LogInformation(
            "Owner provisioned: TenantId={TenantId} KeycloakUserId={KeycloakUserId} Email={OwnerEmail}",
            tenant.Id, userId, tenant.OwnerEmail);
    }

    private static (string First, string Last) SplitOwnerName(string ownerName)
    {
        // Keycloak's User Profile treats users with a null/empty lastName as
        // incomplete and blocks password-grant logins with `resolve_required_actions`.
        // Fall back to repeating the first name so the profile is always complete.
        if (string.IsNullOrWhiteSpace(ownerName))
            return ("Owner", "Owner");

        var trimmed = ownerName.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex < 0)
            return (trimmed, trimmed);

        var last = trimmed[(spaceIndex + 1)..].Trim();
        return (trimmed[..spaceIndex],
            string.IsNullOrWhiteSpace(last) ? trimmed[..spaceIndex] : last);
    }
}
