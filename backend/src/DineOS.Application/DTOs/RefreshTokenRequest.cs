using FluentValidation;

namespace DineOS.Application.DTOs;

public record RefreshTokenRequest(string RefreshToken);

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required.")
            .MaximumLength(4096);
    }
}
