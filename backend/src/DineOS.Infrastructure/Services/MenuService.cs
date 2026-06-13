using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DineOS.Infrastructure.Services;

public class MenuService(
    AppDbContext db,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    ICacheService cache,
    IFileStorageService fileStorage,
    IEmbeddingsClient embeddingsClient,
    IBackgroundJobClient backgroundJobs,
    IValidator<CreateMenuItemRequest> createItemValidator,
    IValidator<UpdateMenuItemRequest> updateItemValidator,
    IValidator<CreateMenuCategoryRequest> createCategoryValidator,
    IValidator<UploadMenuItemImageRequest> uploadImageValidator,
    ILogger<MenuService> logger) : IMenuService
{
    private static readonly TimeSpan MenuItemsCacheTtl = TimeSpan.FromMinutes(5);
    private static string MenuItemsCacheKey(long tenantId) => $"menu:items:tenant:{tenantId}";

    public async Task<ServiceResult<List<MenuItemDto>>> GetMenuItemsAsync(CancellationToken ct = default)
    {
        // No tenant context (e.g. SuperAdmin) -> bypass cache entirely so we
        // never serve cross-tenant data from a tenant-scoped key.
        if (tenantService.TenantId is not { } tenantId)
            return ServiceResult<List<MenuItemDto>>.Ok(await LoadMenuItemsAsync(ct), "Menu items");

        var items = await cache.GetOrSetAsync(
            MenuItemsCacheKey(tenantId),
            LoadMenuItemsAsync,
            MenuItemsCacheTtl,
            ct);

        return ServiceResult<List<MenuItemDto>>.Ok(items, "Menu items");
    }

    public async Task<ServiceResult<List<MenuItemDto>>> SemanticSearchMenuItemsAsync(
        string query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return ServiceResult<List<MenuItemDto>>.BadRequest("Search query cannot be empty.");

        float[] queryVector;
        try
        {
            queryVector = await embeddingsClient.GenerateEmbeddingAsync(query.Trim(), ct);
        }
        catch (AiUnavailableException ex)
        {
            return ServiceResult<List<MenuItemDto>>.UnprocessableEntity(ex.Message);
        }

        var vector = new Vector(queryVector);

        var results = await db.MenuItems
            .AsNoTracking()
            .Where(mi => mi.Embedding != null)
            .OrderBy(mi => mi.Embedding!.CosineDistance(vector))
            .Take(10)
            .Select(mi => new MenuItemDto
            {
                Id          = mi.Id,
                Name        = mi.Name,
                Price       = mi.Price,
                Category    = mi.Category.Name,
                Description = mi.Description,
                ImageUrl    = mi.ImageUrl,
                TenantId    = mi.TenantId,
            })
            .ToListAsync(ct);

        return ServiceResult<List<MenuItemDto>>.Ok(results, "Semantic search results");
    }

    private Task<List<MenuItemDto>> LoadMenuItemsAsync(CancellationToken ct) =>
        db.MenuItems
            .AsNoTracking()
            .OrderBy(mi => mi.Category.Name)
            .ThenBy(mi => mi.Name)
            .Select(mi => new MenuItemDto
            {
                Id          = mi.Id,
                Name        = mi.Name,
                Price       = mi.Price,
                Category    = mi.Category.Name,
                Description = mi.Description,
                ImageUrl    = mi.ImageUrl,
                TenantId    = mi.TenantId,
            })
            .ToListAsync(ct);

    // Resolves a category *name* to a MenuCategory for the tenant, creating it on
    // first use. The new category is attached to the context and persisted by the
    // caller's SaveChanges (EF sets the FK via the navigation on the menu item).
    private async Task<MenuCategory> ResolveCategoryAsync(long tenantId, string name, CancellationToken ct)
    {
        var trimmed = name.Trim();

        var existing = await db.MenuCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name == trimmed, ct);
        if (existing is not null)
            return existing;

        var created = new MenuCategory { TenantId = tenantId, Name = trimmed };
        db.MenuCategories.Add(created);
        return created;
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

        var category = await ResolveCategoryAsync(tenantId, request.Category, ct);

        var item = new MenuItem
        {
            TenantId    = tenantId,
            Name        = request.Name,
            Price       = request.Price,
            Category    = category,
            Description = request.Description,
            ImageUrl    = request.ImageUrl,
        };

        db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(MenuItemsCacheKey(tenantId), ct);

        backgroundJobs.Enqueue<GenerateMenuItemEmbeddingJob>(j => j.RunAsync(item.Id, CancellationToken.None));

        logger.LogInformation(
            "Menu item created: MenuItemId={MenuItemId} TenantId={TenantId} ActorUserId={ActorUserId} Category={Category}",
            item.Id, tenantId, currentUserService.UserId, item.Category.Name);

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

        var category = await ResolveCategoryAsync(item.TenantId, request.Category, ct);

        item.Name        = request.Name;
        item.Price       = request.Price;
        item.Category    = category;
        item.Description = request.Description;
        item.ImageUrl    = request.ImageUrl;

        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(MenuItemsCacheKey(item.TenantId), ct);

        backgroundJobs.Enqueue<GenerateMenuItemEmbeddingJob>(j => j.RunAsync(item.Id, CancellationToken.None));

        logger.LogInformation(
            "Menu item updated: MenuItemId={MenuItemId} TenantId={TenantId} ActorUserId={ActorUserId}",
            item.Id, item.TenantId, currentUserService.UserId);

        return ServiceResult<MenuItemDto>.Ok(ToItemDto(item), "Menu item updated.");
    }

    public async Task<ServiceResult<MenuItemImageUploadDto>> UploadMenuItemImageAsync(
        long id,
        UploadMenuItemImageRequest request,
        CancellationToken ct = default)
    {
        var validation = await uploadImageValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ServiceResult<MenuItemImageUploadDto>.ValidationFailed(
                "File upload validation failed.",
                validation.Errors
                    .Select(e => new ValidationError(e.ErrorCode, e.ErrorMessage))
                    .ToList());

        var item = await db.MenuItems.FirstOrDefaultAsync(mi => mi.Id == id, ct);
        if (item is null)
            return ServiceResult<MenuItemImageUploadDto>.NotFound($"Menu item {id} not found.");

        var imageUrl = await fileStorage.SaveAsync(request.Content, request.FileName, request.ContentType, "menu-items", ct);

        item.ImageUrl = imageUrl;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DB save failed after image upload; cleaning up file {ImageUrl}", imageUrl);
            await fileStorage.DeleteAsync(imageUrl, ct);
            throw;
        }

        await cache.RemoveAsync(MenuItemsCacheKey(item.TenantId), ct);

        logger.LogInformation(
            "Menu item image uploaded: MenuItemId={MenuItemId} TenantId={TenantId} ActorUserId={ActorUserId}",
            item.Id, item.TenantId, currentUserService.UserId);

        return ServiceResult<MenuItemImageUploadDto>.Ok(new MenuItemImageUploadDto(imageUrl), "Image uploaded.");
    }

    public async Task<ServiceResult<MenuItemDto>> DeleteMenuItemAsync(long id, CancellationToken ct = default)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(mi => mi.Id == id, ct);
        if (item is null)
            return ServiceResult<MenuItemDto>.NotFound($"Menu item {id} not found.");

        db.MenuItems.Remove(item);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(MenuItemsCacheKey(item.TenantId), ct);

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

    // Callers set the Category navigation (via ResolveCategoryAsync) before
    // mapping, so mi.Category.Name is always populated here.
    private static MenuItemDto ToItemDto(MenuItem mi) => new()
    {
        Id          = mi.Id,
        Name        = mi.Name,
        Price       = mi.Price,
        Category    = mi.Category.Name,
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
