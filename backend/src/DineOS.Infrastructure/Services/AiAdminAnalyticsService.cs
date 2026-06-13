using System.Diagnostics;
using System.Globalization;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public sealed class AiAdminAnalyticsService(
    AppDbContext db,
    IAiClient aiClient,
    IFeatureFlags featureFlags,
    ILogger<AiAdminAnalyticsService> logger) : IAiAdminAnalyticsService
{
    private const decimal ProMonthlyPrice = 50m;

    public async Task<ServiceResult<AdminBillingInsightDto>> GenerateInsightAsync(CancellationToken ct = default)
    {
        if (!featureFlags.IsEnabled(FeatureFlag.AiAdminAnalytics, defaultValue: true))
            return ServiceResult<AdminBillingInsightDto>.ServiceUnavailable(
                "AI admin analytics is currently disabled.");

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var tenants = db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null);

        var payments = db.Payments
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.DeletedAt == null);

        var totalTenants    = await tenants.CountAsync(ct);
        var activeTenants   = await tenants.CountAsync(t => t.IsActive, ct);
        var proTenants      = await tenants.CountAsync(t => t.Plan == SubscriptionPlan.Pro, ct);
        var freeTenants     = await tenants.CountAsync(t => t.Plan == SubscriptionPlan.Free, ct);
        var pastDueTenants  = await tenants.CountAsync(t => t.BillingStatus == BillingStatus.PastDue, ct);

        var canceledThisMonth = await tenants.CountAsync(
            t => t.BillingStatus == BillingStatus.Canceled
              && t.UpdatedAt >= monthStart
              && t.UpdatedAt < monthEnd,
            ct);

        var newProThisMonth = await tenants.CountAsync(
            t => t.Plan == SubscriptionPlan.Pro
              && t.CreatedAt >= monthStart
              && t.CreatedAt < monthEnd,
            ct);

        var estimatedMrr = proTenants * ProMonthlyPrice;

        var topRestaurants = await BuildTopRestaurantsSummaryAsync(tenants, payments, monthStart, monthEnd, ct);
        var weeklyGrowth   = await BuildWeeklyGrowthSummaryAsync(tenants, now, ct);

        var request = new AdminBillingInsightAiRequest(
            TotalTenants:         totalTenants,
            ActiveTenants:        activeTenants,
            SuspendedTenants:     totalTenants - activeTenants,
            ProTenants:           proTenants,
            FreeTenants:          freeTenants,
            PastDueTenants:       pastDueTenants,
            CanceledThisMonth:    canceledThisMonth,
            NewProThisMonth:      newProThisMonth,
            EstimatedMrr:         estimatedMrr,
            Month:                now.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            TopRestaurantsSummary: topRestaurants,
            WeeklyGrowthSummary:  weeklyGrowth);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await aiClient.GenerateAdminBillingInsightAsync(request, ct);
            sw.Stop();

            logger.LogInformation(
                "AI admin billing insight generated: Model={Model} InputTokens={InputTokens} " +
                "OutputTokens={OutputTokens} LatencyMs={LatencyMs}",
                result.Usage.Model, result.Usage.InputTokens, result.Usage.OutputTokens,
                sw.ElapsedMilliseconds);

            var dto = new AdminBillingInsightDto(
                result.Narrative,
                new AiSuggestionMetadata(
                    result.Usage.Model,
                    result.Usage.InputTokens,
                    result.Usage.OutputTokens,
                    (int)sw.ElapsedMilliseconds));

            return ServiceResult<AdminBillingInsightDto>.Ok(dto);
        }
        catch (AiUnavailableException ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "AI admin billing insight failed. LatencyMs={LatencyMs}", sw.ElapsedMilliseconds);
            return ServiceResult<AdminBillingInsightDto>.UnprocessableEntity(
                "AI assistant is temporarily unavailable. Please try again later.");
        }
    }

    private static async Task<string> BuildTopRestaurantsSummaryAsync(
        IQueryable<Tenant> tenants,
        IQueryable<Payment> payments,
        DateTime monthStart,
        DateTime monthEnd,
        CancellationToken ct)
    {
        var tenantNames = await tenants
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var revenues = await payments
            .Where(p =>
                p.Status == PaymentStatus.Completed &&
                p.CreatedAt >= monthStart &&
                p.CreatedAt < monthEnd)
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Revenue = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var revenueByTenant = revenues.ToDictionary(x => x.TenantId, x => x.Revenue);

        var top5 = tenantNames
            .Select(t => new { t.Name, Revenue = revenueByTenant.GetValueOrDefault(t.Id) })
            .Where(t => t.Revenue > 0)
            .OrderByDescending(t => t.Revenue)
            .Take(5)
            .ToList();

        if (top5.Count == 0)
            return "(no revenue recorded this month)";

        return string.Join("\n", top5.Select((t, i) => $"  {i + 1}. {t.Name}: €{t.Revenue:F0}"));
    }

    private static async Task<string> BuildWeeklyGrowthSummaryAsync(
        IQueryable<Tenant> tenants,
        DateTime now,
        CancellationToken ct)
    {
        var daysSinceMonday = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentWeekStart = now.Date.AddDays(-daysSinceMonday);
        var firstWeekStart = currentWeekStart.AddDays(-7 * 7);
        var finalWeekEnd = currentWeekStart.AddDays(7);

        var createdDates = await tenants
            .Where(t => t.CreatedAt >= firstWeekStart && t.CreatedAt < finalWeekEnd)
            .Select(t => t.CreatedAt)
            .ToListAsync(ct);

        var lines = Enumerable.Range(0, 8).Select(i =>
        {
            var weekStart = firstWeekStart.AddDays(i * 7);
            var weekEnd = weekStart.AddDays(7);
            var count = createdDates.Count(d => d >= weekStart && d < weekEnd);
            return $"  {weekStart.ToString("MMM d", CultureInfo.InvariantCulture)}: {count} new";
        });

        return string.Join("\n", lines);
    }
}
