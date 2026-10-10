using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for retry-queue candidates: failed payments whose scheduled next-retry time has
///     passed and that still have retry budget left, soonest first.
/// </summary>
public sealed class GetRetryingPaymentCandidatesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetRetryingPaymentCandidatesQuery, IReadOnlyList<PaymentResult>>
{
    private const int MaxTake = 100;

    public async Task<IReadOnlyList<PaymentResult>> Handle(GetRetryingPaymentCandidatesQuery request, CancellationToken cancellationToken)
    {
        var take = request.Take < 1 ? 50 : Math.Min(request.Take, MaxTake);
        var asOfUtc = SystemClock.UtcNow;

        var query = context.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Failed)
            .Where(payment => payment.RetryCount < payment.MaxRetries)
            .Where(payment => payment.NextRetryAt != null && payment.NextRetryAt <= asOfUtc);

        if (request.TenantId.HasValue)
        {
            query = query.Where(payment => payment.TenantId == request.TenantId.Value);
        }

        var candidates = await query
            .OrderBy(payment => payment.NextRetryAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates.Select(PaymentQueryMapper.ToResult).ToList();
    }
}
