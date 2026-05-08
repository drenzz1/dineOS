using FluentValidation;

namespace DineOS.Application.Payments;

public class ProcessPaymentRequest
{
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
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
