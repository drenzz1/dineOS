using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Application.Options;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Recurring Hangfire job: finds PaymentStatus.Pending payments older than the
/// configured threshold that haven't yet been notified, groups them by tenant,
/// emails the owner, and marks each row as notified so the next scan ignores it.
/// </summary>
public sealed class OverduePaymentNotificationJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IOptions<PaymentNotificationOptions> options,
    ILogger<OverduePaymentNotificationJob> logger) : IEmailJob
{
    public const string RecurringJobId = "overdue-payment-notifications";
    public const string Subject        = "DineOS — overdue payments need attention";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 30, 120, 300 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(CancellationToken ct)
    {
        var threshold = options.Value.OverdueThresholdMinutes;
        var cutoff    = DateTime.UtcNow.AddMinutes(-threshold);

        var overdue = await db.Payments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Pending
                     && p.OverdueNotifiedAt == null
                     && p.DeletedAt == null
                     && p.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (overdue.Count == 0)
        {
            logger.LogInformation(
                "OverduePaymentNotificationJob: no payments overdue beyond {Threshold} minutes",
                threshold);
            return;
        }

        var grouped = overdue.GroupBy(p => p.TenantId);

        foreach (var group in grouped)
        {
            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == group.Key && t.IsActive && t.DeletedAt == null, ct);

            if (tenant is null) continue;

            var rows = group
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new OverduePaymentEmailModel.Row(
                    PaymentId:  p.Id,
                    OrderId:    p.OrderId,
                    Amount:     p.Amount,
                    AgeMinutes: (int)Math.Round((DateTime.UtcNow - p.CreatedAt).TotalMinutes)))
                .ToList();

            var model = new OverduePaymentEmailModel(
                OwnerName:        tenant.OwnerName,
                RestaurantName:   tenant.Name,
                OverdueCount:     rows.Count,
                OverdueTotal:     group.Sum(p => p.Amount),
                ThresholdMinutes: threshold,
                Items:            rows);

            var html = await templates.RenderAsync("OverduePayment", model, ct);
            var text = $"""
                        {model.OverdueCount} payment(s) at {tenant.Name} have been pending
                        for more than {threshold} minutes, totalling {model.OverdueTotal:C}.
                        Open the DineOS dashboard to resolve them.
                        """;

            await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

            var ids = group.Select(p => p.Id).ToList();
            var now = DateTime.UtcNow;
            await db.Payments
                .IgnoreQueryFilters()
                .Where(p => ids.Contains(p.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.OverdueNotifiedAt, now), ct);

            logger.LogWarning(
                "Overdue payment alert sent: TenantId={TenantId} Count={Count} Total={Total}",
                tenant.Id, rows.Count, model.OverdueTotal);
        }
    }
}
