using MediatR;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for local sign-in command
/// </summary>
public class LocalSignInHandler : IRequestHandler<LocalSignInCommand, SignInResponseDto>
{
    private readonly IAuthService _authService;

    public LocalSignInHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<SignInResponseDto> Handle(LocalSignInCommand request, CancellationToken cancellationToken)
    {
        var signInRequest = new LocalSignInRequestDto
        {
            Email = request.Email,
            Password = request.Password
        };

        return await _authService.LocalSignInAsync(signInRequest);
    }
}
