using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for payment recovery metrics. A payment counts as succeeded when it reached
///     <see cref="PaymentStatus.Succeeded" /> (refunded/disputed payments once succeeded);
///     a payment counts as recovered when it failed at least once and then succeeded.
/// </summary>
public sealed class GetPaymentRecoveryMetricsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPaymentRecoveryMetricsQuery, PaymentRecoveryMetrics>
{
    public async Task<PaymentRecoveryMetrics> Handle(GetPaymentRecoveryMetricsQuery request, CancellationToken cancellationToken)
    {
        if (request.FromUtc > request.ToUtc)
        {
            throw new ArgumentException("FromUtc must be earlier than or equal to ToUtc.", nameof(request));
        }

        var asOfUtc = SystemClock.UtcNow;
        var query = context.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.CreatedAt >= request.FromUtc && payment.CreatedAt <= request.ToUtc);

        if (request.TenantId.HasValue)
        {
            query = query.Where(payment => payment.TenantId == request.TenantId.Value);
        }

        var payments = await query
            .Select(payment => new PaymentRecoveryRow(
                payment.Status,
                payment.RetryCount,
                payment.MaxRetries,
                payment.NextRetryAt,
                payment.Amount,
                payment.Currency,
                payment.ErrorCode,
                payment.CreatedAt,
                payment.ProcessedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = payments.Count;
        var succeeded = payments.Count(payment => payment.Status == PaymentStatus.Succeeded
                                                  || payment.Status == PaymentStatus.Refunded
                                                  || payment.Status == PaymentStatus.Disputed);
        var currentlyFailed = payments.Count(payment => payment.Status == PaymentStatus.Failed);
        var everFailed = payments.Where(payment => payment.Status == PaymentStatus.Failed || payment.RetryCount > 0).ToList();
        var retried = payments.Count(payment => payment.RetryCount > 0);
        var recovered = everFailed.Where(payment => payment.Status == PaymentStatus.Succeeded
                                                    || payment.Status == PaymentStatus.Refunded
                                                    || payment.Status == PaymentStatus.Disputed)
            .ToList();

        var pendingRetry = everFailed.Count(payment =>
            payment.Status == PaymentStatus.Failed &&
            payment.RetryCount < payment.MaxRetries &&
            payment.NextRetryAt > asOfUtc);
        var dueForRetry = everFailed.Count(payment =>
            payment.Status == PaymentStatus.Failed &&
            payment.RetryCount < payment.MaxRetries &&
            payment.NextRetryAt <= asOfUtc);
        var exhausted = everFailed.Count(payment =>
            payment.Status == PaymentStatus.Failed &&
            payment.RetryCount >= payment.MaxRetries);

        var recoveredAmounts = recovered
            .GroupBy(payment => payment.Currency, StringComparer.Ordinal)
            .Select(group => new PaymentCurrencyAmount(
                group.Key,
                group.Sum(payment => payment.Amount),
                group.Count()))
            .OrderBy(amount => amount.Currency, StringComparer.Ordinal)
            .ToList();

        return new PaymentRecoveryMetrics(
            request.FromUtc,
            request.ToUtc,
            request.TenantId,
            new PaymentAttemptMetrics(
                total,
                succeeded,
                currentlyFailed,
                Percentage(succeeded, total)),
            new PaymentRetryRecoveryMetrics(
                everFailed.Count,
                retried,
                recovered.Count,
                Percentage(recovered.Count, everFailed.Count),
                recoveredAmounts),
            new PaymentDunningOutcomeMetrics(pendingRetry, dueForRetry, exhausted),
            ComputeFailureReasonDistribution(everFailed),
            ComputeRevenueRecognitionTiming(payments));
    }

    /// <summary>
    ///     Groups the payments that failed at least once by their recorded gateway error code
    ///     (issue #415 failure reason distribution). Failures without a code fall under
    ///     <c>unclassified</c>; buckets are ordered by descending count, ties by code.
    /// </summary>
    private static PaymentFailureReasonDistribution ComputeFailureReasonDistribution(
        IReadOnlyList<PaymentRecoveryRow> everFailed)
    {
        const string unclassified = "unclassified";
        var buckets = everFailed
            .GroupBy(payment => string.IsNullOrWhiteSpace(payment.ErrorCode) ? unclassified : payment.ErrorCode,
                StringComparer.Ordinal)
            .Select(group => new PaymentFailureReasonBucket(
                group.Key,
                group.Count(),
                Percentage(group.Count(), everFailed.Count)))
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.FailureCode, StringComparer.Ordinal)
            .ToList();

        return new PaymentFailureReasonDistribution(everFailed.Count, buckets);
    }

    /// <summary>
    ///     Creation-to-settlement timing of the window's succeeded payments (issue #415 revenue
    ///     recognition timing): the recognition lag asynchronous confirmations and 3DS flows add
    ///     before a charge becomes settled revenue. Payments still awaiting settlement are not
    ///     counted.
    /// </summary>
    private static PaymentRevenueRecognitionTiming ComputeRevenueRecognitionTiming(
        IReadOnlyList<PaymentRecoveryRow> payments)
    {
        var settlementHours = payments
            .Where(payment => payment.ProcessedAt.HasValue
                && (payment.Status == PaymentStatus.Succeeded
                    || payment.Status == PaymentStatus.Refunded
                    || payment.Status == PaymentStatus.Disputed))
            .Select(payment => (payment.ProcessedAt!.Value - payment.CreatedAt).TotalHours)
            .Where(hours => hours >= 0)
            .OrderBy(hours => hours)
            .ToList();

        if (settlementHours.Count == 0)
        {
            return new PaymentRevenueRecognitionTiming(0, null, null, null);
        }

        return new PaymentRevenueRecognitionTiming(
            settlementHours.Count,
            Math.Round(settlementHours.Average(), 3, MidpointRounding.AwayFromZero),
            Math.Round(settlementHours[settlementHours.Count / 2], 3, MidpointRounding.AwayFromZero),
            Math.Round(settlementHours[^1], 3, MidpointRounding.AwayFromZero));
    }

    private static decimal Percentage(int numerator, int denominator)
        => denominator == 0
            ? 0m
            : Math.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);

    /// <summary>Row projection the metrics aggregate over.</summary>
    private sealed record PaymentRecoveryRow(
        PaymentStatus Status,
        int RetryCount,
        int MaxRetries,
        DateTime? NextRetryAt,
        decimal Amount,
        string Currency,
        string? ErrorCode,
        DateTime CreatedAt,
        DateTime? ProcessedAt);
}
