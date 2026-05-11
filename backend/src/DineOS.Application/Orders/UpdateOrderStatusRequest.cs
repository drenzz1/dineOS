using DineOS.Domain.Enums;
using FluentValidation;

namespace DineOS.Application.Orders;

public class UpdateOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<OrderStatus>(s, out _))
            .WithMessage("Status must be one of: New, InProgress, Ready, Delivered, Cancelled.");
    }
}
