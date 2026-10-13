namespace GameGuild.Commerce.Products;

/// <summary>
/// Service for managing promotional codes
/// </summary>
public class PromoCodeService(
    IPromoCodeRepository promoCodeRepository,
    IPromoCodeUsageRecorder usageRecorder) : IPromoCodeService
{
    /// <inheritdoc />
    public async Task<PromoCode> CreatePromoCodeAsync(PromoCode promoCode)
    {
        ArgumentNullException.ThrowIfNull(promoCode);

        // Codes are stored uppercase/trimmed so every lookup path is case-insensitive.
        promoCode.Code = PromoCodeNormalizer.Normalize(promoCode.Code) ?? promoCode.Code;

        // Check if code already exists (normalized, case-insensitive)
        if (await promoCodeRepository.CodeExistsAsync(promoCode.Code).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Promo code '{promoCode.Code}' already exists");
        }

        await promoCodeRepository.AddAsync(promoCode).ConfigureAwait(false);
        await promoCodeRepository.SaveChangesAsync().ConfigureAwait(false);

        return promoCode;
    }

    /// <inheritdoc />
    public async Task<PromoCode?> GetPromoCodeByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return await promoCodeRepository.GetByCodeAsync(code).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ValidatePromoCodeAsync(string code, Guid userId, Guid? productId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var promoCode = await promoCodeRepository.GetByCodeAsync(code).ConfigureAwait(false);
        if (promoCode == null)
        {
            return false;
        }

        // Check if active
        if (!promoCode.IsActive)
        {
            return false;
        }

        // Check validity period
        if (!promoCode.IsCurrentlyValid())
        {
            return false;
        }

        // Check product restriction
        if (promoCode.ProductId.HasValue && productId.HasValue && promoCode.ProductId != productId)
        {
            return false;
        }

        // Check usage limits
        if (promoCode.MaxUses.HasValue)
        {
            var usageCount = await promoCodeRepository.GetUsageCountAsync(promoCode.Id).ConfigureAwait(false);
            if (usageCount >= promoCode.MaxUses.Value)
            {
                return false;
            }
        }

        // Check per-user usage limits
        if (promoCode.MaxUsesPerUser.HasValue)
        {
            var userUsageCount = await promoCodeRepository.GetUserUsageCountAsync(
                promoCode.Id, userId).ConfigureAwait(false);
            if (userUsageCount >= promoCode.MaxUsesPerUser.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateDiscountAsync(string code, decimal originalAmount)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        var promoCode = await promoCodeRepository.GetByCodeAsync(code).ConfigureAwait(false);
        if (promoCode == null || !promoCode.IsCurrentlyValid())
        {
            return 0;
        }

        return promoCode.CalculateDiscount(originalAmount);
    }

    /// <inheritdoc />
    public async Task<PromoCodeUse> ApplyPromoCodeAsync(string code, Guid userId, Guid transactionId, decimal orderAmount = 0, string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Promo code is required", nameof(code));
        }

        var promoCode = await promoCodeRepository.GetByCodeAsync(code).ConfigureAwait(false);
        if (promoCode == null)
        {
            throw new InvalidOperationException($"Promo code '{PromoCodeNormalizer.Normalize(code)}' not found");
        }

        if (!promoCode.IsCurrentlyValid())
        {
            throw new InvalidOperationException($"Promo code '{promoCode.Code}' is not valid");
        }

        // Record the actual computed discount atomically; limit breaches fail closed.
        var (status, usage) = await usageRecorder.RecordAsync(
            promoCode,
            userId,
            promoCode.CalculateDiscount(orderAmount),
            promoCode.Currency,
            idempotencyKey ?? $"legacy:{transactionId}:user:{userId}:code:{promoCode.Code}",
            orderId: transactionId).ConfigureAwait(false);

        return status switch
        {
            PromoUsageRecordingStatus.Recorded or PromoUsageRecordingStatus.Replay
                => usage ?? throw new InvalidOperationException("Promo usage recording returned no usage row"),
            PromoUsageRecordingStatus.RejectedTotalLimit => throw new InvalidOperationException(
                $"Promo code '{promoCode.Code}' has reached its usage limit or is no longer active"),
            PromoUsageRecordingStatus.RejectedPerUserLimit => throw new InvalidOperationException(
                $"Promo code '{promoCode.Code}' has reached its per-user usage limit"),
            _ => throw new InvalidOperationException($"Unexpected promo usage recording status {status}")
        };
    }

    /// <inheritdoc />
    public async Task<bool> DeactivatePromoCodeAsync(Guid id)
    {
        var promoCode = await promoCodeRepository.GetByIdAsync(id).ConfigureAwait(false);
        if (promoCode == null)
        {
            return false;
        }

        promoCode.IsActive = false;
        promoCode.Touch();

        await promoCodeRepository.UpdateAsync(promoCode).ConfigureAwait(false);
        await promoCodeRepository.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PromoCode>> GetActivePromoCodesAsync()
    {
        return await promoCodeRepository.GetActiveCodesAsync().ConfigureAwait(false);
    }
}
