using MediatR;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Notifications;
using GameGuild.Modules.Auth.Services;

namespace GameGuild.Modules.Auth.Handlers;

/// <summary>
/// Handler for local sign-up command
/// </summary>
public class LocalSignUpHandler : IRequestHandler<LocalSignUpCommand, SignInResponseDto>
{
    private readonly IAuthService _authService;
    private readonly IMediator _mediator;

    public LocalSignUpHandler(IAuthService authService, IMediator mediator)
    {
        _authService = authService;
        _mediator = mediator;
    }

    public async Task<SignInResponseDto> Handle(LocalSignUpCommand request, CancellationToken cancellationToken)
    {
        var signUpRequest = new LocalSignUpRequestDto
        {
            Email = request.Email,
            Password = request.Password,
            Username = request.Username
        };

        SignInResponseDto result = await _authService.LocalSignUpAsync(signUpRequest);

        // Publish notification for side effects (email, analytics, etc.)
        var notification = new UserSignedUpNotification
        {
            UserId = result.User.Id,
            Email = result.User.Email,
            Username = request.Username
        };

        await _mediator.Publish(notification, cancellationToken);

        return result;
    }
}
