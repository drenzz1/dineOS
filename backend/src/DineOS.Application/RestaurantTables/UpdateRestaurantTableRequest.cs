using FluentValidation;

namespace DineOS.Application.RestaurantTables;

public class UpdateRestaurantTableRequest
{
    public int? Number { get; set; }
    public int? Capacity { get; set; }
    public string? Location { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateRestaurantTableRequestValidator : AbstractValidator<UpdateRestaurantTableRequest>
{
    public UpdateRestaurantTableRequestValidator()
    {
        When(x => x.Number is not null, () =>
            RuleFor(x => x.Number!.Value).GreaterThan(0));
        When(x => x.Capacity is not null, () =>
            RuleFor(x => x.Capacity!.Value).InclusiveBetween(1, 50));
        When(x => x.Location is not null, () =>
            RuleFor(x => x.Location).MaximumLength(100));
    }
}
