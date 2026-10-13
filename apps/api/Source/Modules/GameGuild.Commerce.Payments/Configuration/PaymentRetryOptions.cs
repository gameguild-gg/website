using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Configurable payment retry schedule (issue #403), bound from <c>Payments:Retry</c>.
///     Defaults reproduce the legacy hardcoded schedule with the retry budget raised to the
///     issue-#415 default of 4 attempts: an exponential backoff of
///     <c>Math.Pow(5, RetryCount)</c> minutes (1, 5, 25, ...).
///     The entity keeps these defaults as fallback; handlers and services resolve the
///     configured options and pass them into <c>Payment.Create</c> / <c>Payment.MarkAsFailed</c>.
/// </summary>
public sealed class PaymentRetryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Retry";

    /// <summary>Maximum number of retry attempts allowed per payment (issue #415 default: 4).</summary>
    [Range(0, 20)]
    public int MaxRetries { get; set; } = 4;

    /// <summary>
    ///     Backoff delay applied to the first failure, in minutes. Combined with
    ///     <see cref="BackoffMultiplier" /> this reproduces the legacy
    ///     <c>Math.Pow(5, RetryCount)</c> schedule (1, 5, 25 minutes, ...).
    /// </summary>
    [Range(0.0, 1440.0)]
    public double BackoffBaseMinutes { get; set; } = 1.0;

    /// <summary>Exponential factor applied per consecutive retry attempt.</summary>
    [Range(1.0, 60.0)]
    public double BackoffMultiplier { get; set; } = 5.0;

    /// <summary>
    ///     Fraction of the computed backoff delay added as random jitter (issue #415), bounded
    ///     to <c>[0, 0.5]</c>. Applied by <see cref="PaymentRetryPolicyResolver" /> on top of the
    ///     delay so simultaneously-failing payments spread out instead of thundering the gateway
    ///     on the same tick. <c>0</c> disables jitter.
    /// </summary>
    [Range(0.0, 0.5)]
    public double JitterRatio { get; set; } = 0.2;

    /// <summary>
    ///     Computes the backoff delay (in minutes) before the next retry attempt,
    ///     using <c>BackoffBaseMinutes * Math.Pow(BackoffMultiplier, retryCount)</c>.
    ///     Jitter is applied on top of this value by the retry policy resolver.
    /// </summary>
    public double ComputeBackoffDelayMinutes(int retryCount)
    {
        if (retryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count cannot be negative.");
        }

        return BackoffBaseMinutes * Math.Pow(BackoffMultiplier, retryCount);
    }
}

/// <summary>Startup validation for <see cref="PaymentRetryOptions" />; fails fast on out-of-range values.</summary>
public sealed class PaymentRetryOptionsValidator : IValidateOptions<PaymentRetryOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentRetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxRetries is < 0 or > 20)
        {
            return ValidateOptionsResult.Fail($"{nameof(PaymentRetryOptions.MaxRetries)} must be between 0 and 20.");
        }

        if (options.BackoffBaseMinutes is < 0.0 or > 1440.0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PaymentRetryOptions.BackoffBaseMinutes)} must be between 0 and 1440 minutes.");
        }

        if (options.BackoffMultiplier is < 1.0 or > 60.0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PaymentRetryOptions.BackoffMultiplier)} must be between 1 and 60.");
        }

        if (options.JitterRatio is < 0.0 or > 0.5)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PaymentRetryOptions.JitterRatio)} must be between 0 and 0.5.");
        }

        return ValidateOptionsResult.Success;
    }
}

/// <summary>
///     Automated payment retry queue configuration (issue #415), bound from
///     <c>Payments:RetryQueue</c>. The retry queue is opt-in: enabling it starts an hourly
///     (by default) poll of <c>PaymentRepository.GetDueForRetryAsync</c> that dispatches due
///     payments through the existing <c>RetryPaymentCommand</c> retry path. The safe default is
///     disabled, matching the sibling revenue-auditing worker — automatic charging must be a
///     deliberate deployment decision.
/// </summary>
public sealed class PaymentRetryQueueOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:RetryQueue";

    /// <summary>Whether the automated retry queue runs. Default <c>false</c> (opt-in).</summary>
    public bool Enabled { get; set; }

    /// <summary>Minutes between retry-queue polling cycles. Default 60 (hourly, per issue #415).</summary>
    [Range(1, 1440)]
    public int PollIntervalMinutes { get; set; } = 60;

    /// <summary>Maximum payments dispatched per polling cycle. Default 100.</summary>
    [Range(1, 500)]
    public int MaxBatchSize { get; set; } = 100;
}

/// <summary>Startup validation for <see cref="PaymentRetryQueueOptions" />.</summary>
public sealed class PaymentRetryQueueOptionsValidator : IValidateOptions<PaymentRetryQueueOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentRetryQueueOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PollIntervalMinutes is < 1 or > 1440)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PaymentRetryQueueOptions.PollIntervalMinutes)} must be between 1 and 1440 minutes.");
        }

        if (options.MaxBatchSize is < 1 or > 500)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PaymentRetryQueueOptions.MaxBatchSize)} must be between 1 and 500.");
        }

        return ValidateOptionsResult.Success;
    }
}
