using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for generating Web3 challenge
/// </summary>
public sealed class GenerateWeb3ChallengeHandler(IWeb3Service web3Service) : ICommandHandler<GenerateWeb3ChallengeCommand, Web3ChallengeResponse>
{
    public async Task<Web3ChallengeResponse> Handle(GenerateWeb3ChallengeCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Web3Challenge challenge;
        try
        {
            challenge = await web3Service.GenerateChallengeAsync(request.WalletAddress, chainId: request.ChainId).ConfigureAwait(false);
        }
        catch (ArgumentException exception) when (exception.ParamName is "walletAddress" or "chainId")
        {
            var field = exception.ParamName == "walletAddress" ? nameof(request.WalletAddress) : nameof(request.ChainId);
            throw new RequestValidationException([new ValidationError(field, "The wallet address or Ethereum chain is invalid or unsupported.")]);
        }

        return new Web3ChallengeResponse { Challenge = challenge.Message, Nonce = challenge.Nonce, ExpiresAt = challenge.ExpiresAt };
    }
}
