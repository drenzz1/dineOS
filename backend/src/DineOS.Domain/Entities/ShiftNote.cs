using DineOS.Domain.Common;
using DineOS.Domain.Enums;

namespace DineOS.Domain.Entities;

public class ShiftNote : TenantAuditingEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public ShiftNotePriority Priority { get; set; } = ShiftNotePriority.Info;
    public string Author { get; set; } = string.Empty;
}
