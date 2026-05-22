using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Auth;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// One-shot Hangfire job (#205 follow-up) that retroactively applies the
/// temporary-password + <c>UPDATE_PASSWORD</c> required-action security
/// posture to tenant owners provisioned during the window when those
/// controls were disabled. For each affected tenant the job rotates the
/// Keycloak password to a fresh single-use value, stamps the required
/// action, and re-enqueues <see cref="OwnerWelcomeEmailJob"/> so the owner
/// receives current credentials and is forced through the first-login
/// password-change flow.
/// </summary>
/// <remarks>
/// Trigger manually after deployment, e.g.
/// <c>BackgroundJob.Enqueue&lt;OwnerSecurityRemediationJob&gt;(j =&gt; j.RunAsync(CancellationToken.None))</c>.
/// Idempotent: tenants whose Keycloak user already has the
/// <c>UPDATE_PASSWORD</c> required action are skipped.
/// </remarks>
public sealed class OwnerSecurityRemediationJob(
    AppDbContext db,
    IKeycloakAdminClient keycloakAdmin,
    IBackgroundJobClient backgroundJobs,
    ILogger<OwnerSecurityRemediationJob> logger)
{
    private const string FirstLoginRequiredAction = "UPDATE_PASSWORD";

    public async Task RunAsync(CancellationToken ct)
    {
        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.KeycloakUserId != null
                && t.DeletedAt == null
                && (t.BillingStatus == BillingStatus.Active || t.BillingStatus == BillingStatus.Trialing))
            .ToListAsync(ct);

        logger.LogInformation(
            "Owner security remediation starting for {Count} provisioned tenants.",
            tenants.Count);

        var remediated = 0;
        var skipped = 0;

        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();

            var keycloakUserId = tenant.KeycloakUserId!;

            try
            {
                var user = await keycloakAdmin.FindUserByEmailAsync(tenant.OwnerEmail, ct);
                if (user is null)
                {
                    logger.LogWarning(
                        "Owner remediation: no Keycloak user found for TenantId={TenantId} Email={Email}; skipping.",
                        tenant.Id, tenant.OwnerEmail);
                    skipped++;
                    continue;
                }

                if (user.RequiredActions.Contains(FirstLoginRequiredAction))
                {
                    logger.LogInformation(
                        "Owner remediation skipped — UPDATE_PASSWORD already set. TenantId={TenantId}",
                        tenant.Id);
                    skipped++;
                    continue;
                }

                var tempPassword = TempPasswordGenerator.Generate();
                await keycloakAdmin.ResetPasswordAsync(keycloakUserId, tempPassword, temporary: true, ct);
                await keycloakAdmin.SetRequiredActionsAsync(
                    keycloakUserId,
                    new[] { FirstLoginRequiredAction },
                    ct);

                backgroundJobs.Enqueue<OwnerWelcomeEmailJob>(
                    job => job.SendAsync(tenant.Id, tempPassword, CancellationToken.None));

                logger.LogInformation(
                    "Owner remediated: TenantId={TenantId} Email={Email} — fresh temp password emailed.",
                    tenant.Id, tenant.OwnerEmail);
                remediated++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Owner remediation failed for TenantId={TenantId} Email={Email}.",
                    tenant.Id, tenant.OwnerEmail);
            }
        }

        logger.LogInformation(
            "Owner security remediation complete. Remediated={Remediated} Skipped={Skipped} Total={Total}.",
            remediated, skipped, tenants.Count);
    }
}
