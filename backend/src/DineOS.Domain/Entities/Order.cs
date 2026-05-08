using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class Order : TenantAuditingEntity
{
    public string OrderType { get; set; } = string.Empty;
    public int? TableNumber { get; set; }
    public string Status { get; set; } = "New";
    public decimal Total { get; set; }
    public string? Notes { get; set; }
}
