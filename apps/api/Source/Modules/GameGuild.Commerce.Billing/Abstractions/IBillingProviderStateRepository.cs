namespace GameGuild.Commerce.Billing;

/// <summary>
///     Persistence for <see cref="BillingProviderState"/> rows (issue #397
///     provider-management API). Rows only exist for providers an administrator
///     has explicitly toggled; a missing row means "default enabled".
/// </summary>
public interface IBillingProviderStateRepository
{
    /// <summary>
    ///     Gets the persisted management state for a provider key, if any.
    /// </summary>
    Task<BillingProviderState?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets every persisted provider management state row.
    /// </summary>
    Task<IReadOnlyList<BillingProviderState>> GetAllStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new provider management state row.
    /// </summary>
    Task<BillingProviderState> CreateAsync(BillingProviderState state, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing provider management state row.
    /// </summary>
    Task<BillingProviderState> UpdateAsync(BillingProviderState state, CancellationToken cancellationToken = default);
}
