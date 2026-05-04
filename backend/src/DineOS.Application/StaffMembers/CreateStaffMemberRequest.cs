using FluentValidation;

namespace DineOS.Application.StaffMembers;

public class CreateStaffMemberRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
}

public class CreateStaffMemberRequestValidator : AbstractValidator<CreateStaffMemberRequest>
{
    private static readonly string[] ValidRoles = ["Manager", "Cashier", "KitchenStaff", "SuperAdmin"];

    public CreateStaffMemberRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => ValidRoles.Contains(r))
            .WithMessage("Role must be one of: Manager, Cashier, KitchenStaff, SuperAdmin.");

        RuleFor(x => x.Pin)
            .NotEmpty()
            .Matches(@"^\d{4}$")
            .WithMessage("Pin must be exactly 4 digits.");
    }
}
