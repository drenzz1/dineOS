using DineOS.Domain.Common;
using System.Linq.Expressions;

namespace DineOS.Application.Interfaces.Repositories;

public interface IRepository<T> where T : BaseAuditingEntity
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
