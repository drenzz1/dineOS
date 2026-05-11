using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.RestaurantProfile;
using DineOS.Application.RestaurantTables;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class RestaurantService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IValidator<UpdateRestaurantProfileRequest> profileValidator,
    IValidator<CreateRestaurantTableRequest> createTableValidator,
    IValidator<UpdateRestaurantTableRequest> updateTableValidator,
    ILogger<RestaurantService> logger) : IRestaurantService
{
    public async Task<ServiceResult<RestaurantProfileDto>> GetProfileAsync(CancellationToken ct = default)
    {
        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<RestaurantProfileDto>.BadRequest("Tenant context is required.");

        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (tenant is null)
            return ServiceResult<RestaurantProfileDto>.NotFound($"Restaurant {tenantId} not found.");

        return ServiceResult<RestaurantProfileDto>.Ok(ToProfileDto(tenant));
    }

    public async Task<ServiceResult<RestaurantProfileDto>> UpdateProfileAsync(
        UpdateRestaurantProfileRequest request,
        CancellationToken ct = default)
    {
        var validation = await profileValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<RestaurantProfileDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<RestaurantProfileDto>.BadRequest("Tenant context is required.");

        var tenant = await db.Tenants.FindAsync([tenantId], ct);
        if (tenant is null)
            return ServiceResult<RestaurantProfileDto>.NotFound($"Restaurant {tenantId} not found.");

        if (request.Name is not null)      tenant.Name = request.Name;
        if (request.OwnerName is not null) tenant.OwnerName = request.OwnerName;
        if (request.Phone is not null)     tenant.Phone = request.Phone;
        if (request.City is not null)      tenant.City = request.City;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Restaurant profile updated: RestaurantId={RestaurantId} ActorUserId={ActorUserId}",
            tenant.Id, currentUserService.UserId);

        return ServiceResult<RestaurantProfileDto>.Ok(ToProfileDto(tenant), "Restaurant profile updated.");
    }

    public async Task<ServiceResult<List<RestaurantTableDto>>> ListTablesAsync(CancellationToken ct = default)
    {
        var tables = await db.RestaurantTables
            .AsNoTracking()
            .OrderBy(t => t.Number)
            .Select(t => new RestaurantTableDto
            {
                Id       = t.Id,
                Number   = t.Number,
                Capacity = t.Capacity,
                Location = t.Location,
                IsActive = t.IsActive,
                TenantId = t.TenantId,
            })
            .ToListAsync(ct);

        return ServiceResult<List<RestaurantTableDto>>.Ok(tables, "Tables");
    }

    public async Task<ServiceResult<RestaurantTableDto>> AddTableAsync(
        CreateRestaurantTableRequest request,
        CancellationToken ct = default)
    {
        var validation = await createTableValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<RestaurantTableDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<RestaurantTableDto>.BadRequest("Tenant context is required.");

        var duplicate = await db.RestaurantTables
            .AnyAsync(t => t.Number == request.Number, ct);
        if (duplicate)
            return ServiceResult<RestaurantTableDto>.Conflict(
                $"Table number {request.Number} already exists for this restaurant.");

        var table = new RestaurantTable
        {
            TenantId = tenantId,
            Number   = request.Number,
            Capacity = request.Capacity,
            Location = request.Location,
            IsActive = true,
        };

        db.RestaurantTables.Add(table);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Restaurant table created: TableId={TableId} TenantId={TenantId} ActorUserId={ActorUserId} Number={Number}",
            table.Id, tenantId, currentUserService.UserId, table.Number);

        return ServiceResult<RestaurantTableDto>.Created(ToTableDto(table), "Table created.");
    }

    public async Task<ServiceResult<RestaurantTableDto>> UpdateTableAsync(
        long id,
        UpdateRestaurantTableRequest request,
        CancellationToken ct = default)
    {
        var validation = await updateTableValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<RestaurantTableDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var table = await db.RestaurantTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is null)
            return ServiceResult<RestaurantTableDto>.NotFound($"Table {id} not found.");

        if (request.Number is { } newNumber && newNumber != table.Number)
        {
            var duplicate = await db.RestaurantTables
                .AnyAsync(t => t.Number == newNumber && t.Id != id, ct);
            if (duplicate)
                return ServiceResult<RestaurantTableDto>.Conflict(
                    $"Table number {newNumber} already exists for this restaurant.");

            table.Number = newNumber;
        }

        if (request.Capacity is not null) table.Capacity = request.Capacity.Value;
        if (request.Location is not null) table.Location = request.Location;
        if (request.IsActive is not null) table.IsActive = request.IsActive.Value;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Restaurant table updated: TableId={TableId} TenantId={TenantId} ActorUserId={ActorUserId}",
            table.Id, table.TenantId, currentUserService.UserId);

        return ServiceResult<RestaurantTableDto>.Ok(ToTableDto(table), "Table updated.");
    }

    private static RestaurantProfileDto ToProfileDto(Tenant t) => new(
        t.Id, t.Name, t.Slug, t.OwnerName, t.OwnerEmail, t.Phone, t.City,
        t.Plan.ToString(),
        t.IsActive ? "Active" : "Suspended",
        t.CreatedAt);

    private static RestaurantTableDto ToTableDto(RestaurantTable t) => new()
    {
        Id       = t.Id,
        Number   = t.Number,
        Capacity = t.Capacity,
        Location = t.Location,
        IsActive = t.IsActive,
        TenantId = t.TenantId,
    };
}
