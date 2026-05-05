using Asp.Versioning;
using DineOS.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Platform administration endpoints — SuperAdmin only.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Produces("application/json")]
[Authorize(Policy = "SuperAdminOnly")]
[EnableRateLimiting("authenticated")]
public class AdminController : ControllerBase
{
    /// <summary>Lists all tenants registered on the platform.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetTenants() =>
        Ok(ApiResponse<object>.Ok(Array.Empty<object>(), "Tenant list"));

    /// <summary>Creates a new tenant on the platform.</summary>
    [HttpPost("tenants")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult CreateTenant() =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Ok("Tenant created"));

    /// <summary>Deletes a tenant from the platform.</summary>
    [HttpDelete("tenants/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult DeleteTenant(Guid id) =>
        Ok(ApiResponse.Ok($"Tenant {id} deleted"));

    /// <summary>Lists all platform users.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GetUsers() =>
        Ok(ApiResponse<object>.Ok(Array.Empty<object>(), "User list"));
}
