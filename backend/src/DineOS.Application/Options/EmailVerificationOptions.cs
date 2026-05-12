namespace DineOS.Application.Options;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    public int CodeTtlMinutes      { get; init; } = 15;
    public int MaxAttemptsPerCode  { get; init; } = 5;
}
