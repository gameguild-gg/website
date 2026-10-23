using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Commerce;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to transition a pricing experiment's lifecycle (issue #395):
/// start (Draft → Running), pause (Running → Paused) or complete (→ Completed).
/// </summary>
/// <param name="ExperimentId">Experiment id</param>
/// <param name="Transition">Which lifecycle transition to apply</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record TransitionPricingExperimentCommand(
    Guid ExperimentId,
    PricingExperimentTransition Transition) : ICommand<PricingExperimentDto>;

/// <summary>Lifecycle transitions available for a pricing experiment.</summary>
public enum PricingExperimentTransition
{
    /// <summary>Draft → Running: the experiment starts enrolling.</summary>
    Start = 0,

    /// <summary>Running → Paused: enrollment stops, assignments stay sticky.</summary>
    Pause = 1,

    /// <summary>Running/Paused → Completed: the experiment is finished.</summary>
    Complete = 2
}

/// <summary>
/// Handler for pricing experiment lifecycle transitions.
/// </summary>
public sealed class TransitionPricingExperimentCommandHandler(IPricingExperimentRepository repository)
    : ICommandHandler<TransitionPricingExperimentCommand, PricingExperimentDto>
{
    public async Task<PricingExperimentDto> Handle(TransitionPricingExperimentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var experiment = await repository
            .GetByIdAsync(request.ExperimentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Pricing experiment {request.ExperimentId} not found.");

        var (from, to) = request.Transition switch
        {
            PricingExperimentTransition.Start => (PricingExperimentStatus.Draft, PricingExperimentStatus.Running),
            PricingExperimentTransition.Pause => (PricingExperimentStatus.Running, PricingExperimentStatus.Paused),
            PricingExperimentTransition.Complete => (experiment.Status, PricingExperimentStatus.Completed),
            _ => throw new InvalidOperationException($"Unknown transition {request.Transition}.")
        };

        if (experiment.Status != from)
        {
            throw new InvalidOperationException(
                $"Transition {request.Transition} requires status {from}, but experiment '{experiment.Name}' is {experiment.Status}.");
        }

        experiment.Status = to;
        if (to == PricingExperimentStatus.Running && experiment.StartDate is null)
        {
            experiment.StartDate = SystemClock.UtcNow;
        }

        await repository.UpdateAsync(experiment, cancellationToken).ConfigureAwait(false);

        return experiment.ToDto();
    }
}

/// <summary>
/// Validator for <see cref="TransitionPricingExperimentCommand"/>.
/// </summary>
public sealed class TransitionPricingExperimentCommandValidator : AbstractValidator<TransitionPricingExperimentCommand>
{
    /// <summary>
    /// Initializes the validator.
    /// </summary>
    public TransitionPricingExperimentCommandValidator()
    {
        RuleFor(x => x.ExperimentId)
            .NotEmpty()
            .WithMessage("Experiment ID is required.");

        RuleFor(x => x.Transition)
            .IsInEnum()
            .WithMessage("Transition must be Start, Pause or Complete.");
    }
}

/// <summary>
/// Command to record a monetized conversion outcome under the arm a bucket key is
/// assigned to (issue #395 A/B revenue-impact measurement). Callers are checkout and
/// subscription-start flows; requires the monetization permission.
/// </summary>
/// <param name="ExperimentId">Experiment id</param>
/// <param name="BucketKey">Bucket key that converted</param>
/// <param name="Amount">Final monetized amount</param>
/// <param name="Currency">ISO 4217 currency</param>
/// <param name="OrderId">Optional order correlation</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record RecordPricingExperimentOutcomeCommand(
    Guid ExperimentId,
    string BucketKey,
    decimal Amount,
    string Currency = "USD",
    Guid? OrderId = null) : ICommand<bool>;

/// <summary>
/// Handler for recording an experiment conversion outcome.
/// </summary>
public sealed class RecordPricingExperimentOutcomeCommandHandler(IPricingExperimentService experimentService)
    : ICommandHandler<RecordPricingExperimentOutcomeCommand, bool>
{
    public async Task<bool> Handle(RecordPricingExperimentOutcomeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await experimentService
            .RecordOutcomeAsync(request.ExperimentId, request.BucketKey, request.Amount, request.Currency, request.OrderId, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}

/// <summary>
/// Validator for <see cref="RecordPricingExperimentOutcomeCommand"/>.
/// </summary>
public sealed class RecordPricingExperimentOutcomeCommandValidator : AbstractValidator<RecordPricingExperimentOutcomeCommand>
{
    /// <summary>
    /// Initializes the validator.
    /// </summary>
    public RecordPricingExperimentOutcomeCommandValidator()
    {
        RuleFor(x => x.ExperimentId)
            .NotEmpty()
            .WithMessage("Experiment ID is required.");

        RuleFor(x => x.BucketKey)
            .NotEmpty()
            .WithMessage("Bucket key is required.")
            .MaximumLength(200)
            .WithMessage("Bucket key cannot exceed 200 characters.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount cannot be negative.");

        RuleFor(x => x.Currency)
            .Length(3)
            .WithMessage("Currency must be a three-letter ISO 4217 code.")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
    }
}

/// <summary>
/// Command to run one automated pricing scheduling pass (issue #395): opt-in scheduled
/// rules are activated/deactivated to match their window and date-bounded experiments
/// start/complete. Also executed periodically by the background scheduler.
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record RunPricingScheduleSweepCommand() : ICommand<PricingScheduleSweepResult>;

/// <summary>
/// Handler for the scheduling sweep command.
/// </summary>
public sealed class RunPricingScheduleSweepCommandHandler(IPricingExperimentService experimentService)
    : ICommandHandler<RunPricingScheduleSweepCommand, PricingScheduleSweepResult>
{
    public async Task<PricingScheduleSweepResult> Handle(RunPricingScheduleSweepCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await experimentService
            .RunSchedulingSweepAsync(SystemClock.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }
}
