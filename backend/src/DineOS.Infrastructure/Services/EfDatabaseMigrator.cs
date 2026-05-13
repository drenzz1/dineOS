using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DineOS.Infrastructure.Services;

public sealed class EfDatabaseMigrator(IServiceScopeFactory scopeFactory) : IDatabaseMigrator
{
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);
    }
}
