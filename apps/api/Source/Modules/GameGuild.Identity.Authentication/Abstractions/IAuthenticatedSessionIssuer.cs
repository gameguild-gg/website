using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>Issues credentials for a verified account only after persisting its tenant and session binding.</summary>
public interface IAuthenticatedSessionIssuer
{
    Task<SignInResponse> IssueAsync(
        User user,
        Guid? requestedTenantId,
        DeviceInfo deviceInfo,
        CancellationToken cancellationToken);
}
