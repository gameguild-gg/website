using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for the pending fraud-review queue (issue #409). Matches the partial index
///     <c>ix_payments_open_fraud_reviews</c> (open Review decisions per tenant) and applies
///     the review-dashboard filters: minimum score, risk band, charged-amount floor and
///     creation date range.
/// </summary>
public sealed class GetFraudReviewsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetFraudReviewsQuery, IEnumerable<FraudReviewListItem>>
{
    public async Task<IEnumerable<FraudReviewListItem>> Handle(GetFraudReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.FraudDecision == FraudDecision.Review
                              && payment.FraudReviewStatus == FraudReviewStatus.PendingReview);

        if (request.TenantId.HasValue)
        {
            query = query.Where(payment => payment.TenantId == request.TenantId.Value);
        }

        if (request.MinScore.HasValue)
        {
            var minScore = request.MinScore.Value;
            query = query.Where(payment => (payment.FraudScore ?? 0) >= minScore);
        }

        if (request.RiskLevel.HasValue)
        {
            // Risk band boundaries per FraudAssessment.RiskLevelForScore, expressed as
            // score ranges so the filter stays translatable to SQL.
            var (lower, upperExclusive) = request.RiskLevel.Value switch
            {
                FraudRiskLevel.Low => (0, 31),
                FraudRiskLevel.Medium => (31, 61),
                FraudRiskLevel.High => (61, 86),
                _ => (86, int.MaxValue)
            };
            query = query.Where(payment => (payment.FraudScore ?? 0) >= lower
                                           && (payment.FraudScore ?? 0) < upperExclusive);
        }

        if (request.MinAmount.HasValue)
        {
            var minAmount = request.MinAmount.Value;
            query = query.Where(payment => payment.Amount >= minAmount);
        }

        if (request.FromUtc.HasValue)
        {
            var fromUtc = request.FromUtc.Value;
            query = query.Where(payment => payment.CreatedAt >= fromUtc);
        }

        if (request.ToUtc.HasValue)
        {
            var toUtc = request.ToUtc.Value;
            query = query.Where(payment => payment.CreatedAt <= toUtc);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var reviews = await query
            .OrderBy(payment => payment.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(payment => new FraudReviewListItem
            {
                TenantId = payment.TenantId,
                PaymentId = payment.Id,
                SubscriptionId = payment.SubscriptionId,
                ExternalCustomerId = payment.ExternalCustomerId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Score = payment.FraudScore ?? 0,
                RiskLevel = FraudAssessment.RiskLevelForScore(payment.FraudScore ?? 0),
                Reason = payment.FraudDecisionReason ?? string.Empty,
                Evidence = payment.FraudEvidence,
                CreatedAt = payment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return reviews;
    }
}
