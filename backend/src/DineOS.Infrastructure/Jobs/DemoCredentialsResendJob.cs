using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Rotates the Keycloak password on an existing Active demo user (#216) and
/// re-sends the welcome email with the new value. Triggered when an active
/// demo holder re-requests credentials within the TTL.
/// </summary>
public sealed class DemoCredentialsResendJob(
    AppDbContext db,
    IKeycloakAdminClient keycloakAdmin,
    IBackgroundJobClient backgroundJobs,
    ILogger<DemoCredentialsResendJob> logger)
{
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
                "Demo credentials resend skipped — DemoUser not found. DemoUserId={DemoUserId}",
                demoUserId);
            return;
        }

        if (demoUser.KeycloakUserId is null)
        {
            logger.LogWarning(
                "Demo credentials resend skipped — KeycloakUserId is null (provisioning never completed). DemoUserId={DemoUserId}",
                demoUserId);
            return;
        }

        await keycloakAdmin.SetUserEnabledAsync(demoUser.KeycloakUserId, enabled: true, ct);
        await keycloakAdmin.ResetPasswordAsync(demoUser.KeycloakUserId, tempPassword, temporary: false, ct);

        backgroundJobs.Enqueue<DemoWelcomeEmailJob>(
            job => job.SendAsync(demoUser.Id, tempPassword, isReissue: true, CancellationToken.None));

        logger.LogInformation(
            "Demo credentials rotated. DemoUserId={DemoUserId} KeycloakUserId={KeycloakUserId}",
            demoUser.Id, demoUser.KeycloakUserId);
    }
}
