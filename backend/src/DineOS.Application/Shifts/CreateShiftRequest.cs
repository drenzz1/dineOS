using FluentValidation;

namespace DineOS.Application.Shifts;

public class CreateShiftRequest
{
    public long StaffMemberId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Notes { get; set; }
}

public class CreateShiftRequestValidator : AbstractValidator<CreateShiftRequest>
{
    public CreateShiftRequestValidator()
    {
        RuleFor(x => x.StaffMemberId).GreaterThan(0);
        RuleFor(x => x.StartTime).NotEqual(default(DateTime));
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("EndTime must be after StartTime.");
        When(x => x.Notes is not null, () =>
            RuleFor(x => x.Notes).MaximumLength(500));
    }
}
