namespace DineOS.Application.DTOs;

public sealed record AiSettingsDto(
    string ActiveProvider,
    string? AnthropicApiKeyHint,
    string? OpenAiApiKeyHint,
    string? GoogleAiApiKeyHint,
    string EmbeddingsProvider,
    string? EmbeddingsApiKeyHint,
    DateTime? UpdatedAt);

public sealed record SaveAiSettingsRequest(
    string Provider,
    string ApiKey);

public sealed record SaveEmbeddingsSettingsRequest(
    string Provider,
    string ApiKey);

public sealed record TestAiConnectionRequest(
    string Provider,
    string ApiKey);

public sealed record TestAiConnectionResult(
    bool Success,
    string? Error,
    string? Model);
