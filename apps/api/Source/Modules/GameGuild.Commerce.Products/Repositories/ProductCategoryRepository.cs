using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Repository implementation for <see cref="ProductCategory"/> entities (issue #401).
///     Hierarchy traversal (parent maps, descendants) is done in memory over the small
///     per-tenant category table instead of recursive SQL, keeping queries portable.
/// </summary>
public class ProductCategoryRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<ProductCategory>(context), IProductCategoryRepository
{
    public override async Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProductCategory?> GetBySlugAsync(
        string slug,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var normalized = ProductCategory.NormalizeSlug(slug);
        return await Query
            .Where(c => c.TenantId == tenantId && c.Slug == normalized)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<ProductCategory>> ListAsync(
        Guid? tenantId,
        Guid? parentCategoryId = null,
        bool rootsOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = Query.Where(c => c.TenantId == tenantId);

        if (parentCategoryId.HasValue)
            query = query.Where(c => c.ParentCategoryId == parentCategoryId.Value);

        if (rootsOnly)
            query = query.Where(c => c.ParentCategoryId == null);

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, Guid?>> GetParentMapAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await Query
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(
                c => c.Id,
                c => c.ParentCategoryId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<Guid>> GetCategoryAndDescendantIdsAsync(
        Guid categoryId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var parentMap = await GetParentMapAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!parentMap.ContainsKey(categoryId))
            return [];

        // BFS downwards over the id → parent map (children grouped by parent).
        var childrenByParent = parentMap
            .Where(pair => pair.Value.HasValue)
            .GroupBy(pair => pair.Value!.Value)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).ToList());

        var result = new List<Guid> { categoryId };
        var queue = new Queue<Guid>();
        queue.Enqueue(categoryId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
                continue;

            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child);
            }
        }

        return result;
    }

    public Task<bool> HasChildrenAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return Query
            .AnyAsync(c => c.ParentCategoryId == categoryId, cancellationToken);
    }

    public Task<int> CountProductsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return Context.Set<Product>()
            .Where(p => p.DeletedAt == null && p.CategoryId == categoryId)
            .CountAsync(cancellationToken);
    }

    public async Task AddAsync(ProductCategory category, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(category, cancellationToken).ConfigureAwait(false);
    }

    public new Task UpdateAsync(ProductCategory category, CancellationToken cancellationToken = default)
    {
        // Deliberately shadows the base (which saves immediately): handlers persist
        // explicitly so audit + validation + save stay one unit of work.
        Entities.Update(category);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ProductCategory category, CancellationToken cancellationToken = default)
    {
        Entities.Remove(category);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
