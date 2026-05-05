using FluentValidation;

namespace DineOS.Application.Restaurants;

public class CreateRestaurantRequest
{
    public string Name { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
}

public class CreateRestaurantRequestValidator : AbstractValidator<CreateRestaurantRequest>
{
    private static readonly string[] ValidPlans = ["Free", "Pro"];

    public CreateRestaurantRequestValidator()
    {
        RuleFor(x => x.Name)
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

        RuleFor(x => x.Plan)
            .NotEmpty()
            .Must(p => ValidPlans.Contains(p))
            .WithMessage("Plan must be Free or Pro.");
    }
}
