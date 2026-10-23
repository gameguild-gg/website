using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Repository implementation for PromoCode entities
/// </summary>
public class PromoCodeRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<PromoCode>(context), IPromoCodeRepository
{
    private DbSet<PromoCodeUse> PromoCodeUses => Context.Set<PromoCodeUse>();
    private DbSet<PromoCodeUserUsageCounter> UserUsageCounters => Context.Set<PromoCodeUserUsageCounter>();
    private DbSet<PromoCodeValidationAttempt> ValidationAttempts => Context.Set<PromoCodeValidationAttempt>();

    /// <inheritdoc />
    public new async Task<PromoCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Entities
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = PromoCodeNormalizer.Normalize(code);
        if (normalized is null)
        {
            return null;
        }

        // Codes are stored uppercase; compare case-insensitively so legacy rows
        // stored with other casings still resolve. Expression stays translatable
        // on PostgreSQL and SQLite.
        return await Entities
            .Include(p => p.Product)
            .Where(p => p.DeletedAt == null)
            .FirstOrDefaultAsync(p => p.Code.ToUpper() == normalized, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<PromoCode>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default)
    {
        var normalizedCodes = codes
            .Select(PromoCodeNormalizer.Normalize)
            .Where(c => c is not null)
            .Select(c => c!)
            .Distinct()
            .ToList();

        if (normalizedCodes.Count == 0)
        {
            return new List<PromoCode>();
        }

        return await Entities
            .Where(p => p.DeletedAt == null)
            .Where(p => normalizedCodes.Contains(p.Code.ToUpper()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PromoCode>> GetActiveCodesAsync(CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        return await Entities
            .Where(p => p.IsActive)
            .Where(p => p.DeletedAt == null)
            .Where(p => p.ValidFrom == null || p.ValidFrom <= now)
            .Where(p => p.ValidUntil == null || p.ValidUntil > now)
            .OrderByDescending(p => p.StackingPriority)
            .ToListAsync(cancellationToken)
            ;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PromoCode>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await Entities
            .Where(p => p.ProductId == productId || p.ProductId == null)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.StackingPriority)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<PromoCode>> GetByCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        return await Entities
            .Where(p => p.CampaignId == campaignId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<PromoCode> Items, int TotalCount)> GetPagedAsync(
        bool? isActive = null,
        PromoCodeType? type = null,
        Guid? productId = null,
        string? searchTerm = null,
        string? campaignId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = Query.AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(p => p.Type == type.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(p => p.ProductId == productId.Value || p.ProductId == null);
        }

        if (!string.IsNullOrWhiteSpace(campaignId))
        {
            query = query.Where(p => p.CampaignId == campaignId);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(p =>
                p.Code.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<int> GetUsageCountAsync(Guid promoCodeId, CancellationToken cancellationToken = default)
    {
        return await PromoCodeUses
            .CountAsync(u => u.PromoCodeId == promoCodeId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetUserUsageCountAsync(Guid promoCodeId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await PromoCodeUses
            .CountAsync(u => u.PromoCodeId == promoCodeId && u.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TryIncrementTotalUsageAsync(Guid promoCodeId, int? maxUses, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var affected = await Entities
            .Where(p => p.Id == promoCodeId)
            .Where(p => p.DeletedAt == null)
            .Where(p => p.IsActive)
            .Where(p => maxUses == null || p.CurrentUses < maxUses)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.CurrentUses, p => p.CurrentUses + 1)
                    .SetProperty(p => p.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> TryIncrementUserUsageAsync(Guid promoCodeId, Guid userId, int? maxUsesPerUser, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        // Guarded update: increments only while the user is below the limit.
        var affected = await UserUsageCounters
            .Where(c => c.PromoCodeId == promoCodeId && c.UserId == userId)
            .Where(c => maxUsesPerUser == null || c.UseCount < maxUsesPerUser)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.UseCount, c => c.UseCount + 1)
                    .SetProperty(c => c.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        if (affected > 0)
        {
            return true;
        }

        // No row yet (or a concurrent insert created one). Try to insert the first
        // use; the composite primary key makes concurrent first-uses fail closed.
        var existing = await UserUsageCounters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PromoCodeId == promoCodeId && c.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Row exists but the guarded update matched nothing: the per-user limit is exhausted.
            return false;
        }

        var counter = new PromoCodeUserUsageCounter
        {
            PromoCodeId = promoCodeId,
            UserId = userId,
            UseCount = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await UserUsageCounters.AddAsync(counter, cancellationToken).ConfigureAwait(false);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<PromoCodeUse?> FindUsageByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        return await PromoCodeUses
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(PromoCode promoCode, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(promoCode, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IEnumerable<PromoCode> promoCodes, CancellationToken cancellationToken = default)
    {
        await Entities.AddRangeAsync(promoCodes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public new Task UpdateAsync(PromoCode promoCode, CancellationToken cancellationToken = default)
    {
        Entities.Update(promoCode);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(PromoCode promoCode, CancellationToken cancellationToken = default)
    {
        Entities.Remove(promoCode);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RecordUsageAsync(PromoCodeUse usage, CancellationToken cancellationToken = default)
    {
        await PromoCodeUses.AddAsync(usage, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecordValidationAttemptAsync(
        string code,
        Guid? promoCodeId,
        Guid? userId,
        PromoCodeValidationOutcome outcome,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var normalized = PromoCodeNormalizer.Normalize(code) ?? code.Trim().ToUpperInvariant();
        var attempt = new PromoCodeValidationAttempt
        {
            Id = Guid.NewGuid(),
            Code = normalized.Length > 50 ? normalized[..50] : normalized,
            PromoCodeId = promoCodeId,
            UserId = userId,
            Outcome = outcome,
            ErrorMessage = errorMessage is { Length: > 300 } ? errorMessage[..300] : errorMessage,
            AttemptedAt = SystemClock.UtcNow
        };

        await ValidationAttempts.AddAsync(attempt, cancellationToken).ConfigureAwait(false);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<PromoCode>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new List<PromoCode>();
        }

        return await Entities
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromoCodeUse>> GetUninvoicedUsesForSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        return await PromoCodeUses
            .AsNoTracking()
            .Where(u => u.SubscriptionId == subscriptionId && u.InvoiceId == null)
            .OrderBy(u => u.UsedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> MarkUsesInvoicedAsync(
        IReadOnlyList<Guid> usageIds,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (usageIds.Count == 0)
        {
            return 0;
        }

        var now = SystemClock.UtcNow;

        // Guarded update: only rows not yet stamped with an invoice are touched, so
        // replaying the materialization flow is a no-op.
        return await PromoCodeUses
            .Where(u => usageIds.Contains(u.Id) && u.InvoiceId == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.InvoiceId, invoiceId)
                    .SetProperty(u => u.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Entities.AnyAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = PromoCodeNormalizer.Normalize(code);
        if (normalized is null)
        {
            return false;
        }

        return await Entities.AnyAsync(p => p.DeletedAt == null && p.Code.ToUpper() == normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetExistingCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default)
    {
        var normalizedCodes = codes
            .Select(PromoCodeNormalizer.Normalize)
            .Where(c => c is not null)
            .Select(c => c!)
            .Distinct()
            .ToList();

        if (normalizedCodes.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var existing = await Entities
            .Where(p => p.DeletedAt == null)
            .Where(p => normalizedCodes.Contains(p.Code.ToUpper()))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<PromoCodeUsageDto> GetUsageStatsAsync(Guid promoCodeId, CancellationToken cancellationToken = default)
    {
        var promoCode = await GetByIdAsync(promoCodeId, cancellationToken).ConfigureAwait(false);
        if (promoCode == null)
        {
            throw new PromoCodeNotFoundException(promoCodeId);
        }

        var usages = await PromoCodeUses
            .Where(u => u.PromoCodeId == promoCodeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalUses = usages.Count;
        var uniqueUsers = usages.Select(u => u.UserId).Distinct().Count();
        var totalDiscount = usages.Sum(u => u.DiscountApplied);
        var avgDiscount = totalUses > 0 ? totalDiscount / totalUses : 0;
        var firstUsed = usages.Min(u => (DateTime?)u.CreatedAt);
        var lastUsed = usages.Max(u => (DateTime?)u.CreatedAt);
        var remaining = promoCode.MaxUses.HasValue ? promoCode.MaxUses.Value - totalUses : (int?)null;

        return new PromoCodeUsageDto(
            promoCodeId,
            promoCode.Code,
            totalUses,
            uniqueUsers,
            totalDiscount,
            avgDiscount,
            promoCode.MaxUses,
            remaining,
            firstUsed,
            lastUsed
        );
    }

    /// <inheritdoc />
    public async Task<CampaignPerformanceDto> GetCampaignPerformanceAsync(
        string campaignId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var campaignCodes = await Entities
            .Where(p => p.CampaignId == campaignId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var codeIds = campaignCodes.Select(c => c.Id).ToList();

        var usagesQuery = PromoCodeUses.Where(u => codeIds.Contains(u.PromoCodeId));
        if (from.HasValue)
        {
            usagesQuery = usagesQuery.Where(u => u.UsedAt >= from.Value);
        }

        if (to.HasValue)
        {
            usagesQuery = usagesQuery.Where(u => u.UsedAt < to.Value);
        }

        var usages = await usagesQuery
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalUses = usages.Count;
        var uniqueUsers = usages.Select(u => u.UserId).Distinct().Count();
        var totalDiscount = usages.Sum(u => u.DiscountApplied);
        var averageDiscount = totalUses > 0 ? totalDiscount / totalUses : 0;

        var limitedCapacity = campaignCodes.Where(c => c.MaxUses.HasValue).Sum(c => c.MaxUses!.Value);
        decimal? redemptionRate = limitedCapacity > 0
            ? Math.Round((decimal)totalUses / limitedCapacity, 4, MidpointRounding.AwayFromZero)
            : null;

        // Conversion tracking (issue #418): validation attempts are the denominator,
        // recorded uses the numerator. Attempts resolve to the campaign through the
        // promo code they matched, within the same window as the usage query.
        var attemptsQuery = ValidationAttempts.Where(a => a.PromoCodeId != null && codeIds.Contains(a.PromoCodeId!.Value));
        if (from.HasValue)
        {
            attemptsQuery = attemptsQuery.Where(a => a.AttemptedAt >= from.Value);
        }

        if (to.HasValue)
        {
            attemptsQuery = attemptsQuery.Where(a => a.AttemptedAt < to.Value);
        }

        var totalValidationAttempts = await attemptsQuery
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal? conversionRate = totalValidationAttempts > 0
            ? Math.Round((decimal)totalUses / totalValidationAttempts, 4, MidpointRounding.AwayFromZero)
            : null;

        var topCodes = usages
            .GroupBy(u => u.PromoCodeId)
            .Select(g => new CampaignTopCodeDto(
                campaignCodes.FirstOrDefault(c => c.Id == g.Key)?.Code ?? g.Key.ToString(),
                g.Key,
                g.Count(),
                g.Sum(u => u.DiscountApplied)))
            .OrderByDescending(t => t.Uses)
            .ThenBy(t => t.Code, StringComparer.Ordinal)
            .Take(5)
            .ToList();

        var expiryThreshold = SystemClock.UtcNow.AddDays(30);
        var expiringCodes = campaignCodes
            .Where(c => c.IsActive && c.ValidUntil.HasValue && c.ValidUntil.Value > SystemClock.UtcNow && c.ValidUntil.Value <= expiryThreshold)
            .OrderBy(c => c.ValidUntil)
            .Select(c => new CampaignExpiringCodeDto(c.Code, c.Id, c.ValidUntil!.Value))
            .ToList();

        return new CampaignPerformanceDto(
            campaignId,
            from,
            to,
            campaignCodes.Count,
            campaignCodes.Count(c => c.IsActive),
            totalUses,
            uniqueUsers,
            totalDiscount,
            averageDiscount,
            redemptionRate,
            topCodes,
            expiringCodes,
            totalValidationAttempts,
            conversionRate
        );
    }

    /// <inheritdoc />
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Context.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
