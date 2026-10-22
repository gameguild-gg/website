using System.ComponentModel.DataAnnotations;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Tenant-scoped tunables for the payment fraud assessment gate (issue #409).
///     Defaults are conservative: ordinary first-time customers stay well below the
///     review threshold, and the block threshold requires repeated or disputed abuse.
///     Per-tenant overrides can be supplied through <see cref="TenantOverrides"/>.
/// </summary>
public sealed class FraudAssessmentOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:FraudAssessment";

    /// <summary>Enables the pre-gateway fraud assessment. Safe default: enabled; an
    /// assessment with no triggering signals costs one indexed query and allows the payment.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Velocity look-back window for all payment-based signals, in minutes.</summary>
    [Range(1, 1440)]
    public int VelocityWindowMinutes { get; set; } = 10;

    /// <summary>Maximum prior non-cancelled, non-fraud-rejected payments allowed in the
    /// window before the payment-frequency signal starts contributing score.</summary>
    [Range(0, 100)]
    public int MaxPaymentsInWindow { get; set; } = 3;

    /// <summary>Maximum prior gateway-failed payments allowed in the window before the
    /// failed-attempts signal starts contributing score.</summary>
    [Range(0, 100)]
    public int MaxFailedAttemptsInWindow { get; set; } = 2;

    /// <summary>Score contributed per payment above <see cref="MaxPaymentsInWindow"/>,
    /// capped per signal at <see cref="MaxSignalScore"/>.</summary>
    [Range(1, 100)]
    public int PaymentFrequencyWeight { get; set; } = 15;

    /// <summary>Score contributed per failed attempt above <see cref="MaxFailedAttemptsInWindow"/>,
    /// capped per signal at <see cref="MaxSignalScore"/>.</summary>
    [Range(1, 100)]
    public int FailedAttemptWeight { get; set; } = 20;

    /// <summary>Score contributed when the presented payment method was never seen before
    /// for this tenant+customer ("new device / new method" signal).</summary>
    [Range(0, 100)]
    public int NewPaymentMethodWeight { get; set; } = 25;

    /// <summary>Score contributed per disputed prior payment in the window,
    /// capped per signal at <see cref="MaxSignalScore"/>.</summary>
    [Range(0, 100)]
    public int DisputedPaymentWeight { get; set; } = 35;

    /// <summary>Per-signal score cap so a single signal can never block on its own.</summary>
    [Range(1, 100)]
    public int MaxSignalScore { get; set; } = 50;

    /// <summary>Amounts at or below this value are treated as micro-amounts for the
    /// card-testing pattern signal (issue #409: small rapid transactions).</summary>
    [Range(0, 1000)]
    public decimal CardTestingAmountThreshold { get; set; } = 5m;

    /// <summary>Prior micro-amount attempts allowed in the window before the card-testing
    /// pattern signal fires.</summary>
    [Range(0, 100)]
    public int CardTestingPriorSmallAttemptsThreshold { get; set; } = 3;

    /// <summary>Score contributed when the card-testing pattern signal fires.</summary>
    [Range(0, 100)]
    public int CardTestingWeight { get; set; } = 40;

    /// <summary>Amount at or above which a first-cycle charge counts as high-value for the
    /// account-age signal (issue #409: new customer + high-value purchase).</summary>
    [Range(0, 1000000)]
    public decimal NewCustomerHighValueAmountThreshold { get; set; } = 500m;

    /// <summary>Score contributed when a first-cycle (new-customer) charge is high-value.</summary>
    [Range(0, 100)]
    public int NewCustomerHighValueWeight { get; set; } = 30;

    /// <summary>Minimum number of prior successful charges before the behavior-anomaly
    /// signal compares the current amount against the customer's baseline (issue #409).</summary>
    [Range(1, 100)]
    public int BehaviorAnomalyMinHistory { get; set; } = 2;

    /// <summary>Multiplier of the customer's baseline average amount at which the current
    /// charge is flagged as a behavioral anomaly.</summary>
    [Range(1.0, 100.0)]
    public decimal BehaviorAnomalyAmountMultiplier { get; set; } = 5m;

    /// <summary>Score contributed when the behavior-anomaly signal fires.</summary>
    [Range(0, 100)]
    public int BehaviorAnomalyWeight { get; set; } = 25;

    /// <summary>Aggregate score at or above which the gate decision becomes <see cref="FraudDecision.Review"/>.</summary>
    [Range(1, 100)]
    public int ReviewThreshold { get; set; } = 40;

    /// <summary>Aggregate score at or above which the gate decision becomes <see cref="FraudDecision.Block"/>.</summary>
    [Range(2, 100)]
    public int BlockThreshold { get; set; } = 70;

    /// <summary>Velocity look-back window as a time span.</summary>
    public TimeSpan VelocityWindow => TimeSpan.FromMinutes(VelocityWindowMinutes);

    /// <summary>Optional per-tenant overrides keyed by tenant id (GUID, canonical format).
    /// Only the supplied properties replace the defaults; everything else is inherited.</summary>
    public Dictionary<string, FraudAssessmentOverride> TenantOverrides { get; set; } = [];

    /// <summary>
    ///     Resolves the effective options for a tenant by overlaying the configured
    ///     per-tenant override (if any) onto the defaults.
    /// </summary>
    public FraudAssessmentOptions ResolveFor(Guid tenantId)
    {
        if (!TenantOverrides.TryGetValue(tenantId.ToString(), out var overrideValues))
        {
            return this;
        }

        var resolved = new FraudAssessmentOptions
        {
            IsEnabled = overrideValues.IsEnabled ?? IsEnabled,
            VelocityWindowMinutes = overrideValues.VelocityWindowMinutes ?? VelocityWindowMinutes,
            MaxPaymentsInWindow = overrideValues.MaxPaymentsInWindow ?? MaxPaymentsInWindow,
            MaxFailedAttemptsInWindow = overrideValues.MaxFailedAttemptsInWindow ?? MaxFailedAttemptsInWindow,
            PaymentFrequencyWeight = overrideValues.PaymentFrequencyWeight ?? PaymentFrequencyWeight,
            FailedAttemptWeight = overrideValues.FailedAttemptWeight ?? FailedAttemptWeight,
            NewPaymentMethodWeight = overrideValues.NewPaymentMethodWeight ?? NewPaymentMethodWeight,
            DisputedPaymentWeight = overrideValues.DisputedPaymentWeight ?? DisputedPaymentWeight,
            MaxSignalScore = overrideValues.MaxSignalScore ?? MaxSignalScore,
            CardTestingAmountThreshold = overrideValues.CardTestingAmountThreshold ?? CardTestingAmountThreshold,
            CardTestingPriorSmallAttemptsThreshold = overrideValues.CardTestingPriorSmallAttemptsThreshold ?? CardTestingPriorSmallAttemptsThreshold,
            CardTestingWeight = overrideValues.CardTestingWeight ?? CardTestingWeight,
            NewCustomerHighValueAmountThreshold = overrideValues.NewCustomerHighValueAmountThreshold ?? NewCustomerHighValueAmountThreshold,
            NewCustomerHighValueWeight = overrideValues.NewCustomerHighValueWeight ?? NewCustomerHighValueWeight,
            BehaviorAnomalyMinHistory = overrideValues.BehaviorAnomalyMinHistory ?? BehaviorAnomalyMinHistory,
            BehaviorAnomalyAmountMultiplier = overrideValues.BehaviorAnomalyAmountMultiplier ?? BehaviorAnomalyAmountMultiplier,
            BehaviorAnomalyWeight = overrideValues.BehaviorAnomalyWeight ?? BehaviorAnomalyWeight,
            ReviewThreshold = overrideValues.ReviewThreshold ?? ReviewThreshold,
            BlockThreshold = overrideValues.BlockThreshold ?? BlockThreshold
        };

        return resolved;
    }
}

/// <summary>Per-tenant override for <see cref="FraudAssessmentOptions"/>; unset properties inherit the defaults.</summary>
public sealed class FraudAssessmentOverride
{
    /// <summary>See <see cref="FraudAssessmentOptions.IsEnabled"/>.</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.VelocityWindowMinutes"/>.</summary>
    [Range(1, 1440)]
    public int? VelocityWindowMinutes { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.MaxPaymentsInWindow"/>.</summary>
    [Range(0, 100)]
    public int? MaxPaymentsInWindow { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.MaxFailedAttemptsInWindow"/>.</summary>
    [Range(0, 100)]
    public int? MaxFailedAttemptsInWindow { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.PaymentFrequencyWeight"/>.</summary>
    [Range(1, 100)]
    public int? PaymentFrequencyWeight { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.FailedAttemptWeight"/>.</summary>
    [Range(1, 100)]
    public int? FailedAttemptWeight { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.NewPaymentMethodWeight"/>.</summary>
    [Range(0, 100)]
    public int? NewPaymentMethodWeight { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.DisputedPaymentWeight"/>.</summary>
    [Range(0, 100)]
    public int? DisputedPaymentWeight { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.MaxSignalScore"/>.</summary>
    [Range(1, 100)]
    public int? MaxSignalScore { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.CardTestingAmountThreshold"/>.</summary>
    [Range(0, 1000)]
    public decimal? CardTestingAmountThreshold { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.CardTestingPriorSmallAttemptsThreshold"/>.</summary>
    [Range(0, 100)]
    public int? CardTestingPriorSmallAttemptsThreshold { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.CardTestingWeight"/>.</summary>
    [Range(0, 100)]
    public int? CardTestingWeight { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.NewCustomerHighValueAmountThreshold"/>.</summary>
    [Range(0, 1000000)]
    public decimal? NewCustomerHighValueAmountThreshold { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.NewCustomerHighValueWeight"/>.</summary>
    [Range(0, 100)]
    public int? NewCustomerHighValueWeight { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.BehaviorAnomalyMinHistory"/>.</summary>
    [Range(1, 100)]
    public int? BehaviorAnomalyMinHistory { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.BehaviorAnomalyAmountMultiplier"/>.</summary>
    [Range(1.0, 100.0)]
    public decimal? BehaviorAnomalyAmountMultiplier { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.BehaviorAnomalyWeight"/>.</summary>
    [Range(0, 100)]
    public int? BehaviorAnomalyWeight { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.ReviewThreshold"/>.</summary>
    [Range(1, 100)]
    public int? ReviewThreshold { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.BlockThreshold"/>.</summary>
    [Range(2, 100)]
    public int? BlockThreshold { get; set; }
}
