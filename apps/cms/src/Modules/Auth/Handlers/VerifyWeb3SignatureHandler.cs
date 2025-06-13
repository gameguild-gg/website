using MediatR;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for verifying Web3 signature
/// </summary>
public class VerifyWeb3SignatureHandler : IRequestHandler<VerifyWeb3SignatureCommand, SignInResponseDto>
{
    private readonly IAuthService _authService;

    public VerifyWeb3SignatureHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<SignInResponseDto> Handle(VerifyWeb3SignatureCommand request, CancellationToken cancellationToken)
    {
        var verifyRequest = new Web3VerifyRequestDto
        {
            WalletAddress = request.WalletAddress,
            Signature = request.Signature,
            Nonce = request.Nonce,
            ChainId = request.ChainId
        };

        return await _authService.VerifyWeb3SignatureAsync(verifyRequest);
    }
}
