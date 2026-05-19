using FluentValidation;

namespace DineOS.Application.Signup;

public class SignupRequest
{
    public string RestaurantName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class SignupRequestValidator : AbstractValidator<SignupRequest>
{
    public SignupRequestValidator()
    {
        RuleFor(x => x.RestaurantName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.OwnerName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.OwnerEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(100);
    }
}
