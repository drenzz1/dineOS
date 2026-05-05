using FluentValidation;

namespace DineOS.Application.Restaurants;

public class UpdateRestaurantStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateRestaurantStatusRequestValidator : AbstractValidator<UpdateRestaurantStatusRequest>
{
    private static readonly string[] ValidStatuses = ["Active", "Suspended"];

    public UpdateRestaurantStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage("Status must be Active or Suspended.");
    }
}
