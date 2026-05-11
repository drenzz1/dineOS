using FluentValidation;

namespace DineOS.Application.Shifts;

public class UpdateShiftRequest
{
    public long? StaffMemberId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Notes { get; set; }
}

public class UpdateShiftRequestValidator : AbstractValidator<UpdateShiftRequest>
{
    public UpdateShiftRequestValidator()
    {
        When(x => x.StaffMemberId is not null, () =>
            RuleFor(x => x.StaffMemberId!.Value).GreaterThan(0));

        When(x => x.StartTime is not null && x.EndTime is not null, () =>
            RuleFor(x => x.EndTime!.Value)
                .GreaterThan(x => x.StartTime!.Value)
                .WithMessage("EndTime must be after StartTime."));

        When(x => x.Notes is not null, () =>
            RuleFor(x => x.Notes).MaximumLength(500));
    }
}
