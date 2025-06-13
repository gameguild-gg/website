using MediatR;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for generating Web3 challenge
/// </summary>
public class GenerateWeb3ChallengeHandler : IRequestHandler<GenerateWeb3ChallengeCommand, Web3ChallengeResponseDto>
{
    private readonly IAuthService _authService;

    public GenerateWeb3ChallengeHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<Web3ChallengeResponseDto> Handle(GenerateWeb3ChallengeCommand request, CancellationToken cancellationToken)
    {
        var challengeRequest = new Web3ChallengeRequestDto
        {
            WalletAddress = request.WalletAddress,
            ChainId = request.ChainId
        };

        return await _authService.GenerateWeb3ChallengeAsync(challengeRequest);
    }
}
