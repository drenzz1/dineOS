using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public class MenuService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IValidator<CreateMenuItemRequest> createItemValidator,
    IValidator<UpdateMenuItemRequest> updateItemValidator,
    IValidator<CreateMenuCategoryRequest> createCategoryValidator,
    ILogger<MenuService> logger) : IMenuService
{
    public async Task<ServiceResult<List<MenuItemDto>>> GetMenuItemsAsync(CancellationToken ct = default)
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

        return ServiceResult<List<MenuItemDto>>.Ok(items, "Menu items");
    }

    public async Task<ServiceResult<MenuItemDto>> CreateMenuItemAsync(
        CreateMenuItemRequest request,
        CancellationToken ct = default)
    {
        var validation = await createItemValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<MenuItemDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<MenuItemDto>.BadRequest("Tenant context is required.");

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

        logger.LogInformation(
            "Menu item created: MenuItemId={MenuItemId} TenantId={TenantId} ActorUserId={ActorUserId} Category={Category}",
            item.Id, tenantId, currentUserService.UserId, item.Category);

        return ServiceResult<MenuItemDto>.Created(ToItemDto(item), "Menu item created.");
    }

    public async Task<ServiceResult<MenuItemDto>> UpdateMenuItemAsync(
        long id,
        UpdateMenuItemRequest request,
        CancellationToken ct = default)
    {
        var validation = await updateItemValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<MenuItemDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var item = await db.MenuItems.FirstOrDefaultAsync(mi => mi.Id == id, ct);
        if (item is null)
            return ServiceResult<MenuItemDto>.NotFound($"Menu item {id} not found.");

        item.Name        = request.Name;
        item.Price       = request.Price;
        item.Category    = request.Category;
        item.Description = request.Description;
        item.ImageUrl    = request.ImageUrl;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Menu item updated: MenuItemId={MenuItemId} TenantId={TenantId} ActorUserId={ActorUserId}",
            item.Id, item.TenantId, currentUserService.UserId);

        return ServiceResult<MenuItemDto>.Ok(ToItemDto(item), "Menu item updated.");
    }

    public async Task<ServiceResult<MenuItemDto>> DeleteMenuItemAsync(long id, CancellationToken ct = default)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(mi => mi.Id == id, ct);
        if (item is null)
            return ServiceResult<MenuItemDto>.NotFound($"Menu item {id} not found.");

        db.MenuItems.Remove(item);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Menu item deleted: MenuItemId={MenuItemId} TenantId={TenantId} ActorUserId={ActorUserId}",
            item.Id, item.TenantId, currentUserService.UserId);

        return ServiceResult<MenuItemDto>.Ok(ToItemDto(item), $"Menu item {id} deleted.");
    }

    public async Task<ServiceResult<List<MenuCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default)
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

        return ServiceResult<List<MenuCategoryDto>>.Ok(categories, "Menu categories");
    }

    public async Task<ServiceResult<MenuCategoryDto>> CreateCategoryAsync(
        CreateMenuCategoryRequest request,
        CancellationToken ct = default)
    {
        var validation = await createCategoryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<MenuCategoryDto>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<MenuCategoryDto>.BadRequest("Tenant context is required.");

        var category = new MenuCategory
        {
            TenantId = tenantId,
            Name     = request.Name,
        };

        db.MenuCategories.Add(category);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Menu category created: MenuCategoryId={MenuCategoryId} TenantId={TenantId} ActorUserId={ActorUserId}",
            category.Id, tenantId, currentUserService.UserId);

        return ServiceResult<MenuCategoryDto>.Created(ToCategoryDto(category), "Menu category created.");
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
