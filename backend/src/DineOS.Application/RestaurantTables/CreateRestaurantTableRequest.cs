using FluentValidation;

namespace DineOS.Application.RestaurantTables;

public class CreateRestaurantTableRequest
{
    public int Number { get; set; }
    public int Capacity { get; set; }
    public string? Location { get; set; }
}

public class CreateRestaurantTableRequestValidator : AbstractValidator<CreateRestaurantTableRequest>
{
    public CreateRestaurantTableRequestValidator()
    {
        RuleFor(x => x.Number).GreaterThan(0);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 50);
        When(x => x.Location is not null, () =>
            RuleFor(x => x.Location).MaximumLength(100));
    }
}
