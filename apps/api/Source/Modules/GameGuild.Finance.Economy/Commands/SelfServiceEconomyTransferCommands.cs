using GameGuild.CQRS;
using GameGuild.Finance.Economy.Transfers;

namespace GameGuild.Finance.Economy.Commands;

public sealed record CreateMyEconomyTransferCommand(SelfServiceEconomyTransferRequest Request)
    : ICommand<SelfServiceEconomyTransferReceipt>;

public sealed class CreateMyEconomyTransferCommandHandler(
    ISelfServiceEconomyTransferService transfers)
    : ICommandHandler<CreateMyEconomyTransferCommand, SelfServiceEconomyTransferReceipt>
{
    public Task<SelfServiceEconomyTransferReceipt> Handle(
        CreateMyEconomyTransferCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);
        return transfers.TransferAsync(request.Request, cancellationToken);
    }
}
