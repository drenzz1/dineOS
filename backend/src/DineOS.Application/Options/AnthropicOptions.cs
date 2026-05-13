namespace DineOS.Application.Options;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>Anthropic API key. Loaded from env (Anthropic__ApiKey) or user-secrets — never commit.</summary>
    public string ApiKey         { get; init; } = string.Empty;

    /// <summary>Anthropic model identifier. Override through configuration to use any supported model.</summary>
    public string Model          { get; init; } = "claude-sonnet-4-5";

    public string BaseUrl        { get; init; } = "https://api.anthropic.com";
    public string ApiVersion     { get; init; } = "2023-06-01";

    public int    MaxTokens      { get; init; } = 400;
    public int    TimeoutSeconds { get; init; } = 20;
}
