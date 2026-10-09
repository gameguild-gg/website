using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments.Commands.RunRevenueReconciliation;

/// <summary>
///     Command (issue #404): reconcile an external accounting/ERP statement against
///     internal revenue events for an inclusive period. Inline lines win over the
///     configured external statement source.
/// </summary>
/// <param name="TenantId">Optional tenant scope; null reconciles the global aggregate.</param>
/// <param name="Source">External system the statement came from.</param>
/// <param name="PeriodStartUtc">Inclusive period start (UTC).</param>
/// <param name="PeriodEndUtc">Inclusive period end (UTC).</param>
/// <param name="Lines">Inline statement lines, or null to use the configured source.</param>
/// <param name="ExternalStatementId">Optional statement identifier for traceability.</param>
public sealed record RunRevenueReconciliationCommand(
    Guid? TenantId,
    string Source,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    List<ExternalRevenueStatementLine>? Lines,
    string? ExternalStatementId = null) : ICommand<RevenueReconciliationRun>;

/// <summary>Validator for <see cref="RunRevenueReconciliationCommand" />.</summary>
public sealed class RunRevenueReconciliationCommandValidator : AbstractValidator<RunRevenueReconciliationCommand>
{
    /// <summary>Maximum inclusive period length accepted for one run (one leap year).</summary>
    public const int MaxPeriodDays = 400;

    /// <summary>Initializes the validator.</summary>
    public RunRevenueReconciliationCommandValidator(IOptions<RevenueAuditingOptions> options)
    {
        RuleFor(command => command.Source)
            .NotEmpty().WithMessage("Statement source is required.")
            .MaximumLength(100).WithMessage("Statement source cannot exceed 100 characters.");

        RuleFor(command => command.PeriodStartUtc.Kind)
            .Equal(DateTimeKind.Utc).WithMessage("Period start must be a UTC instant.");

        RuleFor(command => command.PeriodEndUtc)
            .GreaterThan(command => command.PeriodStartUtc).WithMessage("Period end must be after period start.")
            .Must((command, end) => (end - command.PeriodStartUtc).TotalDays <= MaxPeriodDays)
            .WithMessage($"The reconciliation period cannot exceed {MaxPeriodDays} days.");

        RuleFor(command => command.Lines)
            .Must(lines => lines is null || lines.Count <= options.Value.MaxStatementLinesPerRun)
            .WithMessage(command => $"A reconciliation run accepts at most {options.Value.MaxStatementLinesPerRun} statement lines.");

        RuleFor(command => command.ExternalStatementId)
            .MaximumLength(200).WithMessage("External statement id cannot exceed 200 characters.");

        RuleForEach(command => command.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.ReferenceId)
                    .NotEmpty().WithMessage("Statement line reference is required.")
                    .MaximumLength(200).WithMessage("Statement line reference cannot exceed 200 characters.");
                line.RuleFor(l => l.Currency)
                    .NotEmpty().WithMessage("Statement line currency is required.")
                    .Length(3).WithMessage("Statement line currency must be a 3-letter ISO 4217 code.");
                line.RuleFor(l => l.Amount).GreaterThan(0).WithMessage("Statement line amount must be positive.");
            });
    }
}

/// <summary>
///     Handler for <see cref="RunRevenueReconciliationCommand" />: enforces actor tenant
///     scoping (non-admin actors may only reconcile their own tenant) and records an audit
///     trail entry for the run.
/// </summary>
public sealed class RunRevenueReconciliationCommandHandler(
    IRevenueReconciliationService reconciliationService,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<RunRevenueReconciliationCommand, RevenueReconciliationRun>
{
    /// <inheritdoc />
    public async Task<RevenueReconciliationRun> Handle(RunRevenueReconciliationCommand request, CancellationToken cancellationToken)
    {
        var actorContext = actorContextAccessor.ActorContext;
        Guid? tenantId = request.TenantId;
        if (!actorContext.IsSystemAdmin)
        {
            tenantId = actorContext.TenantId
                ?? throw new UnauthorizedAccessException("A tenant-scoped actor can only reconcile its own tenant.");
        }

        var initiatedBy = actorContext.SubjectIdAsGuid;
        if (initiatedBy is null && actorContext.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("An authenticated actor with a valid subject is required.");
        }

        var run = await reconciliationService.ReconcileAsync(
            new RevenueReconciliationRequest(
                TenantId: tenantId,
                Source: request.Source,
                PeriodStartUtc: request.PeriodStartUtc,
                PeriodEndUtc: request.PeriodEndUtc,
                Lines: request.Lines,
                ExternalStatementId: request.ExternalStatementId,
                InitiatedByUserId: initiatedBy),
            cancellationToken).ConfigureAwait(false);

        if (initiatedBy is { } actorId)
        {
            await revenueAuditService.RecordAuditTrailAsync(
                entityType: "RevenueReconciliationRun",
                entityId: run.Id,
                action: "ReconciliationRun",
                changedBy: actorId,
                newValue: $"{{\"matchedCount\":{run.MatchedCount},\"discrepancyCount\":{run.DiscrepancyCount}}}",
                reason: $"Statement source '{request.Source}' reconciled.",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return run;
    }
}
