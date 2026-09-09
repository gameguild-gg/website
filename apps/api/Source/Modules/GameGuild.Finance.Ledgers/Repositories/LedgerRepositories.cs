using Microsoft.EntityFrameworkCore;
using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Entities;
using GameGuild.Finance.Ledgers.Enums;

namespace GameGuild.Finance.Ledgers.Repositories;

/// <summary>
/// EF Core implementation of ILedgerRepository.
/// </summary>
public class LedgerRepository : ILedgerRepository
{
    private readonly IApplicationDbContext _context;
    private readonly DbSet<Entities.Ledger> _ledgers;

    public LedgerRepository(IApplicationDbContext context)
    {
        _context = context;
        _ledgers = context.Set<Entities.Ledger>();
    }

    public async Task<Entities.Ledger?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _ledgers
            .Include(l => l.ParentLedger)
            .Include(l => l.Children)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<Entities.Ledger?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct = default)
    {
        return await _ledgers
            .Include(l => l.ParentLedger)
            .Include(l => l.Children)
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Code == code, ct);
    }

    public async Task<Entities.Ledger?> GetBySlugAsync(Guid tenantId, string slug, CancellationToken ct = default)
    {
        return await _ledgers
            .Include(l => l.ParentLedger)
            .Include(l => l.Children)
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<Entities.Ledger>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _ledgers
            .Include(l => l.ParentLedger)
            .Where(l => l.TenantId == tenantId)
            .OrderBy(l => l.HierarchyPath)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entities.Ledger>> GetRootLedgersAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _ledgers
            .Include(l => l.Children)
            .Where(l => l.TenantId == tenantId && l.ParentLedgerId == null && l.Type != LedgerType.Virtual)
            .OrderBy(l => l.Code)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entities.Ledger>> GetChildrenAsync(Guid parentLedgerId, CancellationToken ct = default)
    {
        return await _ledgers
            .Include(l => l.Children)
            .Where(l => l.ParentLedgerId == parentLedgerId)
            .OrderBy(l => l.Code)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Entities.Ledger>> GetVirtualLedgersAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _ledgers
            .Where(l => l.TenantId == tenantId && l.Type == LedgerType.Virtual)
            .OrderBy(l => l.Code)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid tenantId, string code, CancellationToken ct = default)
    {
        return await _ledgers.AnyAsync(l => l.TenantId == tenantId && l.Code == code, ct);
    }

    public async Task AddAsync(Entities.Ledger ledger, CancellationToken ct = default)
    {
        await _ledgers.AddAsync(ledger, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Entities.Ledger ledger, CancellationToken ct = default)
    {
        _ledgers.Update(ledger);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Entities.Ledger ledger, CancellationToken ct = default)
    {
        _ledgers.Remove(ledger);
        await _context.SaveChangesAsync(ct);
    }
}

/// <summary>
/// EF Core implementation of ILedgerClosureRepository.
/// </summary>
public class LedgerClosureRepository : ILedgerClosureRepository
{
    private readonly IApplicationDbContext _context;
    private readonly DbSet<LedgerClosure> _closures;

    public LedgerClosureRepository(IApplicationDbContext context)
    {
        _context = context;
        _closures = context.Set<LedgerClosure>();
    }

    public async Task<IReadOnlyList<LedgerClosure>> GetAncestorsAsync(Guid descendantLedgerId, CancellationToken ct = default)
    {
        return await _closures
            .Include(c => c.Ancestor)
            .Where(c => c.DescendantId == descendantLedgerId && c.Depth > 0)
            .OrderBy(c => c.Depth)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LedgerClosure>> GetDescendantsAsync(Guid ancestorLedgerId, int? maxDepth = null, CancellationToken ct = default)
    {
        var query = _closures
            .Include(c => c.Descendant)
            .Where(c => c.AncestorId == ancestorLedgerId && c.Depth > 0);

        if (maxDepth.HasValue)
        {
            query = query.Where(c => c.Depth <= maxDepth.Value);
        }

        return await query
            .OrderBy(c => c.Depth)
            .ThenBy(c => c.Descendant!.Code)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid ancestorLedgerId, int? maxDepth = null, CancellationToken ct = default)
    {
        var query = _closures
            .Where(c => c.AncestorId == ancestorLedgerId);

        if (maxDepth.HasValue)
        {
            query = query.Where(c => c.Depth <= maxDepth.Value);
        }

        return await query
            .Select(c => c.DescendantId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid descendantLedgerId, CancellationToken ct = default)
    {
        return await _closures
            .Where(c => c.DescendantId == descendantLedgerId)
            .OrderBy(c => c.Depth)
            .Select(c => c.AncestorId)
            .ToListAsync(ct);
    }

    public async Task<bool> IsAncestorOfAsync(Guid ancestorLedgerId, Guid descendantLedgerId, CancellationToken ct = default)
    {
        return await _closures.AnyAsync(
            c => c.AncestorId == ancestorLedgerId && c.DescendantId == descendantLedgerId,
            ct);
    }

    public async Task AddAsync(LedgerClosure closure, CancellationToken ct = default)
    {
        await _closures.AddAsync(closure, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<LedgerClosure> closures, CancellationToken ct = default)
    {
        await _closures.AddRangeAsync(closures, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteByDescendantAsync(Guid descendantLedgerId, CancellationToken ct = default)
    {
        var closures = await _closures
            .Where(c => c.DescendantId == descendantLedgerId)
            .ToListAsync(ct);

        _closures.RemoveRange(closures);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteSubtreeAsync(Guid rootLedgerId, CancellationToken ct = default)
    {
        // Get all descendant IDs first
        var descendantIds = await GetDescendantIdsAsync(rootLedgerId, ct: ct);

        // Delete all closure entries where descendant is in the subtree
        var closuresToDelete = await _closures
            .Where(c => descendantIds.Contains(c.DescendantId))
            .ToListAsync(ct);

        _closures.RemoveRange(closuresToDelete);
        await _context.SaveChangesAsync(ct);
    }
}

/// <summary>
/// EF Core implementation of ILedgerEntryRepository.
/// </summary>
public class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly IApplicationDbContext _context;
    private readonly DbSet<LedgerEntry> _entries;

    public LedgerEntryRepository(IApplicationDbContext context)
    {
        _context = context;
        _entries = context.Set<LedgerEntry>();
    }

    public async Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _entries
            .Include(e => e.Ledger)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<LedgerEntry?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
    {
        return await _entries
            .Include(e => e.Ledger)
            .FirstOrDefaultAsync(e => e.ExternalReferenceId == externalId, ct);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetByLedgerAsync(
        Guid ledgerId,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        var query = _entries.Where(e => e.LedgerId == ledgerId);

        if (fromDate.HasValue)
        {
            var fromDateOnly = DateOnly.FromDateTime(fromDate.Value.UtcDateTime);
            query = query.Where(e => e.TransactionDate >= fromDateOnly);
        }

        if (toDate.HasValue)
        {
            var toDateOnly = DateOnly.FromDateTime(toDate.Value.UtcDateTime);
            query = query.Where(e => e.TransactionDate <= toDateOnly);
        }

        return await query
            .OrderBy(e => e.TransactionDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetByLedgerIncludingDescendantsAsync(
        Guid ledgerId,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        // Use the denormalized ParentLedgerIds field for efficient query
        // Entries belong to this ledger if LedgerId matches OR if ledgerId is in ParentLedgerIds
        var query = _entries
            .Where(e => e.LedgerId == ledgerId || e.ParentLedgerIds.Contains(ledgerId));

        if (fromDate.HasValue)
        {
            var fromDateOnly = DateOnly.FromDateTime(fromDate.Value.UtcDateTime);
            query = query.Where(e => e.TransactionDate >= fromDateOnly);
        }

        if (toDate.HasValue)
        {
            var toDateOnly = DateOnly.FromDateTime(toDate.Value.UtcDateTime);
            query = query.Where(e => e.TransactionDate <= toDateOnly);
        }

        return await query
            .OrderBy(e => e.TransactionDate)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetNextSequenceNumberAsync(Guid ledgerId, CancellationToken ct = default)
    {
        // Since we don't have SequenceNumber, use count + 1
        var count = await _entries
            .Where(e => e.LedgerId == ledgerId)
            .CountAsync(ct);

        return count + 1;
    }

    public async Task AddAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        await _entries.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken ct = default)
    {
        await _entries.AddRangeAsync(entries, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        _entries.Update(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        _entries.Remove(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<(decimal TotalCredits, decimal TotalDebits, int EntryCount)> GetBalanceSummaryAsync(
        Guid ledgerId,
        DateTimeOffset? asOfDate = null,
        CancellationToken ct = default)
    {
        var query = _entries
            .Where(e => e.LedgerId == ledgerId && e.Status == EntryStatus.Posted && e.ReversedByEntryId == null);

        if (asOfDate.HasValue)
        {
            var asOfDateOnly = DateOnly.FromDateTime(asOfDate.Value.UtcDateTime);
            query = query.Where(e => e.TransactionDate <= asOfDateOnly);
        }

        var summary = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCredits = g.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount),
                TotalDebits = g.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount),
                EntryCount = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        return summary is null
            ? (0m, 0m, 0)
            : (summary.TotalCredits, summary.TotalDebits, summary.EntryCount);
    }

    public async Task<(decimal TotalCredits, decimal TotalDebits, int EntryCount)> GetBalanceSummaryIncludingDescendantsAsync(
        Guid ledgerId,
        DateTimeOffset? asOfDate = null,
        CancellationToken ct = default)
    {
        var query = _entries
            .Where(e => (e.LedgerId == ledgerId || e.ParentLedgerIds.Contains(ledgerId))
                        && e.Status == EntryStatus.Posted
                        && e.ReversedByEntryId == null);

        if (asOfDate.HasValue)
        {
            var asOfDateOnly = DateOnly.FromDateTime(asOfDate.Value.UtcDateTime);
            query = query.Where(e => e.TransactionDate <= asOfDateOnly);
        }

        var summary = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCredits = g.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount),
                TotalDebits = g.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount),
                EntryCount = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        return summary is null
            ? (0m, 0m, 0)
            : (summary.TotalCredits, summary.TotalDebits, summary.EntryCount);
    }
}
