using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Recurring (default: daily at 03:00) job that disables expired demo users
/// in Keycloak (#216). The DB row is preserved (not deleted) so the email
/// reservation lives on — a re-request will reset cleanly through the
/// expired-branch in <c>DemoAccessService</c>.
/// </summary>
public sealed class DemoCleanupJob(
    AppDbContext db,
    IKeycloakAdminClient keycloakAdmin,
    ILogger<DemoCleanupJob> logger)
{
    public const string RecurringJobId = "demo-cleanup";

    [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var expired = await db.DemoUsers
            .IgnoreQueryFilters()
            .Where(d => d.Status == DemoUserStatus.Active
                     && d.ExpiresAt < now
                     && d.DeletedAt == null)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            logger.LogDebug("Demo cleanup: no expired users.");
            return;
        }

        int disabled = 0;
        foreach (var user in expired)
        {
            if (user.KeycloakUserId is not null)
            {
                try
                {
                    await keycloakAdmin.SetUserEnabledAsync(user.KeycloakUserId, enabled: false, ct);
                    disabled++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Demo cleanup failed to disable Keycloak user. DemoUserId={DemoUserId} KeycloakUserId={KeycloakUserId}",
                        user.Id, user.KeycloakUserId);
                    continue;
                }
            }

            user.Status = DemoUserStatus.Expired;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Demo cleanup completed. Expired={Expired} KeycloakDisabled={KeycloakDisabled}",
            expired.Count, disabled);
    }
}
