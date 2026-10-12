using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Repository implementation for SubscriptionPlan entity using the shared application context
/// </summary>
public class SubscriptionPlanRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<SubscriptionPlan>(context), ISubscriptionPlanRepository
{
    public new async Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionPlan?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await Query.FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionPlan?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await Query.FirstOrDefaultAsync(p => p.ExternalId == externalId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        // Issue #416: "Public API only returns active, public plans" — plans archived via
        // :archive (IsActive = false) must not leak into the public active list.
        return await Query.Where(p => p.DeletedAt == null && p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Query.OrderBy(p => p.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetFeaturedAsync(CancellationToken cancellationToken = default)
    {
        // Same IsActive guard as GetActiveAsync: the featured list feeds the public pricing
        // surface (issue #416).
        return await Query.Where(p => p.IsFeatured && p.DeletedAt == null && p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<SubscriptionPlan>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        return await Query
            .Where(p => p.DeletedAt == null && (p.Name.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm))))
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<SubscriptionPlan>> GetByPriceRangeAsync(long minPriceInCents, long maxPriceInCents, CancellationToken cancellationToken = default)
    {
        return await Query
            .Where(p => p.DeletedAt == null && p.MonthlyPriceInCents >= minPriceInCents && p.MonthlyPriceInCents <= maxPriceInCents)
            .OrderBy(p => p.MonthlyPriceInCents)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a new subscription plan.
    /// </summary>
    public async Task<SubscriptionPlan> AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        return await CreateAsync(plan, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a subscription plan (soft delete).
    /// </summary>
    public new async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await base.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = Query.Where(p => p.Name == name);
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        return !await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsSlugUniqueAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = Query.Where(p => p.Slug == slug);
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        return !await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetActiveSubscriptionCountAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await Query.Include(p => p.Subscriptions).FirstOrDefaultAsync(p => p.Id == planId, cancellationToken).ConfigureAwait(false);
        if (plan == null) return 0;
        return plan.Subscriptions.Count(s => s.Status == SubscriptionStatus.Active);
    }

    public async Task<PagedResult<SubscriptionPlan>> GetPagedAsync(int skip, int pageSize, string? searchTerm = null, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var query = Query.AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(p => p.DeletedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => p.Name.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(p => p.SortOrder)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return PagedResult<SubscriptionPlan>.FromPage(items, totalCount, skip / pageSize + 1, pageSize);
    }
}
