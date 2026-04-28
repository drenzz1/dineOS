using DineOS.Application.Interfaces.Repositories;
using DineOS.Application.Interfaces.Services;
using DineOS.Domain.Common;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DineOS.Infrastructure.Repositories;

public class GenericRepository<T>(AppDbContext db, ICurrentUserService currentUser) : IRepository<T>
    where T : BaseAuditingEntity
{
    private readonly DbSet<T> _set = db.Set<T>();

    public async Task<T?> GetByIdAsync(long id, CancellationToken ct = default) =>
        await _set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default) =>
        await _set.ToListAsync(ct);

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await _set.Where(predicate).ToListAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _set.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _set.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is null) return;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = currentUser.UserId;
        _set.Update(entity);
        await db.SaveChangesAsync(ct);
    }
}
