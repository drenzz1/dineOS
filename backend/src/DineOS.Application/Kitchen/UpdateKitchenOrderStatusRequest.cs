using FluentValidation;

namespace DineOS.Application.Kitchen;

public class UpdateKitchenOrderStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateKitchenOrderStatusRequestValidator : AbstractValidator<UpdateKitchenOrderStatusRequest>
{
    private static readonly string[] AllowedStatuses = ["New", "InProgress", "Ready"];

    public UpdateKitchenOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage("Kitchen status must be one of: New, InProgress, Ready.");
    }
}
