using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class MenuItem : TenantAuditingEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}
