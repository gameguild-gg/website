namespace GameGuild.Identity.Authorization;

/// <summary>
///     Configuration for the permission evaluation engine (issue #358). Groups the
///     engine's optional capabilities: role inheritance rules, permission-change
///     webhooks, evaluation-layer throttling, restoration retention and external
///     synchronization limits. Every capability is config-gated and fails closed.
/// </summary>
public sealed class PermissionEngineOptions
{
    /// <summary>
    ///     The configuration section name (<c>PermissionEngine</c>).
    /// </summary>
    public const string SectionName = "PermissionEngine";

    /// <summary>
    ///     Role inheritance rules (multi-parent traversal depth, kill switch).
    /// </summary>
    public PermissionInheritanceOptions Inheritance { get; set; } = new();

    /// <summary>
    ///     Webhook notifications for permission changes. Disabled unless an endpoint
    ///     and secret are configured.
    /// </summary>
    public PermissionWebhookOptions Webhooks { get; set; } = new();

    /// <summary>
    ///     Evaluation-layer rate limiting (enumeration protection for repeated deny
    ///     evaluations per user and tenant). Distinct from endpoint rate limiting.
    /// </summary>
    public EvaluationThrottleOptions EvaluationThrottle { get; set; } = new();

    /// <summary>
    ///     Permission restoration retention window.
    /// </summary>
    public PermissionRestorationOptions Restoration { get; set; } = new();

    /// <summary>
    ///     External system permission synchronization limits.
    /// </summary>
    public PermissionSyncOptions ExternalSync { get; set; } = new();
}

/// <summary>
///     Configurable rules for dynamic-role permission inheritance.
/// </summary>
public sealed class PermissionInheritanceOptions
{
    /// <summary>
    ///     Global kill switch for role-permission inheritance. When <c>false</c>, only
    ///     directly assigned role permissions contribute — nothing flows from parents.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Maximum inheritance graph depth traversed per role (cycle guard for
    ///     multi-parent hierarchies). Depth 1 = direct parents only.
    /// </summary>
    public int MaxDepth { get; set; } = 10;

    /// <summary>
    ///     Validates the configuration (positive depth).
    /// </summary>
    public void Validate()
    {
        if (MaxDepth < 1)
        {
            throw new InvalidOperationException("PermissionEngine:Inheritance:MaxDepth must be at least 1.");
        }
    }
}

/// <summary>
///     Webhook notification settings for permission changes. Notifications are only
///     sent when <see cref="Enabled"/> is true and a non-empty <see cref="Endpoint"/>
///     and <see cref="Secret"/> are configured; otherwise the notifier is a no-op (fail closed).
/// </summary>
public sealed class PermissionWebhookOptions
{
    /// <summary>Whether webhook notifications are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>The HTTPS endpoint that receives signed permission-change payloads.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Shared secret used to compute the HMAC-SHA256 signature header.</summary>
    public string? Secret { get; set; }

    /// <summary>Maximum delivery attempts (initial attempt included).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base delay between retries (exponential backoff, per attempt).</summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 200;

    /// <summary>Per-attempt HTTP timeout.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    ///     True when notifications can be delivered: enabled, endpoint and secret present.
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Secret);
}

/// <summary>
///     Evaluation-layer throttle settings: protection against permission enumeration
///     through repeated denied evaluations for the same user and tenant. When the deny
///     count within the window exceeds the limit, further evaluations for that
///     user+tenant pair fail closed (deny) until the throttle window elapses.
/// </summary>
public sealed class EvaluationThrottleOptions
{
    /// <summary>Whether evaluation-layer throttling is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum denied evaluations per user+tenant inside the sliding window.</summary>
    public int MaxDeniedEvaluationsPerWindow { get; set; } = 200;

    /// <summary>Sliding window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>How long a tripped throttle keeps failing closed, in seconds.</summary>
    public int ThrottleDurationSeconds { get; set; } = 60;

    /// <summary>
    ///     Validates the configuration (positive limits and windows).
    /// </summary>
    public void Validate()
    {
        if (MaxDeniedEvaluationsPerWindow < 1)
        {
            throw new InvalidOperationException("PermissionEngine:EvaluationThrottle:MaxDeniedEvaluationsPerWindow must be at least 1.");
        }

        if (WindowSeconds < 1)
        {
            throw new InvalidOperationException("PermissionEngine:EvaluationThrottle:WindowSeconds must be at least 1.");
        }

        if (ThrottleDurationSeconds < 1)
        {
            throw new InvalidOperationException("PermissionEngine:EvaluationThrottle:ThrottleDurationSeconds must be at least 1.");
        }
    }
}

/// <summary>
///     Permission restoration settings (issue #358: undo a grant/revoke within a
///     retention window).
/// </summary>
public sealed class PermissionRestorationOptions
{
    /// <summary>
    ///     How long a soft-deleted permission or an audit-log entry stays restorable.
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    ///     Validates the configuration (non-negative retention).
    /// </summary>
    public void Validate()
    {
        if (RetentionDays < 0)
        {
            throw new InvalidOperationException("PermissionEngine:Restoration:RetentionDays cannot be negative.");
        }
    }
}

/// <summary>
///     External system permission synchronization limits.
/// </summary>
public sealed class PermissionSyncOptions
{
    /// <summary>Maximum roles accepted in one import document (fail-closed above the cap).</summary>
    public int MaxRolesPerImport { get; set; } = 500;

    /// <summary>Maximum permission rows accepted in one import document.</summary>
    public int MaxPermissionEntriesPerImport { get; set; } = 5000;

    /// <summary>
    ///     Validates the configuration (positive caps).
    /// </summary>
    public void Validate()
    {
        if (MaxRolesPerImport < 1)
        {
            throw new InvalidOperationException("PermissionEngine:ExternalSync:MaxRolesPerImport must be at least 1.");
        }

        if (MaxPermissionEntriesPerImport < 1)
        {
            throw new InvalidOperationException("PermissionEngine:ExternalSync:MaxPermissionEntriesPerImport must be at least 1.");
        }
    }
}
