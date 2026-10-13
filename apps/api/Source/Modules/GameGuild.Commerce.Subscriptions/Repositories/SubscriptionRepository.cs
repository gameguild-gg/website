using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Repository implementation for Subscription entity using the shared application context
/// </summary>
public class SubscriptionRepository(IApplicationDbContext context) 
    : CommerceRepositoryBase<Subscription>(context), ISubscriptionRepository
{
    public new async Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).FirstOrDefaultAsync(s => s.ExternalId == externalId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).Where(s => s.TenantId == tenantId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription?> GetActiveTenantSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).Where(s => s.PlanId == planId).OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetByStatusAsync(SubscriptionStatus status, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).Where(s => s.Status == status).OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetByCreatedUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).Where(s => s.CreatedByUserId == userId).OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetExpiringSoonAsync(int days, CancellationToken cancellationToken = default)
    {
        var cutoffDate = SystemClock.UtcNow.AddDays(days);

        return await Query.Include(s => s.Plan)
            .Where(s => s.EndDate.HasValue && s.EndDate <= cutoffDate && s.Status == SubscriptionStatus.Active)
            .OrderBy(s => s.EndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetDueForRenewalAsync(int days, CancellationToken cancellationToken = default)
    {
        var cutoffDate = SystemClock.UtcNow.AddDays(days);

        return await Query.Include(s => s.Plan)
            .Where(s => s.NextBillingDate <= cutoffDate && s.Status == SubscriptionStatus.Active)
            .OrderBy(s => s.NextBillingDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetTrialsExpiringSoonAsync(int days, CancellationToken cancellationToken = default)
    {
        var cutoffDate = SystemClock.UtcNow.AddDays(days);

        return await Query.Include(s => s.Plan)
            .Where(s => s.TrialEndDate.HasValue && s.TrialEndDate <= cutoffDate && s.Status == SubscriptionStatus.Trialing)
            .OrderBy(s => s.TrialEndDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetSuspendedAsync(CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Suspended)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetByBillingCycleAsync(BillingCycle billingCycle, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan).Where(s => s.BillingCycle == billingCycle).OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await Query.Include(s => s.Plan)
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<SubscriptionStatus, int>> GetCountByStatusAsync(CancellationToken cancellationToken = default)
    {
        return await Query.GroupBy(s => s.Status).ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SubscriptionIntervalBreakdown>> GetIntervalBreakdownAsync(CancellationToken cancellationToken = default)
    {
        // Issue #392: interval breakdown of active subscriptions. Grouping is done on a
        // narrow projection; the monthly-equivalent normalization runs in memory so the
        // translated query stays a plain GroupBy/Sum that the persistence providers accept.
        var projections = await Query
            .Where(s => s.Status == SubscriptionStatus.Active)
            .GroupBy(s => s.BillingCycle)
            .Select(group => new { Interval = group.Key, Count = group.Count(), Total = group.Sum(s => s.Amount.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var breakdowns = new List<SubscriptionIntervalBreakdown>(projections.Count);
        foreach (var projection in projections)
        {
            var months = projection.Interval.GetMonths();
            breakdowns.Add(new SubscriptionIntervalBreakdown(
                projection.Interval,
                months,
                projection.Interval.GetDisplayName(),
                projection.Count,
                projection.Total,
                months > 0 ? projection.Total / months : projection.Total));
        }

        return breakdowns
            .OrderBy(item => item.IntervalMonths)
            .ToList();
    }

    public async Task<decimal> GetRevenueForPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await Query.Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate && s.Status == SubscriptionStatus.Active)
            .SumAsync(s => s.Amount.Amount, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Subscription> AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        var entry = await Entities.AddAsync(subscription, cancellationToken).ConfigureAwait(false);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entry.Entity;
    }

    public new async Task<Subscription> UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        Entities.Update(subscription);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return subscription;
    }

    public new async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subscription = await Entities.FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

        if (subscription != null)
        {
            subscription.SoftDelete();
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Query.AnyAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<Subscription>> GetPagedAsync(int page, int pageSize, SubscriptionStatus? status = null, Guid? tenantId = null, Guid? planId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Subscription> query = Query.Include(s => s.Plan);

        if (status.HasValue) query = query.Where(s => s.Status == status.Value);

        if (tenantId.HasValue) query = query.Where(s => s.TenantId == tenantId.Value);

        if (planId.HasValue) query = query.Where(s => s.PlanId == planId.Value);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var skip = (page - 1) * pageSize;
        var items = await query.OrderByDescending(s => s.CreatedAt).Skip(skip).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<Subscription>(items, totalCount, skip, pageSize);
    }
}
