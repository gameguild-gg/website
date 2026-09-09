using GameGuild.CQRS;
using GameGuild.Finance.Economy.Funding;

namespace GameGuild.Finance.Economy.Commands;

public sealed record ConvertMyHardToSoftRequest(
    long PrincipalHardCoinUnits,
    string IdempotencyKey);

public sealed record ConvertMyHardToSoftCommand(ConvertMyHardToSoftRequest Request)
    : ICommand<SelfServiceHardToSoftConversionReceipt>;

public sealed class ConvertMyHardToSoftCommandHandler(IHardToSoftConversionWorkflow workflow)
    : ICommandHandler<ConvertMyHardToSoftCommand, SelfServiceHardToSoftConversionReceipt>
{
    public Task<SelfServiceHardToSoftConversionReceipt> Handle(
        ConvertMyHardToSoftCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Request);

        return workflow.ConvertAsync(
            new SelfServiceHardToSoftConversionRequest(
                request.Request.PrincipalHardCoinUnits,
                request.Request.IdempotencyKey),
            cancellationToken);
    }
}
