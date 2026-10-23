namespace GameGuild.Identity.Authentication;

/// <summary>
///     Tunables for delegated account recovery with trusted contacts (issue #209).
///     Bound from <c>Authentication:AccountRecovery</c>; every value is clamped to a
///     safe default when absent so the feature fails closed, never open.
/// </summary>
public sealed class AccountRecoveryOptions
{
    public const string SectionName = "Authentication:AccountRecovery";

    /// <summary>Config section name used by the DI registration.</summary>
    public const int DefaultRequiredApprovals = 2;

    /// <summary>Maximum number of trusted contacts one account may keep active.</summary>
    public int MaxTrustedContacts { get; set; } = 5;

    /// <summary>Approval threshold (K); clamped to [1, available factors] per request.</summary>
    public int RequiredApprovals { get; set; } = DefaultRequiredApprovals;

    /// <summary>Cooling-off window before any approval counts.</summary>
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Window during which approvals are accepted, measured from the request.</summary>
    public TimeSpan ApprovalWindow { get; set; } = TimeSpan.FromHours(72);

    /// <summary>Lifetime of a trusted-contact invite token.</summary>
    public TimeSpan InviteLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Lifetime of the forced-reset token minted at threshold.</summary>
    public TimeSpan ResetTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Retention of terminal recovery requests before housekeeping expires them.</summary>
    public TimeSpan RequestRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Per-email rate limit for opening recovery requests.</summary>
    public TimeSpan EmailRateLimit { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Effective approval threshold for a given number of available factors.</summary>
    public int EffectiveRequiredApprovals(int availableFactors) =>
        Math.Clamp(RequiredApprovals, 1, Math.Max(1, availableFactors));
}
