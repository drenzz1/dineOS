using FluentValidation;

namespace DineOS.Application.StaffMembers;

public class SetStaffActiveRequest
{
    public bool IsActive { get; set; }
}

public class SetStaffActiveRequestValidator : AbstractValidator<SetStaffActiveRequest>
{
    public SetStaffActiveRequestValidator() { }
}
