using GameGuild.CQRS;

namespace GameGuild.Identity.Users;

/// <summary>
///     Command handler for updating user localization preferences
/// </summary>
public sealed class UpdateUserLocalizationPreferencesCommandHandler(IUserRepository userRepository, IUserPreferencesRepository preferencesRepository)
    : ICommandHandler<UpdateUserLocalizationPreferencesCommand>
{
    public async Task<Unit> Handle(UpdateUserLocalizationPreferencesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            throw new UserNotFoundException(request.UserId);
        }

        var preferences = await preferencesRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        if (preferences == null)
        {
            preferences = UserPreferences.Create(request.UserId);
            await preferencesRepository.AddAsync(preferences, cancellationToken).ConfigureAwait(false);
        }

        var existing = preferences.GetLocalizationPreferences();
        foreach (var pref in request.Request.LocalizationPreferences)
        {
            existing[pref.Key] = pref.Value;
        }
        preferences.SetLocalizationPreferences(existing);

        await preferencesRepository.UpdateAsync(preferences, cancellationToken).ConfigureAwait(false);
        await preferencesRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
