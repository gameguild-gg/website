using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Gift subscription implementation (issue #410 v1). Redemption codes are 32-char
///     URL-safe secrets stored as SHA-256 hashes; redemption grants an account credit of
///     the gift value to the redeeming account in a single SaveChanges batch (gift state
///     transition + credit grant + ledger row), so redemption is atomic.
/// </summary>
public sealed class GiftService(IApplicationDbContext context, IAccountCreditService creditService) : IGiftService
{
    /// <inheritdoc />
    public async Task<Result<GiftPurchaseResult>> PurchaseAsync(PurchaseGiftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currency = NormalizeCurrency(request.Currency);
        if (currency is null)
        {
            return Result.Failure<GiftPurchaseResult>(
                Error.Validation("Gifts.InvalidCurrency", "Currency must be a known ISO 4217 code."));
        }

        GiftSubscription gift;
        var code = GiftRedemptionCodeGenerator.GenerateCode();
        try
        {
            gift = new GiftSubscription(
                request.TenantId,
                request.GiverUserId,
                request.RecipientEmail.Trim(),
                request.Amount,
                currency,
                request.DurationMonths,
                GiftRedemptionCodeGenerator.HashCode(code),
                GiftRedemptionCodeGenerator.PrefixOf(code),
                request.PlanId,
                request.RecipientName?.Trim(),
                request.PersonalMessage?.Trim(),
                request.DeliveryDate,
                request.PaymentId);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<GiftPurchaseResult>(Error.Validation("Gifts.InvalidPurchase", exception.Message));
        }

        context.Set<GiftSubscription>().Add(gift);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new GiftPurchaseResult(ToPublicView(gift, giverDisplayName: null), code));
    }

    /// <inheritdoc />
    public async Task<Result<GiftPublicView>> GetByCodeAsync(Guid tenantId, string redemptionCode, CancellationToken cancellationToken = default)
    {
        var gift = await FindByCodeAsync(tenantId, redemptionCode, cancellationToken).ConfigureAwait(false);
        return gift is null
            ? Result.Failure<GiftPublicView>(Error.NotFound("Gifts.NotFound", "Gift was not found for this redemption code."))
            : Result.Success(ToPublicView(gift, giverDisplayName: null));
    }

    /// <inheritdoc />
    public async Task<Result<GiftPublicView>> RedeemAsync(Guid tenantId, string redemptionCode, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var gift = await FindByCodeAsync(tenantId, redemptionCode, cancellationToken).ConfigureAwait(false);
        if (gift is null)
        {
            return Result.Failure<GiftPublicView>(Error.NotFound("Gifts.NotFound", "Gift was not found for this redemption code."));
        }

        var now = SystemClock.UtcNow;
        if (now >= gift.ExpiresAt)
        {
            gift.MarkExpired(now);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<GiftPublicView>(Error.Conflict("Gifts.Expired", "Gift redemption window has expired."));
        }

        if (gift.Status == GiftStatus.Redeemed)
        {
            return Result.Failure<GiftPublicView>(Error.Conflict("Gifts.AlreadyRedeemed", "Gift has already been redeemed."));
        }

        try
        {
            gift.Redeem(recipientUserId, now);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<GiftPublicView>(Error.Conflict("Gifts.NotRedeemable", exception.Message));
        }

        var grantResult = await creditService.GrantAsync(
            new AccountCreditGrantRequest(
                tenantId,
                recipientUserId,
                gift.Amount,
                gift.Currency,
                AccountCreditSource.Gift,
                AccountCreditType.General,
                $"Gift subscription redemption ({gift.DurationMonths} month(s))",
                gift.Id,
                IdempotencyKey: $"gift:{gift.Id}:redemption"),
            cancellationToken).ConfigureAwait(false);

        if (grantResult.IsFailure)
        {
            return Result.Failure<GiftPublicView>(grantResult.Error);
        }

        gift.AttachCreditGrant(grantResult.Value.Id);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ToPublicView(gift, giverDisplayName: null));
    }

    /// <inheritdoc />
    public async Task<PagedResult<GiftPublicView>> GetSentAsync(Guid tenantId, Guid giverUserId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Set<GiftSubscription>()
            .AsNoTracking()
            .Where(gift => gift.TenantId == tenantId && gift.GiverUserId == giverUserId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var gifts = await query
            .OrderByDescending(gift => gift.PurchasedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<GiftPublicView>(gifts.Select(gift => ToPublicView(gift, giverDisplayName: null)), total, skip, take);
    }

    /// <inheritdoc />
    public async Task<PagedResult<GiftPublicView>> GetReceivedAsync(Guid tenantId, Guid recipientUserId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Set<GiftSubscription>()
            .AsNoTracking()
            .Where(gift => gift.TenantId == tenantId && gift.RecipientUserId == recipientUserId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var gifts = await query
            .OrderByDescending(gift => gift.RedeemedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<GiftPublicView>(gifts.Select(gift => ToPublicView(gift, giverDisplayName: null)), total, skip, take);
    }

    /// <inheritdoc />
    public async Task<int> ProgressDueAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(batchSize, 1, 1000);
        var progressed = 0;

        var dueDeliveries = await context.Set<GiftSubscription>()
            .Where(gift => gift.Status == GiftStatus.Purchased
                           && gift.DeliveryDate != null
                           && gift.DeliveryDate <= nowUtc)
            .OrderBy(gift => gift.DeliveryDate)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var gift in dueDeliveries)
        {
            gift.MarkDelivered(nowUtc);
            progressed++;
        }

        var expired = await context.Set<GiftSubscription>()
            .Where(gift => (gift.Status == GiftStatus.Purchased || gift.Status == GiftStatus.Delivered)
                           && gift.ExpiresAt <= nowUtc)
            .OrderBy(gift => gift.ExpiresAt)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var gift in expired)
        {
            gift.MarkExpired(nowUtc);
            progressed++;
        }

        if (progressed > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return progressed;
    }

    private async Task<GiftSubscription?> FindByCodeAsync(Guid tenantId, string redemptionCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(redemptionCode))
        {
            return null;
        }

        var code = redemptionCode.Trim();
        var hash = GiftRedemptionCodeGenerator.HashCode(code);
        return await context.Set<GiftSubscription>()
            .FirstOrDefaultAsync(gift => gift.TenantId == tenantId && gift.RedemptionCodeHash == hash, cancellationToken)
            .ConfigureAwait(false);
    }

    private static GiftPublicView ToPublicView(GiftSubscription gift, string? giverDisplayName)
        => new(
            gift.Id,
            gift.Status,
            gift.Amount,
            gift.Currency,
            gift.DurationMonths,
            gift.PlanId,
            giverDisplayName ?? "A GameGuild member",
            gift.RecipientName,
            gift.PersonalMessage,
            gift.DeliveryDate,
            gift.ExpiresAt,
            gift.RedemptionCodePrefix);

    private static string? NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return null;
        }

        var upper = currency.ToUpperInvariant();
        return Iso4217CurrencyCatalog.Exists(upper) ? upper : null;
    }
}
