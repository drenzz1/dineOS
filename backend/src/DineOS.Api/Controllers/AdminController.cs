using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Platform administration endpoints — SuperAdmin only.</summary>
/// <remarks>
/// Tenant/restaurant CRUD lives under <c>/api/v1/admin/restaurants</c>
/// (see <see cref="AdminRestaurantsController"/>); this controller exposes
/// platform-level views that aren't restaurant-specific.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Produces("application/json")]
[Authorize(Policy = Policies.SuperAdminOnly)]
[EnableRateLimiting("authenticated")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    /// <summary>Lists platform staff users across all tenants. Note: this is the
    /// internal staff/PIN account list. Keycloak login-account management is a
    /// separate, future integration.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PlatformUserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct) =>
        (await adminService.ListUsersAsync(search, pagination, ct)).ToActionResult();
}
