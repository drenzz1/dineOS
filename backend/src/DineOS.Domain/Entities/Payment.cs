using DineOS.Domain.Common;
using DineOS.Domain.Enums;

namespace DineOS.Domain.Entities;

public class Payment : TenantAuditingEntity
{
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;

    /// <summary>
    /// Set when the OverduePaymentNotificationJob has emailed the owner about
    /// this still-Pending payment. Stops repeat notifications.
    /// </summary>
    public DateTime? OverdueNotifiedAt { get; set; }
}
