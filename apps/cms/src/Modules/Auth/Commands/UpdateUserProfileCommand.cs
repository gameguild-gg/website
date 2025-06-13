using MediatR;

namespace GameGuild.Modules.Auth.Commands;

/// <summary>
/// Command to update user profile with validation
/// </summary>
public class UpdateUserProfileCommand : IRequest<User.Models.User>
{
    public Guid UserId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
}
