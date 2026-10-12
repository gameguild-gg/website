using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     EF Core repository for JIT elevation requests
/// </summary>
public class JitElevationRequestRepository(DbContext context) : IJitElevationRequestRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<JitElevationRequest> DbSet => _context.Set<JitElevationRequest>();

    public async Task<JitElevationRequest> CreateAsync(
        JitElevationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(request, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request;
    }

    public async Task<JitElevationRequest?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        JitElevationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        request.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(request);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (request != null)
        {
            DbSet.Remove(request);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<JitElevationRequest>> GetPendingRequestsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(r => r.Status == ElevationRequestStatus.Pending);

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == new TenantId(tenantId.Value));
        }

        return await query.OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<JitElevationRequest>> GetByRequesterAsync(
        Guid requesterId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(r => r.RequesterId == requesterId);

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == new TenantId(tenantId.Value));
        }

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<JitElevationRequest>> GetActiveByUserAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var now = SystemClock.UtcNow;
        // Approved elevations whose start time has arrived are included (lazy window
        // entry): approval grants the time-bound window, so no separate activation
        // step is required for the grant to take effect.
        var query = DbSet.Where(r =>
            r.RequesterId == userId &&
            (r.Status == ElevationRequestStatus.Active ||
             (r.Status == ElevationRequestStatus.Approved && r.StartsAt != null && r.StartsAt <= now)) &&
            r.ExpiresAt > now
        );

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == new TenantId(tenantId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<JitElevationRequest>> GetExpiredElevationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var now = SystemClock.UtcNow;
        return await DbSet
            .Where(r =>
                (r.Status == ElevationRequestStatus.Active || r.Status == ElevationRequestStatus.Approved) &&
                r.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
///     EF Core repository for permission delegations
/// </summary>
public class PermissionDelegationRepository(DbContext context) : IPermissionDelegationRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<PermissionDelegation> DbSet => _context.Set<PermissionDelegation>();

    public async Task<PermissionDelegation> CreateAsync(
        PermissionDelegation delegation,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(delegation, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return delegation;
    }

    public async Task<PermissionDelegation?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        PermissionDelegation delegation,
        CancellationToken cancellationToken = default
    )
    {
        delegation.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(delegation);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var delegation = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (delegation != null)
        {
            DbSet.Remove(delegation);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<PermissionDelegation>> GetByDelegatorAsync(
        Guid delegatorUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(d => d.DelegatorUserId == delegatorUserId);

        if (tenantId.HasValue)
        {
            query = query.Where(d => d.TenantId == new TenantId(tenantId.Value));
        }

        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<PermissionDelegation>> GetActiveByDelegateAsync(
        Guid delegateUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var now = SystemClock.UtcNow;
        var query = DbSet.Where(d =>
            d.DelegateUserId == delegateUserId &&
            d.IsActive &&
            d.StartsAt <= now &&
            (d.ExpiresAt == null || d.ExpiresAt > now)
        );

        if (tenantId.HasValue)
        {
            query = query.Where(d => d.TenantId == new TenantId(tenantId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionDelegation>> GetExpiredDelegationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var now = SystemClock.UtcNow;
        return await DbSet
            .Where(d => d.IsActive && d.ExpiresAt != null && d.ExpiresAt <= now)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     EF Core repository for SoD rules
/// </summary>
public class SoDRuleRepository(DbContext context) : ISoDRuleRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<SoDRule> DbSet => _context.Set<SoDRule>();

    public async Task<SoDRule> CreateAsync(
        SoDRule rule,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rule;
    }

    public async Task<SoDRule?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task<SoDRule> UpdateAsync(
        SoDRule rule,
        CancellationToken cancellationToken = default
    )
    {
        rule.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(rule);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rule;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule != null)
        {
            DbSet.Remove(rule);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<SoDRule>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = tenantId.HasValue
            ? DbSet.Where(r => r.TenantId == new TenantId(tenantId.Value))
            : DbSet.Where(r => r.TenantId == null);

        return await query.OrderBy(r => r.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<SoDRule>> GetActiveRulesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(r => r.IsEnabled);

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == new TenantId(tenantId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     EF Core repository for SoD violations
/// </summary>
public class SoDViolationRepository(DbContext context) : ISoDViolationRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<SoDViolation> DbSet => _context.Set<SoDViolation>();

    public async Task<SoDViolation> CreateAsync(
        SoDViolation violation,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(violation, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return violation;
    }

    public async Task<SoDViolation?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        SoDViolation violation,
        CancellationToken cancellationToken = default
    )
    {
        violation.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(violation);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var violation = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (violation != null)
        {
            DbSet.Remove(violation);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<SoDViolation>> GetByUserAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(v => v.UserId == userId);

        if (tenantId.HasValue)
        {
            query = query.Where(v => v.TenantId == new TenantId(tenantId.Value));
        }

        return await query.OrderByDescending(v => v.DetectedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<SoDViolation>> GetByRuleAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(v => v.RuleId == ruleId)
        .OrderByDescending(v => v.DetectedAt)
        .ToListAsync(cancellationToken);

    public async Task<List<SoDViolation>> GetActiveViolationsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(v => v.Status == SoDViolationStatus.Active);

        if (tenantId.HasValue)
        {
            query = query.Where(v => v.TenantId == new TenantId(tenantId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     EF Core repository for Access Review Campaigns
/// </summary>
public class AccessReviewCampaignRepository(DbContext context) : IAccessReviewCampaignRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<AccessReviewCampaign> DbSet => _context.Set<AccessReviewCampaign>();

    public async Task<AccessReviewCampaign> CreateAsync(
        AccessReviewCampaign campaign,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(campaign, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return campaign;
    }

    public async Task<AccessReviewCampaign?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        AccessReviewCampaign campaign,
        CancellationToken cancellationToken = default
    )
    {
        campaign.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(campaign);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (campaign != null)
        {
            DbSet.Remove(campaign);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<AccessReviewCampaign>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = tenantId.HasValue
            ? DbSet.Where(c => c.TenantId == new TenantId(tenantId.Value))
            : DbSet.Where(c => c.TenantId == null);

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<AccessReviewCampaign>> GetActiveCampaignsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var now = SystemClock.UtcNow;
        var query = DbSet.Where(c =>
            c.Status == AccessReviewStatus.InProgress &&
            c.StartDate <= now &&
            c.EndDate >= now
        );

        if (tenantId.HasValue)
        {
            query = query.Where(c => c.TenantId == new TenantId(tenantId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AccessReviewCampaign>> GetPendingCampaignsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var now = SystemClock.UtcNow;
        return await DbSet
            .Where(c => c.Status == AccessReviewStatus.InProgress && c.EndDate < now)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     EF Core repository for Access Review Items
/// </summary>
public class AccessReviewItemRepository(DbContext context) : IAccessReviewItemRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<AccessReviewItem> DbSet => _context.Set<AccessReviewItem>();

    public async Task<AccessReviewItem> CreateAsync(
        AccessReviewItem item,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(item, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item;
    }

    public async Task<AccessReviewItem?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet
        .Include(item => item.Campaign)
        .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        .ConfigureAwait(false);

    public async Task UpdateAsync(
        AccessReviewItem item,
        CancellationToken cancellationToken = default
    )
    {
        item.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(item);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (item != null)
        {
            DbSet.Remove(item);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<AccessReviewItem>> GetByCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(i => i.CampaignId == campaignId)
        .OrderBy(i => i.CreatedAt)
        .ToListAsync(cancellationToken);

    public async Task<List<AccessReviewItem>> GetByReviewerAsync(
        Guid reviewerId,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(i => i.ReviewerId == reviewerId)
        .OrderByDescending(i => i.CreatedAt)
        .ToListAsync(cancellationToken);

    public Task<List<AccessReviewItem>> GetPendingByReviewerAsync(Guid reviewerId, Guid? tenantId) =>
        GetPendingByReviewerAsync(reviewerId, tenantId, CancellationToken.None);

    public async Task<List<AccessReviewItem>> GetPendingByReviewerAsync(
        Guid reviewerId,
        Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        IQueryable<AccessReviewItem> query = DbSet
            .Include(item => item.Campaign)
            .Where(item => item.ReviewerId == reviewerId && item.Status == AccessReviewItemStatus.Pending);

        if (tenantId.HasValue)
        {
            query = query.Where(item => item.Campaign.TenantId == new TenantId(tenantId.Value));
        }

        return await query
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
///     EF Core repository for Delegated Admin Scopes
/// </summary>
public class DelegatedAdminScopeRepository(DbContext context) : IDelegatedAdminScopeRepository
{
    private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private DbSet<DelegatedAdminScope> DbSet => _context.Set<DelegatedAdminScope>();

    public async Task<DelegatedAdminScope> CreateAsync(
        DelegatedAdminScope scope,
        CancellationToken cancellationToken = default
    )
    {
        await DbSet.AddAsync(scope, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return scope;
    }

    public async Task<DelegatedAdminScope?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public async Task UpdateAsync(
        DelegatedAdminScope scope,
        CancellationToken cancellationToken = default
    )
    {
        scope.UpdatedAt = SystemClock.UtcNow;
        DbSet.Update(scope);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scope = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (scope != null)
        {
            DbSet.Remove(scope);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<List<DelegatedAdminScope>> GetByAdminUserAsync(
        Guid adminUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = DbSet.Where(s => s.AdminUserId == adminUserId && s.IsActive);

        if (tenantId.HasValue)
        {
            query = query.Where(s => s.TenantId == new TenantId(tenantId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<DelegatedAdminScope>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = tenantId.HasValue
            ? DbSet.Where(s => s.TenantId == new TenantId(tenantId.Value))
            : DbSet.Where(s => s.TenantId == null);

        return await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
    }
}
