using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository implementation for the <see cref="Refund"/> entity (issue #419).
/// </summary>
public class RefundRepository(
    IApplicationDbContext context,
    ILogger<RefundRepository> logger)
    : CommerceRepositoryBase<Refund>(context), IRefundRepository
{
    public new async Task<Refund?> GetByIdAsync(Guid refundId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting refund by ID: {RefundId}", refundId);
        return await Query
            .FirstOrDefaultAsync(refund => refund.Id == refundId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Refund?> GetByPaymentAndIdempotencyKeyAsync(
        Guid paymentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting refund by payment {PaymentId} and idempotency key", paymentId);
        return await Query
            .FirstOrDefaultAsync(
                refund => refund.PaymentId == paymentId && refund.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<Refund> Items, int Total)> ListAsync(
        Guid? tenantId = null,
        RefundStatus? status = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = Query;
        if (tenantId.HasValue)
        {
            query = query.Where(refund => refund.TenantId == tenantId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(refund => refund.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(refund => refund.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(refund => refund.CreatedAt <= toUtc.Value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(refund => refund.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<Refund> AddAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding refund for payment {PaymentId}", refund.PaymentId);

        var existing = await GetByPaymentAndIdempotencyKeyAsync(refund.PaymentId, refund.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            logger.LogInformation(
                "Refund with idempotency key {IdempotencyKey} for payment {PaymentId} already exists, returning existing",
                LogRedaction.Sanitize(refund.IdempotencyKey),
                refund.PaymentId);
            return existing;
        }

        await Entities.AddAsync(refund, cancellationToken).ConfigureAwait(false);
        try
        {
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            Entities.Remove(refund);
            var concurrentWinner = await GetByPaymentAndIdempotencyKeyAsync(refund.PaymentId, refund.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            if (concurrentWinner is null)
            {
                throw;
            }

            logger.LogInformation(
                "Concurrent refund won idempotency key {IdempotencyKey}; replaying refund {RefundId}",
                LogRedaction.Sanitize(refund.IdempotencyKey),
                concurrentWinner.Id);
            return concurrentWinner;
        }

        logger.LogInformation("Successfully added refund {RefundId}", refund.Id);
        return refund;
    }

    public new async Task<Refund> UpdateAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating refund {RefundId}", refund.Id);

        Entities.Update(refund);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return refund;
    }

    /// <inheritdoc />
    public async Task<Refund?> GetOpenRefundForPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting open refund for payment {PaymentId}", paymentId);
        return await Query
            .Where(refund => refund.PaymentId == paymentId
                && (refund.Status == RefundStatus.Pending
                    || refund.Status == RefundStatus.Approved
                    || refund.Status == RefundStatus.Processing
                    || refund.Status == RefundStatus.Failed))
            .OrderByDescending(refund => refund.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Refund>> ListDueForWorkflowAsync(
        int maxExecutionAttempts,
        DateTime failedBeforeUtc,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (take < 1) take = 50;

        logger.LogDebug("Listing refunds due for the automated refund workflow");
        return await Query
            .Where(refund => refund.Status == RefundStatus.Approved
                || (refund.Status == RefundStatus.Failed
                    && refund.AttemptCount < maxExecutionAttempts
                    && refund.FailedAt != null
                    && refund.FailedAt <= failedBeforeUtc))
            .OrderBy(refund => refund.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
