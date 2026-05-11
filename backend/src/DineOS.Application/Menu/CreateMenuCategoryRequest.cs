using FluentValidation;

namespace DineOS.Application.Menu;

public class CreateMenuCategoryRequest
{
    public string Name { get; set; } = string.Empty;
}

public class CreateMenuCategoryRequestValidator : AbstractValidator<CreateMenuCategoryRequest>
{
    public CreateMenuCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
