using DineOS.Application.Common;
using DineOS.Application.DTOs;

namespace DineOS.Application.Interfaces.Services;

public interface IAiSettingsService
{
    /// <summary>Synchronous read used by the IAiClient DI factory at scope resolution time.</summary>
    (string Provider, string ApiKey)? GetEffectiveSettings();

    /// <summary>Synchronous read used by the IEmbeddingsClient DI factory at scope resolution time.</summary>
    (string Provider, string ApiKey)? GetEffectiveEmbeddingsSettings();

    Task<AiSettingsDto> GetAsync(CancellationToken ct = default);
    Task<ServiceResult<AiSettingsDto>> SaveAsync(SaveAiSettingsRequest request, CancellationToken ct = default);
    Task<ServiceResult<AiSettingsDto>> SaveEmbeddingsAsync(SaveEmbeddingsSettingsRequest request, CancellationToken ct = default);
    Task<ServiceResult<TestAiConnectionResult>> TestConnectionAsync(TestAiConnectionRequest request, CancellationToken ct = default);
}
