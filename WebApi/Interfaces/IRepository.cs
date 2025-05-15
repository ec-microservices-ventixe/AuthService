using System.Linq.Expressions;

namespace WebApi.Interfaces;

public interface IRepository<TEntity> where TEntity : class
{
    public Task<IEnumerable<TEntity>> GetAllAsync();
    public Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate);

    public Task<TEntity> CreateAsync(TEntity entity);

    public Task<TEntity> UpdateAsync(Expression<Func<TEntity, bool>> predicate, TEntity updatedEntity);

    public Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate);

    public Task<bool> DeleteAsync(TEntity entity);

    public Task<bool> EntityExistsAsync(Expression<Func<TEntity, bool>> predicate);

    public Task BeginTransactionAsync();
    public Task CommitTransactionAsync();
    public Task RollbackTransactionAsync();
}
