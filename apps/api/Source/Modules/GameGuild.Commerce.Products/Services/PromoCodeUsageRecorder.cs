using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Atomic promo usage recorder. Counter increments (total + per-user) are guarded conditional
/// updates and the usage row insert shares one transaction, so concurrent redemptions cannot
/// exceed <c>MaxUses</c>/<c>MaxUsesPerUser</c> and a replayed idempotency key never counts twice.
/// </summary>
public class PromoCodeUsageRecorder(IPromoCodeRepository promoCodeRepository)
    : IPromoCodeUsageRecorder
{
    /// <inheritdoc />
    public async Task<(PromoUsageRecordingStatus Status, PromoCodeUse? Usage)> RecordAsync(
        PromoCode promoCode,
        Guid userId,
        decimal discountApplied,
        string currency,
        string? idempotencyKey,
        Guid? orderId = null,
        Guid? orderLineItemId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(promoCode);

        // Idempotent replay: a repeated submission of the same key returns the original row.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await promoCodeRepository.FindUsageByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                return (PromoUsageRecordingStatus.Replay, existing);
            }
        }

        if (!promoCode.IsCurrentlyValid())
        {
            return (PromoUsageRecordingStatus.RejectedTotalLimit, null);
        }

        await using var transaction = await promoCodeRepository.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Guarded total increment: single conditional UPDATE, fails closed on breach.
            var totalIncremented = await promoCodeRepository.TryIncrementTotalUsageAsync(
                promoCode.Id, promoCode.MaxUses, cancellationToken).ConfigureAwait(false);
            if (!totalIncremented)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (PromoUsageRecordingStatus.RejectedTotalLimit, null);
            }

            // Guarded per-user increment.
            var userIncremented = await promoCodeRepository.TryIncrementUserUsageAsync(
                promoCode.Id, userId, promoCode.MaxUsesPerUser, cancellationToken).ConfigureAwait(false);
            if (!userIncremented)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return (PromoUsageRecordingStatus.RejectedPerUserLimit, null);
            }

            var usage = new PromoCodeUse
            {
                Id = Guid.NewGuid(),
                PromoCodeId = promoCode.Id,
                UserId = userId,
                TenantId = promoCode.TenantId,
                UsedAt = SystemClock.UtcNow,
                DiscountApplied = decimal.Round(discountApplied, 2, MidpointRounding.AwayFromZero),
                Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.ToUpperInvariant(),
                OrderId = orderId,
                OrderLineItemId = orderLineItemId,
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
            };

            await promoCodeRepository.RecordUsageAsync(usage, cancellationToken).ConfigureAwait(false);
            await promoCodeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return (PromoUsageRecordingStatus.Recorded, usage);
        }
        catch (DbUpdateException)
        {
            // A concurrent request with the same idempotency key may have inserted the row
            // first (unique index). Roll back, then resolve the winner by key.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var winner = await promoCodeRepository.FindUsageByIdempotencyKeyAsync(idempotencyKey, CancellationToken.None)
                    .ConfigureAwait(false);
                if (winner is not null)
                {
                    return (PromoUsageRecordingStatus.Replay, winner);
                }
            }

            throw;
        }
    }
}
