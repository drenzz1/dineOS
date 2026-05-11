using FluentValidation;

namespace DineOS.Application.Orders;

public class CreateOrderItemRequest
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class CreateOrderRequest
{
    public string OrderType { get; set; } = string.Empty;
    public int? TableNumber { get; set; }
    public string? Notes { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    private static readonly string[] ValidOrderTypes = ["dine-in", "pickup"];

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.OrderType)
            .NotEmpty()
            .Must(t => ValidOrderTypes.Contains(t))
            .WithMessage("OrderType must be dine-in or pickup.");

        When(x => x.OrderType == "dine-in", () =>
            RuleFor(x => x.TableNumber)
                .NotNull()
                .GreaterThan(0)
                .WithMessage("TableNumber is required for dine-in orders."));

        RuleFor(x => x.Notes)
            .MaximumLength(300)
            .When(x => x.Notes is not null);

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("An order must have at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Name)
                .NotEmpty()
                .MaximumLength(100);

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithMessage("Item quantity must be greater than zero.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThan(0)
                .WithMessage("Item unit price must be greater than zero.");
        });
    }
}
