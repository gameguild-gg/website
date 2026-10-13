using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Referrals API (issue #410 v1): the caller's unique code and trackable link, click
///     and signup tracking, funnel stats and history. Rewards (account credit for both
///     parties) are issued server-side on the referee's first paid invoice only.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/referrals")]
[Microsoft.AspNetCore.Http.Tags("referrals")]
[Authorize]
public sealed class ReferralsController(ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>Gets the caller's referral code and trackable link, creating them on first use.</summary>
    [HttpGet("code")]
    [EndpointSummary("Get my referral code and link")]
    [EndpointDescription(
        "Returns the calling user's opaque unique referral code and the trackable share link built from it (AccountCredits:ReferralLinkBaseUrl base). Idempotent: every call returns the same per-user code.")]
    [ProducesResponseType<ReferralCodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyCode(CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        var result = await sender.Send(new GetMyReferralCodeQuery(), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(new ReferralCodeResponse(result.Value.Code, result.Value.Link))
            : ProblemFrom(result.Error);
    }

    /// <summary>Funnel stats of the caller's referrals.</summary>
    [HttpGet("stats")]
    [EndpointSummary("Get my referral stats")]
    [EndpointDescription(
        "Returns the calling user's referral funnel counters: shared rows, clicks, signups, conversions (first paid invoice), rewards issued, fraudulent blocks and expiries.")]
    [ProducesResponseType<ReferralStats>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyStats(CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        var result = await sender.Send(new GetMyReferralStatsQuery(), ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);
    }

    /// <summary>Tracking rows of the caller's referrals, newest first.</summary>
    [HttpGet("history")]
    [EndpointSummary("Get my referral history")]
    [EndpointDescription("Returns a paged list of the calling user's referral tracking rows, newest first, with funnel timestamps and status.")]
    [ProducesResponseType<PagedResult<Referral>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyHistory([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (!HasSelfServiceContext()) return Forbid();

        NormalizePaging(ref skip, ref take);
        return Ok(await sender.Send(new GetMyReferralsQuery(skip, take), ct).ConfigureAwait(false));
    }

    /// <summary>Records a referral link click for funnel tracking.</summary>
    [HttpPost("track-click")]
    [EndpointSummary("Track a referral link click")]
    [EndpointDescription(
        "Records a click against a referral code (first click time kept on idempotent replays). The caller IP is hashed (SHA-256) for the same-IP fraud block and never persisted in clear.")]
    [ProducesResponseType<Referral>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrackClick([FromBody] TrackReferralClickRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HasSelfServiceContext()) return Forbid();

        var result = await sender.Send(new TrackReferralClickCommand(request.ReferralCode, request.RefereeEmail), ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);
    }

    /// <summary>The signed-up caller applies a referral code to their account.</summary>
    [HttpPost("track-signup")]
    [EndpointSummary("Apply a referral code to my signup")]
    [EndpointDescription(
        "Attaches the calling user as the referee of the referral code. Fail-closed blocks: self-referral, referral cap (AccountCredits:MaxReferralsPerUser) and same-IP repeats under the same referrer are rejected or recorded as fraudulent without reward eligibility.")]
    [ProducesResponseType<ReferralSignupResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TrackSignup([FromBody] TrackReferralSignupRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HasSelfServiceContext()) return Forbid();

        var result = await sender.Send(new TrackReferralSignupCommand(request.ReferralCode), ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);
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

    /// <summary>Response body carrying the referral code and share link.</summary>
    /// <param name="Code">Opaque unique referral code of the caller.</param>
    /// <param name="Link">Trackable share link.</param>
    public sealed record ReferralCodeResponse(string Code, string Link);

    /// <summary>Request body for click tracking.</summary>
    /// <param name="ReferralCode">Referral code from the shared link.</param>
    /// <param name="RefereeEmail">Optional referee email captured at click time.</param>
    public sealed record TrackReferralClickRequest(string ReferralCode, string? RefereeEmail = null);

    /// <summary>Request body for signup tracking.</summary>
    /// <param name="ReferralCode">Referral code being applied to the caller's signup.</param>
    public sealed record TrackReferralSignupRequest(string ReferralCode);
}
