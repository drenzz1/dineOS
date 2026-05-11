using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Infrastructure.Services;

public class ReportsService(AppDbContext db) : IReportsService
{
    public async Task<ServiceResult<SalesReportDto>> GetSalesReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var (fromDate, toDate, start, end) = ResolveRange(from, to);

        var ordersInRange = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt <= end);

        var orderCount = await ordersInRange.CountAsync(ct);
        var totalRevenue = await ordersInRange
            .Where(o => o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var byMethod = await db.Payments
            .AsNoTracking()
            .Where(p => p.CreatedAt >= start && p.CreatedAt <= end && p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Method)
            .Select(g => new SalesByMethodDto(g.Key.ToString(), g.Sum(p => p.Amount), g.Count()))
            .ToListAsync(ct);

        var avgTicket = orderCount > 0 ? Math.Round(totalRevenue / orderCount, 2) : 0m;

        var report = new SalesReportDto(
            fromDate,
            toDate,
            orderCount,
            totalRevenue,
            avgTicket,
            byMethod);

        return ServiceResult<SalesReportDto>.Ok(report, "Sales report");
    }

    public async Task<ServiceResult<OrdersReportDto>> GetOrdersReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var (fromDate, toDate, start, end) = ResolveRange(from, to);

        var ordersInRange = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt <= end);

        var total = await ordersInRange.CountAsync(ct);

        var byStatus = await ordersInRange
            .GroupBy(o => o.Status)
            .Select(g => new OrdersByStatusDto(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var byType = await ordersInRange
            .GroupBy(o => o.OrderType)
            .Select(g => new OrdersByTypeDto(g.Key, g.Count()))
            .ToListAsync(ct);

        var report = new OrdersReportDto(fromDate, toDate, total, byStatus, byType);
        return ServiceResult<OrdersReportDto>.Ok(report, "Orders report");
    }

    public async Task<ServiceResult<StaffReportDto>> GetStaffReportAsync(CancellationToken ct = default)
    {
        var staff = db.StaffMembers.AsNoTracking();

        var total = await staff.CountAsync(ct);
        var active = await staff.CountAsync(s => s.IsActive, ct);

        var byRole = await staff
            .GroupBy(s => s.Role)
            .Select(g => new StaffByRoleDto(
                g.Key,
                g.Count(),
                g.Count(s => s.IsActive)))
            .ToListAsync(ct);

        var report = new StaffReportDto(total, active, total - active, byRole);
        return ServiceResult<StaffReportDto>.Ok(report, "Staff report");
    }

    private static (DateOnly fromDate, DateOnly toDate, DateTime start, DateTime end) ResolveRange(
        DateOnly? from,
        DateOnly? to)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? toDate.AddDays(-29);
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

        var start = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end   = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        return (fromDate, toDate, start, end);
    }
}
