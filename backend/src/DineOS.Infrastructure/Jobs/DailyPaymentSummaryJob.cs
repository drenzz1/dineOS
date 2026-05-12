using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Recurring Hangfire job: aggregates each active tenant's payments for the
/// day in server-local time and emails the owner an HTML summary.
/// </summary>
public sealed class DailyPaymentSummaryJob(
    AppDbContext db,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    ILogger<DailyPaymentSummaryJob> logger) : IEmailJob
{
    public const string RecurringJobId = "daily-payment-summary";
    public const string Subject        = "DineOS — your daily payment summary";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 30, 120, 300 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(CancellationToken ct)
    {
        var today        = DateOnly.FromDateTime(DateTime.UtcNow);
        var startOfDay   = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = startOfDay.AddDays(1);

        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.IsActive && t.DeletedAt == null && t.OwnerEmailVerified)
            .ToListAsync(ct);

        logger.LogInformation(
            "DailyPaymentSummaryJob starting: TenantCount={TenantCount} Date={Date}",
            tenants.Count, today);

        foreach (var tenant in tenants)
        {
            var payments = await db.Payments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.TenantId == tenant.Id
                         && p.DeletedAt == null
                         && p.Status == PaymentStatus.Completed
                         && p.CreatedAt >= startOfDay
                         && p.CreatedAt <  endExclusive)
                .ToListAsync(ct);

            var byMethod = payments
                .GroupBy(p => p.Method)
                .Select(g => new DailyPaymentSummaryEmailModel.LineItem(
                    g.Key.ToString(), g.Count(), g.Sum(p => p.Amount)))
                .OrderByDescending(li => li.Total)
                .ToList();

            var model = new DailyPaymentSummaryEmailModel(
                OwnerName:      tenant.OwnerName,
                RestaurantName: tenant.Name,
                Date:           today,
                TotalRevenue:   payments.Sum(p => p.Amount),
                PaymentCount:   payments.Count,
                ByMethod:       byMethod);

            var html = await templates.RenderAsync("DailyPaymentSummary", model, ct);
            var text = $"""
                        Daily payment summary for {tenant.Name} on {today:yyyy-MM-dd}
                        Revenue: {model.TotalRevenue:C}
                        Payments: {model.PaymentCount}
                        """;

            await emailSender.SendAsync(tenant.OwnerEmail, Subject, text, html, ct);

            logger.LogInformation(
                "Daily summary sent: TenantId={TenantId} Payments={PaymentCount} Revenue={Revenue}",
                tenant.Id, model.PaymentCount, model.TotalRevenue);
        }
    }
}
