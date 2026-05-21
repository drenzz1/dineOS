using FluentValidation;

namespace DineOS.Application.DemoAccess;

/// <summary>
/// Payload for <c>POST /api/v1/demo/request</c> (#216). The
/// <see cref="CompanyName"/> field is a honeypot: legitimate clients
/// must leave it empty.
/// </summary>
public sealed class RequestDemoAccessRequest
{
    public string Email { get; set; } = string.Empty;
    public bool   AcceptedTerms { get; set; }

    /// <summary>Honeypot. Hidden from the UI; bots fill it.</summary>
    public string? CompanyName { get; set; }
}

public sealed class RequestDemoAccessRequestValidator : AbstractValidator<RequestDemoAccessRequest>
{
    public RequestDemoAccessRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.AcceptedTerms)
            .Equal(true)
            .WithMessage("You must accept the demo terms to continue.");
    }
}
