using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments.Commands.RunRevenueAnomalyDetection;

/// <summary>
///     Command (issue #404): run anomaly detection for the trailing days and persist
///     idempotent alerts. Returns the number of new alerts created.
/// </summary>
/// <param name="TenantId">Optional tenant scope; null evaluates the global aggregate.</param>
/// <param name="EvaluationDateUtc">Reference "today"; defaults to the current UTC date.</param>
public sealed record RunRevenueAnomalyDetectionCommand(
    Guid? TenantId,
    DateTime? EvaluationDateUtc = null) : ICommand<int>;

/// <summary>Validator for <see cref="RunRevenueAnomalyDetectionCommand" />.</summary>
public sealed class RunRevenueAnomalyDetectionCommandValidator : AbstractValidator<RunRevenueAnomalyDetectionCommand>
{
    /// <summary>Initializes the validator.</summary>
    public RunRevenueAnomalyDetectionCommandValidator()
    {
        RuleFor(command => command.EvaluationDateUtc)
            .Must(date => date is null || date.Value.Kind == DateTimeKind.Utc)
            .WithMessage("Evaluation date must be a UTC instant.");
    }
}

/// <summary>
///     Handler for <see cref="RunRevenueAnomalyDetectionCommand" />: non-admin actors are
///     limited to their own tenant.
/// </summary>
public sealed class RunRevenueAnomalyDetectionCommandHandler(
    IRevenueAnomalyService anomalyService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<RunRevenueAnomalyDetectionCommand, int>
{
    /// <inheritdoc />
    public async Task<int> Handle(RunRevenueAnomalyDetectionCommand request, CancellationToken cancellationToken)
    {
        var actorContext = actorContextAccessor.ActorContext;
        Guid? tenantId = request.TenantId;
        if (!actorContext.IsSystemAdmin)
        {
            tenantId = actorContext.TenantId
                ?? throw new UnauthorizedAccessException("A tenant-scoped actor can only detect anomalies for its own tenant.");
        }

        return await anomalyService
            .DetectAndPersistAsync(request.EvaluationDateUtc ?? SystemClock.UtcNow, tenantId, cancellationToken)
            .ConfigureAwait(false);
    }
}
