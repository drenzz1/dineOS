using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class OrderItem : TenantAuditingEntity
{
    public long OrderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}
