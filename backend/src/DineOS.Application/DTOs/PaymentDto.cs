namespace DineOS.Application.DTOs;

/// <summary>
/// A completed payment record returned by <c>POST /api/v1/payments</c>.
/// Payments are tenant-scoped and immutable once created.
/// </summary>
public class PaymentDto
{
    /// <summary>Payment identifier assigned by the platform.</summary>
    /// <example>987</example>
    public long Id { get; set; }

    /// <summary>Order this payment settles. Joins back to <c>OrderDto.Id</c>.</summary>
    /// <example>1234</example>
    public long OrderId { get; set; }

    /// <summary>Amount tendered, in the tenant's reporting currency.</summary>
    /// <example>18.50</example>
    public decimal Amount { get; set; }

    /// <summary>Payment method used: <c>Cash</c> or <c>Card</c>.</summary>
    /// <example>Card</example>
    public string Method { get; set; } = string.Empty;

    /// <summary>Payment status. New payments are recorded as <c>Completed</c>.</summary>
    /// <example>Completed</example>
    public string Status { get; set; } = string.Empty;

    /// <summary>Tenant the payment belongs to. Matches the caller's JWT <c>tenant_id</c>.</summary>
    /// <example>1</example>
    public long TenantId { get; set; }

    /// <summary>UTC timestamp the payment was recorded.</summary>
    /// <example>2026-05-12T18:30:00Z</example>
    public DateTime CreatedAt { get; set; }
}
