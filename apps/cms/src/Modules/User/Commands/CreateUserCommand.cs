using MediatR;

namespace GameGuild.Modules.User.Commands;

/// <summary>
/// Command to create a new user
/// </summary>
public class CreateUserCommand : IRequest<Models.User>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
