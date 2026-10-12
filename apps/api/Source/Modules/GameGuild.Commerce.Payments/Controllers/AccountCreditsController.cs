using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Account credits API (issue #410 v1): actor-scoped balance and history plus
///     SystemAdmin-gated manual issuance, revocation and growth metrics.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/account-credits")]
[Microsoft.AspNetCore.Http.Tags("account-credits")]
[Authorize]
public sealed class AccountCreditsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>Per-currency spendable credit balances of the calling user.</summary>
    [HttpGet("balance")]
    [EndpointSummary("Get my account credit balances")]
    [EndpointDescription(
        "Returns the spendable account credit balance of the calling user per ISO 4217 currency (Active grants, expiry-aware). User and tenant are derived from the authenticated actor.")]
    [ProducesResponseType<IReadOnlyList<AccountCreditBalance>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyBalance(CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        return Ok(await sender.Send(new GetMyCreditBalancesQuery(), ct).ConfigureAwait(false));
    }

    /// <summary>Paged credit ledger history of the calling user (grants, consumption, revocation, expiry).</summary>
    [HttpGet("history")]
    [EndpointSummary("Get my account credit history")]
    [EndpointDescription(
        "Returns a paged list of the calling user's account credit ledger rows, newest first. User and tenant are derived from the authenticated actor.")]
    [ProducesResponseType<PagedResult<AccountCreditTransaction>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyHistory([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        if (!HasSelfServiceContext()) return Forbid();

        NormalizePaging(ref skip, ref take);
        return Ok(await sender.Send(new GetMyCreditHistoryQuery(skip, take), ct).ConfigureAwait(false));
    }

    /// <summary>Growth metrics: credit issuance/usage/expiry rates, referral funnel and top referrers. SystemAdmin only.</summary>
    [HttpGet("metrics")]
    [EndpointSummary("Get account credit and referral growth metrics")]
    [EndpointDescription(
        "Credit ledger aggregates per currency (issued, applied, expired-unused waste, revoked, active balance, usage and expiration-waste rates), the referral conversion funnel (clicks → signups → conversions → rewards) and the top-referrers leaderboard. Requires the SystemAdmin role.")]
    [ProducesResponseType<AccountCreditMetricsReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("read account credit metrics");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        return Ok(await sender.Send(new GetAccountCreditMetricsQuery(), ct).ConfigureAwait(false));
    }

    /// <summary>Manually issues account credit to a user of the calling tenant. SystemAdmin only.</summary>
    [HttpPost("grants")]
    [EndpointSummary("Issue account credit to a user")]
    [EndpointDescription(
        "Manually grants account credit to a tenant user with an optional expiry. The credit type restricts what the credit may be applied to. Requires the SystemAdmin role; the tenant is derived from the authenticated actor.")]
    [ProducesResponseType<AccountCreditGrant>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> IssueGrant([FromBody] IssueCreditRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var adminGuard = RequireSystemAdmin("issue account credit");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        var result = await sender.Send(
            new IssueAccountCreditCommand(
                request.UserId,
                request.Amount,
                request.Currency,
                request.CreditType,
                request.Description,
                request.ExpiresAt,
                request.IdempotencyKey),
            ct).ConfigureAwait(false);

        return result.IsSuccess
            ? Created(string.Empty, result.Value)
            : ProblemFrom(result.Error);
    }

    /// <summary>Revokes the remaining balance of a credit grant with an auditable reason. SystemAdmin only.</summary>
    [HttpPost("{grantId:guid}:revoke")]
    [EndpointSummary("Revoke an account credit grant")]
    [EndpointDescription(
        "Revokes the remaining balance of an account credit grant; the reason is captured on the grant and in the credit ledger. Requires the SystemAdmin role.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeGrant(Guid grantId, [FromBody] RevokeCreditRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var adminGuard = RequireSystemAdmin("revoke account credit");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        var result = await sender.Send(new RevokeAccountCreditCommand(grantId, request.Reason), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ProblemFrom(result.Error);
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

    private IActionResult? RequireSystemAdmin(string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"Only system administrators may {operation}."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

    private static void NormalizePaging(ref int skip, ref int take)
    {
        if (skip < 0) skip = 0;
        if (take < 1) take = 20;
        if (take > 100) take = 100;
    }

    /// <summary>Request body for manual credit issuance.</summary>
    /// <param name="UserId">Tenant user receiving the credit.</param>
    /// <param name="Amount">Positive amount to grant.</param>
    /// <param name="Currency">ISO 4217 currency code.</param>
    /// <param name="CreditType">What the credit may be applied to (General, Subscription, Addon).</param>
    /// <param name="Description">Reason/description captured on the ledger.</param>
    /// <param name="ExpiresAt">Optional expiry (UTC).</param>
    /// <param name="IdempotencyKey">Optional idempotency key unique per tenant.</param>
    public sealed record IssueCreditRequest(
        Guid UserId,
        decimal Amount,
        string Currency,
        AccountCreditType CreditType,
        string Description,
        DateTime? ExpiresAt = null,
        string? IdempotencyKey = null);

    /// <summary>Request body for credit revocation.</summary>
    /// <param name="Reason">Auditable revocation reason.</param>
    public sealed record RevokeCreditRequest(string Reason);
}
