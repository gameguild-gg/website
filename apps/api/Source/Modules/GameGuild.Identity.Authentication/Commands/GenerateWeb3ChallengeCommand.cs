using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to generate a Web3 challenge for wallet authentication
/// </summary>
public class GenerateWeb3ChallengeCommand : ICommand<Web3ChallengeResponse>
{
    public string WalletAddress { get; set; } = string.Empty;

    public string ChainId { get; set; } = string.Empty;
}
