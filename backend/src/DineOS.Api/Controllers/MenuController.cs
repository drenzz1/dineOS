using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DineOS.Api.Controllers;

/// <summary>Menu management endpoints — read: all authenticated staff; write: Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/menu")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class MenuController(IMenuService menuService) : ControllerBase
{
    /// <summary>Lists all menu items for the current tenant (base route, Manager and above).</summary>
    [HttpGet]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetMenu(CancellationToken ct) =>
        (await menuService.GetMenuItemsAsync(ct)).ToActionResult();

    /// <summary>Lists all menu items for the current tenant, ordered by category then name.</summary>
    [HttpGet("items")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetMenuItems(CancellationToken ct) =>
        (await menuService.GetMenuItemsAsync(ct)).ToActionResult();

    /// <summary>Adds a new menu item.</summary>
    [HttpPost("items")]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<MenuItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateMenuItem(
        [FromBody] CreateMenuItemRequest request,
        CancellationToken ct) =>
        (await menuService.CreateMenuItemAsync(request, ct)).ToActionResult();

    /// <summary>Updates an existing menu item.</summary>
    [HttpPut("items/{id:long}")]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UpdateMenuItem(
        long id,
        [FromBody] UpdateMenuItemRequest request,
        CancellationToken ct) =>
        (await menuService.UpdateMenuItemAsync(id, request, ct)).ToActionResult();

    /// <summary>Soft-deletes a menu item by ID.</summary>
    [HttpDelete("items/{id:long}")]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteMenuItem(long id, CancellationToken ct) =>
        (await menuService.DeleteMenuItemAsync(id, ct)).ToActionResult();

    /// <summary>Lists all menu categories for the current tenant.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetMenuCategories(CancellationToken ct) =>
        (await menuService.GetCategoriesAsync(ct)).ToActionResult();

    /// <summary>Adds a new menu category.</summary>
    [HttpPost("categories")]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<MenuCategoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateMenuCategory(
        [FromBody] CreateMenuCategoryRequest request,
        CancellationToken ct) =>
        (await menuService.CreateCategoryAsync(request, ct)).ToActionResult();
}
