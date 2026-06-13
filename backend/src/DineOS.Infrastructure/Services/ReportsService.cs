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

        var revenueByDayRaw = await ordersInRange
            .Where(o => o.Status == OrderStatus.Delivered)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.Total), OrderCount = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        var revenueByDay = revenueByDayRaw
            .Select(x => new RevenueByDayDto(DateOnly.FromDateTime(x.Date), x.Revenue, x.OrderCount))
            .ToList();

        var avgTicket = orderCount > 0 ? Math.Round(totalRevenue / orderCount, 2) : 0m;

        var report = new SalesReportDto(
            fromDate,
            toDate,
            orderCount,
            totalRevenue,
            avgTicket,
            byMethod,
            revenueByDay);

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

        var byHour = await ordersInRange
            .GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new OrdersByHourDto(g.Key, g.Count()))
            .OrderBy(x => x.Hour)
            .ToListAsync(ct);

        var report = new OrdersReportDto(fromDate, toDate, total, byStatus, byType, byHour);
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

    public async Task<ServiceResult<ItemsReportDto>> GetItemsReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var (fromDate, toDate, start, end) = ResolveRange(from, to);

        var topItems = await db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.CreatedAt >= start && oi.CreatedAt <= end)
            .GroupBy(oi => oi.Name)
            .Select(g => new TopItemDto(
                g.Key,
                g.Sum(oi => oi.Quantity),
                g.Sum(oi => oi.Quantity * oi.UnitPrice)))
            .OrderByDescending(x => x.Quantity)
            .Take(20)
            .ToListAsync(ct);

        var report = new ItemsReportDto(fromDate, toDate, topItems);
        return ServiceResult<ItemsReportDto>.Ok(report, "Items report");
    }

    public async Task<ServiceResult<OrderHistoryReportDto>> GetOrderHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (fromDate, toDate, start, end) = ResolveRange(from, to);

        var query = db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt <= end)
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var rawOrders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.CreatedAt,
                o.TableNumber,
                o.OrderType,
                o.Status,
                o.Total,
                ItemCount = o.Items.Count(),
            })
            .ToListAsync(ct);

        var orderIds = rawOrders.Select(o => o.Id).ToList();
        // Fetch the (orderId, method) pairs flat and group in memory — EF Core
        // cannot translate `GroupBy(...).Select(g => g.First().Method)` to SQL.
        var payments = await db.Payments
            .AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId) && p.Status == PaymentStatus.Completed)
            .Select(p => new { p.OrderId, p.Method })
            .ToListAsync(ct);

        var paymentMap = payments
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.First().Method.ToString());

        var orders = rawOrders.Select(o => new OrderHistoryItemDto(
            o.Id,
            o.CreatedAt,
            o.TableNumber,
            o.OrderType,
            o.Status.ToString(),
            o.ItemCount,
            o.Total,
            paymentMap.GetValueOrDefault(o.Id)
        )).ToList();

        var report = new OrderHistoryReportDto(fromDate, toDate, page, pageSize, totalCount, orders);
        return ServiceResult<OrderHistoryReportDto>.Ok(report, "Order history");
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
