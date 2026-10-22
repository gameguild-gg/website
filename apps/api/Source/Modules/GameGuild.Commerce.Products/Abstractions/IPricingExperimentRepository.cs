using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Repository abstraction for pricing A/B experiment entities (issue #395):
///     experiments, arms, sticky assignments and conversion outcomes.
/// </summary>
public interface IPricingExperimentRepository
{
    /// <summary>Gets an experiment by id, including its variants. Null when not found or soft-deleted.</summary>
    Task<PricingExperiment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the experiments that can enroll calculations for a product (Running, in-window,
    ///     product-scoped or global), including variants — highest specificity first
    ///     (product-scoped before global).
    /// </summary>
    Task<IReadOnlyList<PricingExperiment>> GetEnrollingExperimentsAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Gets a paged list of experiments with optional filters, newest first.</summary>
    Task<(IReadOnlyList<PricingExperiment> Items, int TotalCount)> GetPagedAsync(
        PricingExperimentStatus? status = null,
        Guid? productId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the sticky assignment of a bucket key in an experiment, if one exists.</summary>
    Task<PricingExperimentAssignment?> GetAssignmentAsync(Guid experimentId, string bucketKey, CancellationToken cancellationToken = default);

    /// <summary>Adds a new assignment. Callers must call <see cref="SaveChangesAsync"/> to persist.</summary>
    Task AddAssignmentAsync(PricingExperimentAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>Adds a conversion outcome. Callers must call <see cref="SaveChangesAsync"/> to persist.</summary>
    Task AddOutcomeAsync(PricingExperimentOutcome outcome, CancellationToken cancellationToken = default);

    /// <summary>Gets all conversion outcomes of an experiment inside an optional window.</summary>
    Task<IReadOnlyList<PricingExperimentOutcome>> GetOutcomesAsync(
        Guid experimentId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>Counts the durable assignments per variant of an experiment.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAssignmentCountsAsync(Guid experimentId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new experiment. Callers must call <see cref="SaveChangesAsync"/> to persist.</summary>
    Task AddAsync(PricingExperiment experiment, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing experiment (touches UpdatedAt) and saves immediately.</summary>
    Task<PricingExperiment> UpdateAsync(PricingExperiment experiment, CancellationToken cancellationToken = default);

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
