using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Repository implementation for pricing A/B experiment entities (issue #395).
/// </summary>
public class PricingExperimentRepository(IApplicationDbContext context)
    : CommerceRepositoryBase<PricingExperiment>(context), IPricingExperimentRepository
{
    /// <inheritdoc />
    public new async Task<PricingExperiment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Query
            .Include(e => e.Variants)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PricingExperiment>> GetEnrollingExperimentsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        var experiments = await Query
            .Include(e => e.Variants)
            .Where(e => e.Status == PricingExperimentStatus.Running)
            .Where(e => e.ProductId == productId || e.ProductId == null)
            .Where(e => e.StartDate == null || e.StartDate <= now)
            .Where(e => e.EndDate == null || e.EndDate > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Product-scoped experiments win over global ones so a specific product can
        // run its own test while a global experiment is also live.
        return experiments
            .OrderBy(e => e.ProductId.HasValue ? 0 : 1)
            .ThenByDescending(e => e.UpdatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PricingExperiment> Items, int TotalCount)> GetPagedAsync(
        PricingExperimentStatus? status = null,
        Guid? productId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PricingExperiment> query = Query.Include(e => e.Variants);

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (productId.HasValue)
        {
            // Filtering by product also surfaces global experiments (null ProductId).
            query = query.Where(e => e.ProductId == productId.Value || e.ProductId == null);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<PricingExperimentAssignment?> GetAssignmentAsync(
        Guid experimentId,
        string bucketKey,
        CancellationToken cancellationToken = default)
    {
        var normalized = bucketKey.Trim();

        return await Context.Set<PricingExperimentAssignment>()
            .Where(a => a.DeletedAt == null)
            .Where(a => a.ExperimentId == experimentId && a.BucketKey == normalized)
            .Include(a => a.Variant)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAssignmentAsync(PricingExperimentAssignment assignment, CancellationToken cancellationToken = default)
    {
        await Context.Set<PricingExperimentAssignment>().AddAsync(assignment, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddOutcomeAsync(PricingExperimentOutcome outcome, CancellationToken cancellationToken = default)
    {
        await Context.Set<PricingExperimentOutcome>().AddAsync(outcome, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PricingExperimentOutcome>> GetOutcomesAsync(
        Guid experimentId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var outcomes = Context.Set<PricingExperimentOutcome>()
            .Where(o => o.DeletedAt == null)
            .Where(o => o.ExperimentId == experimentId);

        if (fromUtc.HasValue)
        {
            outcomes = outcomes.Where(o => o.OccurredAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            outcomes = outcomes.Where(o => o.OccurredAt <= toUtc.Value);
        }

        return await outcomes
            .OrderBy(o => o.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> GetAssignmentCountsAsync(
        Guid experimentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await Context.Set<PricingExperimentAssignment>()
            .Where(a => a.DeletedAt == null)
            .Where(a => a.ExperimentId == experimentId)
            .GroupBy(a => a.VariantId)
            .Select(g => new { VariantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.VariantId, row => row.Count);
    }

    /// <inheritdoc />
    public async Task AddAsync(PricingExperiment experiment, CancellationToken cancellationToken = default)
    {
        await Entities.AddAsync(experiment, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public new async Task<PricingExperiment> UpdateAsync(PricingExperiment experiment, CancellationToken cancellationToken = default)
    {
        experiment.Touch();
        Entities.Update(experiment);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return experiment;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Context.SaveChangesAsync(cancellationToken);
    }
}
