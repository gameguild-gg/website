using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for TransferFundsCommand. Records a privileged-action audit event for both the
///     debited and the credited wallet when an administrator actor is present (issue #406).
/// </summary>
public sealed class TransferFundsCommandHandler(
    IWalletService walletService,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<TransferFundsCommand, TransferResult>
{
    public async Task<TransferResult> Handle(TransferFundsCommand request, CancellationToken cancellationToken)
    {
        (var debitTransaction, var creditTransaction) = await walletService.TransferFundsAsync(
            request.FromUserId,
            request.ToUserId,
            request.Amount,
            request.Description,
            request.ReferenceId,
            request.Currency,
            cancellationToken
        ).ConfigureAwait(false);

        if (actorContextAccessor.ActorContext.SubjectIdAsGuid is { } actorId)
        {
            await revenueAuditService.RecordAuditTrailAsync(
                "UserWallet",
                debitTransaction.WalletId,
                "BalanceTransferred",
                actorId,
                newValue: $"{{\"direction\":\"transfer-out\",\"amount\":{debitTransaction.Amount},\"balanceAfter\":{debitTransaction.BalanceAfter},\"referenceId\":\"{debitTransaction.ReferenceId ?? string.Empty}\"}}",
                reason: request.Description,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await revenueAuditService.RecordAuditTrailAsync(
                "UserWallet",
                creditTransaction.WalletId,
                "BalanceTransferred",
                actorId,
                newValue: $"{{\"direction\":\"transfer-in\",\"amount\":{creditTransaction.Amount},\"balanceAfter\":{creditTransaction.BalanceAfter},\"referenceId\":\"{creditTransaction.ReferenceId ?? string.Empty}\"}}",
                reason: request.Description,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new TransferResult(debitTransaction, creditTransaction);
    }
}
