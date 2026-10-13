using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Audit logging for product-category and inventory mutations (issue #401), published
///     to the central Compliance.Audit pipeline. Publishing is best-effort from the request
///     path: an audit failure is logged but never propagates to the caller. Mirrors
///     <see cref="PromoCodeAuditLogger"/>.
/// </summary>
public interface IProductCatalogAuditLogger
{
    /// <summary>Log a catalog mutation (category create/update/delete, inventory configure).</summary>
    Task LogMutationAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        Guid entityId,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Best-effort audit logger delegating to <see cref="IAuditService"/>.
/// </summary>
public class ProductCatalogAuditLogger(IAuditService auditService, ILogger<ProductCatalogAuditLogger> logger)
    : IProductCatalogAuditLogger
{
    public async Task LogMutationAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        Guid entityId,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await auditService.LogAdminActionAsync(
                actorUserId ?? Guid.Empty,
                $"ProductCatalog.{action}",
                $"Product {entityType.ToLowerInvariant()} '{entityId}' {action.ToLowerInvariant()}",
                new Dictionary<string, object?>
                {
                    ["entityType"] = entityType,
                    ["entityId"] = entityId,
                    ["metadata"] = metadata
                })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish product catalog audit event {Action}", action);
        }
    }
}
