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
/// <see cref="DemoOptions.TenantSlug"/> (#216): the tenant row, a small but
/// realistic 3-category / 12-item menu, a few staff per role, a small set of
/// tables, and a handful of historical + in-progress orders so the kitchen
/// board and reports surfaces are not empty on first login.
///
/// Re-runs are safe: every section short-circuits if the target rows already
/// exist for the demo tenant. Gated by <see cref="DemoOptions.Enabled"/> so
/// production can opt out via configuration.
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
        await EnsureMenuAsync(db, tenant.Id, ct);
        await EnsureStaffAsync(db, pinHasher, tenant.Id, ct);
        await EnsureTablesAsync(db, tenant.Id, ct);
        await EnsureOrdersAsync(db, tenant.Id, ct);

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
            Name        = "Demo Restaurant",
            Slug        = slug,
            IsActive    = true,
            OwnerName   = "Demo Owner",
            OwnerEmail  = $"owner@{slug}.dineos.local",
            Phone       = "+1 555 000 0000",
            City        = "Tirana",
            Plan        = SubscriptionPlan.Pro,
            CreatedAt   = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        return tenant;
    }

    private static async Task EnsureMenuAsync(
        AppDbContext db, long tenantId, CancellationToken ct)
    {
        var hasCategories = await db.MenuCategories
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.DeletedAt == null, ct);
        if (hasCategories)
            return;

        string[] categories = ["Starters", "Mains", "Drinks"];
        var categoriesByName = new Dictionary<string, MenuCategory>();
        foreach (var name in categories)
        {
            var category = new MenuCategory
            {
                TenantId  = tenantId,
                Name      = name,
                CreatedAt = DateTime.UtcNow,
            };
            db.MenuCategories.Add(category);
            categoriesByName[name] = category;
        }

        (string Name, decimal Price, string Category, string Description)[] items =
        [
            ("Bruschetta",       4.50m, "Starters", "Toasted bread, tomato, basil."),
            ("Calamari Fritti",  7.90m, "Starters", "Crispy squid rings, lemon aioli."),
            ("Caesar Salad",     6.50m, "Starters", "Romaine, parmesan, croutons, anchovy."),
            ("Margherita Pizza", 9.00m, "Mains",    "San Marzano, fior di latte, basil."),
            ("Carbonara",        11.50m,"Mains",    "Guanciale, pecorino, egg yolk."),
            ("Ribeye Steak",     19.00m,"Mains",    "250g, rosemary butter, fries."),
            ("Grilled Salmon",   16.50m,"Mains",    "Lemon herb, seasonal greens."),
            ("Veggie Bowl",      10.00m,"Mains",    "Quinoa, roasted veg, tahini."),
            ("Espresso",          2.00m,"Drinks",   "Single shot."),
            ("Cappuccino",        3.00m,"Drinks",   "Double shot, steamed milk."),
            ("House Red",         5.50m,"Drinks",   "Glass, Tuscan blend."),
            ("Sparkling Water",   2.50m,"Drinks",   "500ml, San Pellegrino."),
        ];

        foreach (var (name, price, category, description) in items)
        {
            db.MenuItems.Add(new MenuItem
            {
                TenantId    = tenantId,
                Name        = name,
                Price       = price,
                Category    = categoriesByName[category],
                Description = description,
                CreatedAt   = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // PinHash placeholder used before demo staff had real, loginable PINs
    // (#staff-pin-auth). Rows carrying it are healed to a real hash below.
    private const string LegacyPlaceholderPinHash = "demo-pin-hash";

    private static async Task EnsureStaffAsync(
        AppDbContext db, IPinHasher pinHasher, long tenantId, CancellationToken ct)
    {
        // Non-secret demo PINs so the /select-staff roster (#staff-pin-auth
        // Phase 3) is usable out-of-the-box on the shared demo tenant. Documented
        // in docs/keycloak-setup.md. Per-row idempotent: adds any missing staff
        // and heals the legacy placeholder hash, without disturbing real edits.
        (string FullName, string Email, string Role, string Pin)[] staff =
        [
            ("Ada Manager",    "ada.manager@demo.dineos.local",    "Manager",      "1111"),
            ("Bram Cashier",   "bram.cashier@demo.dineos.local",   "Cashier",      "2222"),
            ("Cleo Cashier",   "cleo.cashier@demo.dineos.local",   "Cashier",      "3333"),
            ("Dario Kitchen",  "dario.kitchen@demo.dineos.local",  "KitchenStaff", "4444"),
            ("Elif Kitchen",   "elif.kitchen@demo.dineos.local",   "KitchenStaff", "5555"),
        ];

        var changed = false;
        foreach (var (fullName, email, role, pin) in staff)
        {
            var existing = await db.StaffMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    s => s.TenantId == tenantId && s.Email == email && s.DeletedAt == null, ct);

            if (existing is null)
            {
                db.StaffMembers.Add(new StaffMember
                {
                    TenantId  = tenantId,
                    FullName  = fullName,
                    Email     = email,
                    Role      = role,
                    PinHash   = pinHasher.Hash(pin),
                    IsActive  = true,
                    CreatedAt = DateTime.UtcNow,
                });
                changed = true;
            }
            else if (existing.PinHash == LegacyPlaceholderPinHash)
            {
                // Heal demo tenants seeded before real PINs existed.
                existing.PinHash = pinHasher.Hash(pin);
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureTablesAsync(
        AppDbContext db, long tenantId, CancellationToken ct)
    {
        var hasTables = await db.RestaurantTables
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId && t.DeletedAt == null, ct);
        if (hasTables)
            return;

        for (int n = 1; n <= 6; n++)
        {
            db.RestaurantTables.Add(new RestaurantTable
            {
                TenantId  = tenantId,
                Number    = n,
                Capacity  = n <= 2 ? 2 : 4,
                Location  = n <= 3 ? "Main Hall" : "Terrace",
                IsActive  = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureOrdersAsync(
        AppDbContext db, long tenantId, CancellationToken ct)
    {
        var hasOrders = await db.Orders
            .IgnoreQueryFilters()
            .AnyAsync(o => o.TenantId == tenantId && o.DeletedAt == null, ct);
        if (hasOrders)
            return;

        var now = DateTime.UtcNow;

        // Historical, delivered orders for reports/shift surfaces.
        for (int i = 0; i < 5; i++)
        {
            var createdAt = now.AddDays(-1 - i).AddHours(-i);
            var order = new Order
            {
                TenantId    = tenantId,
                OrderType   = i % 2 == 0 ? "DineIn" : "Takeaway",
                TableNumber = i % 2 == 0 ? ((i % 6) + 1) : null,
                Status      = OrderStatus.Delivered,
                Total       = 0m,
                Notes       = null,
                CreatedAt   = createdAt,
            };
            order.Items.Add(new OrderItem
            {
                TenantId  = tenantId,
                Name      = "Margherita Pizza",
                Quantity  = 1,
                UnitPrice = 9.00m,
                CreatedAt = createdAt,
            });
            order.Items.Add(new OrderItem
            {
                TenantId  = tenantId,
                Name      = "House Red",
                Quantity  = 1,
                UnitPrice = 5.50m,
                CreatedAt = createdAt,
            });
            order.Total = order.Items.Sum(it => it.Quantity * it.UnitPrice);
            db.Orders.Add(order);
        }

        // In-progress + new tickets so the kitchen board isn't empty.
        var newOrder = new Order
        {
            TenantId    = tenantId,
            OrderType   = "DineIn",
            TableNumber = 2,
            Status      = OrderStatus.New,
            Total       = 0m,
            CreatedAt   = now.AddMinutes(-3),
        };
        newOrder.Items.Add(new OrderItem
        {
            TenantId  = tenantId,
            Name      = "Carbonara",
            Quantity  = 2,
            UnitPrice = 11.50m,
            CreatedAt = newOrder.CreatedAt,
        });
        newOrder.Total = newOrder.Items.Sum(it => it.Quantity * it.UnitPrice);
        db.Orders.Add(newOrder);

        var inProgressOrder = new Order
        {
            TenantId    = tenantId,
            OrderType   = "DineIn",
            TableNumber = 5,
            Status      = OrderStatus.InProgress,
            Total       = 0m,
            CreatedAt   = now.AddMinutes(-12),
        };
        inProgressOrder.Items.Add(new OrderItem
        {
            TenantId  = tenantId,
            Name      = "Ribeye Steak",
            Quantity  = 1,
            UnitPrice = 19.00m,
            CreatedAt = inProgressOrder.CreatedAt,
        });
        inProgressOrder.Items.Add(new OrderItem
        {
            TenantId  = tenantId,
            Name      = "Sparkling Water",
            Quantity  = 1,
            UnitPrice = 2.50m,
            CreatedAt = inProgressOrder.CreatedAt,
        });
        inProgressOrder.Total = inProgressOrder.Items.Sum(it => it.Quantity * it.UnitPrice);
        db.Orders.Add(inProgressOrder);

        await db.SaveChangesAsync(ct);
    }
}
