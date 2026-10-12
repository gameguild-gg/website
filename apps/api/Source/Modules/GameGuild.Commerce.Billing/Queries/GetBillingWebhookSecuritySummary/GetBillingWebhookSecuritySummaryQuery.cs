using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Query for the admin-facing billing webhook security summary: allowlist state,
///     suspicious-activity blocking, and the security event pipeline delivery health.
/// </summary>
public sealed record GetBillingWebhookSecuritySummaryQuery : IQuery<BillingWebhookSecuritySummaryDto>
{
    /// <summary>
    ///     Cache key for the short-TTL query cache of the platform-wide webhook security
    ///     summary (issue #394). Shared with <see cref="WebhookSecurityEventPublisher"/>, which
    ///     evicts the entry whenever a webhook security event is published.
    /// </summary>
    public const string CacheKey = "billing:queries:webhook-security-summary:v1";

    /// <summary>
    ///     Time-to-live of the cached summary. The summary is a SystemAdmin monitoring surface
    ///     that tolerates seconds of staleness; the TTL bounds how long a security event can be
    ///     absent from the summary when no eviction hook fires.
    /// </summary>
    public static readonly TimeSpan CacheTimeToLive = TimeSpan.FromSeconds(15);
}
