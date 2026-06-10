using FluentValidation;

namespace DineOS.Application.DTOs;

public record ResetPasswordRequest(string Email, string Code, string NewPassword);

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(320);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Reset code is required.")
            .Matches(@"^\d{6}$").WithMessage("Reset code must be exactly 6 digits.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(12).WithMessage("New password must be at least 12 characters.")
            .MaximumLength(200);
    }
}
