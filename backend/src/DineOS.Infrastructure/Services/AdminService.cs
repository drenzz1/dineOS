using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DineOS.Infrastructure.Services;

public class AdminService(AppDbContext db) : IAdminService
{
    public async Task<ServiceResult<PagedResponse<PlatformUserDto>>> ListUsersAsync(
        string? search,
        PagedRequest pagination,
        CancellationToken ct = default)
    {
        // SuperAdmin sees staff across every tenant; bypass the tenant query filter.
        var staff = db.StaffMembers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            staff = staff.Where(s =>
                s.FullName.ToLower().Contains(q) ||
                s.Email.ToLower().Contains(q));
        }

        var tenants = db.Tenants.AsNoTracking().IgnoreQueryFilters();

        var query = from s in staff
                    join t in tenants on s.TenantId equals t.Id into tj
                    from t in tj.DefaultIfEmpty()
                    select new
                    {
                        s.Id,
                        s.TenantId,
                        TenantName = t != null ? t.Name : "(unknown)",
                        s.FullName,
                        s.Email,
                        s.Role,
                        s.IsActive,
                        s.CreatedAt
                    };

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.TenantName)
            .ThenBy(u => u.FullName)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(u => new PlatformUserDto(
                u.Id,
                u.TenantId,
                u.TenantName,
                u.FullName,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt))
            .ToListAsync(ct);

        return ServiceResult<PagedResponse<PlatformUserDto>>.Ok(
            PagedResponse<PlatformUserDto>.From(items, total, pagination));
    }
}
