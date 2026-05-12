namespace DineOS.Application.Options;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host       { get; init; } = "localhost";
    public int    Port       { get; init; } = 1025;
    public string Username   { get; init; } = string.Empty;
    public string Password   { get; init; } = string.Empty;
    public bool   UseStartTls { get; init; } = false;
}
