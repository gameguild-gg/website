using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     EF Core repository for <see cref="BillingProviderState"/> rows. Create/Update
///     semantics are inherited from <see cref="CommerceRepositoryBase{TEntity}"/>.
/// </summary>
public class BillingProviderStateRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<BillingProviderState>(context), IBillingProviderStateRepository
{
    /// <inheritdoc />
    public async Task<BillingProviderState?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var normalizedKey = PaymentProviders.Normalize(providerKey);

        return await Entities
            .FirstOrDefaultAsync(state => state.ProviderKey == normalizedKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BillingProviderState>> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        return await Entities
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
