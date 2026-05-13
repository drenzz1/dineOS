namespace DineOS.Application.Options;

public sealed class GoogleAiOptions
{
    public const string SectionName = "GoogleAI";

    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gemini-2.5-flash";
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com";
    public string ApiVersion { get; init; } = "v1beta";

    public int MaxTokens { get; init; } = 400;
    public int TimeoutSeconds { get; init; } = 20;
}
