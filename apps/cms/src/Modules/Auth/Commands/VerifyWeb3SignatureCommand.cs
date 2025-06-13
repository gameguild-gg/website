using MediatR;
using GameGuild.Modules.Auth.Dtos;

namespace GameGuild.Modules.Auth.Commands;

/// <summary>
/// Command to verify Web3 signature and authenticate user
/// </summary>
public class VerifyWeb3SignatureCommand : IRequest<SignInResponseDto>
{
    public string WalletAddress { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string ChainId { get; set; } = string.Empty;
}
