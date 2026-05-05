using Asp.Versioning;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Restaurants;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Api.Controllers;

/// <summary>SuperAdmin restaurant management — onboarding, status, and plan changes.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/restaurants")]
[Produces("application/json")]
[Authorize(Policy = "SuperAdminOnly")]
[EnableRateLimiting("authenticated")]
public class AdminRestaurantsController(
    AppDbContext db,
    IValidator<CreateRestaurantRequest> createValidator,
    IValidator<UpdateRestaurantStatusRequest> statusValidator,
    IValidator<UpdateRestaurantPlanRequest> planValidator) : ControllerBase
{
    /// <summary>Lists all restaurants with optional search and pagination.</summary>
    /// <param name="search">Optional search term matched against name or owner email.</param>
    /// <param name="pagination">Offset pagination parameters (page, pageSize).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RestaurantDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetRestaurants(
        [FromQuery] string? search,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var query = db.Tenants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(q) ||
                t.OwnerEmail.ToLower().Contains(q));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Name)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(t => ToDto(t))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResponse<RestaurantDto>>.Ok(
            PagedResponse<RestaurantDto>.From(items, total, pagination)));
    }

    /// <summary>Gets a single restaurant by ID.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetRestaurant(long id, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tenant is null)
            return NotFound(ApiResponse.Fail($"Restaurant {id} not found."));

        return Ok(ApiResponse<RestaurantDto>.Ok(ToDto(tenant)));
    }

    /// <summary>Creates a new restaurant (and its corresponding tenant).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RestaurantDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateRestaurant(
        [FromBody] CreateRestaurantRequest request,
        CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var slug = GenerateSlug(request.Name);
        var plan = Enum.TryParse<SubscriptionPlan>(request.Plan, out var p) ? p : SubscriptionPlan.Free;

        var tenant = new Tenant
        {
            Name = request.Name,
            Slug = slug,
            IsActive = true,
            OwnerName = request.OwnerName,
            OwnerEmail = request.OwnerEmail,
            Phone = request.Phone,
            City = request.City,
            Plan = plan,
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<RestaurantDto>.Ok(ToDto(tenant), "Restaurant created."));
    }

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
        CancellationToken ct)
    {
        var validation = await statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null)
            return NotFound(ApiResponse.Fail($"Restaurant {id} not found."));

        tenant.IsActive = request.Status == "Active";
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<RestaurantDto>.Ok(ToDto(tenant)));
    }

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
        CancellationToken ct)
    {
        var validation = await planValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));

        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null)
            return NotFound(ApiResponse.Fail($"Restaurant {id} not found."));

        tenant.Plan = Enum.TryParse<SubscriptionPlan>(request.Plan, out var p) ? p : tenant.Plan;
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<RestaurantDto>.Ok(ToDto(tenant)));
    }

    private static RestaurantDto ToDto(Tenant t) => new(
        t.Id,
        t.Name,
        t.OwnerName,
        t.OwnerEmail,
        t.Phone,
        t.City,
        t.Plan.ToString(),
        t.IsActive ? "Active" : "Suspended",
        t.TotalOrders,
        t.StaffCount,
        t.Revenue,
        t.CreatedAt
    );

    private static string GenerateSlug(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToLower().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}
