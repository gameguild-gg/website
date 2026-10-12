using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to initiate an orchestrated authentication flow: validates the local
///     primary credential, assesses sign-in risk and persists the flow state with the
///     required challenge ladder (MFA / device trust / risk challenge).
/// </summary>
public class InitiateOrchestratedAuthenticationCommand : ICommand<OrchestratedFlowStateResponse>
{
    /// <summary>
    ///     Credential identifier (email) of the account signing in.
    /// </summary>
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    ///     The account password; verified as the primary credential at initiation.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    ///     Optional tenant context for the resulting sign-in.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     Optional device fingerprint used for trusted-device recognition.
    ///     Missing or unrecognized fingerprints fail closed to a device-trust challenge.
    /// </summary>
    public string? DeviceFingerprint { get; init; }
}

/// <summary>
///     Command to process (satisfy) one challenge step of an orchestrated
///     authentication flow with a TOTP code, backup code or WebAuthn assertion.
/// </summary>
public class ProcessOrchestratedAuthenticationStepCommand : ICommand<OrchestratedFlowStateResponse>
{
    /// <summary>
    ///     The flow whose step is being satisfied.
    /// </summary>
    public Guid FlowId { get; init; }

    /// <summary>
    ///     The challenge step being satisfied (never <see cref="AuthenticationStep.PrimaryCredential" />).
    /// </summary>
    public AuthenticationStep Step { get; init; }

    /// <summary>
    ///     TOTP verification code, when satisfying the step with the authenticator app.
    /// </summary>
    public string? TotpCode { get; init; }

    /// <summary>
    ///     One-time backup code, when satisfying the step with a recovery code.
    /// </summary>
    public string? BackupCode { get; init; }

    /// <summary>
    ///     Serialized WebAuthn assertion, when satisfying the step with a passkey/security key.
    /// </summary>
    public string? WebAuthnAssertion { get; init; }
}

/// <summary>
///     Command to complete an orchestrated authentication flow whose required steps
///     are all satisfied; issues the access and refresh tokens.
/// </summary>
public class CompleteOrchestratedAuthenticationCommand : ICommand<SignInResponse>
{
    /// <summary>
    ///     The flow to complete.
    /// </summary>
    public Guid FlowId { get; init; }
}

/// <summary>
///     Command to abandon an in-flight orchestrated authentication flow.
/// </summary>
public class AbandonOrchestratedAuthenticationCommand : ICommand
{
    /// <summary>
    ///     The flow to abandon.
    /// </summary>
    public Guid FlowId { get; init; }
}

/// <summary>
///     Query for the current state of an orchestrated authentication flow.
///     Abandoned, expired and unknown flows are not observable (null).
/// </summary>
public class GetOrchestratedAuthenticationFlowStateQuery : IQuery<OrchestratedFlowStateResponse?>
{
    /// <summary>
    ///     The flow to look up.
    /// </summary>
    public Guid FlowId { get; init; }
}
