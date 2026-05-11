using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Menu;

namespace DineOS.Application.Interfaces.Services;

public interface IMenuService
{
    Task<ServiceResult<List<MenuItemDto>>> GetMenuItemsAsync(CancellationToken ct = default);

    Task<ServiceResult<MenuItemDto>> CreateMenuItemAsync(
        CreateMenuItemRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<MenuItemDto>> UpdateMenuItemAsync(
        long id,
        UpdateMenuItemRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<MenuItemDto>> DeleteMenuItemAsync(long id, CancellationToken ct = default);

    Task<ServiceResult<List<MenuCategoryDto>>> GetCategoriesAsync(CancellationToken ct = default);

    Task<ServiceResult<MenuCategoryDto>> CreateCategoryAsync(
        CreateMenuCategoryRequest request,
        CancellationToken ct = default);
}
