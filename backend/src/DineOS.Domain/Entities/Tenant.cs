using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class Tenant : BaseAuditingEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
