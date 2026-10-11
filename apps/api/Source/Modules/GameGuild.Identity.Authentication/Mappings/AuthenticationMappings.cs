using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Mapping extensions for converting between Domain models and Application DTOs
/// </summary>
public static class AuthenticationMappings
{
    /// <summary>
    ///     Maps Domain SignInResponse to Application SignInResponse DTO
    /// </summary>
    public static Task<SignInResponse> ToDto(this SignInResponse domainResponse, IUserRepository userRepository)
        => domainResponse.ToDto(userRepository, CancellationToken.None);

    /// <summary>
    ///     Maps the server response and repository profile without modifying either source.
    /// </summary>
    public static async Task<SignInResponse> ToDto(this SignInResponse domainResponse, IUserRepository userRepository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainResponse);
        ArgumentNullException.ThrowIfNull(userRepository);
        // These outcomes are created only after their trusted persistence/audit succeeds.
        // Replacing them with a DTO would make the command owner roll back that durable state.
        if (domainResponse is SignInMfaPendingResponse or SignInMfaCommittedDenial)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return domainResponse;
        }

        // Try to fetch user details from repository
        // Note: In some scenarios (e.g., tests with separate DbContext scopes), the user might not be available yet
        var user = await userRepository.GetByIdAsync(domainResponse.UserId, cancellationToken).ConfigureAwait(false);
        var now = SystemClock.UtcNow;
        var authenticationComplete = domainResponse.Success && !domainResponse.RequiresMfa && !domainResponse.RequiresStepUp
            && !string.IsNullOrWhiteSpace(domainResponse.AccessToken);

        var accessTokenExpiresAt = domainResponse.AccessTokenExpiresAt == default
            ? (domainResponse.ExpiresIn > 0 ? now.AddSeconds(domainResponse.ExpiresIn) : domainResponse.ExpiresAt)
            : domainResponse.AccessTokenExpiresAt;

        var refreshTokenExpiresAt = domainResponse.RefreshTokenExpiresAt == default
            ? domainResponse.ExpiresAt
            : domainResponse.RefreshTokenExpiresAt;

        return new SignInResponse
        {
            Success = domainResponse.Success,
            Message = domainResponse.Message,
            AccessToken = domainResponse.AccessToken,
            RefreshToken = domainResponse.RefreshToken,
            ExpiresAt = domainResponse.ExpiresAt,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            ExpiresIn = domainResponse.ExpiresIn,
            UserId = domainResponse.UserId,
            Email = domainResponse.Email,
            SessionId = domainResponse.SessionId,
            TempToken = domainResponse.TempToken,
            MfaToken = domainResponse.MfaToken,
            MfaEnrollmentBackupCodes = domainResponse.MfaEnrollmentBackupCodes?.ToArray(),
            User = CreateUserProfile(user, domainResponse.UserId, domainResponse.Email, authenticationComplete, now),
            TenantId = domainResponse.TenantId,
            AvailableTenants = domainResponse.AvailableTenants?.ToArray(),
            RequiresMfa = domainResponse.RequiresMfa,
            MfaSessionId = domainResponse.MfaSessionId,
            RequiresStepUp = domainResponse.RequiresStepUp,
            StepUpToken = domainResponse.StepUpToken,
            StepUpExpiresAt = domainResponse.StepUpExpiresAt,
            RiskLevel = domainResponse.RiskLevel,
            RiskFactors = domainResponse.RiskFactors?.ToList(),
            AvailableMethods = domainResponse.AvailableMethods?.ToList(),
            AuthenticationMethodReferences = domainResponse.AuthenticationMethodReferences?.ToArray(),
            AuthenticationContextClassReference = domainResponse.AuthenticationContextClassReference,
            MfaVerifiedByProvider = domainResponse.MfaVerifiedByProvider
        };
    }

    internal static UserDto CreateUserProfile(User? user, Guid userId, string email, bool authenticationComplete, DateTime fallbackCreatedAt)
    {
        var nameParts = user?.Name?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return new UserDto
        {
            Id = userId,
            Email = user?.Email ?? email,
            Username = user?.Username ?? email,
            FirstName = nameParts is { Length: > 0 } ? nameParts[0] : null,
            LastName = nameParts is { Length: > 1 } ? string.Join(' ', nameParts.Skip(1)) : null,
            PhoneNumber = authenticationComplete ? user?.PhoneNumber : null,
            EmailVerified = user?.IsEmailVerified ?? false,
            PhoneNumberVerified = false,
            CreatedAt = user?.CreatedAt ?? fallbackCreatedAt,
            LastLoginAt = user?.LastLoginAt
        };
    }

    /// <summary>
    ///     Maps Domain RefreshTokenResponse to Application RefreshTokenResponse DTO
    /// </summary>
    public static RefreshTokenResponse ToDto(this RefreshTokenResponse domainResponse)
    {
        ArgumentNullException.ThrowIfNull(domainResponse);

        return new RefreshTokenResponse
        {
            AccessToken = domainResponse.AccessToken,
            RefreshToken = domainResponse.RefreshToken,
            ExpiresAt = domainResponse.ExpiresAt == default && domainResponse.ExpiresIn > 0
                ? SystemClock.UtcNow.AddSeconds(domainResponse.ExpiresIn)
                : domainResponse.ExpiresAt,
            ExpiresIn = domainResponse.ExpiresIn,
            User = new UserDto
            {
                Id = domainResponse.User.Id,
                Email = domainResponse.User.Email,
                Username = domainResponse.User.Username,
                FirstName = domainResponse.User.FirstName,
                LastName = domainResponse.User.LastName,
                PhoneNumber = domainResponse.User.PhoneNumber,
                EmailVerified = domainResponse.User.EmailVerified,
                PhoneNumberVerified = domainResponse.User.PhoneNumberVerified,
                CreatedAt = domainResponse.User.CreatedAt,
                LastLoginAt = domainResponse.User.LastLoginAt
            }
        };
    }
}
