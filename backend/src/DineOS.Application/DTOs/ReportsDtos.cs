namespace DineOS.Application.DTOs;

public record SalesByMethodDto(string Method, decimal Total, int Count);

public record SalesReportDto(
    DateOnly From,
    DateOnly To,
    int OrderCount,
    decimal TotalRevenue,
    decimal AverageTicket,
    List<SalesByMethodDto> ByPaymentMethod);

public record OrdersByStatusDto(string Status, int Count);

public record OrdersByTypeDto(string OrderType, int Count);

public record OrdersReportDto(
    DateOnly From,
    DateOnly To,
    int TotalOrders,
    List<OrdersByStatusDto> ByStatus,
    List<OrdersByTypeDto> ByType);

public record StaffByRoleDto(string Role, int Total, int Active);

public record StaffReportDto(int Total, int Active, int Inactive, List<StaffByRoleDto> ByRole);
