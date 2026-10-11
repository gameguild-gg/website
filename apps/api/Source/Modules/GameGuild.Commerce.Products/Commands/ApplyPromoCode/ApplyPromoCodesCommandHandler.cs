using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for applying promo codes to an order. Computes the rule-driven discount via the
/// pricing engine and then records one usage row per applied code atomically. Usage
/// recording is idempotent on the supplied key and fails closed on usage limits: a code
/// whose usage cannot be recorded is dropped from the applied set and reported as rejected.
/// </summary>
public sealed class ApplyPromoCodesCommandHandler(
    IPricingEngineService pricingEngine,
    IPromoCodeRepository promoCodeRepository,
    IPromoCodeUsageRecorder usageRecorder,
    IPromoCodeAuditLogger auditLogger,
    ILogger<ApplyPromoCodesCommandHandler> logger)
    : ICommandHandler<ApplyPromoCodesCommand, PromoCodeApplicationResult>
{
    public async Task<PromoCodeApplicationResult> Handle(
        ApplyPromoCodesCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await pricingEngine.ApplyPromoCodesAsync(
            request.OrderAmount,
            request.PromoCodes,
            request.ProductId,
            request.UserId,
            cancellationToken).ConfigureAwait(false);

        var usageRecorded = false;

        if (request.UserId.HasValue && result.AppliedCodes.Count > 0)
        {
            var baseKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? $"apply:{Guid.NewGuid():N}"
                : request.IdempotencyKey.Trim();

            var applied = new List<AppliedPromoCode>();
            var rejected = new List<RejectedPromoCode>(result.RejectedCodes);

            foreach (var entry in result.AppliedCodes)
            {
                var promoCode = await promoCodeRepository.GetByCodeAsync(entry.Code, cancellationToken)
                    .ConfigureAwait(false);

                if (promoCode is null)
                {
                    rejected.Add(new RejectedPromoCode(entry.Code, "Code not found"));
                    continue;
                }

                var perCodeKey = $"{baseKey}:code:{promoCode.Code}";
                var (status, usage) = await usageRecorder.RecordAsync(
                    promoCode,
                    request.UserId.Value,
                    entry.DiscountAmount,
                    promoCode.Currency,
                    perCodeKey,
                    request.OrderId,
                    null,
                    cancellationToken).ConfigureAwait(false);

                switch (status)
                {
                    case PromoUsageRecordingStatus.Recorded:
                        applied.Add(entry);
                        usageRecorded = true;
                        await auditLogger.LogRedemptionAsync(
                            request.UserId.Value, promoCode.Code, entry.DiscountAmount, promoCode.Currency, usage?.Id, cancellationToken).ConfigureAwait(false);
                        break;

                    case PromoUsageRecordingStatus.Replay:
                        // Idempotent replay: keep the code applied but never count it twice.
                        applied.Add(entry);
                        usageRecorded = true;
                        break;

                    case PromoUsageRecordingStatus.RejectedTotalLimit:
                        rejected.Add(new RejectedPromoCode(entry.Code, "Promo code has reached maximum uses"));
                        break;

                    case PromoUsageRecordingStatus.RejectedPerUserLimit:
                        rejected.Add(new RejectedPromoCode(entry.Code, "You have already used this promo code the maximum number of times"));
                        break;

                    default:
                        logger.LogWarning("Unhandled promo usage recording status {Status} for code {Code}", status, promoCode.Code);
                        rejected.Add(new RejectedPromoCode(entry.Code, "Usage could not be recorded"));
                        break;
                }
            }

            var totalDiscount = applied.Sum(a => a.DiscountAmount);
            result = result with
            {
                AppliedCodes = applied,
                RejectedCodes = rejected,
                TotalDiscount = totalDiscount,
                FinalAmount = Math.Max(0, request.OrderAmount - totalDiscount)
            };
        }

        return result with { UsageRecorded = usageRecorded };
    }
}
