using FluentValidation;

namespace DineOS.Application.DTOs;

/// <summary>
/// Request to start a staff session: the staff member to act as, and their PIN.
/// Sent by a client already authenticated as the business (Keycloak token).
/// </summary>
public sealed class StartStaffSessionRequest
{
    public long StaffMemberId { get; set; }
    public string Pin { get; set; } = string.Empty;
}

public sealed class StartStaffSessionRequestValidator : AbstractValidator<StartStaffSessionRequest>
{
    public StartStaffSessionRequestValidator()
    {
        RuleFor(x => x.StaffMemberId)
            .GreaterThan(0);

        RuleFor(x => x.Pin)
            .NotEmpty()
            .Matches(@"^\d{4}$")
            .WithMessage("Pin must be exactly 4 digits.");
    }
}
