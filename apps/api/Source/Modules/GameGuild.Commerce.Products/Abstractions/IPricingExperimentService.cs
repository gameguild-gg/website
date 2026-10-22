using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Pricing A/B experiment framework (issue #395): sticky bucket assignment, variant
///     application during calculation, conversion outcomes, revenue-impact analytics, and
///     the automated scheduling pass for experiments and opt-in scheduled rules.
/// </summary>
public interface IPricingExperimentService
{
    /// <summary>
    ///     Resolves the arm a bucket key belongs to for a product's live experiment, creating
    ///     the durable assignment on first enrollment. Returns null when no experiment is
    ///     enrolling for the product or the key is held out by the traffic gate.
    /// </summary>
    /// <param name="productId">Product being priced</param>
    /// <param name="bucketKey">Stable bucketing key (usually the acting user id)</param>
    /// <param name="userId">Optional user correlation stored on the assignment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<PricingExperimentVariant?> ResolveVariantForProductAsync(
        Guid productId,
        string bucketKey,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves (or creates) the sticky assignment of a bucket key inside one experiment.
    /// </summary>
    /// <param name="experimentId">Experiment id</param>
    /// <param name="bucketKey">Stable bucketing key</param>
    /// <param name="userId">Optional user correlation stored on the assignment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown when the experiment does not exist or is not enrolling.</exception>
    Task<PricingExperimentAssignmentResult> AssignAsync(
        Guid experimentId,
        string bucketKey,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a monetized conversion outcome under the arm a bucket key is assigned to.
    /// </summary>
    /// <param name="experimentId">Experiment id</param>
    /// <param name="bucketKey">Bucket key that converted</param>
    /// <param name="amount">Final monetized amount</param>
    /// <param name="currency">ISO 4217 currency</param>
    /// <param name="orderId">Optional order correlation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown when the key has no assignment in the experiment.</exception>
    Task RecordOutcomeAsync(
        Guid experimentId,
        string bucketKey,
        decimal amount,
        string currency,
        Guid? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Computes revenue-impact analytics for an experiment over an optional window.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the experiment does not exist.</exception>
    Task<PricingExperimentAnalytics> GetAnalyticsAsync(
        Guid experimentId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Runs one automated scheduling pass (issue #395): opt-in scheduled pricing rules are
    ///     activated/deactivated to match their date window, and experiments start/complete at
    ///     their bounds. Returns the transition counts.
    /// </summary>
    /// <param name="now">Instant the pass runs against (defaults to UTC now)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<PricingScheduleSweepResult> RunSchedulingSweepAsync(
        DateTime? now = null,
        CancellationToken cancellationToken = default);
}
