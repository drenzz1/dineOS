using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class StaffMember : TenantAuditingEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
