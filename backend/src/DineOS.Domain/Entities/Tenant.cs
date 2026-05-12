using DineOS.Domain.Common;
using DineOS.Domain.Enums;

namespace DineOS.Domain.Entities;

public class Tenant : BaseAuditingEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public int TotalOrders { get; set; }
    public int StaffCount { get; set; }
    public decimal Revenue { get; set; }
    public bool OwnerEmailVerified { get; set; }
    public DateTime? OwnerEmailVerifiedAt { get; set; }
}
