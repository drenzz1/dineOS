namespace DineOS.Domain.Entities;

public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}
