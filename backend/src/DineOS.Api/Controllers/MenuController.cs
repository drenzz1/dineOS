using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Application.Authorization;
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
    [Authorize(Policy = Policies.ManagerAndAbove)]
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
    [Authorize(Policy = Policies.ManagerAndAbove)]
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
    [Authorize(Policy = Policies.ManagerAndAbove)]
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

    /// <summary>Uploads or replaces the image for a menu item.</summary>
    /// <remarks>
    /// Accepts a single image file via the <c>image</c> multipart form field.
    /// The original filename is discarded — the server stores the file under a
    /// UUID-based name to prevent collisions and path-traversal attacks.
    ///
    /// **Accepted types:** `image/jpeg` (.jpg / .jpeg), `image/png` (.png), `image/webp` (.webp)
    ///
    /// **Size limit:** 5 MB (configurable via `FileStorage:MaxBytes`)
    ///
    /// On success, `data.imageUrl` contains a root-relative URL that can be appended
    /// to the API base to display the image:
    ///
    ///     GET /uploads/menu-items/3f1a2b4c5d6e7f8a9b0c1d2e3f4a5b6c.png
    ///
    /// **Validation error codes returned in 400 `errors` dictionary:**
    ///
    /// | Code | Meaning |
    /// |---|---|
    /// | `FILE_EMPTY` | File has zero bytes |
    /// | `FILE_TOO_LARGE` | Exceeds 5 MB limit |
    /// | `UNSUPPORTED_CONTENT_TYPE` | Content-Type is not jpeg / png / webp |
    /// | `INVALID_EXTENSION` | Extension is not .jpg / .jpeg / .png / .webp |
    /// | `EXTENSION_MISMATCH` | Content-Type does not match the file extension |
    ///
    /// Example curl:
    ///
    ///     curl -X POST /api/v1/menu/items/42/image \
    ///       -H "Authorization: Bearer $TOKEN" \
    ///       -F "image=@photo.png;type=image/png"
    /// </remarks>
    /// <param name="id">ID of the menu item to attach the image to.</param>
    /// <param name="image">Image file (multipart form field name: <c>image</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Image uploaded — `data.imageUrl` contains the root-relative URL.</response>
    /// <response code="400">Validation failure — `errors` dictionary contains one or more error codes from the table above.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="403">Caller does not have the Manager or SuperAdmin role.</response>
    /// <response code="404">No menu item with the given ID exists for this tenant.</response>
    /// <response code="429">Rate limit exceeded.</response>
    [HttpPost("items/{id:long}/image")]
    [Authorize(Policy = Policies.ManagerAndAbove)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<MenuItemImageUploadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UploadMenuItemImage(
        long id,
        IFormFile image,
        CancellationToken ct)
    {
        var request = new UploadMenuItemImageRequest(image.OpenReadStream(), image.FileName, image.ContentType, image.Length);
        return (await menuService.UploadMenuItemImageAsync(id, request, ct)).ToActionResult();
    }

    /// <summary>Soft-deletes a menu item by ID.</summary>
    [HttpDelete("items/{id:long}")]
    [Authorize(Policy = Policies.ManagerAndAbove)]
    [ProducesResponseType(typeof(ApiResponse<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteMenuItem(long id, CancellationToken ct) =>
        (await menuService.DeleteMenuItemAsync(id, ct)).ToActionResult();

    /// <summary>Returns up to 10 menu items ranked by semantic similarity to the query.</summary>
    [HttpPost("items/semantic-search")]
    [EnableRateLimiting("ai-expensive")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SemanticSearch(
        [FromBody] SemanticMenuSearchRequest request,
        CancellationToken ct) =>
        (await menuService.SemanticSearchMenuItemsAsync(request.Query, ct)).ToActionResult();

    /// <summary>Lists all menu categories for the current tenant.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<List<MenuCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetMenuCategories(CancellationToken ct) =>
        (await menuService.GetCategoriesAsync(ct)).ToActionResult();

    /// <summary>Adds a new menu category.</summary>
    [HttpPost("categories")]
    [Authorize(Policy = Policies.ManagerAndAbove)]
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
