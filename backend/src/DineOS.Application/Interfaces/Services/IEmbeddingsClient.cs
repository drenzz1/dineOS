namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Generates dense vector embeddings for text. Separate from IAiClient because
/// Anthropic has no embeddings API — only OpenAI and Google are supported here.
/// </summary>
public interface IEmbeddingsClient
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
