using System.Text.RegularExpressions;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Restaurants;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class AdminRestaurantService(
    AppDbContext db,
    ICurrentUserService currentUserService,
    IValidator<CreateRestaurantRequest> createValidator,
    IValidator<UpdateRestaurantStatusRequest> statusValidator,
    IValidator<UpdateRestaurantPlanRequest> planValidator,
    IBackgroundJobClient backgroundJobs,
    ILogger<AdminRestaurantService> logger) : IAdminRestaurantService
{
    public async Task<ServiceResult<PagedResponse<RestaurantDto>>> ListAsync(
        string? search,
        PagedRequest pagination,
        CancellationToken ct = default)
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
            .Select(t => new RestaurantDto(
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
                t.CreatedAt,
                t.OwnerEmailVerified))
            .ToListAsync(ct);

        return ServiceResult<PagedResponse<RestaurantDto>>.Ok(
            PagedResponse<RestaurantDto>.From(items, total, pagination));
    }

    public async Task<ServiceResult<RestaurantDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tenant is null)
            return ServiceResult<RestaurantDto>.NotFound($"Restaurant {id} not found.");

        return ServiceResult<RestaurantDto>.Ok(ToDto(tenant));
    }

    public async Task<ServiceResult<RestaurantDto>> CreateAsync(
        CreateRestaurantRequest request,
        CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<RestaurantDto>.ValidationFailed(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

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

        logger.LogInformation(
            "Restaurant created: RestaurantId={RestaurantId} Slug={Slug} Plan={Plan} ActorUserId={ActorUserId}",
            tenant.Id, tenant.Slug, tenant.Plan, currentUserService.UserId);

        // Enqueue the account-verification email. Hangfire owns the retry +
        // dead-letter pipeline from here — failures will not block the response.
        var jobId = backgroundJobs.Enqueue<AccountVerificationEmailJob>(
            job => job.SendAsync(tenant.Id, CancellationToken.None));

        logger.LogInformation(
            "Account verification email enqueued: RestaurantId={RestaurantId} JobId={JobId}",
            tenant.Id, jobId);

        return ServiceResult<RestaurantDto>.Created(ToDto(tenant), "Restaurant created.");
    }

    public async Task<ServiceResult<RestaurantDto>> UpdateStatusAsync(
        long id,
        UpdateRestaurantStatusRequest request,
        CancellationToken ct = default)
    {
        var validation = await statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<RestaurantDto>.ValidationFailed(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null)
            return ServiceResult<RestaurantDto>.NotFound($"Restaurant {id} not found.");

        var previous = tenant.IsActive;
        tenant.IsActive = request.Status == "Active";
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Restaurant status changed: RestaurantId={RestaurantId} Previous={Previous} Current={Current} ActorUserId={ActorUserId}",
            tenant.Id,
            previous ? "Active" : "Suspended",
            tenant.IsActive ? "Active" : "Suspended",
            currentUserService.UserId);

        return ServiceResult<RestaurantDto>.Ok(ToDto(tenant));
    }

    public async Task<ServiceResult<RestaurantDto>> UpdatePlanAsync(
        long id,
        UpdateRestaurantPlanRequest request,
        CancellationToken ct = default)
    {
        var validation = await planValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<RestaurantDto>.ValidationFailed(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null)
            return ServiceResult<RestaurantDto>.NotFound($"Restaurant {id} not found.");

        var previous = tenant.Plan;
        tenant.Plan = Enum.TryParse<SubscriptionPlan>(request.Plan, out var p) ? p : tenant.Plan;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Restaurant plan changed: RestaurantId={RestaurantId} Previous={Previous} Current={Current} ActorUserId={ActorUserId}",
            tenant.Id, previous, tenant.Plan, currentUserService.UserId);

        return ServiceResult<RestaurantDto>.Ok(ToDto(tenant));
    }

    public async Task<ServiceResult<RestaurantDto>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null)
            return ServiceResult<RestaurantDto>.NotFound($"Restaurant {id} not found.");

        var dto = ToDto(tenant);

        // Soft delete — AuditInterceptor + query filters keep deleted tenants out of listings.
        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Restaurant deleted: RestaurantId={RestaurantId} Slug={Slug} ActorUserId={ActorUserId}",
            tenant.Id, tenant.Slug, currentUserService.UserId);

        return ServiceResult<RestaurantDto>.Ok(dto, $"Restaurant {id} deleted.");
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
        t.CreatedAt,
        t.OwnerEmailVerified);

    private static string GenerateSlug(string name) =>
        Regex.Replace(name.ToLower().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}
