using System.Diagnostics;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Menu;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Services;

public sealed class AiMenuService(
    AppDbContext db,
    IAiClient aiClient,
    ICurrentUserService currentUser,
    IFeatureFlags featureFlags,
    ILogger<AiMenuService> logger) : IAiMenuService
{
    public async Task<ServiceResult<MenuItemDescriptionSuggestionDto>> SuggestDescriptionAsync(
        long menuItemId,
        CancellationToken ct = default)
    {
        // Feature flag (M5.6): AI menu generation is a runtime kill-switch. It defaults
        // ON when no flag provider is configured, so removing Unleash never silently
        // disables AI; flipping 'ai-menu-generation' off in Unleash stops generation
        // (e.g. to cap provider spend) with no redeploy — short-circuit before any work.
        if (!featureFlags.IsEnabled(FeatureFlag.AiMenuGeneration, defaultValue: true))
            return ServiceResult<MenuItemDescriptionSuggestionDto>.ServiceUnavailable(
                "AI menu generation is currently disabled.");

        // Tenant-scoped query filter is applied automatically, so an item from
        // another tenant returns null without leaking existence.
        var item = await db.MenuItems.AsNoTracking()
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == menuItemId, ct);

        if (item is null)
            return ServiceResult<MenuItemDescriptionSuggestionDto>.NotFound(
                $"Menu item {menuItemId} not found.");

        var request = new MenuDescriptionAiRequest(
            Name:                item.Name,
            Category:            item.Category.Name,
            Price:               item.Price,
            ExistingDescription: item.Description);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await aiClient.GenerateMenuDescriptionAsync(request, ct);
            sw.Stop();

            // Metadata only — we deliberately do not log the user-facing copy
            // so reviewers can audit cost/latency without prompt content leakage.
            logger.LogInformation(
                "AI menu description generated: MenuItemId={MenuItemId} TenantUser={UserId} " +
                "Model={Model} InputTokens={InputTokens} OutputTokens={OutputTokens} LatencyMs={LatencyMs}",
                item.Id, currentUser.UserId,
                result.Usage.Model, result.Usage.InputTokens, result.Usage.OutputTokens, sw.ElapsedMilliseconds);

            var dto = new MenuItemDescriptionSuggestionDto(
                MenuItemId:           item.Id,
                ItemName:             item.Name,
                Category:             item.Category.Name,
                SuggestedDescription: result.Description,
                SuggestedAllergens:   result.Allergens,
                Metadata: new AiSuggestionMetadata(
                    Model:        result.Usage.Model,
                    InputTokens:  result.Usage.InputTokens,
                    OutputTokens: result.Usage.OutputTokens,
                    LatencyMs:    (int)sw.ElapsedMilliseconds));

            return ServiceResult<MenuItemDescriptionSuggestionDto>.Ok(dto);
        }
        catch (AiUnavailableException ex)
        {
            sw.Stop();
            logger.LogWarning(ex,
                "AI menu description fell back: MenuItemId={MenuItemId} LatencyMs={LatencyMs}",
                item.Id, sw.ElapsedMilliseconds);

            return ServiceResult<MenuItemDescriptionSuggestionDto>.UnprocessableEntity(
                "AI assistant is temporarily unavailable. Please write the description manually for now.");
        }
    }
}
