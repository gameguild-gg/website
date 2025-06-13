using MediatR;
using GameGuild.Modules.Auth.Dtos;

namespace GameGuild.Modules.Auth.Commands;

/// <summary>
/// Command to generate Web3 challenge for wallet authentication
/// </summary>
public class GenerateWeb3ChallengeCommand : IRequest<Web3ChallengeResponseDto>
{
    public string WalletAddress { get; set; } = string.Empty;
    public string ChainId { get; set; } = string.Empty;
}
