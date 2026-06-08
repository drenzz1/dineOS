using FluentValidation;

namespace DineOS.Application.DTOs;

/// <summary>
/// Completes the first-login password rotation for a freshly provisioned
/// tenant owner (#205). The owner authenticates with the temporary password
/// emailed by the Stripe webhook flow and chooses a permanent replacement;
/// the server clears the <c>UPDATE_PASSWORD</c> Keycloak required action and
/// returns a normal token pair so the FE can proceed straight to the app.
/// </summary>
public record FirstLoginPasswordChangeRequest(string Email, string CurrentPassword, string NewPassword);

public class FirstLoginPasswordChangeRequestValidator : AbstractValidator<FirstLoginPasswordChangeRequest>
{
    public FirstLoginPasswordChangeRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid address.")
            .MaximumLength(254);

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.")
            .MaximumLength(200);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(12).WithMessage("New password must be at least 12 characters.")
            .MaximumLength(200)
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the temporary password.");
    }
}
