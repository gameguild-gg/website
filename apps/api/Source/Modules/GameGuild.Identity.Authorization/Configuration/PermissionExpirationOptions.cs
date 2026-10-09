namespace GameGuild.Identity.Authorization;

/// <summary>
///     Configurable expiration defaults for permission grants.
///     Supports a global default, per-permission-type overrides (exact key or
///     <c>prefix:*</c> wildcard), and the upcoming-expiration notification window.
/// </summary>
public sealed class PermissionExpirationOptions
{
    public static string SectionName => "Authorization:PermissionExpiration";

    /// <summary>
    ///     Master switch for the expiration background worker. When false, no automatic
    ///     cleanup or reminder publishing happens (administrators can still trigger
    ///     processing through the tenant-permission endpoints).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Delay before the first worker cycle after startup, so the host can finish
    ///     warming up before expiration sweeps start.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Spacing between two worker cycles.
    /// </summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Upper bound for a single worker cycle; a cycle that exceeds it is abandoned
    ///     and retried on the next interval.
    /// </summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Maximum number of grants processed by a single worker phase (expired cleanup
    ///     or upcoming reminders). Keeps cycles bounded on large tenants.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    ///     How far ahead the background worker looks when publishing
    ///     <see cref="PermissionExpirationNotification">upcoming expiration notifications</see>.
    /// </summary>
    public TimeSpan UpcomingNotificationWindow { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    ///     Minimum spacing between two upcoming-expiration reminders for the same grant
    ///     (deduplicated via the grant's <c>Metadata["expirationReminderAt"]</c> stamp).
    /// </summary>
    public TimeSpan MinimumReminderInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Default expiration period applied when a caller does not supply an explicit
    ///     <c>ExpiresAt</c> and per-permission defaults do not match. <c>null</c> = no default.
    /// </summary>
    public TimeSpan? DefaultGrantExpiration { get; set; }

    /// <summary>
    ///     When true, grants issued without an explicit expiration receive the configured
    ///     default period. Opt-in so existing permanent grants keep their semantics.
    /// </summary>
    public bool ApplyDefaultsOnGrant { get; set; }

    /// <summary>
    ///     Default expiration periods per permission type. Keys are either exact permission
    ///     strings (e.g. <c>courses:create</c>) or wildcards (e.g. <c>courses:*</c>); the
    ///     most specific (longest wildcard prefix, then exact) match wins.
    /// </summary>
    public Dictionary<string, TimeSpan> DefaultExpirationByPermission { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Resolves the default expiration period for a grant carrying the given permissions.
    ///     Exact permission match wins, then the longest wildcard prefix, then the global default.
    /// </summary>
    public TimeSpan? ResolveDefaultExpiration(IEnumerable<string>? permissions)
    {
        if (permissions is not null)
        {
            TimeSpan? bestWildcard = null;
            var bestPrefixLength = -1;

            foreach (var permission in permissions)
            {
                if (string.IsNullOrWhiteSpace(permission))
                {
                    continue;
                }

                if (DefaultExpirationByPermission.TryGetValue(permission, out var exact))
                {
                    return exact;
                }
            }

            foreach (var (key, period) in DefaultExpirationByPermission)
            {
                if (!key.EndsWith(":*", StringComparison.Ordinal))
                {
                    continue;
                }

                var prefix = key[..^1];
                if (prefix.Length <= bestPrefixLength)
                {
                    continue;
                }

                if (permissions.Any(p =>
                        p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    bestWildcard = period;
                    bestPrefixLength = prefix.Length;
                }
            }

            if (bestWildcard is not null)
            {
                return bestWildcard;
            }
        }

        return DefaultGrantExpiration;
    }

    /// <summary>
    ///     Validates bounds. Returns a list of error messages (empty when valid).
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (InitialDelay < TimeSpan.Zero)
        {
            errors.Add("InitialDelay must not be negative.");
        }

        if (ScanInterval <= TimeSpan.Zero)
        {
            errors.Add("ScanInterval must be positive.");
        }

        if (ExecutionTimeout <= TimeSpan.Zero)
        {
            errors.Add("ExecutionTimeout must be positive.");
        }

        if (BatchSize <= 0)
        {
            errors.Add("BatchSize must be at least 1.");
        }

        if (UpcomingNotificationWindow < TimeSpan.Zero || UpcomingNotificationWindow > TimeSpan.FromDays(365))
        {
            errors.Add("UpcomingNotificationWindow must be between zero and 365 days.");
        }

        if (MinimumReminderInterval < TimeSpan.Zero || MinimumReminderInterval > TimeSpan.FromDays(365))
        {
            errors.Add("MinimumReminderInterval must be between zero and 365 days.");
        }

        if (DefaultGrantExpiration is { } globalDefault && globalDefault <= TimeSpan.Zero)
        {
            errors.Add("DefaultGrantExpiration must be positive.");
        }

        foreach (var (key, period) in DefaultExpirationByPermission)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("DefaultExpirationByPermission keys must be non-empty permission names.");
                break;
            }

            if (period <= TimeSpan.Zero)
            {
                errors.Add($"DefaultExpirationByPermission['{key}'] must be positive.");
            }
        }

        return errors;
    }
}
