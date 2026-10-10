using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository implementation for Payment entity
/// </summary>
public class PaymentRepository(
    IApplicationDbContext context,
    ILogger<PaymentRepository> logger) 
    : CommerceRepositoryBase<Payment>(context), IPaymentRepository
{
    public new async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payment by ID: {PaymentId}", id);
        return await Query
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payment by idempotency key: {IdempotencyKey}", LogRedaction.Sanitize(idempotencyKey));
        return await Query
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Payment?> GetByExternalPaymentIdAsync(string externalPaymentId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payment by external payment ID: {ExternalPaymentId}", LogRedaction.Sanitize(externalPaymentId));
        return await Query
            .FirstOrDefaultAsync(p => p.ExternalPaymentId == externalPaymentId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Payment?> GetByProviderMappingAsync(
        string provider,
        string providerEnvironment,
        string providerAccountId,
        string providerObjectId,
        string providerObjectType,
        string providerMonetaryLeg,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Getting payment by provider mapping: {Provider}/{ProviderEnvironment}/{ProviderAccountId}/{ProviderObjectType}/{ProviderObjectId}/{ProviderMonetaryLeg}",
            LogRedaction.Sanitize(provider),
            LogRedaction.Sanitize(providerEnvironment),
            LogRedaction.RedactId(providerAccountId, "provider"),
            LogRedaction.Sanitize(providerObjectType),
            LogRedaction.Sanitize(providerObjectId),
            LogRedaction.Sanitize(providerMonetaryLeg));

        return await Query
            .FirstOrDefaultAsync(
                payment => payment.Provider == provider
                           && payment.ProviderEnvironment == providerEnvironment
                           && payment.ProviderAccountId == providerAccountId
                           && payment.ProviderObjectId == providerObjectId
                           && payment.ProviderObjectType == providerObjectType
                           && payment.ProviderMonetaryLeg == providerMonetaryLeg,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Payment>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payments for tenant: {TenantId}", tenantId);
        return await Query
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Payment>> GetBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payments for subscription: {SubscriptionId}", subscriptionId);
        return await Query
            .Where(p => p.SubscriptionId == subscriptionId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Payment>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payments for order: {OrderId}", orderId);
        return await Query
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Payment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payments for invoice: {InvoiceId}", invoiceId);
        return await Query
            .Where(p => p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payments by status: {Status}", status);
        return await Query
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Payment>> GetDueForRetryAsync(CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        logger.LogDebug("Getting payments due for retry at: {Now}", now);

        return await Query
            .Where(p => p.Status == PaymentStatus.Failed
                        && p.NextRetryAt != null
                        && p.NextRetryAt <= now
                        && !p.MaxRetriesReached)
            .OrderBy(p => p.NextRetryAt)
            .ToListAsync(cancellationToken)
            ;
    }

    public async Task<IEnumerable<Payment>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payments from {StartDate} to {EndDate} for tenant: {TenantId}",
            startDate, endDate, tenantId);

        var query = Query
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate);

        if (tenantId.HasValue)
        {
            query = query.Where(p => p.TenantId == tenantId.Value);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<PaymentStatus, int>> GetCountByStatusAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting payment count by status for tenant: {TenantId}", tenantId);

        var query = Query;

        if (tenantId.HasValue)
        {
            query = query.Where(p => p.TenantId == tenantId.Value);
        }

        return await query
            .GroupBy(p => p.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<decimal> GetRevenueForPeriodAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting revenue from {StartDate} to {EndDate} for tenant: {TenantId}",
            startDate, endDate, tenantId);

        var query = Query
            .Where(p => p.Status == PaymentStatus.Succeeded
                        && p.ProcessedAt >= startDate
                        && p.ProcessedAt <= endDate);

        if (tenantId.HasValue)
        {
            query = query.Where(p => p.TenantId == tenantId.Value);
        }

        return await query
            .SumAsync(p => p.NetAmount, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding new payment with idempotency key: {IdempotencyKey}", LogRedaction.Sanitize(payment.IdempotencyKey));

        // Check for existing payment with same idempotency key
        var existing = await GetByIdempotencyKeyAsync(payment.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            logger.LogWarning("Payment with idempotency key {IdempotencyKey} already exists, returning existing",
                LogRedaction.Sanitize(payment.IdempotencyKey));
            return existing;
        }

        await Entities.AddAsync(payment, cancellationToken).ConfigureAwait(false);
        try
        {
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            Entities.Remove(payment);
            var concurrentWinner = await GetByIdempotencyKeyAsync(payment.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            if (concurrentWinner is null)
                throw;

            logger.LogInformation(
                "Concurrent payment reservation won idempotency key {IdempotencyKey}; replaying payment {PaymentId}",
                LogRedaction.Sanitize(payment.IdempotencyKey),
                concurrentWinner.Id);
            return concurrentWinner;
        }

        logger.LogInformation("Successfully added payment {PaymentId}", payment.Id);
        return payment;
    }

    public new async Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating payment {PaymentId}", payment.Id);

        Entities.Update(payment);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Successfully updated payment {PaymentId}", payment.Id);
        return payment;
    }

    public async Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Checking if payment exists with idempotency key: {IdempotencyKey}", LogRedaction.Sanitize(idempotencyKey));
        return await Query
            .AnyAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
    }
}
