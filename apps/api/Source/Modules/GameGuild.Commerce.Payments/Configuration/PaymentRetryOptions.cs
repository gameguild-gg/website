using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Configurable payment retry schedule (issue #403), bound from <c>Payments:Retry</c>.
///     Defaults reproduce the legacy hardcoded schedule exactly: at most 3 retries with an
///     exponential backoff of <c>Math.Pow(5, RetryCount)</c> minutes (1, 5, 25, ...).
///     The entity keeps these defaults as fallback; handlers and services resolve the
///     configured options and pass them into <c>Payment.Create</c> / <c>Payment.MarkAsFailed</c>.
/// </summary>
public sealed class PaymentRetryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:Retry";

    /// <summary>Maximum number of retry attempts allowed per payment.</summary>
    [Range(0, 20)]
    public int MaxRetries { get; set; } = 3;

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
    ///     Computes the backoff delay (in minutes) before the next retry attempt,
    ///     using <c>BackoffBaseMinutes * Math.Pow(BackoffMultiplier, retryCount)</c>.
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

        return ValidateOptionsResult.Success;
    }
}
