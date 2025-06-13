using MediatR;

namespace GameGuild.Modules.UserProfile.Queries;

/// <summary>
/// Query to get user profile by user ID
/// </summary>
public class GetUserProfileByUserIdQuery : IRequest<Models.UserProfile?>
{
    public Guid UserId { get; set; }
}
