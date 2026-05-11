using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Api.Controllers;

/// <summary>Menu management endpoints — read: all authenticated staff; write: Manager and above.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/menu")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("authenticated")]
public class MenuController(
    AppDbContext db,
    ITenantService tenantService,
    IValidator<CreateMenuItemRequest> createItemValidator,
    IValidator<UpdateMenuItemRequest> updateItemValidator,
    IValidator<CreateMenuCategoryRequest> createCategoryValidator) : ControllerBase
{
    // ── Base route (ManagerAndAbove) ─────────────────────────────────────────

    /// <summary>Lists all menu items for the current tenant (base route, Manager and above).</summary>
    [HttpGet]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetMenu(CancellationToken ct)
    {
        var items = await db.MenuItems
            .AsNoTracking()
            .OrderBy(mi => mi.Category)
            .ThenBy(mi => mi.Name)
            .Select(mi => new MenuItemDto
            {
                Id          = mi.Id,
                Name        = mi.Name,
                Price       = mi.Price,
                Category    = mi.Category,
                Description = mi.Description,
                ImageUrl    = mi.ImageUrl,
                TenantId    = mi.TenantId,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<MenuItemDto>>.Ok(items, "Menu items"));
    }

    // ── Menu Items ────────────────────────────────────────────────────────────

    /// <summary>Lists all menu items for the current tenant, ordered by category then name.</summary>
    [HttpGet("items")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetMenuItems(CancellationToken ct)
    {
        var items = await db.MenuItems
            .AsNoTracking()
            .OrderBy(mi => mi.Category)
            .ThenBy(mi => mi.Name)
            .Select(mi => new MenuItemDto
            {
                Id          = mi.Id,
                Name        = mi.Name,
                Price       = mi.Price,
                Category    = mi.Category,
                Description = mi.Description,
                ImageUrl    = mi.ImageUrl,
                TenantId    = mi.TenantId,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<MenuItemDto>>.Ok(items, "Menu items"));
    }

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
        CancellationToken ct)
    {
        var validation = await createItemValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (tenantService.TenantId is not { } tenantId)
            return BadRequest(ApiResponse.Fail("Tenant context is required."));

        var item = new MenuItem
        {
            TenantId    = tenantId,
            Name        = request.Name,
            Price       = request.Price,
            Category    = request.Category,
            Description = request.Description,
            ImageUrl    = request.ImageUrl,
        };

        db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<MenuItemDto>.Ok(ToItemDto(item), "Menu item created."));
    }

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
        CancellationToken ct)
    {
        var validation = await updateItemValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        var item = await db.MenuItems.FirstOrDefaultAsync(mi => mi.Id == id, ct);
        if (item is null)
            return NotFound(ApiResponse.Fail($"Menu item {id} not found."));

        item.Name        = request.Name;
        item.Price       = request.Price;
        item.Category    = request.Category;
        item.Description = request.Description;
        item.ImageUrl    = request.ImageUrl;

        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<MenuItemDto>.Ok(ToItemDto(item), "Menu item updated."));
    }

    /// <summary>Soft-deletes a menu item by ID.</summary>
    [HttpDelete("items/{id:long}")]
    [Authorize(Policy = "ManagerAndAbove")]
    [ProducesResponseType(typeof(ApiResponse<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteMenuItem(long id, CancellationToken ct)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(mi => mi.Id == id, ct);
        if (item is null)
            return NotFound(ApiResponse.Fail($"Menu item {id} not found."));

        db.MenuItems.Remove(item);
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<MenuItemDto>.Ok(ToItemDto(item), $"Menu item {id} deleted."));
    }

    // ── Menu Categories ───────────────────────────────────────────────────────

    /// <summary>Lists all menu categories for the current tenant.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetMenuCategories(CancellationToken ct)
    {
        var categories = await db.MenuCategories
            .AsNoTracking()
            .OrderBy(mc => mc.Name)
            .Select(mc => new MenuCategoryDto
            {
                Id       = mc.Id,
                Name     = mc.Name,
                TenantId = mc.TenantId,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<MenuCategoryDto>>.Ok(categories, "Menu categories"));
    }

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
        CancellationToken ct)
    {
        var validation = await createCategoryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed",
                validation.Errors.Select(e => e.ErrorMessage)));

        if (tenantService.TenantId is not { } tenantId)
            return BadRequest(ApiResponse.Fail("Tenant context is required."));

        var category = new MenuCategory
        {
            TenantId = tenantId,
            Name     = request.Name,
        };

        db.MenuCategories.Add(category);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<MenuCategoryDto>.Ok(ToCategoryDto(category), "Menu category created."));
    }

    private static MenuItemDto ToItemDto(MenuItem mi) => new()
    {
        Id          = mi.Id,
        Name        = mi.Name,
        Price       = mi.Price,
        Category    = mi.Category,
        Description = mi.Description,
        ImageUrl    = mi.ImageUrl,
        TenantId    = mi.TenantId,
    };

    private static MenuCategoryDto ToCategoryDto(MenuCategory mc) => new()
    {
        Id       = mc.Id,
        Name     = mc.Name,
        TenantId = mc.TenantId,
    };
}
