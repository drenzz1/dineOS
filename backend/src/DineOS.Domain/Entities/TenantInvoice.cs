using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

public class TenantInvoice : BaseAuditingEntity
{
    public long TenantId { get; set; }
    public string StripeInvoiceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "eur";
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public string? HostedInvoiceUrl { get; set; }
}
