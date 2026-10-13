namespace GameGuild.Identity.Authentication;

/// <summary>
///     A user's request to regain access after losing their MFA device; tracks the
///     cooling-off window and one-time confirmation tokens (stored as hashes only).
/// </summary>
public sealed class MfaRecoveryRequest
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>End of the mandatory waiting period before a recovery can complete.</summary>
    public DateTimeOffset CooldownEndsAt { get; set; }

    public MfaRecoveryRequestStatus Status { get; set; } = MfaRecoveryRequestStatus.Pending;

    /// <summary>Hex hash of the token that opened the request; null until issued.</summary>
    public string? RequestTokenHash { get; set; }

    /// <summary>Hex hash of the token that confirms and completes the recovery.</summary>
    public string? ConfirmationTokenHash { get; set; }

    public DateTimeOffset? ConfirmationTokenExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
