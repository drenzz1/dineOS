using FluentValidation;

namespace DineOS.Application.Restaurants;

public sealed class ConfirmEmailVerificationRequest
{
    public string Code { get; init; } = string.Empty;
}

public class ConfirmEmailVerificationRequestValidator : AbstractValidator<ConfirmEmailVerificationRequest>
{
    public ConfirmEmailVerificationRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Verification code is required.")
            .Matches(@"^\d{6}$")
            .WithMessage("Verification code must be exactly 6 digits.");
    }
}
