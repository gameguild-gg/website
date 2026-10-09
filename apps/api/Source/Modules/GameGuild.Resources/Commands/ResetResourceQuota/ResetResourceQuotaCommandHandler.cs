using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Resources;

/// <summary>
///     Handler for resetting resource quotas with audit logging for SOC2/ISO 27001 compliance
/// </summary>
public sealed class ResetResourceQuotaCommandHandler(
    IResourceQuotaRepository resourceQuotaRepository,
    IPublisher publisher,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<ResetResourceQuotaCommand>
{
    public async Task<Unit> Handle(ResetResourceQuotaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quota = await resourceQuotaRepository.GetByTenantAndTypeAsync(request.TenantId, request.Type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return Unit.Value;
        }

        var previousUsage = quota.CurrentUsage;
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor?.SubjectIdAsGuid;

        quota.ResetUsage();
        quota.Touch();

        await resourceQuotaRepository.UpdateAsync(quota, cancellationToken).ConfigureAwait(false);

        // Publish audit event for SOC2/ISO 27001 compliance
        await publisher.Publish(new QuotaChangedEvent(
            TenantId: request.TenantId,
            ResourceType: request.Type,
            ChangeType: QuotaChangeType.Reset,
            PreviousUsage: previousUsage,
            CurrentUsage: 0,
            SoftLimit: quota.SoftLimit,
            HardLimit: quota.HardLimit,
            Source: "ResetResourceQuotaCommand",
            ActorId: actorId,
            Timestamp: SystemClock.UtcNow), cancellationToken);

        return Unit.Value;
    }
}
