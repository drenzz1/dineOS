using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Common;
using DineOS.Domain.Entities;
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
    public DbSet<Payment> Payments => Set<Payment>();

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
        var tenantIdValue = Expression.Property(tenantIdField, nameof(Nullable<long>.Value));
        var entityTenantId = Expression.Property(param, nameof(TenantAuditingEntity.TenantId));

        // filter: notDeleted && (!tenantId.HasValue || e.TenantId == tenantId.Value)
        var tenantMatches = Expression.OrElse(
            Expression.Not(tenantIdHasValue),
            Expression.Equal(entityTenantId, tenantIdValue));

        var body = Expression.AndAlso(notDeleted, tenantMatches);
        return Expression.Lambda(body, param);
    }
}
