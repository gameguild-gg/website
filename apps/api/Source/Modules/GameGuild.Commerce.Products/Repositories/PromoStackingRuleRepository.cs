using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Repository implementation for PromoStackingRule entities
/// </summary>
public class PromoStackingRuleRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<PromoStackingRule>(context), IPromoStackingRuleRepository
{
    /// <inheritdoc />
    public async Task<PromoStackingRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default)
    {
        return await Query
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public new async Task<PromoStackingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<PromoStackingRule>> ListAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = Query.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await Query.AnyAsync(r => r.Name == name, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(PromoStackingRule rule, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(rule, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UpdateAsync(PromoStackingRule rule, CancellationToken cancellationToken = default)
    {
        Entities.Update(rule);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(PromoStackingRule rule, CancellationToken cancellationToken = default)
    {
        Entities.Remove(rule);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
