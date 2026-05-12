using DineOS.Application.Common;
using DineOS.Application.Menu;

namespace DineOS.Application.Interfaces.Services;

public interface IAiMenuService
{
    /// <summary>
    /// Suggests a generated description + allergen list for the given menu item.
    /// Does not persist the suggestion — callers decide whether to apply it.
    /// </summary>
    Task<ServiceResult<MenuItemDescriptionSuggestionDto>> SuggestDescriptionAsync(
        long menuItemId,
        CancellationToken ct = default);
}
