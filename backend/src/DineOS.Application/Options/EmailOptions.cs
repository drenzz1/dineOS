namespace DineOS.Application.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool   Enabled         { get; init; } = true;
    public string FromAddress     { get; init; } = "no-reply@dineos.local";
    public string FromName        { get; init; } = "DineOS";

    // Dev-only flag that forces the sender to throw, used to exercise the
    // Hangfire retry and dead-letter pipeline end to end without breaking SMTP.
    public bool   SimulateFailure { get; init; } = false;
}
