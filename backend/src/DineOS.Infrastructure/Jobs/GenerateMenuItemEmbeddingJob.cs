using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Fire-and-forget Hangfire job that generates a vector embedding for a menu item
/// and stores it on the row. Enqueued after create/update so the HTTP response
/// is not blocked by the (potentially slow) embeddings API call.
/// </summary>
public sealed class GenerateMenuItemEmbeddingJob(
    AppDbContext db,
    IEmbeddingsClient embeddingsClient,
    ILogger<GenerateMenuItemEmbeddingJob> logger)
{
    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 30, 120, 300 },
        OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task RunAsync(long menuItemId, CancellationToken ct = default)
    {
        var item = await db.MenuItems.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == menuItemId, ct);
        if (item is null)
        {
            logger.LogWarning("GenerateMenuItemEmbeddingJob: MenuItemId={MenuItemId} not found, skipping.", menuItemId);
            return;
        }

        var text = string.IsNullOrWhiteSpace(item.Description)
            ? item.Name
            : $"{item.Name}. {item.Description}";

        try
        {
            var vector = await embeddingsClient.GenerateEmbeddingAsync(text, ct);
            item.Embedding = new Vector(vector);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Embedding generated: MenuItemId={MenuItemId} TenantId={TenantId} Dimensions={Dims}",
                item.Id, item.TenantId, vector.Length);
        }
        catch (AiUnavailableException ex)
        {
            logger.LogWarning(ex, "Embeddings unavailable for MenuItemId={MenuItemId}; will retry.", menuItemId);
            throw;
        }
    }
}
