using GameGuild.CQRS;

namespace GameGuild.Resources;

/// <summary>
///     Handler for toggling resource quota activation status
/// </summary>
public sealed class ToggleResourceQuotaCommandHandler(IResourceQuotaRepository resourceQuotaRepository) : ICommandHandler<ToggleResourceQuotaCommand>
{
    public async Task<Unit> Handle(ToggleResourceQuotaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quota = await resourceQuotaRepository.GetByTenantAndTypeAsync(request.TenantId, request.Type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return Unit.Value;
        }

        quota.IsActive = request.IsActive;
        quota.Touch();

        await resourceQuotaRepository.UpdateAsync(quota, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
