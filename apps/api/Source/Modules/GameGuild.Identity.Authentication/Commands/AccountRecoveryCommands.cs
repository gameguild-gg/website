using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Opens a delegated account recovery for an email address (issue #209). The owner
///     has lost their credentials; confirmed trusted contacts receive single-use
///     recovery codes after a cooling-off window. The response is enumeration-safe.
/// </summary>
public sealed class RequestAccountRecoveryCommand : ICommand<AccountRecoveryRequestResult>
{
    public string Email { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Cancels the acting owner's live recovery request (protective action available
///     during the cooling-off window when the owner still has access).
/// </summary>
public sealed class CancelAccountRecoveryCommand : ICommand<AccountRecoveryCancelResult>
{
    public Guid OwnerUserId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Redeems one trusted contact's single-use recovery code (anonymous). Counted only
///     after the cooling-off window, inside the approval window, while the contact is
///     still Confirmed. Denials are generic and never reveal code state.
/// </summary>
public sealed class ApproveAccountRecoveryCommand : ICommand<AccountRecoveryApprovalResult>
{
    public string ApprovalCode { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Begins a passkey assertion for recovery approval (anonymous): issues WebAuthn
///     options scoped to the recovering account so an enrolled credential on a
///     surviving device can later supply one approval factor. Issues no sign-in
///     credentials.
/// </summary>
public sealed class BeginPasskeyRecoveryApprovalCommand : ICommand<WebAuthnAuthenticationOptionsResult>
{
    public string? Email { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Completes a passkey assertion (anonymous) issued for recovery approval: an
///     enrolled WebAuthn credential on a surviving device counts as one approval
///     factor. The assertion is cryptographically verified and never issues sign-in
///     credentials.
/// </summary>
public sealed class ApproveAccountRecoveryWithPasskeyCommand : ICommand<AccountRecoveryApprovalResult>
{
    public required string AssertionResponse { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Consumes the single-use forced-reset token minted when the approval threshold
///     was reached (anonymous): invalidates all sessions, revokes all passkeys
///     (mandatory re-enrollment) and forces a new password.
/// </summary>
public sealed class CompleteAccountRecoveryResetCommand : ICommand<AccountRecoveryResetResult>
{
    public string ResetToken { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>Enumeration-safe acknowledgement; identical for known and unknown emails.</summary>
public sealed class AccountRecoveryRequestResult
{
    public bool Success { get; init; } = true;

    public string Message { get; init; } =
        "If an account with that email exists and has recovery factors enrolled, recovery instructions have been sent.";
}

/// <summary>Outcome of the owner's cancellation.</summary>
public sealed class AccountRecoveryCancelResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>Outcome of one approval attempt; a denial never distinguishes code or request state.</summary>
public sealed class AccountRecoveryApprovalResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Seconds remaining in the cooling-off window; only set when the proof is valid but too early.</summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>True when this approval reached the K-of-N threshold and unlocked the forced reset.</summary>
    public bool ResetUnlocked { get; init; }
}

/// <summary>Outcome of the forced reset; a denial never distinguishes token state.</summary>
public sealed class AccountRecoveryResetResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}
