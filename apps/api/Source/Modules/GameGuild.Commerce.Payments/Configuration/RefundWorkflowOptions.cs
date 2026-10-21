using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Automated refund-workflow tunables (issues #403/#419). Safe defaults: the background
///     workflow is disabled and wallet crediting on partial refunds is off, so refund money
///     movement stays a deliberate deployment decision (matching the retry-queue precedent).
/// </summary>
public sealed class RefundWorkflowOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Refunds:Workflow";

    /// <summary>
    ///     Enables the automated refund workflow: approved refunds are executed and failed
    ///     refunds retried by the background worker (issue #419). Default false — the workflow
    ///     only runs when an operator opts in.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Poll interval of the refund workflow worker, in minutes (issue #419 spec: 5).</summary>
    [Range(1, 1440)]
    public int PollIntervalMinutes { get; set; } = 5;

    /// <summary>
    ///     Total gateway-execution attempts per refund before it is left in
    ///     <see cref="RefundStatus.Failed"/> for manual handling (issue #419 spec: max 3).
    /// </summary>
    [Range(1, 10)]
    public int MaxExecutionAttempts { get; set; } = 3;

    /// <summary>Cooldown before a failed refund is retried by the worker, in minutes (issue #419 spec: hourly).</summary>
    [Range(1, 10080)]
    public int FailedRetryDelayMinutes { get; set; } = 60;

    /// <summary>Maximum refunds processed per worker cycle.</summary>
    [Range(1, 500)]
    public int MaxBatchSize { get; set; } = 50;

    /// <summary>
    ///     Credits the refunded amount to the customer wallet for partial refunds in addition to
    ///     the gateway refund (issue #419 example flow). Default false: this doubles the value the
    ///     customer receives (card refund + wallet credit), so it is strictly opt-in.
    /// </summary>
    public bool CreditWalletOnPartialRefund { get; set; }
}

/// <summary>Validates <see cref="RefundWorkflowOptions"/> bound from configuration.</summary>
public sealed class RefundWorkflowOptionsValidator : IValidateOptions<RefundWorkflowOptions>
{
    public ValidateOptionsResult Validate(string? name, RefundWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.PollIntervalMinutes is < 1 or > 1440)
        {
            failures.Add("PollIntervalMinutes must be between 1 and 1440.");
        }

        if (options.MaxExecutionAttempts is < 1 or > 10)
        {
            failures.Add("MaxExecutionAttempts must be between 1 and 10.");
        }

        if (options.FailedRetryDelayMinutes is < 1 or > 10080)
        {
            failures.Add("FailedRetryDelayMinutes must be between 1 and 10080.");
        }

        if (options.MaxBatchSize is < 1 or > 500)
        {
            failures.Add("MaxBatchSize must be between 1 and 500.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
