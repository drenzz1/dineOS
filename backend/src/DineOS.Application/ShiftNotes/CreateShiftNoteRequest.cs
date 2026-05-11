using DineOS.Domain.Enums;
using FluentValidation;

namespace DineOS.Application.ShiftNotes;

public class CreateShiftNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

public class CreateShiftNoteRequestValidator : AbstractValidator<CreateShiftNoteRequest>
{
    public CreateShiftNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(p => Enum.TryParse<ShiftNotePriority>(p, out _))
            .WithMessage("Priority must be one of: Info, Warning, Urgent.");

        RuleFor(x => x.Author)
            .NotEmpty()
            .MaximumLength(100);
    }
}
