using FluentValidation;

namespace DineOS.Application.Signup;

public class SetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class SetPasswordRequestValidator : AbstractValidator<SetPasswordRequest>
{
    public SetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}
