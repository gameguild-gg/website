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

    /// <summary>See <see cref="FraudAssessmentOptions.ReviewThreshold"/>.</summary>
    [Range(1, 100)]
    public int? ReviewThreshold { get; set; }

    /// <summary>See <see cref="FraudAssessmentOptions.BlockThreshold"/>.</summary>
    [Range(2, 100)]
    public int? BlockThreshold { get; set; }
}
