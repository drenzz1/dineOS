using DineOS.Application.Interfaces.Services;

namespace DineOS.Infrastructure.Services;

/// <summary>Resolved when no embeddings provider is configured. Throws so callers show a clear error.</summary>
public sealed class NoOpEmbeddingsClient : IEmbeddingsClient
{
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default) =>
        throw new AiUnavailableException("Semantic search is not configured. Visit Admin → Settings to choose an embeddings provider.");
}
