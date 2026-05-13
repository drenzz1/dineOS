namespace DineOS.Application.Options;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-4o-mini";
    public string BaseUrl { get; init; } = "https://api.openai.com";

    public int MaxTokens { get; init; } = 400;
    public int TimeoutSeconds { get; init; } = 20;
}
