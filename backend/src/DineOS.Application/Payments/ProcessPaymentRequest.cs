using FluentValidation;

namespace DineOS.Application.Payments;

/// <summary>
/// Body of <c>POST /api/v1/payments</c>. Settles a single open order with a
/// matching payment amount and a supported payment method. The order is moved
/// to <c>Delivered</c> on success.
/// </summary>
/// <example>
/// {
///   "orderId": 1234,
///   "amount": 18.50,
///   "method": "Card"
/// }
/// </example>
public class ProcessPaymentRequest
{
    /// <summary>
    /// Identifier of the order being paid. Must reference an open order
    /// (i.e. not already <c>Delivered</c> or <c>Cancelled</c>) inside the
    /// current tenant.
    /// </summary>
    /// <example>1234</example>
    public long OrderId { get; set; }

    /// <summary>
    /// Tendered amount. Must equal the order total exactly — partial or
    /// over-payments are rejected with 422.
    /// </summary>
    /// <example>18.50</example>
    public decimal Amount { get; set; }

    /// <summary>
    /// Payment method. Allowed values: <c>Cash</c>, <c>Card</c>.
    /// </summary>
    /// <example>Card</example>
    public string Method { get; set; } = string.Empty;
}

public class ProcessPaymentRequestValidator : AbstractValidator<ProcessPaymentRequest>
{
    private static readonly string[] ValidMethods = ["Cash", "Card"];

    public ProcessPaymentRequestValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0)
            .WithMessage("OrderId must be a valid order identifier.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Method)
            .NotEmpty()
            .Must(m => ValidMethods.Contains(m))
            .WithMessage("Method must be Cash or Card.");
    }
}
