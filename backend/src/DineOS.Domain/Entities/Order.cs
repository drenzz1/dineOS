using DineOS.Domain.Common;
using DineOS.Domain.Enums;

namespace DineOS.Domain.Entities;

public class Order : TenantAuditingEntity
{
    public string OrderType { get; set; } = string.Empty;
    public int? TableNumber { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
