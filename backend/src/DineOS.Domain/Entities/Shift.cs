using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class Shift : TenantAuditingEntity
{
    public long StaffMemberId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Notes { get; set; }

    public StaffMember? StaffMember { get; set; }
}
