using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Audit logging for promo code operations (mutations and redemptions), published to the
/// central Compliance.Audit pipeline. Publishing is best-effort from the request path:
/// an audit failure is logged but never propagates to the caller.
/// </summary>
public interface IPromoCodeAuditLogger
{
    /// <summary>Log a promo code mutation (create/update/activate/deactivate/delete/bulk-generate).</summary>
    Task LogMutationAsync(
        Guid? actorUserId,
        string action,
        Guid promoCodeId,
        string code,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>Log a promo code redemption.</summary>
    Task LogRedemptionAsync(
        Guid userId,
        string code,
        decimal discountApplied,
        string currency,
        Guid? usageId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Best-effort audit logger delegating to <see cref="IAuditService"/>.
/// </summary>
public class PromoCodeAuditLogger(IAuditService auditService, ILogger<PromoCodeAuditLogger> logger)
    : IPromoCodeAuditLogger
{
    private const string MutationActionPrefix = "PromoCode.";

    /// <inheritdoc />
    public Task LogMutationAsync(
        Guid? actorUserId,
        string action,
        Guid promoCodeId,
        string code,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return LogSafeAsync(
            actorUserId ?? Guid.Empty,
            MutationActionPrefix + action,
            $"Promo code '{code}' {action.ToLowerInvariant()}",
            new Dictionary<string, object?>
            {
                ["promoCodeId"] = promoCodeId,
                ["code"] = code,
                ["metadata"] = metadata
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task LogRedemptionAsync(
        Guid userId,
        string code,
        decimal discountApplied,
        string currency,
        Guid? usageId = null,
        CancellationToken cancellationToken = default)
    {
        return LogSafeAsync(
            userId,
            MutationActionPrefix + "Redeemed",
            $"Promo code '{code}' redeemed",
            new Dictionary<string, object?>
            {
                ["code"] = code,
                ["discountApplied"] = discountApplied,
                ["currency"] = currency,
                ["usageId"] = usageId
            },
            cancellationToken);
    }

    private async Task LogSafeAsync(Guid userId, string actionType, string description, object metadata, CancellationToken cancellationToken)
    {
        try
        {
            await auditService.LogAdminActionAsync(userId, actionType, description, metadata).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish promo code audit event {ActionType}", actionType);
        }
    }
}
