using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Platform AI provider configuration — SuperAdmin only.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/settings/ai")]
[Produces("application/json")]
[Authorize(Policy = Policies.SuperAdminOnly)]
[EnableRateLimiting("authenticated")]
public class AiSettingsController(IAiSettingsService aiSettingsService) : ControllerBase
{
    /// <summary>Returns the current AI provider configuration (API keys masked).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AiSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dto = await aiSettingsService.GetAsync(ct);
        return Ok(ApiResponse<AiSettingsDto>.Ok(dto));
    }

    /// <summary>Saves the chosen provider and its API key.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<AiSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Save([FromBody] SaveAiSettingsRequest request, CancellationToken ct) =>
        (await aiSettingsService.SaveAsync(request, ct)).ToActionResult();

    /// <summary>Saves the embeddings provider and its API key for semantic search.</summary>
    [HttpPut("embeddings")]
    [ProducesResponseType(typeof(ApiResponse<AiSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveEmbeddings([FromBody] SaveEmbeddingsSettingsRequest request, CancellationToken ct) =>
        (await aiSettingsService.SaveEmbeddingsAsync(request, ct)).ToActionResult();

    /// <summary>Tests a provider + API key by making a minimal live API call.</summary>
    [HttpPost("test")]
    [EnableRateLimiting("ai-expensive")]
    [ProducesResponseType(typeof(ApiResponse<TestAiConnectionResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> TestConnection([FromBody] TestAiConnectionRequest request, CancellationToken ct) =>
        (await aiSettingsService.TestConnectionAsync(request, ct)).ToActionResult();
}
