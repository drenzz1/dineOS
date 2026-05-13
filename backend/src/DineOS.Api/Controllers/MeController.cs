using Asp.Versioning;
using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DineOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class MeController : ControllerBase
{
    /// <summary>Returns the authenticated user's profile decoded from the JWT.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetMe()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
            .Select(c => c.Value)
            .ToList();

        var tenantId = User.FindFirstValue("tenant_id");

        return Ok(ApiResponse<object>.Ok(new
        {
            Id       = User.FindFirstValue("sub"),
            Email    = User.FindFirstValue("email"),
            Username = User.FindFirstValue("preferred_username"),
            Name     = User.FindFirstValue("name"),
            Roles    = roles,
            TenantId = tenantId
        }));
    }
}
