using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DineOS.Infrastructure.Services;

public class AdminService(AppDbContext db) : IAdminService
{
    public async Task<ServiceResult<AdminAnalyticsDto>> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var tenants = db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null);

        var orders = db.Orders
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(o => o.DeletedAt == null);

        var payments = db.Payments
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.DeletedAt == null);
        var tenantIds = tenants.Select(t => t.Id);
        var ordersForCurrentTenants = orders.Where(o => tenantIds.Contains(o.TenantId));
        var paymentsForCurrentTenants = payments.Where(p => tenantIds.Contains(p.TenantId));

        var totalRestaurants = await tenants.CountAsync(ct);
        var activeRestaurants = await tenants.CountAsync(t => t.IsActive, ct);
        var ordersToday = await ordersForCurrentTenants.CountAsync(
            o => o.CreatedAt >= todayStart && o.CreatedAt < tomorrowStart,
            ct);
        var revenueToday = await paymentsForCurrentTenants
            .Where(p =>
                p.Status == PaymentStatus.Completed &&
                p.CreatedAt >= todayStart &&
                p.CreatedAt < tomorrowStart)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var weeklyGrowth = await BuildWeeklyGrowthAsync(tenants, now, ct);
        var topRestaurants = await BuildTopRestaurantsAsync(
            tenants,
            ordersForCurrentTenants,
            paymentsForCurrentTenants,
            monthStart,
            monthEnd,
            ct);
        var activityFeed = await BuildActivityFeedAsync(tenants, ct);

        return ServiceResult<AdminAnalyticsDto>.Ok(
            new AdminAnalyticsDto(
                totalRestaurants,
                activeRestaurants,
                ordersToday,
                revenueToday,
                weeklyGrowth,
                topRestaurants,
                activityFeed),
            "Admin analytics");
    }

    public async Task<ServiceResult<PagedResponse<PlatformUserDto>>> ListUsersAsync(
        string? search,
        PagedRequest pagination,
        CancellationToken ct = default)
    {
        // SuperAdmin sees staff across every tenant; bypass the tenant query filter.
        var staff = db.StaffMembers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            staff = staff.Where(s =>
                s.FullName.ToLower().Contains(q) ||
                s.Email.ToLower().Contains(q));
        }

        var tenants = db.Tenants.AsNoTracking().IgnoreQueryFilters();

        var query = from s in staff
                    join t in tenants on s.TenantId equals t.Id into tj
                    from t in tj.DefaultIfEmpty()
                    select new
                    {
                        s.Id,
                        s.TenantId,
                        TenantName = t != null ? t.Name : "(unknown)",
                        s.FullName,
                        s.Email,
                        s.Role,
                        s.IsActive,
                        s.CreatedAt
                    };

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.TenantName)
            .ThenBy(u => u.FullName)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(u => new PlatformUserDto(
                u.Id,
                u.TenantId,
                u.TenantName,
                u.FullName,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt))
            .ToListAsync(ct);

        return ServiceResult<PagedResponse<PlatformUserDto>>.Ok(
            PagedResponse<PlatformUserDto>.From(items, total, pagination));
    }

    private static async Task<List<WeeklyGrowthDto>> BuildWeeklyGrowthAsync(
        IQueryable<Tenant> tenants,
        DateTime now,
        CancellationToken ct)
    {
        var currentWeekStart = StartOfWeek(now.Date);
        var firstWeekStart = currentWeekStart.AddDays(-7 * 7);
        var finalWeekEnd = currentWeekStart.AddDays(7);

        var createdDates = await tenants
            .Where(t => t.CreatedAt >= firstWeekStart && t.CreatedAt < finalWeekEnd)
            .Select(t => t.CreatedAt)
            .ToListAsync(ct);

        return Enumerable.Range(0, 8)
            .Select(i =>
            {
                var weekStart = firstWeekStart.AddDays(i * 7);
                var weekEnd = weekStart.AddDays(7);
                var count = createdDates.Count(d => d >= weekStart && d < weekEnd);
                return new WeeklyGrowthDto(
                    weekStart.ToString("MMM d", CultureInfo.InvariantCulture),
                    count);
            })
            .ToList();
    }

    private static async Task<List<TopRestaurantDto>> BuildTopRestaurantsAsync(
        IQueryable<Tenant> tenants,
        IQueryable<Order> orders,
        IQueryable<Payment> payments,
        DateTime monthStart,
        DateTime monthEnd,
        CancellationToken ct)
    {
        var tenantNames = await tenants
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var orderCounts = await orders
            .Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd)
            .GroupBy(o => o.TenantId)
            .Select(g => new { TenantId = g.Key, Orders = g.Count() })
            .ToListAsync(ct);

        var revenues = await payments
            .Where(p =>
                p.Status == PaymentStatus.Completed &&
                p.CreatedAt >= monthStart &&
                p.CreatedAt < monthEnd)
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Revenue = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var orderCountsByTenant = orderCounts.ToDictionary(x => x.TenantId, x => x.Orders);
        var revenuesByTenant = revenues.ToDictionary(x => x.TenantId, x => x.Revenue);

        return tenantNames
            .Select(t => new
            {
                t.Name,
                Orders = orderCountsByTenant.GetValueOrDefault(t.Id),
                Revenue = revenuesByTenant.GetValueOrDefault(t.Id)
            })
            .Where(t => t.Orders > 0 || t.Revenue > 0)
            .OrderByDescending(t => t.Revenue)
            .ThenByDescending(t => t.Orders)
            .ThenBy(t => t.Name)
            .Take(5)
            .Select((t, index) => new TopRestaurantDto(
                index + 1,
                t.Name,
                t.Orders,
                t.Revenue))
            .ToList();
    }

    private static async Task<List<ActivityEventDto>> BuildActivityFeedAsync(
        IQueryable<Tenant> tenants,
        CancellationToken ct)
    {
        var recentTenants = await tenants
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Take(8)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync(ct);

        return recentTenants
            .Select(t =>
            {
                var timestamp = t.UpdatedAt ?? t.CreatedAt;
                var isUpdate = t.UpdatedAt.HasValue && t.UpdatedAt.Value > t.CreatedAt;
                var description = isUpdate
                    ? t.IsActive
                        ? $"Restaurant updated: {t.Name}"
                        : $"Restaurant suspended: {t.Name}"
                    : $"New restaurant registered: {t.Name}";

                return new ActivityEventDto(
                    $"restaurant-{t.Id}-{timestamp.Ticks}",
                    description,
                    timestamp);
            })
            .ToList();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
