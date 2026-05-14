using DineOS.Domain.Enums;
using FluentValidation;

namespace DineOS.Application.Billing;

/// <summary>
/// Request body for <c>POST /api/v1/billing/checkout-session</c>. The cycle
/// picks which Stripe price the customer subscribes to.
/// </summary>
public class CreateCheckoutSessionRequest
{
    /// <example>Monthly</example>
    public BillingCycle Cycle { get; set; }
}

public class CreateCheckoutSessionRequestValidator : AbstractValidator<CreateCheckoutSessionRequest>
{
    public CreateCheckoutSessionRequestValidator()
    {
        RuleFor(x => x.Cycle)
            .IsInEnum()
            .WithMessage("Cycle must be Monthly or Annual.");
    }
}

/// <summary>URL the client can redirect the browser to.</summary>
public class StripeRedirectDto
{
    /// <example>https://checkout.stripe.com/c/pay/cs_test_xxx</example>
    public string Url { get; set; } = string.Empty;
}
