using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Starts a lost-MFA-device recovery for an email address. Identity comes from the
///     email only; the response is always the same generic success.
/// </summary>
public sealed class RequestMfaRecoveryCommand : ICommand<MfaRecoveryRequestResult>
{
    public string Email { get; init; } = string.Empty;
}

/// <summary>
///     Opens the second stage of a recovery after the cooling-off window, using the
///     one-time request token from the first recovery email.
/// </summary>
public sealed class ContinueMfaRecoveryCommand : ICommand<MfaRecoveryContinueResult>
{
    public string RequestToken { get; init; } = string.Empty;
}

/// <summary>
///     Completes a recovery using the confirmation token from the second recovery email;
///     disables MFA for the recovered account.
/// </summary>
public sealed class ConfirmMfaRecoveryCommand : ICommand<MfaRecoveryConfirmResult>
{
    public string ConfirmationToken { get; init; } = string.Empty;
}

/// <summary>Enumeration-safe acknowledgement; identical for known and unknown emails.</summary>
public sealed class MfaRecoveryRequestResult
{
    public bool Success { get; init; } = true;

    public string Message { get; init; } =
        "If an account with that email exists and multi-factor authentication is enabled, recovery instructions have been sent.";
}

/// <summary>Outcome of the continue stage; a denial never distinguishes token state.</summary>
public sealed class MfaRecoveryContinueResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Seconds remaining in the cooling-off window; only set when the request is too early.</summary>
    public int? RetryAfterSeconds { get; init; }
}

/// <summary>Outcome of the confirm stage; a denial never distinguishes token state.</summary>
public sealed class MfaRecoveryConfirmResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}
