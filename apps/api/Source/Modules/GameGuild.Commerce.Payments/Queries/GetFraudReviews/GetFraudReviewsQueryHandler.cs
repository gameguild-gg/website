using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for the pending fraud-review queue (issue #409). Matches the partial index
///     <c>ix_payments_open_fraud_reviews</c> (open Review decisions per tenant).
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
