using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication;

/// <summary>
/// Admits a password attempt for its canonical account identifier and request source.
/// The caller retains the returned lease through authentication and recording.
/// A denied attempt performs timing compensation and throws the generic authentication failure.
/// </summary>
public interface IPasswordSignInAdmissionService
{
    Task<IAsyncDisposable> AdmitAsync(string identifier, HttpContext? context,
        AuthenticationTimingOrigin timingOrigin, CancellationToken cancellationToken);
}
