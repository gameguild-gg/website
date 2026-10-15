using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Refund and dispute analytics for an inclusive window (issue #419 follow-up, the
///     deferred dispute statistics from the #810 spec): refund counts and refunded amounts
///     grouped by status, currency and UTC day, dispute counts by status, the win/loss
///     resolution rate of decided disputes and the average time to dispute resolution.
///     Requires the <c>payments:admin</c> permission; the tenant scope is enforced at the
///     API boundary from the actor context.
/// </summary>
/// <param name="FromUtc">Inclusive window start (UTC).</param>
/// <param name="ToUtc">Inclusive window end (UTC).</param>
/// <param name="TenantId">Optional tenant scope; enforced at the API boundary.</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record GetRefundAnalyticsQuery(DateTime FromUtc, DateTime ToUtc, Guid? TenantId = null)
    : IQuery<RefundAnalyticsReport>;

/// <summary>One UTC-day bucket of the refund period breakdown.</summary>
/// <param name="Date">UTC day (midnight) the bucket covers.</param>
/// <param name="Count">Refunds created that day (all statuses).</param>
/// <param name="CompletedAmount">Sum of amounts of refunds completed that day.</param>
public sealed record RefundDailyBucket(DateTime Date, int Count, decimal CompletedAmount);

/// <summary>
///     Combined refund/dispute analytics read model (issue #419 follow-up).
/// </summary>
public sealed record RefundAnalyticsReport
{
    /// <summary>Inclusive window start.</summary>
    public required DateTime FromUtc { get; init; }

    /// <summary>Inclusive window end.</summary>
    public required DateTime ToUtc { get; init; }

    /// <summary>Total refunds considered in the window (all statuses).</summary>
    public required int TotalRefundCount { get; init; }

    /// <summary>Refund count by lifecycle status in the window.</summary>
    public required IReadOnlyDictionary<RefundStatus, int> RefundCountByStatus { get; init; }

    /// <summary>Refunded amount by lifecycle status in the window.</summary>
    public required IReadOnlyDictionary<RefundStatus, decimal> RefundAmountByStatus { get; init; }

    /// <summary>Refund count by currency in the window (all statuses).</summary>
    public required IReadOnlyDictionary<string, int> RefundCountByCurrency { get; init; }

    /// <summary>Refunded amount by currency for completed refunds in the window.</summary>
    public required IReadOnlyDictionary<string, decimal> CompletedRefundAmountByCurrency { get; init; }

    /// <summary>UTC-day breakdown of refund counts and completed amounts.</summary>
    public required IReadOnlyList<RefundDailyBucket> RefundsByDay { get; init; }

    /// <summary>Total disputes considered in the window.</summary>
    public required int TotalDisputeCount { get; init; }

    /// <summary>Dispute count by status in the window.</summary>
    public required IReadOnlyDictionary<DisputeStatus, int> DisputeCountByStatus { get; init; }

    /// <summary>Disputes won by the merchant in the window.</summary>
    public required int DisputesWon { get; init; }

    /// <summary>Disputes lost by the merchant in the window.</summary>
    public required int DisputesLost { get; init; }

    /// <summary>Share of won disputes among decided disputes; null when none were decided.</summary>
    public double? DisputeWinRate { get; init; }

    /// <summary>Share of decided (won or lost) disputes among all disputes; null when there are none.</summary>
    public double? DisputeResolutionRate { get; init; }

    /// <summary>Average hours from creation to resolution for resolved disputes; null when none resolved.</summary>
    public double? AverageDisputeResolutionHours { get; init; }
}

/// <summary>Handler for <see cref="GetRefundAnalyticsQuery"/>.</summary>
public sealed class GetRefundAnalyticsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRefundAnalyticsQuery, RefundAnalyticsReport>
{
    public async Task<RefundAnalyticsReport> Handle(GetRefundAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var refundsQuery = context.Set<Refund>().AsNoTracking()
            .Where(refund => refund.CreatedAt >= request.FromUtc && refund.CreatedAt <= request.ToUtc);
        if (request.TenantId.HasValue)
        {
            refundsQuery = refundsQuery.Where(refund => refund.TenantId == request.TenantId.Value);
        }

        var disputesQuery = context.Set<PaymentDispute>().AsNoTracking()
            .Where(dispute => dispute.CreatedAt >= request.FromUtc && dispute.CreatedAt <= request.ToUtc);
        if (request.TenantId.HasValue)
        {
            // PaymentDispute has no tenant column; the tenant enters through the payment.
            disputesQuery = disputesQuery.Where(dispute => context.Set<Payment>()
                .Any(payment => payment.Id == dispute.PaymentId && payment.TenantId == request.TenantId.Value));
        }

        var refunds = await refundsQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        var disputes = await disputesQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

        var won = disputes.Count(dispute => dispute.Status == DisputeStatus.Won);
        var lost = disputes.Count(dispute => dispute.Status == DisputeStatus.Lost);
        var decided = won + lost;
        var resolvedWithTimestamp = disputes
            .Where(dispute => dispute.ResolvedAt.HasValue)
            .Select(dispute => (dispute.CreatedAt, ResolvedAt: dispute.ResolvedAt!.Value))
            .ToList();

        return new RefundAnalyticsReport
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            TotalRefundCount = refunds.Count,
            RefundCountByStatus = refunds
                .GroupBy(refund => refund.Status)
                .ToDictionary(group => group.Key, group => group.Count()),
            RefundAmountByStatus = refunds
                .GroupBy(refund => refund.Status)
                .ToDictionary(group => group.Key, group => group.Sum(refund => refund.Amount)),
            RefundCountByCurrency = refunds
                .GroupBy(refund => refund.Currency, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            CompletedRefundAmountByCurrency = refunds
                .Where(refund => refund.Status == RefundStatus.Completed)
                .GroupBy(refund => refund.Currency, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(refund => refund.Amount), StringComparer.Ordinal),
            RefundsByDay = refunds
                .GroupBy(refund => refund.CreatedAt.Date)
                .Select(group => new RefundDailyBucket(
                    group.Key,
                    group.Count(),
                    group.Where(refund => refund.Status == RefundStatus.Completed).Sum(refund => refund.Amount)))
                .OrderBy(bucket => bucket.Date)
                .ToList(),
            TotalDisputeCount = disputes.Count,
            DisputeCountByStatus = disputes
                .GroupBy(dispute => dispute.Status)
                .ToDictionary(group => group.Key, group => group.Count()),
            DisputesWon = won,
            DisputesLost = lost,
            DisputeWinRate = decided == 0 ? null : (double)won / decided,
            DisputeResolutionRate = disputes.Count == 0 ? null : (double)decided / disputes.Count,
            AverageDisputeResolutionHours = resolvedWithTimestamp.Count == 0
                ? null
                : resolvedWithTimestamp.Average(entry => (entry.ResolvedAt - entry.CreatedAt).TotalHours)
        };
    }
}
