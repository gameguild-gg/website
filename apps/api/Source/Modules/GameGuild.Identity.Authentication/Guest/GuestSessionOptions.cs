using GameGuild.Configuration;
using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Guest session options (issue #213), bound from the <c>Authentication:Guest</c> section.
///     Defaults are the safe production posture: enabled, short-lived, no refresh credential.
/// </summary>
public sealed class GuestSessionOptions : BaseOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Authentication:Guest";

    /// <summary>
    ///     Whether anonymous guest sessions may be issued at all. When disabled, issuance
    ///     fails closed (403) and every rejection is audited through the security-event pipeline.
    /// </summary>
    public override bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Guest access-token lifetime in minutes. Guest sessions are intentionally
    ///     short-lived and carry no refresh token: expiry always ends the session.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 30;

    /// <summary>
    ///     Extra retention beyond the token lifetime for the store-level safety-net
    ///     eviction, mirroring the WebAuthn challenge store.
    /// </summary>
    public int StoreEvictionSlackMinutes { get; set; } = 5;

    /// <inheritdoc />
    public override void Validate()
    {
        if (AccessTokenLifetimeMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException(
                $"{nameof(AccessTokenLifetimeMinutes)} must be between 1 and 60 minutes; guest sessions must stay short-lived.");
        }

        if (StoreEvictionSlackMinutes < 0)
        {
            throw new InvalidOperationException(
                $"{nameof(StoreEvictionSlackMinutes)} must not be negative.");
        }
    }
}
