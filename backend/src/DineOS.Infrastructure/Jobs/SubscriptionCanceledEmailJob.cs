using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire job: sends a cancellation confirmation email when a tenant's
/// Stripe subscription is deleted and the account reverts to Free.
/// </summary>
public sealed class SubscriptionCanceledEmailJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    ILogger<SubscriptionCanceledEmailJob> logger) : IEmailJob
{
    public const string Subject = "Your dineOS subscription has been canceled";

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
                "Subscription canceled email skipped — tenant not found. TenantId={TenantId}",
                tenantId);
            return;
        }

        var model = new SubscriptionCanceledEmailModel(tenant.OwnerName, tenant.Name);
        var html = await templates.RenderAsync("SubscriptionCanceled", model, ct);
        var text = $"Your dineOS subscription for {tenant.Name} has been canceled.";

        await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

        logger.LogInformation(
            "Subscription canceled email sent: TenantId={TenantId}", tenantId);
    }
}
