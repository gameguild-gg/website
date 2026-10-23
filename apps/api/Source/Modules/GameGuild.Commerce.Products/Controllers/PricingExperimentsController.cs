using Asp.Versioning;
using GameGuild.Commerce;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Pricing A/B experiment endpoints (issue #395) under the pricing-engine route.
///     Reads and per-key arm assignment require <c>products:read</c> (a customer resolving
///     their own arm is the same surface as the calculate endpoint); creating experiments,
///     lifecycle transitions, conversion recording and scheduling sweeps additionally require
///     <c>products:pricing:manage</c> plus the <c>monetization:monetize</c> permission on the
///     CQRS pipeline.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/pricing-engine/experiments")]
[Microsoft.AspNetCore.Http.Tags("billing-pricing-engine")]
[Authorize]
[RequirePermission(ProductsPermission.Keys.Read)]
public class PricingExperimentsController(IMediator mediator, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    /// List pricing experiments (paginated) with optional filters.
    /// </summary>
    /// <param name="status">Filter by lifecycle status</param>
    /// <param name="productId">Filter by product (includes global experiments)</param>
    /// <param name="skip">Items to skip</param>
    /// <param name="take">Items to take</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PricingExperimentDto>>> GetExperiments(
        [FromQuery] PricingExperimentStatus? status = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPricingExperimentsQuery(status, productId, skip, take);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Get one pricing experiment with its arms.
    /// </summary>
    /// <param name="experimentId">The experiment id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{experimentId:guid}")]
    public async Task<ActionResult<PricingExperimentDto>> GetExperimentById(
        Guid experimentId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPricingExperimentByIdQuery(experimentId);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Revenue-impact analytics for one experiment over an optional window: per-variant
    /// assignments, conversions, revenue, average order value and lift versus the control.
    /// </summary>
    /// <param name="experimentId">The experiment id</param>
    /// <param name="fromUtc">Inclusive window start (ISO-8601)</param>
    /// <param name="toUtc">Inclusive window end (ISO-8601)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{experimentId:guid}:analytics")]
    public async Task<ActionResult<PricingExperimentAnalytics>> GetExperimentAnalytics(
        Guid experimentId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPricingExperimentAnalyticsQuery(experimentId, fromUtc, toUtc);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Resolve (or create) the sticky assignment of a bucket key, returning the arm the key
    /// belongs to (null arm = held out by the traffic gate).
    /// </summary>
    /// <param name="experimentId">The experiment id</param>
    /// <param name="request">Assignment request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{experimentId:guid}:assign")]
    public async Task<ActionResult<PricingExperimentAssignmentResult>> AssignBucketKey(
        Guid experimentId,
        [FromBody] AssignExperimentRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new AssignPricingExperimentCommand(experimentId, request.BucketKey, GetUserId());
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Create a pricing experiment with its arms (starts in Draft).
    /// </summary>
    /// <param name="request">Creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingExperimentDto>> CreateExperiment(
        [FromBody] CreatePricingExperimentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _)) return Forbid();

        var command = new CreatePricingExperimentCommand(
            request.ProductId,
            request.Name,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.TrafficPercentage,
            request.Variants);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetExperimentById), new { experimentId = result.Id }, result);
    }

    /// <summary>
    /// Start a Draft experiment (begins enrolling bucket keys).
    /// </summary>
    /// <param name="experimentId">The experiment id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{experimentId:guid}:start")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingExperimentDto>> StartExperiment(
        Guid experimentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _)) return Forbid();

        var result = await mediator.Send(
            new TransitionPricingExperimentCommand(experimentId, PricingExperimentTransition.Start),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Pause a Running experiment (assignments stay sticky).
    /// </summary>
    /// <param name="experimentId">The experiment id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{experimentId:guid}:pause")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingExperimentDto>> PauseExperiment(
        Guid experimentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _)) return Forbid();

        var result = await mediator.Send(
            new TransitionPricingExperimentCommand(experimentId, PricingExperimentTransition.Pause),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Complete an experiment (retains assignments and outcomes for analysis).
    /// </summary>
    /// <param name="experimentId">The experiment id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{experimentId:guid}:complete")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingExperimentDto>> CompleteExperiment(
        Guid experimentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _)) return Forbid();

        var result = await mediator.Send(
            new TransitionPricingExperimentCommand(experimentId, PricingExperimentTransition.Complete),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Record a monetized conversion outcome for a bucket key under its assigned arm.
    /// </summary>
    /// <param name="experimentId">The experiment id</param>
    /// <param name="request">Outcome request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{experimentId:guid}:convert")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<IActionResult> RecordConversion(
        Guid experimentId,
        [FromBody] RecordExperimentOutcomeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _)) return Forbid();

        await mediator.Send(
            new RecordPricingExperimentOutcomeCommand(experimentId, request.BucketKey, request.Amount, request.Currency, request.OrderId),
            cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private bool TryActor(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.TenantId.HasValue && actor.SubjectIdAsGuid.HasValue;
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

/// <summary>Request to resolve the arm of a bucket key.</summary>
/// <param name="BucketKey">Stable bucketing key (e.g. user id or anonymous session id)</param>
public sealed record AssignExperimentRequest(string BucketKey);

/// <summary>Request to record a conversion outcome under an experiment arm.</summary>
/// <param name="BucketKey">Bucket key that converted</param>
/// <param name="Amount">Final monetized amount</param>
/// <param name="Currency">ISO 4217 currency (default USD)</param>
/// <param name="OrderId">Optional order correlation</param>
public sealed record RecordExperimentOutcomeRequest(
    string BucketKey,
    decimal Amount,
    string Currency = "USD",
    Guid? OrderId = null);
