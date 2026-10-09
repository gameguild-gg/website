using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Resources;

/// <summary>
///     Handler for deleting resource quotas with audit logging for SOC2/ISO 27001 compliance
/// </summary>
public sealed class DeleteResourceQuotaCommandHandler(
    IResourceQuotaRepository resourceQuotaRepository,
    IPublisher publisher,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<DeleteResourceQuotaCommand>
{
    public async Task<Unit> Handle(DeleteResourceQuotaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quota = await resourceQuotaRepository.GetByTenantAndTypeAsync(request.TenantId, request.Type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return Unit.Value;
        }

        var previousUsage = quota.CurrentUsage;
        var softLimit = quota.SoftLimit;
        var hardLimit = quota.HardLimit;
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor?.SubjectIdAsGuid;

        await resourceQuotaRepository.DeleteAsync(quota.Id, cancellationToken).ConfigureAwait(false);

        // Publish audit event for SOC2/ISO 27001 compliance
        await publisher.Publish(new QuotaChangedEvent(
            TenantId: request.TenantId,
            ResourceType: request.Type,
            ChangeType: QuotaChangeType.Deleted,
            PreviousUsage: previousUsage,
            CurrentUsage: 0,
            SoftLimit: softLimit,
            HardLimit: hardLimit,
            Source: "DeleteResourceQuotaCommand",
            ActorId: actorId,
            Timestamp: SystemClock.UtcNow), cancellationToken);

        return Unit.Value;
    }
}
