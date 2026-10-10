namespace GameGuild.Commerce.Products;

/// <summary>
/// Repository interface for PromoStackingRule entities
/// </summary>
public interface IPromoStackingRuleRepository
{
    /// <summary>Get the active stacking rule with the highest priority (ties resolved by earliest creation for determinism).</summary>
    Task<PromoStackingRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default);

    /// <summary>Get a stacking rule by ID</summary>
    Task<PromoStackingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Get all stacking rules ordered by priority (descending)</summary>
    Task<List<PromoStackingRule>> ListAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>Check whether a rule name is already in use</summary>
    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Add a new stacking rule</summary>
    Task AddAsync(PromoStackingRule rule, CancellationToken cancellationToken = default);

    /// <summary>Update an existing stacking rule</summary>
    Task UpdateAsync(PromoStackingRule rule, CancellationToken cancellationToken = default);

    /// <summary>Delete a stacking rule</summary>
    Task DeleteAsync(PromoStackingRule rule, CancellationToken cancellationToken = default);

    /// <summary>Save changes</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
