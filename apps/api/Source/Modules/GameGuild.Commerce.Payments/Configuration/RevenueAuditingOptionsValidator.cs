using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Validates <see cref="RevenueAuditingOptions" /> ranges (issue #404). The
///     <see cref="System.ComponentModel.DataAnnotations.RangeAttribute" /> values are
///     restated here so misconfiguration fails fast at startup instead of producing
///     nonsensical reconciliation or anomaly windows at runtime.
/// </summary>
public sealed class RevenueAuditingOptionsValidator : IValidateOptions<RevenueAuditingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RevenueAuditingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.MaxStatementLinesPerRun is < 1 or > 10_000)
        {
            failures.Add($"{nameof(options.MaxStatementLinesPerRun)} must be between 1 and 10000.");
        }

        if (options.AnomalyEvaluationDays is < 1 or > 30)
        {
            failures.Add($"{nameof(options.AnomalyEvaluationDays)} must be between 1 and 30.");
        }

        if (options.AnomalyBaselineDays is < 7 or > 730)
        {
            failures.Add($"{nameof(options.AnomalyBaselineDays)} must be between 7 and 730.");
        }

        if (options.AnomalyMinBaselineDays is < 2 or > 60)
        {
            failures.Add($"{nameof(options.AnomalyMinBaselineDays)} must be between 2 and 60.");
        }

        if (options.AnomalyMinBaselineDays > options.AnomalyBaselineDays)
        {
            failures.Add(
                $"{nameof(options.AnomalyMinBaselineDays)} cannot exceed {nameof(options.AnomalyBaselineDays)}.");
        }

        if (options.AnomalyZScoreThreshold < 1m || options.AnomalyZScoreThreshold > 10m)
        {
            failures.Add($"{nameof(options.AnomalyZScoreThreshold)} must be between 1 and 10.");
        }

        if (options.WorkerEnabled && options.WorkerIntervalMinutes is < 5 or > 10_080)
        {
            failures.Add(
                $"{nameof(options.WorkerIntervalMinutes)} must be between 5 and 10080 when the worker is enabled.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
