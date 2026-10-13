using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for DeductFundsCommand. Records a privileged-action audit event when an
///     administrator actor is present (issue #406 administrative adjustment audit trail).
/// </summary>
public sealed class DeductFundsCommandHandler(
    IWalletService walletService,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<DeductFundsCommand, WalletTransaction>
{
    public async Task<WalletTransaction> Handle(DeductFundsCommand request, CancellationToken cancellationToken)
    {
        var transaction = await walletService.DeductFundsAsync(
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
                newValue: $"{{\"direction\":\"debit\",\"amount\":{transaction.Amount},\"balanceAfter\":{transaction.BalanceAfter},\"referenceId\":\"{transaction.ReferenceId ?? string.Empty}\"}}",
                reason: request.Description,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return transaction;
    }
}
