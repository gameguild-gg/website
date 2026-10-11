using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Refund statistics for an inclusive window (issue #419): refund counts and refunded
///     amounts grouped by status, per-currency completed totals and the reason breakdown,
///     aggregated from the refund ledger recorded by every executed refund.
/// </summary>
/// <param name="FromUtc">Inclusive window start (UTC).</param>
/// <param name="ToUtc">Inclusive window end (UTC).</param>
/// <param name="TenantId">Optional tenant scope; enforced at the API boundary.</param>
public sealed record GetRefundStatisticsQuery(DateTime FromUtc, DateTime ToUtc, Guid? TenantId = null)
    : IQuery<RefundStatistics>;

/// <summary>Refund statistics read model (issue #419).</summary>
public sealed record RefundStatistics
{
    /// <summary>Inclusive window start.</summary>
    public required DateTime FromUtc { get; init; }

    /// <summary>Inclusive window end.</summary>
    public required DateTime ToUtc { get; init; }

    /// <summary>Refund count by lifecycle status in the window.</summary>
    public required IReadOnlyDictionary<RefundStatus, int> CountByStatus { get; init; }

    /// <summary>Refunded amount by currency for completed refunds in the window.</summary>
    public required IReadOnlyDictionary<string, decimal> CompletedAmountByCurrency { get; init; }

    /// <summary>Completed refund count per reason in the window.</summary>
    public required IReadOnlyDictionary<string, int> CompletedCountByReason { get; init; }

    /// <summary>Total refunds considered in the window (all statuses).</summary>
    public required int TotalCount { get; init; }
}

/// <summary>Handler for <see cref="GetRefundStatisticsQuery"/>.</summary>
public sealed class GetRefundStatisticsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRefundStatisticsQuery, RefundStatistics>
{
    public async Task<RefundStatistics> Handle(GetRefundStatisticsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Set<Refund>().AsNoTracking()
            .Where(refund => refund.CreatedAt >= request.FromUtc && refund.CreatedAt <= request.ToUtc);
        if (request.TenantId.HasValue)
        {
            query = query.Where(refund => refund.TenantId == request.TenantId.Value);
        }

        var refunds = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return new RefundStatistics
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            TotalCount = refunds.Count,
            CountByStatus = refunds
                .GroupBy(refund => refund.Status)
                .ToDictionary(group => group.Key, group => group.Count()),
            CompletedAmountByCurrency = refunds
                .Where(refund => refund.Status == RefundStatus.Completed)
                .GroupBy(refund => refund.Currency, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(refund => refund.Amount), StringComparer.Ordinal),
            CompletedCountByReason = refunds
                .Where(refund => refund.Status == RefundStatus.Completed)
                .GroupBy(refund => refund.Reason, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
        };
    }
}

/// <summary>
///     Dispute statistics for an inclusive window (issue #419): counts by status, win/loss
///     ratio for decided disputes and the average time to resolution.
/// </summary>
/// <param name="FromUtc">Inclusive window start (UTC).</param>
/// <param name="ToUtc">Inclusive window end (UTC).</param>
/// <param name="TenantId">Optional tenant scope; enforced at the API boundary.</param>
public sealed record GetDisputeStatisticsQuery(DateTime FromUtc, DateTime ToUtc, Guid? TenantId = null)
    : IQuery<DisputeStatistics>;

/// <summary>Dispute statistics read model (issue #419).</summary>
public sealed record DisputeStatistics
{
    /// <summary>Inclusive window start.</summary>
    public required DateTime FromUtc { get; init; }

    /// <summary>Inclusive window end.</summary>
    public required DateTime ToUtc { get; init; }

    /// <summary>Total disputed amount in the window (currency follows the underlying payments).</summary>
    public required decimal TotalDisputedAmount { get; init; }

    /// <summary>Dispute count by status in the window.</summary>
    public required IReadOnlyDictionary<DisputeStatus, int> CountByStatus { get; init; }

    /// <summary>Disputes won by the merchant in the window.</summary>
    public required int WonCount { get; init; }

    /// <summary>Disputes lost by the merchant in the window.</summary>
    public required int LostCount { get; init; }

    /// <summary>Win/loss ratio for decided disputes; null when none were decided.</summary>
    public double? WinLossRatio { get; init; }

    /// <summary>Average hours from dispute creation to resolution for resolved disputes; null when none resolved.</summary>
    public double? AverageResolutionHours { get; init; }

    /// <summary>Total disputes considered in the window.</summary>
    public required int TotalCount { get; init; }
}

/// <summary>Handler for <see cref="GetDisputeStatisticsQuery"/>.</summary>
public sealed class GetDisputeStatisticsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetDisputeStatisticsQuery, DisputeStatistics>
{
    public async Task<DisputeStatistics> Handle(GetDisputeStatisticsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Set<PaymentDispute>().AsNoTracking()
            .Where(dispute => dispute.CreatedAt >= request.FromUtc && dispute.CreatedAt <= request.ToUtc);
        if (request.TenantId.HasValue)
        {
            // PaymentDispute has no tenant column; the tenant enters through the payment.
            query = query.Where(dispute => context.Set<Payment>()
                .Any(payment => payment.Id == dispute.PaymentId && payment.TenantId == request.TenantId.Value));
        }

        var disputes = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        var won = disputes.Count(dispute => dispute.Status == DisputeStatus.Won);
        var lost = disputes.Count(dispute => dispute.Status == DisputeStatus.Lost);
        var decided = won + lost;
        var resolvedWithTimestamp = disputes
            .Where(dispute => dispute.ResolvedAt.HasValue)
            .Select(dispute => (dispute.CreatedAt, ResolvedAt: dispute.ResolvedAt!.Value))
            .ToList();

        return new DisputeStatistics
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            TotalCount = disputes.Count,
            TotalDisputedAmount = disputes.Sum(dispute => dispute.Amount),
            CountByStatus = disputes
                .GroupBy(dispute => dispute.Status)
                .ToDictionary(group => group.Key, group => group.Count()),
            WonCount = won,
            LostCount = lost,
            WinLossRatio = decided == 0 ? null : (double)won / decided,
            AverageResolutionHours = resolvedWithTimestamp.Count == 0
                ? null
                : resolvedWithTimestamp.Average(entry => (entry.ResolvedAt - entry.CreatedAt).TotalHours)
        };
    }
}
