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

    // ── Stripe subscription state ─────────────────────────────────────────
    // Free tenants leave all four fields null/None. Stripe-backed tenants
    // get them populated by the BillingController + Stripe webhook.
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeSessionId { get; set; }
    public BillingStatus BillingStatus { get; set; } = BillingStatus.None;
    public BillingCycle? BillingCycle { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    // Populated by the Stripe checkout.session.completed webhook (#205) when
    // the owner's Keycloak account is provisioned. Doubles as the idempotency
    // guard for that flow: provisioning is skipped when this is non-null.
    public string? KeycloakUserId { get; set; }
}
