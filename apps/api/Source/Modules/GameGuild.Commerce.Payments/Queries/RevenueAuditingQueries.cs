using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments.Queries.RevenueAuditing;

/// <summary>Query (issue #404): list reconciliation runs, newest first.</summary>
/// <param name="TenantId">Optional tenant scope.</param>
/// <param name="Skip">Items to skip.</param>
/// <param name="Take">Items to take (1-100).</param>
public sealed record GetRevenueReconciliationRunsQuery(Guid? TenantId = null, int Skip = 0, int Take = 20)
    : IQuery<PagedResult<RevenueReconciliationRun>>;

/// <summary>Validator for <see cref="GetRevenueReconciliationRunsQuery" />.</summary>
public sealed class GetRevenueReconciliationRunsQueryValidator : AbstractValidator<GetRevenueReconciliationRunsQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetRevenueReconciliationRunsQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
    }
}

/// <summary>Handler for <see cref="GetRevenueReconciliationRunsQuery" />.</summary>
public sealed class GetRevenueReconciliationRunsQueryHandler(
    IRevenueReconciliationRepository reconciliationRepository,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetRevenueReconciliationRunsQuery, PagedResult<RevenueReconciliationRun>>
{
    /// <inheritdoc />
    public Task<PagedResult<RevenueReconciliationRun>> Handle(GetRevenueReconciliationRunsQuery request, CancellationToken cancellationToken)
    {
        Guid? tenantId = request.TenantId;
        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated && !actorContext.IsSystemAdmin)
        {
            tenantId = actorContext.TenantId;
        }

        return reconciliationRepository.GetRunsAsync(tenantId, request.Skip, request.Take, cancellationToken);
    }
}

/// <summary>Query (issue #404): get one reconciliation run by id.</summary>
/// <param name="RunId">Run identifier.</param>
public sealed record GetRevenueReconciliationRunByIdQuery(Guid RunId) : IQuery<RevenueReconciliationRun>;

/// <summary>Validator for <see cref="GetRevenueReconciliationRunByIdQuery" />.</summary>
public sealed class GetRevenueReconciliationRunByIdQueryValidator : AbstractValidator<GetRevenueReconciliationRunByIdQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetRevenueReconciliationRunByIdQueryValidator()
    {
        RuleFor(query => query.RunId).NotEmpty();
    }
}

/// <summary>Handler for <see cref="GetRevenueReconciliationRunByIdQuery" />.</summary>
public sealed class GetRevenueReconciliationRunByIdQueryHandler(
    IRevenueReconciliationRepository reconciliationRepository,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetRevenueReconciliationRunByIdQuery, RevenueReconciliationRun>
{
    /// <inheritdoc />
    public async Task<RevenueReconciliationRun> Handle(GetRevenueReconciliationRunByIdQuery request, CancellationToken cancellationToken)
    {
        var run = await reconciliationRepository.GetRunByIdAsync(request.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Revenue reconciliation run '{request.RunId}' was not found.");

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated
            && !actorContext.IsSystemAdmin
            && run.TenantId is { } runTenantId
            && runTenantId != actorContext.TenantId)
        {
            throw new UnauthorizedAccessException("A tenant-scoped actor can only inspect reconciliation runs of their own tenant.");
        }

        return run;
    }
}

/// <summary>Query (issue #404): list discrepancies of a reconciliation run.</summary>
/// <param name="RunId">Run identifier.</param>
/// <param name="Kind">Optional discrepancy kind filter.</param>
/// <param name="Skip">Items to skip.</param>
/// <param name="Take">Items to take (1-100).</param>
public sealed record GetRevenueReconciliationDiscrepanciesQuery(
    Guid RunId,
    RevenueDiscrepancyKind? Kind = null,
    int Skip = 0,
    int Take = 50) : IQuery<PagedResult<RevenueReconciliationDiscrepancy>>;

/// <summary>Validator for <see cref="GetRevenueReconciliationDiscrepanciesQuery" />.</summary>
public sealed class GetRevenueReconciliationDiscrepanciesQueryValidator : AbstractValidator<GetRevenueReconciliationDiscrepanciesQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetRevenueReconciliationDiscrepanciesQueryValidator()
    {
        RuleFor(query => query.RunId).NotEmpty();
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
    }
}

/// <summary>Handler for <see cref="GetRevenueReconciliationDiscrepanciesQuery" />.</summary>
public sealed class GetRevenueReconciliationDiscrepanciesQueryHandler(
    IRevenueReconciliationRepository reconciliationRepository,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetRevenueReconciliationDiscrepanciesQuery, PagedResult<RevenueReconciliationDiscrepancy>>
{
    /// <inheritdoc />
    public async Task<PagedResult<RevenueReconciliationDiscrepancy>> Handle(GetRevenueReconciliationDiscrepanciesQuery request, CancellationToken cancellationToken)
    {
        var run = await reconciliationRepository.GetRunByIdAsync(request.RunId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Revenue reconciliation run '{request.RunId}' was not found.");

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated
            && !actorContext.IsSystemAdmin
            && run.TenantId is { } runTenantId
            && runTenantId != actorContext.TenantId)
        {
            throw new UnauthorizedAccessException("A tenant-scoped actor can only inspect reconciliation runs of their own tenant.");
        }

        return await reconciliationRepository.GetDiscrepanciesAsync(request.RunId, request.Kind, request.Skip, request.Take, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Query (issue #404): list revenue anomaly alerts, newest detection first.</summary>
/// <param name="TenantId">Optional tenant scope.</param>
/// <param name="Status">Optional status filter.</param>
/// <param name="Skip">Items to skip.</param>
/// <param name="Take">Items to take (1-100).</param>
public sealed record GetRevenueAnomalyAlertsQuery(
    Guid? TenantId = null,
    RevenueAnomalyStatus? Status = null,
    int Skip = 0,
    int Take = 20) : IQuery<PagedResult<RevenueAnomalyAlert>>;

/// <summary>Validator for <see cref="GetRevenueAnomalyAlertsQuery" />.</summary>
public sealed class GetRevenueAnomalyAlertsQueryValidator : AbstractValidator<GetRevenueAnomalyAlertsQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetRevenueAnomalyAlertsQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
    }
}

/// <summary>Handler for <see cref="GetRevenueAnomalyAlertsQuery" />.</summary>
public sealed class GetRevenueAnomalyAlertsQueryHandler(
    IRevenueAnomalyAlertRepository alertRepository,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetRevenueAnomalyAlertsQuery, PagedResult<RevenueAnomalyAlert>>
{
    /// <inheritdoc />
    public Task<PagedResult<RevenueAnomalyAlert>> Handle(GetRevenueAnomalyAlertsQuery request, CancellationToken cancellationToken)
    {
        Guid? tenantId = request.TenantId;
        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated && !actorContext.IsSystemAdmin)
        {
            tenantId = actorContext.TenantId;
        }

        return alertRepository.GetAlertsAsync(tenantId, request.Status, request.Skip, request.Take, cancellationToken);
    }
}
