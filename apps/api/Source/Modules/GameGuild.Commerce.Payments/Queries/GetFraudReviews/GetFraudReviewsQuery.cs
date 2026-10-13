using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Query for the manual fraud-review queue (issue #409): payments whose fraud decision
///     is <see cref="FraudDecision.Review"/> and whose review is still pending. Requires the
///     <c>payments:admin</c> permission.
/// </summary>
/// <param name="TenantId">Optional tenant scope; enforced at the API boundary from the actor context.</param>
/// <param name="Page">Page number for pagination (1-based).</param>
/// <param name="PageSize">Page size (1-100).</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record GetFraudReviewsQuery(Guid? TenantId = null, int Page = 1, int PageSize = 20)
    : IQuery<IEnumerable<FraudReviewListItem>>;

/// <summary>Read model for one pending fraud review.</summary>
public sealed record FraudReviewListItem
{
    /// <summary>Tenant that owns the payment.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Payment awaiting review.</summary>
    public required Guid PaymentId { get; init; }

    /// <summary>Subscription the payment was charged for.</summary>
    public Guid? SubscriptionId { get; init; }

    /// <summary>Provider customer reference, when bound.</summary>
    public string? ExternalCustomerId { get; init; }

    /// <summary>Charged amount.</summary>
    public required decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; init; }

    /// <summary>Aggregate fraud score (0-100).</summary>
    public required int Score { get; init; }

    /// <summary>Risk band for the score.</summary>
    public required FraudRiskLevel RiskLevel { get; init; }

    /// <summary>Bounded decision reason.</summary>
    public required string Reason { get; init; }

    /// <summary>Bounded JSON signal evidence bundle.</summary>
    public string? Evidence { get; init; }

    /// <summary>When the payment attempt was created.</summary>
    public required DateTime CreatedAt { get; init; }
}
