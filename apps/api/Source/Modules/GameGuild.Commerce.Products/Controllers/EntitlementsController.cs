using Asp.Versioning;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Controller for managing product entitlements
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/entitlements")]
[Microsoft.AspNetCore.Http.Tags("entitlements")]
[Authorize]
public class EntitlementsController(
    IEntitlementService entitlementService,
    IActorContextAccessor actorContextAccessor,
    ISender sender) : BaseApiController
{
    /// <summary>
    /// List entitlements with optional status filter
    /// </summary>
    /// <param name="status">Filter by status (e.g., 'expiring', 'active', 'expired')</param>
    /// <param name="days">Number of days for expiring filter (default: 7)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    [RequirePermission(EntitlementsPermission.Keys.ReadAll)]
    public async Task<ActionResult<IEnumerable<EntitlementInfoDto>>> ListEntitlements(
        [FromQuery] string? status = null,
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(status, "expiring", StringComparison.OrdinalIgnoreCase))
        {
            var expiringEntitlements = await entitlementService.GetExpiringEntitlementsAsync(
                days,
                cancellationToken).ConfigureAwait(false);
            return Ok(expiringEntitlements.Select(MapToDto));
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            var activeEntitlements = await entitlementService.GetAllActiveEntitlementsAsync(
                cancellationToken).ConfigureAwait(false);
            return Ok(activeEntitlements.Select(MapToDto));
        }

        if (string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase))
        {
            // No direct method for expired, return empty for now
            return Ok(Enumerable.Empty<EntitlementInfoDto>());
        }

        // No status filter — return all active by default
        var allActive = await entitlementService.GetAllActiveEntitlementsAsync(
            cancellationToken).ConfigureAwait(false);
        return Ok(allActive.Select(MapToDto));
    }

    /// <summary>
    /// Check if current user has access to a product
    /// </summary>
    /// <param name="productId">Product ID to check access for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet(":check")]
    [RequirePermission(EntitlementsPermission.Keys.ReadSelf)]
    public async Task<ActionResult<EntitlementCheckResult>> CheckAccess(
        [FromQuery] Guid productId,
        CancellationToken cancellationToken = default)
    {
        var hasAccess = await entitlementService.HasAccessAsync(
            GetUserId(),
            productId,
            cancellationToken).ConfigureAwait(false);

        return Ok(new EntitlementCheckResult(productId, hasAccess));
    }

    /// <summary>
    /// Check if current user has access to multiple products
    /// </summary>
    [HttpPost(":check-batch")]
    [RequirePermission(EntitlementsPermission.Keys.ReadSelf)]
    public async Task<ActionResult<IDictionary<Guid, bool>>> CheckMultipleAccess(
        [FromBody] CheckMultipleAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = await sender.Send(
            new CheckMultipleEntitlementsCommand(GetUserId(), request.ProductIds),
            cancellationToken).ConfigureAwait(false);

        return Ok(results);
    }

    /// <summary>
    /// Grant entitlement to a user (create)
    /// </summary>
    [HttpPost]
    [RequirePermission(EntitlementsPermission.Keys.Grant)]
    public async Task<ActionResult<EntitlementInfoDto>> GrantEntitlement(
        [FromBody] GrantEntitlementRequest request,
        CancellationToken cancellationToken = default)
    {
        var outcome = await sender.Send(new GrantEntitlementCommand(
            request.UserId,
            request.ProductId,
            request.AcquisitionType,
            request.PricePaid,
            request.Currency,
            request.ExpiresAt), cancellationToken).ConfigureAwait(false);

        if (!outcome.Result.Success)
        {
            return BadRequest(new { error = outcome.Result.ErrorMessage });
        }

        return Ok(outcome.Entitlement != null ? MapToDto(outcome.Entitlement) : null);
    }

    /// <summary>
    /// Revoke an entitlement (admin only)
    /// </summary>
    /// <param name="entitlementId">The entitlement ID to revoke</param>
    /// <param name="request">Revoke request with reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{entitlementId:guid}:revoke")]
    [RequirePermission(EntitlementsPermission.Keys.Revoke)]
    public async Task<IActionResult> RevokeEntitlement(
        Guid entitlementId,
        [FromBody] RevokeEntitlementRequest request,
        CancellationToken cancellationToken = default)
    {
        var success = await sender.Send(new RevokeEntitlementCommand(
            entitlementId,
            request.UserId,
            request.ProductId,
            request.Reason), cancellationToken).ConfigureAwait(false);

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    private Guid GetUserId()
    {
        return actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;
    }

    private static EntitlementInfoDto MapToDto(EntitlementInfo info) => new(
        info.ProductId,
        info.ProductName,
        info.Status.ToString(),
        info.AcquisitionType.ToString(),
        info.AccessStartDate,
        info.AccessEndDate,
        info.IsSubscription,
        info.SubscriptionStatus?.ToString(),
        info.PricePaid,
        info.Currency);
}

/// <summary>Result of an entitlement check</summary>
public sealed record EntitlementCheckResult(Guid ProductId, bool HasAccess);

/// <summary>Request to check multiple product access</summary>
public sealed record CheckMultipleAccessRequest(IEnumerable<Guid> ProductIds);

/// <summary>Request to grant an entitlement</summary>
public sealed record GrantEntitlementRequest(
    Guid UserId,
    Guid ProductId,
    ProductAcquisitionType AcquisitionType,
    decimal PricePaid = 0,
    string Currency = "USD",
    DateTime? ExpiresAt = null);

/// <summary>Request to revoke an entitlement</summary>
public sealed record RevokeEntitlementRequest(
    Guid UserId,
    Guid ProductId,
    string? Reason = null);

/// <summary>Entitlement info DTO</summary>
public sealed record EntitlementInfoDto(
    Guid ProductId,
    string ProductName,
    string Status,
    string AcquisitionType,
    DateTime? AccessStartDate,
    DateTime? AccessEndDate,
    bool IsSubscription,
    string? SubscriptionStatus,
    decimal PricePaid,
    string Currency);
