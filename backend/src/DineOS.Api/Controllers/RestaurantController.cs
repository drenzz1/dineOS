using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.RestaurantProfile;
using DineOS.Application.RestaurantTables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Restaurant operations endpoints — Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/restaurant")]
[Produces("application/json")]
[Authorize(Policy = "ManagerAndAbove")]
[EnableRateLimiting("authenticated")]
public class RestaurantController(IRestaurantService restaurantService) : ControllerBase
{
    /// <summary>Gets the current tenant's restaurant profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<RestaurantProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetRestaurant(CancellationToken ct) =>
        (await restaurantService.GetProfileAsync(ct)).ToActionResult();

    /// <summary>Updates the restaurant's profile fields (name, owner name, phone, city).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<RestaurantProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateRestaurant(
        [FromBody] UpdateRestaurantProfileRequest request,
        CancellationToken ct) =>
        (await restaurantService.UpdateProfileAsync(request, ct)).ToActionResult();

    /// <summary>Lists all tables for the current tenant.</summary>
    [HttpGet("tables")]
    [ProducesResponseType(typeof(ApiResponse<List<RestaurantTableDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetTables(CancellationToken ct) =>
        (await restaurantService.ListTablesAsync(ct)).ToActionResult();

    /// <summary>Adds a new table.</summary>
    [HttpPost("tables")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantTableDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AddTable(
        [FromBody] CreateRestaurantTableRequest request,
        CancellationToken ct) =>
        (await restaurantService.AddTableAsync(request, ct)).ToActionResult();

    /// <summary>Updates a table's details.</summary>
    [HttpPut("tables/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantTableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateTable(
        long id,
        [FromBody] UpdateRestaurantTableRequest request,
        CancellationToken ct) =>
        (await restaurantService.UpdateTableAsync(id, request, ct)).ToActionResult();
}
