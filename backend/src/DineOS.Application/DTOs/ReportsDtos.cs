namespace DineOS.Application.DTOs;

public record SalesByMethodDto(string Method, decimal Total, int Count);

public record RevenueByDayDto(DateOnly Date, decimal Revenue, int OrderCount);

public record SalesReportDto(
    DateOnly From,
    DateOnly To,
    int OrderCount,
    decimal TotalRevenue,
    decimal AverageTicket,
    List<SalesByMethodDto> ByPaymentMethod,
    List<RevenueByDayDto> RevenueByDay);

public record OrdersByStatusDto(string Status, int Count);

public record OrdersByTypeDto(string OrderType, int Count);

public record OrdersByHourDto(int Hour, int Count);

public record OrdersReportDto(
    DateOnly From,
    DateOnly To,
    int TotalOrders,
    List<OrdersByStatusDto> ByStatus,
    List<OrdersByTypeDto> ByType,
    List<OrdersByHourDto> ByHour);

public record StaffByRoleDto(string Role, int Total, int Active);

public record StaffReportDto(int Total, int Active, int Inactive, List<StaffByRoleDto> ByRole);

public record TopItemDto(string Name, int Quantity, decimal Revenue);

public record ItemsReportDto(DateOnly From, DateOnly To, List<TopItemDto> TopItems);

public record OrderHistoryItemDto(
    long Id,
    DateTime CreatedAt,
    int? TableNumber,
    string OrderType,
    string Status,
    int ItemCount,
    decimal Total,
    string? PaymentMethod);

public record OrderHistoryReportDto(
    DateOnly From,
    DateOnly To,
    int Page,
    int PageSize,
    int TotalCount,
    List<OrderHistoryItemDto> Orders);
