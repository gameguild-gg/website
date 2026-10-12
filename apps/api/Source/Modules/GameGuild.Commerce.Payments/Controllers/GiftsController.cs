using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Gift subscriptions API (issue #410 v1): purchase with a one-time full redemption
///     code, non-secret preview by code, redemption granting account credit, and the
///     caller's sent/received gift lists.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/gifts")]
[Microsoft.AspNetCore.Http.Tags("gifts")]
[Authorize]
public sealed class GiftsController(ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>Purchases a gift subscription for any plan and duration (1-36 months).</summary>
    [HttpPost]
    [EndpointSummary("Purchase a gift subscription")]
    [EndpointDescription(
        "Creates a purchased gift for the given recipient with a positive amount in an ISO 4217 currency, a duration of 1-36 months, an optional plan restriction, an optional personal message and an optional scheduled delivery date (redemption is blocked before it). The full redemption code is returned exactly once and only stored as a SHA-256 hash; unredeemed gifts expire one year after purchase.")]
    [ProducesResponseType<GiftPurchaseResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Purchase([FromBody] PurchaseGiftRequestBody request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HasSelfServiceContext()) return Forbid();

        var result = await sender.Send(
            new PurchaseGiftCommand(
                request.RecipientEmail,
                request.Amount,
                request.Currency,
                request.DurationMonths,
                request.PlanId,
                request.RecipientName,
                request.PersonalMessage,
                request.DeliveryDate),
            ct).ConfigureAwait(false);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ProblemFrom(result.Error);
    }

    /// <summary>Non-secret preview of a gift by redemption code (no giver identity exposed).</summary>
    [HttpGet("{code}")]
    [EndpointSummary("Preview a gift by redemption code")]
    [EndpointDescription(
        "Returns the public view of a gift (status, amount, currency, duration, plan, recipient name, personal message, delivery/expiry) for a redemption code. The giver identity is never exposed.")]
    [ProducesResponseType<GiftPublicView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByCode([FromRoute] string code, CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        var result = await sender.Send(new GetGiftByCodeQuery(code), ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);
    }

    /// <summary>Redeems a gift with its redemption code; grants account credit to the calling account.</summary>
    [HttpPost("{code}/redeem")]
    [EndpointSummary("Redeem a gift")]
    [EndpointDescription(
        "Redeems the gift for the authenticated user and grants an account credit of the gift value to their account (existing or newly created accounts alike — the credit lands on whichever account redeems). Blocked before the scheduled delivery date, after the one-year expiry, for already-redeemed gifts, and when the giver tries to redeem their own gift.")]
    [ProducesResponseType<GiftPublicView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Redeem([FromRoute] string code, CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        var result = await sender.Send(new RedeemGiftCommand(code), ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);
    }

    /// <summary>Gifts the calling user has sent, newest first.</summary>
    [HttpGet("sent")]
    [EndpointSummary("List gifts I have sent")]
    [EndpointDescription("Returns a paged list of gifts purchased by the calling user, newest first, with their status machine state.")]
    [ProducesResponseType<PagedResult<GiftPublicView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSent([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (!HasSelfServiceContext()) return Forbid();

        NormalizePaging(ref skip, ref take);
        return Ok(await sender.Send(new GetMySentGiftsQuery(skip, take), ct).ConfigureAwait(false));
    }

    /// <summary>Gifts the calling user has redeemed, newest first.</summary>
    [HttpGet("received")]
    [EndpointSummary("List gifts I have received")]
    [EndpointDescription("Returns a paged list of gifts redeemed by the calling user, newest first.")]
    [ProducesResponseType<PagedResult<GiftPublicView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReceived([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (!HasSelfServiceContext()) return Forbid();

        NormalizePaging(ref skip, ref take);
        return Ok(await sender.Send(new GetMyReceivedGiftsQuery(skip, take), ct).ConfigureAwait(false));
    }


    private IActionResult ProblemFrom(Error error)
    {
        var problem = ProblemDetailsMapper.ToProblemDetails(error);
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }

    private bool HasSelfServiceContext()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.SubjectIdAsGuid.HasValue && actor.TenantId.HasValue;
    }

    private static void NormalizePaging(ref int skip, ref int take)
    {
        if (skip < 0) skip = 0;
        if (take < 1) take = 20;
        if (take > 100) take = 100;
    }

    /// <summary>Request body for gift purchase.</summary>
    /// <param name="RecipientEmail">Recipient email address.</param>
    /// <param name="Amount">Positive monetary value.</param>
    /// <param name="Currency">ISO 4217 currency code.</param>
    /// <param name="DurationMonths">Gift duration 1-36 months.</param>
    /// <param name="PlanId">Optional plan restriction; null means any plan.</param>
    /// <param name="RecipientName">Optional recipient display name.</param>
    /// <param name="PersonalMessage">Optional personal message (max 500 characters).</param>
    /// <param name="DeliveryDate">Optional scheduled delivery date (UTC); redemption is blocked before it.</param>
    public sealed record PurchaseGiftRequestBody(
        string RecipientEmail,
        decimal Amount,
        string Currency,
        int DurationMonths,
        Guid? PlanId = null,
        string? RecipientName = null,
        string? PersonalMessage = null,
        DateTime? DeliveryDate = null);
}
