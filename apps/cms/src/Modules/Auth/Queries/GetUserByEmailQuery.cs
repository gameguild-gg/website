using MediatR;

namespace GameGuild.Modules.Auth.Queries;

/// <summary>
/// Query to get user by email
/// </summary>
public class GetUserByEmailQuery : IRequest<User.Models.User?>
{
    public string Email { get; set; } = string.Empty;
}
