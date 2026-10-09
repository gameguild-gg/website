namespace GameGuild.Identity.Authentication;

/// <summary>
///     Lifecycle status of a WebAuthn credential.
/// </summary>
public enum WebAuthnCredentialStatus
{
    /// <summary>
    ///     The credential is active and can be used for authentication ceremonies.
    /// </summary>
    Active = 1,

    /// <summary>
    ///     The credential is temporarily deactivated and cannot be used for
    ///     authentication ceremonies, but the transition is reversible via activation.
    /// </summary>
    Deactivated = 2,

    /// <summary>
    ///     The credential is revoked. Revocation is terminal: a revoked credential
    ///     can never be reactivated.
    /// </summary>
    Revoked = 3
}
