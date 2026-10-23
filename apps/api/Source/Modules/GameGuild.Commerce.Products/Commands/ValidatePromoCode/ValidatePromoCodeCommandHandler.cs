using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for validating a promo code. Every attempt (successful or not) is persisted as a
/// <see cref="PromoCodeValidationAttempt"/> so campaign performance can compute a
/// validations-to-applications conversion rate (issue #418). Attempt persistence is
/// best effort: a tracking write failure is logged and never fails the validation itself.
/// </summary>
public sealed class ValidatePromoCodeCommandHandler(
    IPricingEngineService pricingEngine,
    IPromoCodeRepository promoCodeRepository,
    ILogger<ValidatePromoCodeCommandHandler> logger)
    : ICommandHandler<ValidatePromoCodeCommand, PromoCodeValidationResult>
{
    public async Task<PromoCodeValidationResult> Handle(
        ValidatePromoCodeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await pricingEngine.ValidatePromoCodeAsync(
            request.Code,
            request.OrderAmount,
            request.ProductId,
            request.UserId,
            request.PlanId,
            cancellationToken).ConfigureAwait(false);

        await RecordAttemptAsync(request, result, cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Persists the conversion-tracking denominator. The outcome carries the resolved
    /// promo code id, so unknown code strings remain attributable only by their raw text —
    /// code-farming patterns stay observable without inflating any campaign's numbers.
    /// </summary>
    private async Task RecordAttemptAsync(
        ValidatePromoCodeCommand request,
        PromoCodeValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await promoCodeRepository.RecordValidationAttemptAsync(
                request.Code,
                result.PromoCodeId,
                request.UserId,
                result.Outcome ?? (result.IsValid
                    ? PromoCodeValidationOutcome.Valid
                    : PromoCodeValidationOutcome.Invalid),
                result.ErrorMessage,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to persist promo validation attempt for code {Code}; the validation result is unaffected",
                request.Code);
        }
    }
}
