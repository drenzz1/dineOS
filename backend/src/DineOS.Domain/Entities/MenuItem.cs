using DineOS.Domain.Common;
using Pgvector;

namespace DineOS.Domain.Entities;

public class MenuItem : TenantAuditingEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Normalized: a menu item references a MenuCategory rather than storing the
    // category name as free text. MenuCategory.Name is the single source of truth.
    // The API still speaks category *names* — MenuService resolves a name to this
    // FK (get-or-create per tenant), so callers never deal with the id directly.
    public long CategoryId { get; set; }
    public MenuCategory Category { get; set; } = null!;

    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public Vector? Embedding { get; set; }
}
