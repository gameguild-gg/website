using GameGuild.Commerce;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Products;

/// <summary>
/// <see cref="ISubscriptionPromoCoordinator"/> implementation (issue #418). Bridges the
/// promo engine into subscription creation (quote + usage recording linked to the created
/// subscription) and invoice materialization (uninvoiced usage lookup + invoice stamping),
/// keeping the Subscriptions and Billing project graphs free of Products references.
/// </summary>
public sealed class SubscriptionPromoCoordinator(
    IPricingEngineService pricingEngine,
    IPromoCodeRepository promoCodeRepository,
    IPromoCodeUsageRecorder usageRecorder,
    ILogger<SubscriptionPromoCoordinator> logger) : ISubscriptionPromoCoordinator
{
    /// <inheritdoc />
    public async Task<SubscriptionPromoQuoteResult> QuoteForSubscriptionCreationAsync(
        SubscriptionPromoQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await pricingEngine.ApplyPromoCodesAsync(
            request.Amount,
            request.PromoCodes,
            request.ProductId,
            request.UserId,
            request.PlanId,
            cancellationToken).ConfigureAwait(false);

        var applied = new List<SubscriptionPromoCodeApplication>();
        string? currency = null;

        foreach (var entry in result.AppliedCodes)
        {
            var promoCode = await promoCodeRepository.GetByCodeAsync(entry.Code, cancellationToken)
                .ConfigureAwait(false);

            if (promoCode is null)
            {
                // The engine validated this code moments ago; treat a vanishing row as
                // rejected rather than silently dropping the discount.
                continue;
            }

            currency ??= promoCode.Currency;
            applied.Add(new SubscriptionPromoCodeApplication(
                promoCode.Id,
                promoCode.Code,
                entry.DiscountAmount,
                entry.Type?.ToString(),
                entry.TrialExtensionDays,
                entry.FreeMonthsGranted));
        }

        var rejected = result.RejectedCodes
            .Select(code => new SubscriptionPromoRejectedCode(code.Code, code.Reason))
            .ToList();

        return new SubscriptionPromoQuoteResult(
            result.OriginalAmount,
            result.FinalAmount,
            result.TotalDiscount,
            applied,
            rejected,
            currency ?? "USD",
            result.StackingRuleName);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> RecordSubscriptionUsageAsync(
        SubscriptionPromoUsageRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AppliedCodes.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var baseKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"subscription:{request.SubscriptionId:N}:promo"
            : request.IdempotencyKey.Trim();

        var recordedUsageIds = new List<Guid>();

        foreach (var entry in request.AppliedCodes)
        {
            var promoCode = await promoCodeRepository.GetByCodeAsync(entry.Code, cancellationToken)
                .ConfigureAwait(false);

            if (promoCode is null)
            {
                logger.LogWarning(
                    "Promo code {Code} disappeared before subscription {SubscriptionId} usage recording",
                    entry.Code,
                    request.SubscriptionId);
                continue;
            }

            var perCodeKey = $"{baseKey}:code:{promoCode.Code}";
            var (status, usage) = await usageRecorder.RecordAsync(
                promoCode,
                request.UserId,
                entry.DiscountAmount,
                promoCode.Currency,
                perCodeKey,
                request.OrderId,
                null,
                request.SubscriptionId,
                cancellationToken).ConfigureAwait(false);

            if (status is PromoUsageRecordingStatus.Recorded or PromoUsageRecordingStatus.Replay && usage is not null)
            {
                recordedUsageIds.Add(usage.Id);
            }
            else
            {
                logger.LogWarning(
                    "Promo usage recording for code {Code} on subscription {SubscriptionId} returned {Status}",
                    entry.Code,
                    request.SubscriptionId,
                    status);
            }
        }

        return recordedUsageIds;
    }

    /// <inheritdoc />
    public async Task<SubscriptionPromoInvoiceContext?> GetUninvoicedPromoContextAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var uses = await promoCodeRepository.GetUninvoicedUsesForSubscriptionAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (uses.Count == 0)
        {
            return null;
        }

        var codeIds = uses.Select(u => u.PromoCodeId).Distinct().ToList();
        var codes = (await promoCodeRepository.GetByIdsAsync(codeIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(c => c.Id);

        var applications = uses
            .Select(u => codes.TryGetValue(u.PromoCodeId, out var promoCode)
                ? new SubscriptionPromoCodeApplication(
                    promoCode.Id,
                    promoCode.Code,
                    u.DiscountApplied,
                    promoCode.Type.ToString(),
                    promoCode.Type == PromoCodeType.FreeTrialExtension ? promoCode.FreeTrialExtensionDays : null,
                    promoCode.Type == PromoCodeType.FreeMonths ? promoCode.DurationMonths : null)
                : new SubscriptionPromoCodeApplication(u.PromoCodeId, u.PromoCodeId.ToString("N"), u.DiscountApplied))
            .ToList();

        return new SubscriptionPromoInvoiceContext(
            subscriptionId,
            uses.Sum(u => u.DiscountApplied),
            applications,
            uses.Select(u => u.Id).ToList());
    }

    /// <inheritdoc />
    public Task<int> MarkPromoUsesInvoicedAsync(
        IReadOnlyList<Guid> usageIds,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
        => promoCodeRepository.MarkUsesInvoicedAsync(usageIds, invoiceId, cancellationToken);
}
