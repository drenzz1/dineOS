namespace DineOS.Domain.Entities;

/// <summary>Single-row table that stores the SuperAdmin's chosen AI provider and API keys.</summary>
public class PlatformAiSettings
{
    public int Id { get; set; }
    public string ActiveProvider { get; set; } = "Anthropic";
    public string AnthropicApiKey { get; set; } = string.Empty;
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string GoogleAiApiKey { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
