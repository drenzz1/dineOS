using FluentValidation;

namespace DineOS.Application.RestaurantProfile;

public class UpdateRestaurantProfileRequest
{
    public string? Name { get; set; }
    public string? OwnerName { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
}

public class UpdateRestaurantProfileRequestValidator : AbstractValidator<UpdateRestaurantProfileRequest>
{
    public UpdateRestaurantProfileRequestValidator()
    {
        When(x => x.Name is not null, () =>
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200));
        When(x => x.OwnerName is not null, () =>
            RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(100));
        When(x => x.Phone is not null, () =>
            RuleFor(x => x.Phone).NotEmpty().MaximumLength(30));
        When(x => x.City is not null, () =>
            RuleFor(x => x.City).NotEmpty().MaximumLength(100));
    }
}
