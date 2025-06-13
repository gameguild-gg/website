using MediatR;

namespace GameGuild.Modules.UserProfile.Commands;

/// <summary>
/// Command to update user profile with validation and business logic
/// </summary>
public class UpdateUserProfileCommand : IRequest<Models.UserProfile>
{
    public Guid UserProfileId { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? DisplayName { get; set; }
}
