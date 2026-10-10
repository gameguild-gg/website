using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Query for the admin-facing billing webhook security summary: allowlist state,
///     suspicious-activity blocking, and the security event pipeline delivery health.
/// </summary>
public sealed record GetBillingWebhookSecuritySummaryQuery : IQuery<BillingWebhookSecuritySummaryDto>;
