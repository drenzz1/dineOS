using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotently materialises the shared demo tenant referenced by
/// <see cref="DemoOptions.TenantSlug"/> (#216). Delegates all data seeding to
/// <see cref="DemoDataSeeder"/> so the same data set is used for both the
/// shared tenant (startup seed) and per-user isolated demo tenants
/// provisioned by <see cref="DineOS.Infrastructure.Jobs.DemoProvisioningJob"/>.
/// </summary>
public sealed class DemoTenantSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<DemoOptions> demoOptions,
    ILogger<DemoTenantSeeder> logger) : IDemoTenantSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var opts = demoOptions.Value;
        if (!opts.Enabled)
        {
            logger.LogInformation("Demo seeder skipped — Demo:Enabled = false.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pinHasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();

        var tenant = await EnsureTenantAsync(db, opts.TenantSlug, ct);
        await DemoDataSeeder.SeedAsync(db, pinHasher, tenant.Id, ct);

        logger.LogInformation(
            "Demo tenant seeded. TenantId={TenantId} Slug={Slug}",
            tenant.Id, tenant.Slug);
    }

    private static async Task<Tenant> EnsureTenantAsync(
        AppDbContext db, string slug, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug && t.DeletedAt == null, ct);

        if (tenant is not null)
            return tenant;

        tenant = new Tenant
        {
            Name       = "Demo Restaurant",
            Slug       = slug,
            IsActive   = true,
            OwnerName  = "Demo Owner",
            OwnerEmail = $"owner@{slug}.dineos.local",
            Phone      = "+1 555 000 0000",
            City       = "Tirana",
            Plan       = SubscriptionPlan.Pro,
            CreatedAt  = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        return tenant;
    }
}
