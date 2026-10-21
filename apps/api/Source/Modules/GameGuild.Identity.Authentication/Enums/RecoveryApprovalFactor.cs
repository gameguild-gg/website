namespace GameGuild.Identity.Authentication;

/// <summary>The kind of proof that produced one recovery approval (issue #209).</summary>
public enum RecoveryApprovalFactor
{
    /// <summary>A confirmed delegated contact redeemed their single-use recovery code.</summary>
    TrustedContact = 0,

    /// <summary>An enrolled WebAuthn credential on a surviving device verified an assertion.</summary>
    Passkey = 1
}
