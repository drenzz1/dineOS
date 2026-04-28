namespace DineOS.Domain.Common;

public abstract class TenantAuditingEntity : BaseAuditingEntity
{
    public long TenantId { get; set; }
}
