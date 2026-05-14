namespace DineOS.Application.DTOs;

/// <summary>
/// Current subscription state for a tenant. Returned by
/// <c>GET /api/v1/billing/subscription</c>. Free tenants get a DTO with
/// <c>Plan = "Free"</c>, <c>BillingStatus = "None"</c>, and no Stripe IDs.
/// </summary>
public class BillingSubscriptionDto
{
    /// <summary>Subscription plan: <c>Free</c> or <c>Pro</c>.</summary>
    /// <example>Pro</example>
    public string Plan { get; set; } = string.Empty;

    /// <summary>
    /// Stripe-backed billing status. One of <c>None</c>, <c>Trialing</c>,
    /// <c>Active</c>, <c>PastDue</c>, <c>Canceled</c>, <c>Incomplete</c>.
    /// </summary>
    /// <example>Active</example>
    public string BillingStatus { get; set; } = "None";

    /// <summary>Billing cycle when on a paid plan: <c>Monthly</c> or <c>Annual</c>.</summary>
    /// <example>Monthly</example>
    public string? BillingCycle { get; set; }

    /// <summary>End of the current Stripe billing period. Null for Free tenants.</summary>
    public DateTime? CurrentPeriodEnd { get; set; }

    /// <summary>True when the tenant has an active Stripe subscription this user can manage via the portal.</summary>
    public bool HasStripeSubscription { get; set; }
}
