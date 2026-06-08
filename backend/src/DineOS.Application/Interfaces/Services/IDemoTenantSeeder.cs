namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Materialises the shared demo tenant (#216): tenant row, menu, staff,
/// tables, and a handful of orders. Idempotent — safe to call on every
/// startup. Gated by <c>Demo:Enabled</c>.
/// </summary>
public interface IDemoTenantSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
