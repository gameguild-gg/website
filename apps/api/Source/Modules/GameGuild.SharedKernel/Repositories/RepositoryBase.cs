using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace GameGuild;

/// <summary>
///     Generic EF Core implementation of <see cref="IRepository{T, TKey}"/>.
///     Provides standard CRUD, soft-delete, paging, and query operations.
///     Module-specific repositories can inherit and override any method.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
/// <typeparam name="TKey">The primary key type</typeparam>
public abstract class RepositoryBase<T, TKey> : IRepository<T, TKey>
    where T : class, IEntity<TKey>
    where TKey : IEquatable<TKey>
{
    protected readonly IApplicationDbContext Context;

    protected RepositoryBase(IApplicationDbContext context)
    {
        Context = context;
    }

    /// <summary>
    ///     Gets the DbSet for the entity type.
    /// </summary>
    protected virtual DbSet<T> DbSet => Context.Set<T>();

    /// <summary>
    ///     Gets a base queryable. Override to add global filters (e.g. soft-delete, tenant).
    /// </summary>
    protected virtual IQueryable<T> Query => DbSet.AsQueryable();

    // ── Read operations ──────────────────────────────────────────────────

    /// <inheritdoc />
    public virtual async Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await Query
            .FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Maximum number of entities returned by <see cref="GetAllAsync"/>.
    ///     Override in derived repositories to adjust for specific entity types.
    ///     Use <see cref="GetPagedAsync"/> for larger datasets.
    /// </summary>
    protected virtual int MaxGetAllCount => 1000;

    /// <inheritdoc />
    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Query
            .OrderByDescending(e => e.CreatedAt)
            .Take(MaxGetAllCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<IPage<T>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await Query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await Query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult<T>.FromPage(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await Query
            .Where(predicate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await Query
            .FirstOrDefaultAsync(predicate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await Query
            .AnyAsync(predicate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = Query;
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // ── Write operations ─────────────────────────────────────────────────

    /// <inheritdoc />
    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return entity;
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await DbSet.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Note: Synchronous EF Core operation wrapped for interface compliance.
    ///     <see cref="DbSet{T}.Update"/> is inherently synchronous; changes are persisted
    ///     when <see cref="SaveChangesAsync"/> is called.
    /// </remarks>
    public virtual Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        return Task.FromResult(entity);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Note: Synchronous EF Core operation wrapped for interface compliance.
    ///     UpdateRange is inherently synchronous; changes are persisted
    ///     when <see cref="SaveChangesAsync"/> is called.
    /// </remarks>
    public virtual Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        DbSet.UpdateRange(entities);
        return Task.CompletedTask;
    }

    // ── Delete operations ────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    ///     Note: Synchronous EF Core operation wrapped for interface compliance.
    ///     <see cref="DbSet{T}.Remove"/> is inherently synchronous; changes are persisted
    ///     when <see cref="SaveChangesAsync"/> is called.
    /// </remarks>
    public virtual Task RemoveAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async Task RemoveAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entity of type {typeof(T).Name} with id '{id}' was not found.");

        DbSet.Remove(entity);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Note: Synchronous EF Core operation wrapped for interface compliance.
    ///     RemoveRange is inherently synchronous; changes are persisted
    ///     when <see cref="SaveChangesAsync"/> is called.
    /// </remarks>
    public virtual Task RemoveRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        DbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async Task SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entity of type {typeof(T).Name} with id '{id}' was not found for soft-delete.");

        entity.SoftDelete();
    }

    /// <inheritdoc />
    public virtual async Task RestoreAsync(TKey id, CancellationToken cancellationToken = default)
    {
        // Must query without soft-delete filter to find deleted entities
        var entity = await DbSet
            .FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Entity of type {typeof(T).Name} with id '{id}' was not found for restore.");

        entity.Restore();
    }

    /// <inheritdoc />
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Convenience base class for repositories of entities with <see cref="Guid"/> keys.
///     Most entities in GameGuild use Guid keys, so this is the common case.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public abstract class RepositoryBase<T> : RepositoryBase<T, Guid>, IRepository<T>
    where T : class, IEntity<Guid>
{
    protected RepositoryBase(IApplicationDbContext context) : base(context) { }
}
