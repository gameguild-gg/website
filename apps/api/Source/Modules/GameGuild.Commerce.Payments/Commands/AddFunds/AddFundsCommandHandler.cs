using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for AddFundsCommand. Records a privileged-action audit event when an
///     administrator actor is present (issue #406 administrative adjustment audit trail);
///     system-driven credits (e.g. webhook wallet credits) are attributed to the system actor.
/// </summary>
public sealed class AddFundsCommandHandler(
    IWalletService walletService,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<AddFundsCommand, WalletTransaction>
{
    public async Task<WalletTransaction> Handle(AddFundsCommand request, CancellationToken cancellationToken)
    {
        var transaction = await walletService.AddFundsAsync(
            request.UserId,
            request.Amount,
            request.Description,
            request.ReferenceId,
            request.Currency,
            cancellationToken).ConfigureAwait(false);

        if (actorContextAccessor.ActorContext.SubjectIdAsGuid is { } actorId)
        {
            await revenueAuditService.RecordAuditTrailAsync(
                "UserWallet",
                transaction.WalletId,
                "BalanceAdjusted",
                actorId,
                newValue: $"{{\"direction\":\"credit\",\"amount\":{transaction.Amount},\"balanceAfter\":{transaction.BalanceAfter},\"referenceId\":\"{transaction.ReferenceId ?? string.Empty}\"}}",
                reason: request.Description,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return transaction;
    }
}
