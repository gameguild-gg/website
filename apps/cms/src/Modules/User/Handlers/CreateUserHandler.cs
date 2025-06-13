using MediatR;
using GameGuild.Modules.User.Commands;
using GameGuild.Modules.User.Services;

namespace GameGuild.Modules.User.Handlers;

/// <summary>
/// Handler for creating a new user
/// </summary>
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Models.User>
{
    private readonly IUserService _userService;

    public CreateUserHandler(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<Models.User> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new Models.User
        {
            Name = request.Name,
            Email = request.Email,
            IsActive = request.IsActive
        };

        return await _userService.CreateUserAsync(user);
    }
}
