using GameGuild.CQRS;

namespace GameGuild.Identity.Users;

public sealed class UpdateUserProfileCommandHandler(
    IUserRepository userRepository,
    IUserProfileRepository profileRepository) : ICommandHandler<UpdateUserProfileCommand, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
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

        if (request.Request.DisplayName != null)
        {
            profile.DisplayName = request.Request.DisplayName;
        }

        if (request.Request.Bio != null)
        {
            profile.Bio = request.Request.Bio;
        }

        if (request.Request.Location != null)
        {
            profile.Location = request.Request.Location;
        }

        if (request.Request.Website != null)
        {
            profile.Website = request.Request.Website;
        }

        if (request.Request.JobTitle != null)
        {
            profile.JobTitle = request.Request.JobTitle;
        }

        if (request.Request.Company != null)
        {
            profile.Company = request.Request.Company;
        }

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
