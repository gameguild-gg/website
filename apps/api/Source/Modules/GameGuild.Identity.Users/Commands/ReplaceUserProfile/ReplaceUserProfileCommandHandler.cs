using GameGuild.CQRS;

namespace GameGuild.Identity.Users;

public sealed class ReplaceUserProfileCommandHandler(IUserRepository userRepository, IUserProfileRepository profileRepository) : ICommandHandler<ReplaceUserProfileCommand, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(ReplaceUserProfileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            throw new UserNotFoundException(request.UserId);
        }

        var profile = await profileRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        var isNewProfile = profile == null;
        profile ??= new UserProfile { UserId = request.UserId };

        // Replace all fields
        profile.DisplayName = request.Request.DisplayName;
        profile.Bio = request.Request.Bio;
        profile.Location = request.Request.Location;
        profile.Website = request.Request.Website;
        profile.JobTitle = request.Request.JobTitle;
        profile.Company = request.Request.Company;
        // Note: TimeZone, Language, ProfileVisibility, ShowEmail, ShowLocation are not properties of UserProfile
        // These appear to belong to UserPreferences instead

        profile.Touch();
        if (isNewProfile)
        {
            await profileRepository.AddAsync(profile, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await profileRepository.UpdateAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        await profileRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return UserProfileDto.FromEntity(profile);
    }
}
