namespace GameGuild.Commerce.Billing;

/// <summary>
///     Security hardening configuration for provider billing webhook callbacks.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="SourceIpAllowlist"/> is disabled by default (empty list): when entries are
///         configured the callback endpoints fail closed — a request whose client address does not
///         match any entry is rejected with 403 before the payload is read. Providers rotate their
///         egress ranges, so operators enabling the allowlist must keep the published provider
///         ranges current.
///     </para>
///     <para>
///         <see cref="SuspiciousActivity"/> auto-blocking is disabled by default and fails open:
///         when disabled (or on any internal error) sources are never blocked, so a monitor
///         malfunction cannot take down legitimate provider traffic.
///     </para>
/// </remarks>
public class WebhookSecuritySettings
{
    /// <summary>
    ///     CIDR networks (IPv4 or IPv6) allowed to call the provider webhook callback endpoints.
    ///     Empty (default) disables the allowlist. When configured, unmatched sources are
    ///     rejected with 403 before the payload is read (fail closed).
    /// </summary>
    public IReadOnlyList<string> SourceIpAllowlist { get; set; } = [];

    /// <summary>
    ///     Threshold-based temporary blocking of webhook sources with repeated security failures.
    ///     Disabled by default; fail-open when disabled.
    /// </summary>
    public WebhookSuspiciousActivitySettings SuspiciousActivity { get; set; } = new();
}

/// <summary>
///     Suspicious-activity auto-blocking thresholds for webhook sources.
/// </summary>
public class WebhookSuspiciousActivitySettings
{
    /// <summary>
    ///     Whether sources exceeding the failure threshold are temporarily blocked.
    ///     Default false (fail open): monitoring is recorded, no blocking happens.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Security failures within <see cref="WindowSeconds"/> that trigger a temporary block.
    /// </summary>
    public int FailureThreshold { get; set; } = 20;

    /// <summary>
    ///     Sliding window (in seconds) in which failures are counted per source.
    /// </summary>
    public int WindowSeconds { get; set; } = 300;

    /// <summary>
    ///     How long (in seconds) a blocked source stays blocked.
    /// </summary>
    public int BlockDurationSeconds { get; set; } = 900;
}
