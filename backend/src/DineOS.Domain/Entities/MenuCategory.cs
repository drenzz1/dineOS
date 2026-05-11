using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class MenuCategory : TenantAuditingEntity
{
    public string Name { get; set; } = string.Empty;
}
