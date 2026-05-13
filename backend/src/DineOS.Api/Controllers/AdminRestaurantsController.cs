using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Restaurants;
using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>SuperAdmin restaurant management — onboarding, status, and plan changes.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/restaurants")]
[Produces("application/json")]
[Authorize(Policy = Policies.SuperAdminOnly)]
[EnableRateLimiting("authenticated")]
public class AdminRestaurantsController(IAdminRestaurantService restaurantService) : ControllerBase
{
    /// <summary>Lists all restaurants with optional search and pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RestaurantDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] string? search,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct) =>
        (await restaurantService.ListAsync(search, pagination, ct)).ToActionResult();

    /// <summary>Gets a single restaurant by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetRestaurant(long id, CancellationToken ct) =>
        (await restaurantService.GetByIdAsync(id, ct)).ToActionResult();

    /// <summary>Creates a new restaurant (and its corresponding tenant).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateRestaurant(
        [FromBody] CreateRestaurantRequest request,
        CancellationToken ct) =>
        (await restaurantService.CreateAsync(request, ct)).ToActionResult();

    /// <summary>Updates a restaurant's active/suspended status.</summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateStatus(
        long id,
        [FromBody] UpdateRestaurantStatusRequest request,
        CancellationToken ct) =>
        (await restaurantService.UpdateStatusAsync(id, request, ct)).ToActionResult();

    /// <summary>Updates a restaurant's subscription plan.</summary>
    [HttpPatch("{id:long}/plan")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdatePlan(
        long id,
        [FromBody] UpdateRestaurantPlanRequest request,
        CancellationToken ct) =>
        (await restaurantService.UpdatePlanAsync(id, request, ct)).ToActionResult();

    /// <summary>Soft-deletes a restaurant (tenant).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteRestaurant(long id, CancellationToken ct) =>
        (await restaurantService.DeleteAsync(id, ct)).ToActionResult();
}
