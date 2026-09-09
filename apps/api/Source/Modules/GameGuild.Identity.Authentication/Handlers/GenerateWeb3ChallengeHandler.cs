using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for generating Web3 challenge
/// </summary>
public sealed class GenerateWeb3ChallengeHandler(IWeb3Service web3Service) : ICommandHandler<GenerateWeb3ChallengeCommand, Web3ChallengeResponse>
{
    public async Task<Web3ChallengeResponse> Handle(GenerateWeb3ChallengeCommand request, CancellationToken cancellationToken)
    {
        var challenge = await web3Service.GenerateChallengeAsync(request.WalletAddress).ConfigureAwait(false);

        return new Web3ChallengeResponse { Challenge = challenge.Message, ExpiresAt = challenge.ExpiresAt };
    }
}
