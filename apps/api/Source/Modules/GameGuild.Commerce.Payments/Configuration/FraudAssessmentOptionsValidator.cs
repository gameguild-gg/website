using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Startup validation for <see cref="FraudAssessmentOptions"/> (issue #409): the gate
///     must never start with an unusable threshold/weight configuration.
/// </summary>
public sealed class FraudAssessmentOptionsValidator : IValidateOptions<FraudAssessmentOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, FraudAssessmentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.ReviewThreshold >= options.BlockThreshold)
        {
            failures.Add("Payments:FraudAssessment ReviewThreshold must be strictly lower than BlockThreshold.");
        }

        if (options.MaxSignalScore > options.BlockThreshold)
        {
            failures.Add("Payments:FraudAssessment MaxSignalScore must not exceed BlockThreshold so a single signal can never block a payment on its own.");
        }

        if (options.CardTestingAmountThreshold < 0)
        {
            failures.Add("Payments:FraudAssessment CardTestingAmountThreshold must not be negative.");
        }

        if (options.NewCustomerHighValueAmountThreshold < 0)
        {
            failures.Add("Payments:FraudAssessment NewCustomerHighValueAmountThreshold must not be negative.");
        }

        if (options.BehaviorAnomalyAmountMultiplier < 1.0m)
        {
            failures.Add("Payments:FraudAssessment BehaviorAnomalyAmountMultiplier must be at least 1.0.");
        }

        if (options.BehaviorAnomalyMinHistory < 1)
        {
            failures.Add("Payments:FraudAssessment BehaviorAnomalyMinHistory must be at least 1.");
        }

        if (options.TenantOverrides.Count == 0)
        {
            return failures.Count > 0
                ? ValidateOptionsResult.Fail(failures)
                : ValidateOptionsResult.Success;
        }

        foreach (var (tenantKey, overrideValues) in options.TenantOverrides)
        {
            if (!Guid.TryParse(tenantKey, out _))
            {
                failures.Add($"Payments:FraudAssessment TenantOverrides key '{tenantKey}' is not a valid GUID.");
                continue;
            }

            var resolved = options.ResolveFor(Guid.Parse(tenantKey));
            if (resolved.ReviewThreshold >= resolved.BlockThreshold)
            {
                failures.Add($"Payments:FraudAssessment override for tenant '{tenantKey}' has ReviewThreshold >= BlockThreshold.");
            }

            if (resolved.MaxSignalScore > resolved.BlockThreshold)
            {
                failures.Add($"Payments:FraudAssessment override for tenant '{tenantKey}' has MaxSignalScore > BlockThreshold.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
