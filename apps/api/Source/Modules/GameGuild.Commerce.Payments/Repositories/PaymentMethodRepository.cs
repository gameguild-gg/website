using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository implementation for the payment-method registry (issue #414).
/// </summary>
public class PaymentMethodRepository(
    IApplicationDbContext context,
    ILogger<PaymentMethodRepository> logger)
    : CommerceRepositoryBase<PaymentMethod>(context), IPaymentMethodRepository
{
    public new async Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payment method by ID: {PaymentMethodId}", id);
        return await Query
            .FirstOrDefaultAsync(pm => pm.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaymentMethod?> GetByGatewayIdAsync(Guid tenantId, string gatewayProvider, string gatewayPaymentMethodId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payment method by gateway ID for tenant {TenantId}", tenantId);
        return await Query
            .FirstOrDefaultAsync(
                pm => pm.TenantId == tenantId
                      && pm.GatewayProvider == gatewayProvider
                      && pm.GatewayPaymentMethodId == gatewayPaymentMethodId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaymentMethod?> GetDeletedByGatewayIdAsync(Guid tenantId, string gatewayProvider, string gatewayPaymentMethodId, CancellationToken cancellationToken = default)
    {
        return await Entities
            .Where(pm => pm.DeletedAt != null)
            .OrderByDescending(pm => pm.DeletedAt)
            .FirstOrDefaultAsync(
                pm => pm.TenantId == tenantId
                      && pm.GatewayProvider == gatewayProvider
                      && pm.GatewayPaymentMethodId == gatewayPaymentMethodId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PaymentMethod>> ListAsync(Guid tenantId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Listing payment methods for tenant {TenantId}", tenantId);
        var query = Query
            .Where(pm => pm.TenantId == tenantId);

        if (userId.HasValue)
        {
            query = query.Where(pm => pm.UserId == userId.Value);
        }

        return await query
            .OrderByDescending(pm => pm.IsDefault)
            .ThenByDescending(pm => pm.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaymentMethod?> GetDefaultAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await Query
            .FirstOrDefaultAsync(pm => pm.TenantId == tenantId && pm.UserId == userId && pm.IsDefault, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PaymentMethod>> ListExpiringCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await Query
            .Where(pm => pm.Type == PaymentMethodType.Card && pm.ExpiresAt != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(paymentMethod, cancellationToken).ConfigureAwait(false);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        Entities.Update(paymentMethod);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return paymentMethod;
    }
}
