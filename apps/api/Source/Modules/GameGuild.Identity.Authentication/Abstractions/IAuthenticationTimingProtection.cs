namespace GameGuild.Identity.Authentication;

/// <summary>Optional monotonic, cancellable capability; the existing service signature remains available.</summary>
public interface IAuthenticationTimingProtection
{
    Task CompleteAuthenticationTimingAsync(AuthenticationTimingOrigin origin, bool credentialWorkCompleted,
        CancellationToken cancellationToken);
}
