using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>
/// AI-assisted endpoints. Each endpoint sits behind a tight rate limit
/// (`ai-expensive`) so a single tenant cannot run up a provider bill.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ai")]
[Produces("application/json")]
[Authorize(Policy = "ManagerAndAbove")]
[EnableRateLimiting("ai-expensive")]
public class AiController(IAiMenuService aiMenuService) : ControllerBase
{
    /// <summary>
    /// Generates a customer-facing description and likely-allergen list for
    /// an existing menu item using the configured AI provider. The result is
    /// a suggestion only — the caller decides whether to persist it.
    /// </summary>
    [HttpPost("menu-items/{id:long}/describe")]
    [ProducesResponseType(typeof(ApiResponse<MenuItemDescriptionSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DescribeMenuItem(long id, CancellationToken ct) =>
        (await aiMenuService.SuggestDescriptionAsync(id, ct)).ToActionResult();
}
