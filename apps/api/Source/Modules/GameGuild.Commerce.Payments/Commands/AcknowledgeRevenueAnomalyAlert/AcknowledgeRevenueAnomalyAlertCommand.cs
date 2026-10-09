using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments.Commands.AcknowledgeRevenueAnomalyAlert;

/// <summary>
///     Command (issue #404): acknowledge an open revenue anomaly alert, recording the
///     reviewing operator and optional notes. The acting identity always comes from the
///     actor context, never from the request body.
/// </summary>
/// <param name="AlertId">Alert to acknowledge.</param>
/// <param name="Notes">Optional review notes.</param>
public sealed record AcknowledgeRevenueAnomalyAlertCommand(Guid AlertId, string? Notes = null) : ICommand;

/// <summary>Validator for <see cref="AcknowledgeRevenueAnomalyAlertCommand" />.</summary>
public sealed class AcknowledgeRevenueAnomalyAlertCommandValidator : AbstractValidator<AcknowledgeRevenueAnomalyAlertCommand>
{
    /// <summary>Initializes the validator.</summary>
    public AcknowledgeRevenueAnomalyAlertCommandValidator()
    {
        RuleFor(command => command.AlertId)
            .NotEmpty().WithMessage("Alert ID is required.");

        RuleFor(command => command.Notes)
            .MaximumLength(1000).WithMessage("Acknowledgement notes cannot exceed 1000 characters.")
            .When(command => !string.IsNullOrEmpty(command.Notes));
    }
}

/// <summary>
///     Handler for <see cref="AcknowledgeRevenueAnomalyAlertCommand" />: requires an
///     authenticated actor, enforces tenant scoping and records an audit trail entry.
/// </summary>
public sealed class AcknowledgeRevenueAnomalyAlertCommandHandler(
    IRevenueAnomalyAlertRepository alertRepository,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<AcknowledgeRevenueAnomalyAlertCommand>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(AcknowledgeRevenueAnomalyAlertCommand request, CancellationToken cancellationToken)
    {
        var actorContext = actorContextAccessor.ActorContext;
        var actorId = actorContext.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated operator is required to acknowledge a revenue anomaly alert.");

        var alert = await alertRepository.GetByIdAsync(request.AlertId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Revenue anomaly alert '{request.AlertId}' was not found.");

        if (!actorContext.IsSystemAdmin
            && alert.TenantId is { } alertTenantId
            && alertTenantId != actorContext.TenantId)
        {
            throw new UnauthorizedAccessException("An operator can only acknowledge alerts of their own tenant.");
        }

        alert.Acknowledge(actorId, request.Notes);
        await alertRepository.UpdateAsync(alert, cancellationToken).ConfigureAwait(false);
        await alertRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: "RevenueAnomalyAlert",
            entityId: alert.Id,
            action: "Acknowledged",
            changedBy: actorId,
            oldValue: "{\"status\":\"Open\"}",
            newValue: "{\"status\":\"Acknowledged\"}",
            reason: request.Notes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
