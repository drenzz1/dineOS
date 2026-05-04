using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DineOS.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[Authorize]
public class MeController : ControllerBase
{
    /// <summary>Returns the authenticated user's profile decoded from the JWT.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetMe()
    {
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
            .Select(c => c.Value)
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            Id       = User.FindFirstValue("sub"),
            Email    = User.FindFirstValue("email"),
            Username = User.FindFirstValue("preferred_username"),
            Name     = User.FindFirstValue("name"),
            Roles    = roles
        }));
    }
}
