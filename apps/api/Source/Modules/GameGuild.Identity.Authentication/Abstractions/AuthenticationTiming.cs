using System.Diagnostics;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Classification of the credential work completed inside a timing-compensation window.
///     This replaces user-existence in the enumeration-protection contract: what matters for
///     timing equality is not whether an account exists, but whether the request already
///     performed expensive credential verification. Missing accounts, passwordless accounts
///     and unusable stored credentials all complete <see cref="None" /> and therefore receive
///     equivalent dummy verification at the configured BCrypt work factor.
/// </summary>
public enum CredentialWorkClassification
{
    /// <summary>
    ///     The window completed no usable credential verification. This covers an unknown
    ///     account, an account without a local password, and credentials the hasher rejected
    ///     before expensive verification (malformed stored hash, input beyond BCrypt's
    ///     72-byte boundary). Compensation must supply equivalent dummy credential work.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The window completed real, expensive credential verification (BCrypt or the
    ///     versioned full-length fallback). No dummy work is required.
    /// </summary>
    Completed = 1
}

/// <summary>
///     Server-owned monotonic timing scope for one authentication attempt. The origin is a
///     <see cref="Stopwatch.GetTimestamp()" /> value captured by the constructor itself, so
///     callers can never supply a wall-clock instant after the fact; the scope must simply be
///     created <em>before</em> account resolution so the lookup and all credential work fall
///     inside the compensated window.
/// </summary>
public sealed class AuthenticationTimingScope
{
    private readonly long _originTimestamp;

    /// <summary>Captures the server-owned monotonic origin at construction time.</summary>
    public AuthenticationTimingScope()
    {
        _originTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    ///     Monotonic elapsed time since the origin. Includes every operation performed after
    ///     the scope was created: account resolution, real credential verification and any
    ///     compensation-side dummy work performed so far.
    /// </summary>
    public TimeSpan ElapsedSinceOrigin => Stopwatch.GetElapsedTime(_originTimestamp);
}
