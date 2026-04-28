using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DineOS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Converts hard deletes into soft deletes for any entity that inherits BaseAuditingEntity.
/// This is a safety net — prefer using the repository's DeleteAsync which sets DeletedAt directly.
/// </summary>
public class SoftDeleteInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplySoftDelete(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditingEntity>()
            .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.DeletedAt = now;
            entry.Entity.DeletedBy = userId;
        }
    }
}
