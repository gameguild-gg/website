using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Repository abstraction for <see cref="PricingRule"/> entities (issue #395).
/// </summary>
public interface IPricingRuleRepository
{
    /// <summary>
    /// Gets a pricing rule by ID, including its tiers. Returns null when not found or soft-deleted.
    /// </summary>
    Task<PricingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently active rules (product-scoped and global) for a product, including tiers,
    /// ordered by priority (highest first) so callers can pick the first applicable rule.
    /// </summary>
    Task<IReadOnlyList<PricingRule>> GetActiveRulesForProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a change stamp for every non-deleted rule that can affect the given product
    /// (product-scoped and global rules). The stamp changes whenever any such rule is created,
    /// updated, activated, deactivated, or deleted, so it can be embedded in cache keys to
    /// invalidate cached rule sets deterministically.
    /// </summary>
    Task<string> GetChangeStampAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of pricing rules with optional filters.
    /// </summary>
    Task<(IReadOnlyList<PricingRule> Items, int TotalCount)> GetPagedAsync(
        bool? isActive = null,
        PricingRuleType? ruleType = null,
        Guid? productId = null,
        string? searchTerm = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new pricing rule. Callers must call <see cref="SaveChangesAsync"/> to persist.
    /// </summary>
    Task AddAsync(PricingRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing pricing rule (touches <c>UpdatedAt</c>) and saves immediately.
    /// </summary>
    Task<PricingRule> UpdateAsync(PricingRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a pricing rule by ID. Returns false when not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
