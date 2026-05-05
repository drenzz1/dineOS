using FluentValidation;

namespace DineOS.Application.Restaurants;

public class UpdateRestaurantPlanRequest
{
    public string Plan { get; set; } = string.Empty;
}

public class UpdateRestaurantPlanRequestValidator : AbstractValidator<UpdateRestaurantPlanRequest>
{
    private static readonly string[] ValidPlans = ["Free", "Pro"];

    public UpdateRestaurantPlanRequestValidator()
    {
        RuleFor(x => x.Plan)
            .NotEmpty()
            .Must(p => ValidPlans.Contains(p))
            .WithMessage("Plan must be Free or Pro.");
    }
}
