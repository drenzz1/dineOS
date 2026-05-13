namespace DineOS.Application.Options;

public sealed class AiProviderOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; init; } = Providers.Anthropic;

    public static class Providers
    {
        public const string Anthropic = "Anthropic";
        public const string OpenAI = "OpenAI";
        public const string Google = "Google";
    }
}
