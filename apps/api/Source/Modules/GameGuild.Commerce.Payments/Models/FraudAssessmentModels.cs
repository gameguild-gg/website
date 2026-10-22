using System.Text.Json;

namespace GameGuild.Commerce.Payments;

/// <summary>Outcome of the fraud gate for a payment attempt.</summary>
public enum FraudDecision
{
    /// <summary>Transaction allowed through the gate.</summary>
    Allow = 0,

    /// <summary>Transaction held for manual review; it fails without gateway execution.</summary>
    Review = 1,

    /// <summary>Transaction blocked; it fails without gateway execution and is never retried.</summary>
    Block = 2
}

/// <summary>Risk band derived from the aggregate fraud score.</summary>
public enum FraudRiskLevel
{
    /// <summary>Score 0-30.</summary>
    Low = 0,

    /// <summary>Score 31-60.</summary>
    Medium = 1,

    /// <summary>Score 61-85.</summary>
    High = 2,

    /// <summary>Score 86-100.</summary>
    Critical = 3
}

/// <summary>Manual review state for a payment whose fraud decision was <see cref="FraudDecision.Review"/>.</summary>
public enum FraudReviewStatus
{
    /// <summary>Waiting for an administrator to approve or decline the review.</summary>
    PendingReview = 0,

    /// <summary>An administrator approved the payment; the fraud hold is released.</summary>
    Approved = 1,

    /// <summary>An administrator declined the payment; it stays failed and non-retryable.</summary>
    Declined = 2
}

/// <summary>
///     A single fraud indicator detected for a payment attempt, with its bounded score
///     contribution and a small JSON detail payload used as assessment evidence.
/// </summary>
/// <param name="SignalType">Stable signal identifier, e.g. <c>Velocity.PaymentFrequency</c>.</param>
/// <param name="Weight">Score contribution (0-100 per signal; the aggregate is clamped to 0-100).</param>
/// <param name="Details">Bounded JSON details describing why the signal fired.</param>
public sealed record FraudSignal(string SignalType, int Weight, string Details);

/// <summary>The payment attempt being assessed, scoped to one tenant.</summary>
/// <param name="TenantId">Tenant the attempt belongs to; all signal queries are scoped to it.</param>
/// <param name="PaymentId">Payment row created for this attempt; excluded from its own velocity counts.</param>
/// <param name="SubscriptionId">Subscription being charged; customer fallback for unbound attempts.</param>
/// <param name="ExternalCustomerId">Provider customer id when bound; may be null for unbound attempts.</param>
/// <param name="PaymentMethodId">Payment method presented for this attempt.</param>
/// <param name="Amount">Authoritative amount from the subscription context.</param>
/// <param name="Currency">Authoritative currency from the subscription context.</param>
/// <param name="BillingCycleNumber">
///     Billing cycle this attempt settles (1 = the subscription's first-ever charge). Used
///     as the customer-tenure proxy for the account-age fraud signal (issue #409).
/// </param>
public sealed record FraudSignalContext(
    Guid TenantId,
    Guid PaymentId,
    Guid SubscriptionId,
    string? ExternalCustomerId,
    string? PaymentMethodId,
    decimal Amount,
    string Currency,
    int BillingCycleNumber = 1);

/// <summary>Aggregate fraud assessment for one payment attempt.</summary>
/// <param name="TenantId">Tenant the assessment belongs to.</param>
/// <param name="PaymentId">Payment row assessed.</param>
/// <param name="Score">Aggregate score clamped to 0-100.</param>
/// <param name="RiskLevel">Risk band derived from <paramref name="Score"/>.</param>
/// <param name="Decision">Gate decision (Allow / Review / Block).</param>
/// <param name="Reason">Human-readable decision reason (bounded).</param>
/// <param name="Signals">Signal evidence bundle backing the score.</param>
/// <param name="AssessedAt">Assessment timestamp (UTC).</param>
public sealed record FraudAssessment(
    Guid TenantId,
    Guid PaymentId,
    int Score,
    FraudRiskLevel RiskLevel,
    FraudDecision Decision,
    string Reason,
    IReadOnlyList<FraudSignal> Signals,
    DateTime AssessedAt)
{
    /// <summary>Maximum length of the serialized evidence bundle persisted on the payment.</summary>
    public const int MaxEvidenceJsonLength = 4000;

    /// <summary>Serializes the signal evidence bundle, bounded to <see cref="MaxEvidenceJsonLength"/> characters.</summary>
    public string ToEvidenceJson()
    {
        var payload = new
        {
            assessedAt = AssessedAt,
            score = Score,
            riskLevel = RiskLevel.ToString(),
            decision = Decision.ToString(),
            reason = Reason,
            signals = Signals.Select(signal => new
            {
                type = signal.SignalType,
                weight = signal.Weight,
                details = signal.Details
            })
        };

        var json = JsonSerializer.Serialize(payload, JsonSerializerOptions.Default);
        return json.Length <= MaxEvidenceJsonLength
            ? json
            : json[..MaxEvidenceJsonLength];
    }

    /// <summary>Maps a numeric score to its risk band.</summary>
    public static FraudRiskLevel RiskLevelForScore(int score) => score switch
    {
        <= 30 => FraudRiskLevel.Low,
        <= 60 => FraudRiskLevel.Medium,
        <= 85 => FraudRiskLevel.High,
        _ => FraudRiskLevel.Critical
    };
}
