using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds a tenant with demo data: 3-category / 12-item menu, 5 staff members,
/// 6 tables, and a handful of historical + active orders. Idempotent per
/// section — safe to call on a tenant that is already partially seeded.
/// </summary>
internal static class DemoDataSeeder
{
    internal static async Task SeedAsync(
        AppDbContext db,
        IPinHasher pinHasher,
        long tenantId,
        CancellationToken ct)
    {
        await EnsureMenuAsync(db, tenantId, ct);
        await EnsureStaffAsync(db, pinHasher, tenantId, ct);
        await EnsureTablesAsync(db, tenantId, ct);
        await EnsureOrdersAsync(db, tenantId, ct);
    }

    private static async Task EnsureMenuAsync(AppDbContext db, long tenantId, CancellationToken ct)
    {
        var hasCategories = await db.MenuCategories
            .IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.DeletedAt == null, ct);
        if (hasCategories) return;

        string[] categoryNames = ["Starters", "Mains", "Drinks"];
        var categoriesByName = new Dictionary<string, MenuCategory>();
        foreach (var name in categoryNames)
        {
            var cat = new MenuCategory
            {
                TenantId  = tenantId,
                Name      = name,
                CreatedAt = DateTime.UtcNow,
            };
            db.MenuCategories.Add(cat);
            categoriesByName[name] = cat;
        }

        (string Name, decimal Price, string Cat, string Description)[] items =
        [
            ("Bruschetta",        4.50m, "Starters", "Toasted bread, tomato, basil."),
            ("Calamari Fritti",   7.90m, "Starters", "Crispy squid rings, lemon aioli."),
            ("Caesar Salad",      6.50m, "Starters", "Romaine, parmesan, croutons, anchovy."),
            ("Margherita Pizza",  9.00m, "Mains",    "San Marzano, fior di latte, basil."),
            ("Carbonara",        11.50m, "Mains",    "Guanciale, pecorino, egg yolk."),
            ("Ribeye Steak",     19.00m, "Mains",    "250g, rosemary butter, fries."),
            ("Grilled Salmon",   16.50m, "Mains",    "Lemon herb, seasonal greens."),
            ("Veggie Bowl",      10.00m, "Mains",    "Quinoa, roasted veg, tahini."),
            ("Espresso",          2.00m, "Drinks",   "Single shot."),
            ("Cappuccino",        3.00m, "Drinks",   "Double shot, steamed milk."),
            ("House Red",         5.50m, "Drinks",   "Glass, Tuscan blend."),
            ("Sparkling Water",   2.50m, "Drinks",   "500ml, San Pellegrino."),
        ];

        foreach (var (name, price, cat, description) in items)
        {
            db.MenuItems.Add(new MenuItem
            {
                TenantId    = tenantId,
                Name        = name,
                Price       = price,
                Category    = categoriesByName[cat],
                Description = description,
                CreatedAt   = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private const string LegacyPlaceholderPinHash = "demo-pin-hash";

    private static async Task EnsureStaffAsync(
        AppDbContext db, IPinHasher pinHasher, long tenantId, CancellationToken ct)
    {
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
                existing.PinHash = pinHasher.Hash(pin);
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureTablesAsync(AppDbContext db, long tenantId, CancellationToken ct)
    {
        var hasTables = await db.RestaurantTables
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId && t.DeletedAt == null, ct);
        if (hasTables) return;

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

    private static async Task EnsureOrdersAsync(AppDbContext db, long tenantId, CancellationToken ct)
    {
        var hasOrders = await db.Orders
            .IgnoreQueryFilters()
            .AnyAsync(o => o.TenantId == tenantId && o.DeletedAt == null, ct);
        if (hasOrders) return;

        var now = DateTime.UtcNow;

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
                CreatedAt   = createdAt,
            };
            order.Items.Add(new OrderItem
            {
                TenantId  = tenantId, Name = "Margherita Pizza",
                Quantity  = 1, UnitPrice = 9.00m, CreatedAt = createdAt,
            });
            order.Items.Add(new OrderItem
            {
                TenantId  = tenantId, Name = "House Red",
                Quantity  = 1, UnitPrice = 5.50m, CreatedAt = createdAt,
            });
            order.Total = order.Items.Sum(it => it.Quantity * it.UnitPrice);
            db.Orders.Add(order);
        }

        var newOrder = new Order
        {
            TenantId = tenantId, OrderType = "DineIn", TableNumber = 2,
            Status = OrderStatus.New, Total = 0m, CreatedAt = now.AddMinutes(-3),
        };
        newOrder.Items.Add(new OrderItem
        {
            TenantId = tenantId, Name = "Carbonara",
            Quantity = 2, UnitPrice = 11.50m, CreatedAt = newOrder.CreatedAt,
        });
        newOrder.Total = newOrder.Items.Sum(it => it.Quantity * it.UnitPrice);
        db.Orders.Add(newOrder);

        var inProgressOrder = new Order
        {
            TenantId = tenantId, OrderType = "DineIn", TableNumber = 5,
            Status = OrderStatus.InProgress, Total = 0m, CreatedAt = now.AddMinutes(-12),
        };
        inProgressOrder.Items.Add(new OrderItem
        {
            TenantId = tenantId, Name = "Ribeye Steak",
            Quantity = 1, UnitPrice = 19.00m, CreatedAt = inProgressOrder.CreatedAt,
        });
        inProgressOrder.Items.Add(new OrderItem
        {
            TenantId = tenantId, Name = "Sparkling Water",
            Quantity = 1, UnitPrice = 2.50m, CreatedAt = inProgressOrder.CreatedAt,
        });
        inProgressOrder.Total = inProgressOrder.Items.Sum(it => it.Quantity * it.UnitPrice);
        db.Orders.Add(inProgressOrder);

        await db.SaveChangesAsync(ct);
    }
}
