using FluentValidation;
using GameGuild.Commerce.Billing;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Persists an explicit enable/disable decision for an external billing provider
///     (issue #397 provider-management API). The acting user is resolved from the actor
///     context, never from the request. Fail-closed: unknown provider keys are rejected
///     by the validator and by the registry.
/// </summary>
public sealed record SetExternalBillingProviderEnabledCommand(string ProviderKey, bool Enable)
    : ICommand<ExternalBillingProviderStatusDto>;

/// <summary>
///     Validator for <see cref="SetExternalBillingProviderEnabledCommand"/>: only known
///     provider keys may be toggled (fail-closed).
/// </summary>
public sealed class SetExternalBillingProviderEnabledCommandValidator : AbstractValidator<SetExternalBillingProviderEnabledCommand>
{
    public SetExternalBillingProviderEnabledCommandValidator()
    {
        RuleFor(command => command.ProviderKey)
            .NotEmpty()
            .Must(providerKey => PaymentProviders.IsSupported(providerKey))
            .WithMessage("ProviderKey must be a supported external billing provider (stripe, paypal, applepay, apple_app_store, googlepay, google_play_store).");
    }
}

/// <summary>
///     Handler for <see cref="SetExternalBillingProviderEnabledCommand"/>. Applies the
///     override through the registry and writes an audit event for the state change.
/// </summary>
public sealed class SetExternalBillingProviderEnabledCommandHandler(
    IExternalBillingProviderRegistry registry,
    IActorContextAccessor actorContextAccessor,
    IAuditService auditService,
    ILogger<SetExternalBillingProviderEnabledCommandHandler> logger) : ICommandHandler<SetExternalBillingProviderEnabledCommand, ExternalBillingProviderStatusDto>
{
    public async Task<ExternalBillingProviderStatusDto> Handle(SetExternalBillingProviderEnabledCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = actorContextAccessor.ActorContext;
        var actorUserId = actor.SubjectIdAsGuid;

        var status = await registry
            .SetEnabledAsync(command.ProviderKey, command.Enable, actorUserId ?? Guid.Empty, cancellationToken)
            .ConfigureAwait(false);


        logger.LogInformation(
            "External billing provider {ProviderKey} {Action} by actor {ActorId}",
            status.ProviderKey,
            status.Enabled ? "enabled" : "disabled",
            actor.SubjectId);

        // Audit delivery failures must never block or roll back the authorized state
        // change, but they are surfaced loudly (mirrors the permission-audit model).
        try
        {
            await auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = command.Enable
                    ? AuditActionTypes.BillingProviderEnabled
                    : AuditActionTypes.BillingProviderDisabled,
                ResourceType = "ExternalBillingProvider",
                ResourceId = status.ProviderKey,
                UserId = actorUserId,
                Description = $"External billing provider '{status.ProviderKey}' was {(command.Enable ? "enabled" : "disabled")} by an administrator.",
                Metadata = new Dictionary<string, object?>
                {
                    ["providerKey"] = status.ProviderKey,
                    ["enabled"] = status.Enabled
                },
                Success = true,
                RiskLevel = AuditRiskLevel.Medium,
                Category = AuditCategory.General
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to write the audit record for the external billing provider state change of {ProviderKey}; the state change remains committed",
                status.ProviderKey);
        }

        return status;
    }
}
