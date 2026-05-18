using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire job: sends a "You're on dineOS Pro!" confirmation email when a
/// tenant's subscription transitions from Free to Pro (Active or Trialing).
/// Retries and dead-lettering are owned by Hangfire via the shared pipeline.
/// </summary>
public sealed class SubscriptionActivatedEmailJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    ILogger<SubscriptionActivatedEmailJob> logger) : IEmailJob
{
    public const string Subject = "You're on dineOS Pro!";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 10, 30, 90 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendAsync(long tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.DeletedAt == null, ct);

        if (tenant is null)
        {
            logger.LogWarning(
                "Subscription activated email skipped — tenant not found. TenantId={TenantId}",
                tenantId);
            return;
        }

        var model = new SubscriptionActivatedEmailModel(
            tenant.OwnerName,
            tenant.Name,
            tenant.BillingCycle?.ToString() ?? "Monthly",
            tenant.CurrentPeriodEnd);

        var html = await templates.RenderAsync("SubscriptionActivated", model, ct);
        var text = $"Your dineOS Pro subscription for {tenant.Name} is now active.";

        await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

        logger.LogInformation(
            "Subscription activated email sent: TenantId={TenantId}", tenantId);
    }
}
