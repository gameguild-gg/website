using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Admin-assisted reset of a user's multi-factor authentication. The acting
///     administrator is resolved from the actor context — never from the request.
/// </summary>
public sealed record AdminResetUserMfaCommand : ICommand<AdminResetUserMfaResult>
{
    /// <summary>The user whose MFA configuration and live recovery requests are reset.</summary>
    public required Guid UserId { get; init; }
}

/// <summary>Outcome of an admin-assisted MFA reset.</summary>
public sealed class AdminResetUserMfaResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Whether MFA was enabled and has now been disabled.</summary>
    public bool MfaWasEnabled { get; init; }

    /// <summary>Whether a live (Pending/AwaitingConfirmation) recovery request was cancelled.</summary>
    public bool LiveRecoveryCancelled { get; init; }
}
