namespace DineOS.Application.Options;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Stripe secret key. Loaded via env (Stripe__SecretKey) or user-secrets — never commit.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Webhook signing secret. Each environment has its own — never commit.</summary>
    public string WebhookSecret { get; init; } = string.Empty;

    /// <summary>Stripe Price ID for the monthly Pro plan.</summary>
    public string ProMonthlyPriceId { get; init; } = string.Empty;

    /// <summary>Stripe Price ID for the annual Pro plan.</summary>
    public string ProAnnualPriceId { get; init; } = string.Empty;

    /// <summary>URL Stripe Checkout returns the customer to after a successful checkout.</summary>
    public string CheckoutSuccessUrl { get; init; } = "http://localhost:3000/settings/billing?status=success";

    /// <summary>URL Stripe Checkout returns the customer to when they abandon checkout.</summary>
    public string CheckoutCancelUrl { get; init; } = "http://localhost:3000/settings/billing?status=cancelled";

    /// <summary>URL Stripe Customer Portal returns the customer to after they finish managing billing.</summary>
    public string PortalReturnUrl { get; init; } = "http://localhost:3000/settings/billing";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey);
}
