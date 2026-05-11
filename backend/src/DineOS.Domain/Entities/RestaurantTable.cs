using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class RestaurantTable : TenantAuditingEntity
{
    public int Number { get; set; }
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}
