using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Common;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DineOS.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly long? _currentTenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantService tenantService)
        : base(options)
    {
        _currentTenantId = tenantService.TenantId;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ShiftNote> ShiftNotes => Set<ShiftNote>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<DeadLetterEmail> DeadLetterEmails => Set<DeadLetterEmail>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<TenantInvoice> TenantInvoices => Set<TenantInvoice>();
    public DbSet<DemoUser> DemoUsers => Set<DemoUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType)
                    .Property(nameof(BaseEntity.Id))
                    .ValueGeneratedOnAdd();
            }

            if (typeof(TenantAuditingEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType)
                    .HasQueryFilter(BuildTenantSoftDeleteFilter(clrType));
            }
            else if (typeof(BaseAuditingEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType)
                    .HasQueryFilter(BuildSoftDeleteFilter(clrType));
            }
        }

        // Indexes for common order query patterns (tenant/date/status)
        modelBuilder.Entity<Order>()
            .HasIndex(o => new { o.TenantId, o.Status });
        modelBuilder.Entity<Order>()
            .HasIndex(o => new { o.TenantId, o.CreatedAt });
        modelBuilder.Entity<OrderItem>()
            .HasIndex(i => i.OrderId);
        modelBuilder.Entity<ShiftNote>()
            .HasIndex(sn => new { sn.TenantId, sn.CreatedAt });
        modelBuilder.Entity<MenuItem>()
            .HasIndex(mi => new { mi.TenantId, mi.Category });
        // Supports the daily/period revenue queries the reports & admin
        // dashboards run. See docs/backend/sql-optimization.md (Q4) for the
        // EXPLAIN ANALYZE proof.
        modelBuilder.Entity<Payment>()
            .HasIndex(p => new { p.TenantId, p.CreatedAt });

        modelBuilder.Entity<RestaurantTable>()
            .HasIndex(t => new { t.TenantId, t.Number })
            .IsUnique();

        modelBuilder.Entity<Shift>()
            .HasIndex(s => new { s.TenantId, s.StartTime });
        modelBuilder.Entity<Shift>()
            .HasIndex(s => s.StaffMemberId);

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.HasKey(m => m.MessageId);
            entity.Property(m => m.MessageId).HasMaxLength(128);
            entity.Property(m => m.MessageType).HasMaxLength(128).IsRequired();
            entity.HasIndex(m => new { m.TenantId, m.ProcessedAt });
        });

        modelBuilder.Entity<DeadLetterEmail>()
            .HasIndex(d => d.FailedAt);
        modelBuilder.Entity<DeadLetterEmail>()
            .HasIndex(d => d.TenantId);

        modelBuilder.Entity<EmailVerificationCode>()
            .HasIndex(c => new { c.Email, c.Purpose, c.CreatedAt });
        modelBuilder.Entity<EmailVerificationCode>()
            .HasIndex(c => c.ExpiresAt);

        // Stripe webhooks look up the tenant by StripeCustomerId; both lookups
        // are 1:1 so we make them unique.
        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.StripeCustomerId)
            .IsUnique()
            .HasFilter("\"StripeCustomerId\" IS NOT NULL");
        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.StripeSubscriptionId)
            .IsUnique()
            .HasFilter("\"StripeSubscriptionId\" IS NOT NULL");
        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.StripeSessionId)
            .HasFilter("\"StripeSessionId\" IS NOT NULL");

        // Idempotency for invoice webhook events.
        modelBuilder.Entity<TenantInvoice>()
            .HasIndex(i => i.StripeInvoiceId)
            .IsUnique();
        modelBuilder.Entity<TenantInvoice>()
            .HasIndex(i => i.TenantId);

        // Find Pending payments that need an overdue email (NULL = not yet notified).
        modelBuilder.Entity<Payment>()
            .HasIndex(p => new { p.Status, p.CreatedAt, p.OverdueNotifiedAt });

        // Demo access (#216): one row per email; expiry scans drive the cleanup job.
        modelBuilder.Entity<DemoUser>(entity =>
        {
            entity.Property(d => d.Email).HasMaxLength(254).IsRequired();
            entity.HasIndex(d => d.Email).IsUnique();
            entity.HasIndex(d => new { d.Status, d.ExpiresAt });
        });

        SeedData(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasData(
            new Tenant
            {
                Id = 1,
                Name = "Demo Restaurant",
                Slug = "demo-restaurant",
                IsActive = true,
                OwnerName = "Demo Owner",
                OwnerEmail = "owner@demo-restaurant.com",
                Phone = "+1 555 000 0000",
                City = "Tirana",
                Plan = DineOS.Domain.Enums.SubscriptionPlan.Pro,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    private static LambdaExpression BuildSoftDeleteFilter(Type type)
    {
        var param = Expression.Parameter(type, "e");
        var deletedAt = Expression.Property(param, nameof(BaseAuditingEntity.DeletedAt));
        var notDeleted = Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTime?)));
        return Expression.Lambda(notDeleted, param);
    }

    // EF Core parameterizes member accesses on the DbContext instance at query time,
    // so _currentTenantId is evaluated per-request (DbContext is scoped).
    private LambdaExpression BuildTenantSoftDeleteFilter(Type type)
    {
        var param = Expression.Parameter(type, "e");

        var deletedAt = Expression.Property(param, nameof(BaseAuditingEntity.DeletedAt));
        var notDeleted = Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTime?)));

        var contextConst = Expression.Constant(this, typeof(AppDbContext));
        var tenantIdField = Expression.Field(contextConst, nameof(_currentTenantId));
        var tenantIdHasValue = Expression.Property(tenantIdField, nameof(Nullable<long>.HasValue));
        var entityTenantId = Expression.Property(param, nameof(TenantAuditingEntity.TenantId));
        // Cast entity TenantId to long? so we compare long? == long? (avoids .Value on null for SuperAdmin)
        var entityTenantIdNullable = Expression.Convert(entityTenantId, typeof(long?));

        // filter: notDeleted && (!tenantId.HasValue || (long?)e.TenantId == tenantId)
        // Using tenantIdField (long?) directly instead of tenantIdValue (.Value) prevents
        // InvalidOperationException when _currentTenantId is null (e.g. SuperAdmin has no tenant_id claim).
        var tenantMatches = Expression.OrElse(
            Expression.Not(tenantIdHasValue),
            Expression.Equal(entityTenantIdNullable, tenantIdField));

        var body = Expression.AndAlso(notDeleted, tenantMatches);
        return Expression.Lambda(body, param);
    }
}
