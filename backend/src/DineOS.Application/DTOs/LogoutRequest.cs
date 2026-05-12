using FluentValidation;

namespace DineOS.Application.DTOs;

public record LogoutRequest(string RefreshToken);

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required.")
            .MaximumLength(4096);
    }
}
