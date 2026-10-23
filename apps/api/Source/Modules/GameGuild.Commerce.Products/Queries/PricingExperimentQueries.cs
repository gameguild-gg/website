using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to list pricing A/B experiments (issue #395) with optional filters.
/// </summary>
/// <param name="Status">Optional status filter</param>
/// <param name="ProductId">Optional product filter (includes global experiments)</param>
/// <param name="Skip">Items to skip</param>
/// <param name="Take">Items to take</param>
public sealed record GetPricingExperimentsQuery(
    PricingExperimentStatus? Status = null,
    Guid? ProductId = null,
    int Skip = 0,
    int Take = 50) : IQuery<PagedResult<PricingExperimentDto>>;

/// <summary>
/// Handler for listing pricing experiments.
/// </summary>
public sealed class GetPricingExperimentsQueryHandler(IPricingExperimentRepository repository)
    : IQueryHandler<GetPricingExperimentsQuery, PagedResult<PricingExperimentDto>>
{
    public async Task<PagedResult<PricingExperimentDto>> Handle(GetPricingExperimentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (items, totalCount) = await repository
            .GetPagedAsync(request.Status, request.ProductId, request.Skip, request.Take, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<PricingExperimentDto>(
            items.Select(e => e.ToDto()).ToList(),
            totalCount,
            Math.Max(0, request.Skip),
            Math.Clamp(request.Take, 1, 200));
    }
}

/// <summary>
/// Query to get one pricing experiment with its arms.
/// </summary>
/// <param name="ExperimentId">Experiment id</param>
public sealed record GetPricingExperimentByIdQuery(Guid ExperimentId) : IQuery<PricingExperimentDto?>;

/// <summary>
/// Handler for getting one pricing experiment.
/// </summary>
public sealed class GetPricingExperimentByIdQueryHandler(IPricingExperimentRepository repository)
    : IQueryHandler<GetPricingExperimentByIdQuery, PricingExperimentDto?>
{
    public async Task<PricingExperimentDto?> Handle(GetPricingExperimentByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var experiment = await repository
            .GetByIdAsync(request.ExperimentId, cancellationToken)
            .ConfigureAwait(false);

        return experiment?.ToDto();
    }
}

/// <summary>
/// Query for revenue-impact analytics of one experiment over an optional window (issue #395).
/// </summary>
/// <param name="ExperimentId">Experiment id</param>
/// <param name="FromUtc">Inclusive window start (null = unbounded)</param>
/// <param name="ToUtc">Inclusive window end (null = unbounded)</param>
public sealed record GetPricingExperimentAnalyticsQuery(
    Guid ExperimentId,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IQuery<PricingExperimentAnalytics>;

/// <summary>
/// Validator for <see cref="GetPricingExperimentAnalyticsQuery"/>.
/// </summary>
public sealed class GetPricingExperimentAnalyticsQueryValidator : AbstractValidator<GetPricingExperimentAnalyticsQuery>
{
    /// <summary>
    /// Initializes the validator.
    /// </summary>
    public GetPricingExperimentAnalyticsQueryValidator()
    {
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

/// <summary>
/// Handler for experiment analytics.
/// </summary>
public sealed class GetPricingExperimentAnalyticsQueryHandler(IPricingExperimentService experimentService)
    : IQueryHandler<GetPricingExperimentAnalyticsQuery, PricingExperimentAnalytics>
{
    public async Task<PricingExperimentAnalytics> Handle(GetPricingExperimentAnalyticsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await experimentService
            .GetAnalyticsAsync(request.ExperimentId, request.FromUtc, request.ToUtc, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Command to resolve (or create) the sticky assignment of a bucket key in an experiment
/// (issue #395). Same permission surface as the calculate endpoint: a customer resolving
/// their own arm must not need monetization rights.
/// </summary>
/// <param name="ExperimentId">Experiment id</param>
/// <param name="BucketKey">Stable bucketing key</param>
/// <param name="UserId">Optional user correlation</param>
public sealed record AssignPricingExperimentCommand(
    Guid ExperimentId,
    string BucketKey,
    Guid? UserId = null) : ICommand<PricingExperimentAssignmentResult>;

/// <summary>
/// Handler for bucket-key assignment.
/// </summary>
public sealed class AssignPricingExperimentCommandHandler(IPricingExperimentService experimentService)
    : ICommandHandler<AssignPricingExperimentCommand, PricingExperimentAssignmentResult>
{
    public async Task<PricingExperimentAssignmentResult> Handle(AssignPricingExperimentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await experimentService
            .AssignAsync(request.ExperimentId, request.BucketKey, request.UserId, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Validator for <see cref="AssignPricingExperimentCommand"/>.
/// </summary>
public sealed class AssignPricingExperimentCommandValidator : AbstractValidator<AssignPricingExperimentCommand>
{
    /// <summary>
    /// Initializes the validator.
    /// </summary>
    public AssignPricingExperimentCommandValidator()
    {
        RuleFor(x => x.ExperimentId)
            .NotEmpty()
            .WithMessage("Experiment ID is required.");

        RuleFor(x => x.BucketKey)
            .NotEmpty()
            .WithMessage("Bucket key is required.")
            .MaximumLength(200)
            .WithMessage("Bucket key cannot exceed 200 characters.");
    }
}
