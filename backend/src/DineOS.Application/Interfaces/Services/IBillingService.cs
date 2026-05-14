using DineOS.Application.Billing;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Domain.Enums;

namespace DineOS.Application.Interfaces.Services;

public interface IBillingService
{
    /// <summary>Creates a Stripe Checkout session for the current tenant on the chosen cycle.</summary>
    Task<ServiceResult<StripeRedirectDto>> CreateCheckoutSessionAsync(
        BillingCycle cycle,
        CancellationToken ct = default);

    /// <summary>Creates a Stripe Customer Portal session so the tenant can manage card / cancel / view invoices.</summary>
    Task<ServiceResult<StripeRedirectDto>> CreatePortalSessionAsync(CancellationToken ct = default);

    /// <summary>Returns the current subscription state for the caller's tenant.</summary>
    Task<ServiceResult<BillingSubscriptionDto>> GetSubscriptionAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies a verified Stripe webhook event to the database. The event must
    /// already have its signature verified by the controller before calling
    /// this method. Returns Ok when the event is recognized and applied (or
    /// idempotently ignored); returns BadRequest only for malformed events.
    /// </summary>
    Task<ServiceResult<string>> HandleWebhookAsync(string eventJson, string signature, CancellationToken ct = default);
}
