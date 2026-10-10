namespace GameGuild.Commerce.Billing;

/// <summary>State of the CIDR source allowlist enforced on the provider callback endpoints.</summary>
public sealed record BillingWebhookAllowlistStatusDto
{
    /// <summary>Whether any allowlist entry is configured (fail closed when true).</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Number of configured CIDR entries.</summary>
    public int ConfiguredNetworkCount { get; init; }

    /// <summary>Configured CIDR entries (admin-visible; providers rotate egress ranges).</summary>
    public IReadOnlyList<string> ConfiguredNetworks { get; init; } = [];
}

/// <summary>State of the suspicious-activity auto-blocking monitor.</summary>
public sealed record BillingWebhookSuspiciousActivityStatusDto
{
    /// <summary>Whether threshold blocking is enabled (fail open when false).</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Failures within the window that trigger a temporary block.</summary>
    public int FailureThreshold { get; init; }

    /// <summary>Sliding window (seconds) in which failures are counted per source.</summary>
    public int WindowSeconds { get; init; }

    /// <summary>How long (seconds) a blocked source stays blocked.</summary>
    public int BlockDurationSeconds { get; init; }

    /// <summary>Sources currently blocked, ordered by remaining block duration.</summary>
    public IReadOnlyList<BillingWebhookBlockedSourceDto> BlockedSources { get; init; } = [];
}

/// <summary>A webhook source currently blocked for suspicious activity.</summary>
public sealed record BillingWebhookBlockedSourceDto(
    string SourceKey,
    DateTime BlockedUntilUtc,
    int FailureCount);

/// <summary>
///     Admin-facing summary of the billing webhook security posture: allowlist state,
///     suspicious-activity blocking, and the delivery health of the security event pipeline.
/// </summary>
public sealed record BillingWebhookSecuritySummaryDto
{
    public BillingWebhookAllowlistStatusDto SourceIpAllowlist { get; init; } = new();

    public BillingWebhookSuspiciousActivityStatusDto SuspiciousActivity { get; init; } = new();

    /// <summary>UTC moment the summary was produced.</summary>
    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>Durable delivery status of the central security event pipeline.</summary>
    public GameGuild.Compliance.Audit.SecurityEventDeliveryStatusResponse? SecurityEventPipeline { get; init; }

    /// <summary>Open (unacknowledged) security alerts raised by the pipeline.</summary>
    public IReadOnlyList<BillingWebhookSecurityAlertDto> OpenSecurityAlerts { get; init; } = [];
}

/// <summary>A security alert surfaced from the Compliance.Audit pipeline.</summary>
public sealed record BillingWebhookSecurityAlertDto(
    Guid Id,
    string RuleId,
    string Kind,
    string Severity,
    string Title,
    DateTime? RaisedAtUtc);
