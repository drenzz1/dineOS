using DineOS.Application.Authorization;
using FluentValidation;

namespace DineOS.Application.StaffMembers;

public class UpdateStaffMemberRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Pin { get; set; }
}

public class UpdateStaffMemberRequestValidator : AbstractValidator<UpdateStaffMemberRequest>
{
    private static readonly string[] ValidRoles = [Roles.Manager, Roles.Cashier, Roles.KitchenStaff, Roles.SuperAdmin];

    public UpdateStaffMemberRequestValidator()
    {
        When(x => x.FullName is not null, () =>
            RuleFor(x => x.FullName)
                .NotEmpty()
                .MaximumLength(100));

        When(x => x.Email is not null, () =>
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress());

        When(x => x.Role is not null, () =>
            RuleFor(x => x.Role)
                .Must(r => ValidRoles.Contains(r!))
                .WithMessage("Role must be one of: Manager, Cashier, KitchenStaff, SuperAdmin."));

        When(x => x.Pin is not null, () =>
            RuleFor(x => x.Pin)
                .Matches(@"^\d{4}$")
                .WithMessage("Pin must be exactly 4 digits."));
    }
}
