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
            .Select(payment => new
            {
                payment.Status,
                payment.RetryCount,
                payment.MaxRetries,
                payment.NextRetryAt,
                payment.Amount,
                payment.Currency
            })
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
            new PaymentDunningOutcomeMetrics(pendingRetry, dueForRetry, exhausted));
    }

    private static decimal Percentage(int numerator, int denominator)
        => denominator == 0
            ? 0m
            : Math.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
}
