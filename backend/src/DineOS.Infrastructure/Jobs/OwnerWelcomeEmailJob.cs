using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire job: sends the post-checkout welcome email to a tenant owner
/// containing a single-use link to the dineOS <c>/set-password</c> page.
/// Enqueued by <see cref="OwnerProvisioningJob"/> after the Keycloak user
/// has been created and the setup token has been persisted in Redis.
/// </summary>
/// <remarks>
/// The setup URL is serialized into Hangfire's Postgres job arguments. The
/// Redis token behind it has a 24h TTL and is invalidated on first use, so
/// even if a job row leaks the token's blast radius is bounded.
/// </remarks>
public sealed class OwnerWelcomeEmailJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    ILogger<OwnerWelcomeEmailJob> logger) : IEmailJob
{
    public const string Subject = "Welcome to DineOS — set your password";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 10, 30, 90 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendAsync(long tenantId, string setPasswordUrl, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
        {
            logger.LogWarning(
                "Owner welcome email skipped — tenant not found. TenantId={TenantId}",
                tenantId);
            return;
        }

        var model = new OwnerWelcomeEmailModel(
            OwnerName:      tenant.OwnerName,
            RestaurantName: tenant.Name,
            Email:          tenant.OwnerEmail,
            SetPasswordUrl: setPasswordUrl);

        var html = await templates.RenderAsync("OwnerWelcome", model, ct);
        var text = $"""
                    Hi {tenant.OwnerName},

                    Your DineOS account for {tenant.Name} is ready.
                    Sign-in email: {tenant.OwnerEmail}

                    Set your password (single-use link, expires in 24 hours):
                    {setPasswordUrl}
                    """;

        await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

        logger.LogInformation(
            "Owner welcome email sent: TenantId={TenantId} Email={OwnerEmail}",
            tenant.Id, tenant.OwnerEmail);
    }
}
