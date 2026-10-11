using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Repository implementation for <see cref="PricingRule"/> entities (issue #395).
/// </summary>
public class PricingRuleRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<PricingRule>(context), IPricingRuleRepository
{
    /// <inheritdoc />
    public new async Task<PricingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query
            .Include(r => r.PricingTiers)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PricingRule>> GetActiveRulesForProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        var rules = await Query
            .Include(r => r.PricingTiers)
            .Where(r => r.IsActive)
            .Where(r => r.ProductId == productId || r.ProductId == null)
            .Where(r => r.StartDate == null || r.StartDate <= now)
            .Where(r => r.EndDate == null || r.EndDate > now)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rules;
    }

    /// <inheritdoc />
    public async Task<string> GetChangeStampAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        // Only Version/UpdatedAt are projected, so the stamp query stays cheap even with many rules.
        // Any create/update/touch/soft-delete of a product-scoped or global rule changes at least
        // one component of the stamp (count, max version, or max updated ticks).
        var rows = await Query
            .Where(r => r.ProductId == productId || r.ProductId == null)
            .Select(r => new { r.Version, r.UpdatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var maxVersion = rows.Count > 0 ? rows.Max(r => r.Version) : 0;
        var maxUpdatedTicks = rows.Count > 0 ? rows.Max(r => r.UpdatedAt.Ticks) : 0L;

        return $"{rows.Count}:{maxVersion}:{maxUpdatedTicks}";
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PricingRule> Items, int TotalCount)> GetPagedAsync(
        bool? isActive = null,
        PricingRuleType? ruleType = null,
        Guid? productId = null,
        string? searchTerm = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PricingRule> query = Query
            .Include(r => r.PricingTiers);

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        if (ruleType.HasValue)
        {
            query = query.Where(r => r.RuleType == ruleType.Value);
        }

        if (productId.HasValue)
        {
            // Filtering by product also surfaces global rules (null ProductId) that affect it.
            query = query.Where(r => r.ProductId == productId.Value || r.ProductId == null);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(term) ||
                (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await query
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.UpdatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task AddAsync(PricingRule rule, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(rule, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public new async Task<PricingRule> UpdateAsync(PricingRule rule, CancellationToken cancellationToken = default)
    {
        rule.Touch();

        // Replacement tiers (see PricingRuleMutationHelpers.ApplyTiers) are brand-new rows and
        // must be tracked as Added. Anything that runs change detection first (DbSet.Local,
        // SaveChanges, DbSet.Update on the graph) discovers them through the tracked rule's
        // collection and tracks them as Modified, emitting concurrency-checked UPDATEs for
        // rows that do not exist — relational providers then fail the save with
        // DbUpdateConcurrencyException. DbContext.Entry does not run change detection, so the
        // check below still sees a not-yet-persisted tier as Detached. IApplicationDbContext
        // intentionally exposes only Set/SaveChanges/BeginTransaction, hence the cast.
        var dbContext = (DbContext)Context;
        foreach (var tier in rule.PricingTiers)
        {
            if (dbContext.Entry(tier).State == EntityState.Detached)
            {
                Context.Set<PricingRuleTier>().Add(tier);
            }
        }

        // Command handlers load the rule through this repository (same context), so the root
        // is already tracked and DetectChanges at save picks up its mutations. Update() is
        // kept for a detached root, preserving the previous behavior for direct callers.
        if (dbContext.Entry(rule).State == EntityState.Detached)
        {
            Entities.Update(rule);
        }

        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rule;
    }

    /// <inheritdoc />
    public new async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await Entities
            .FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);

        if (rule == null)
        {
            return false;
        }

        rule.SoftDelete();
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
