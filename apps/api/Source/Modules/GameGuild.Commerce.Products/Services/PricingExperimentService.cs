using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Pricing A/B experiment framework (issue #395). Assignments are sticky: the first
///     calculation for a bucket key persists the deterministic hash allocation, and every
///     later calculation resolves to the same arm. Conversion outcomes are attributed to the
///     assigned arm and aggregated into revenue-impact analytics. The scheduling pass keeps
///     opt-in scheduled rules and date-bounded experiments aligned with the clock.
/// </summary>
public sealed class PricingExperimentService(
    IPricingExperimentRepository experimentRepository,
    IPricingRuleRepository pricingRuleRepository) : IPricingExperimentService
{
    /// <inheritdoc />
    public async Task<PricingExperimentVariant?> ResolveVariantForProductAsync(
        Guid productId,
        string bucketKey,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketKey))
        {
            return null;
        }

        var experiments = await experimentRepository
            .GetEnrollingExperimentsAsync(productId, cancellationToken)
            .ConfigureAwait(false);

        if (experiments.Count == 0)
        {
            return null;
        }

        // The most specific live experiment (product-scoped before global) owns the key.
        var experiment = experiments[0];
        var (variant, _, _) = await ResolveOrCreateAssignmentAsync(experiment, bucketKey, userId, cancellationToken)
            .ConfigureAwait(false);

        return variant;
    }

    /// <inheritdoc />
    public async Task<PricingExperimentAssignmentResult> AssignAsync(
        Guid experimentId,
        string bucketKey,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketKey))
        {
            throw new InvalidOperationException("A bucket key is required to assign an experiment variant.");
        }

        var experiment = await experimentRepository
            .GetByIdAsync(experimentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Pricing experiment {experimentId} not found.");

        if (!experiment.IsEnrolling())
        {
            throw new InvalidOperationException(
                $"Pricing experiment '{experiment.Name}' is {experiment.Status} and not enrolling.");
        }

        var (variant, assignmentId, assignedAt) = await ResolveOrCreateAssignmentAsync(
            experiment, bucketKey, userId, cancellationToken).ConfigureAwait(false);

        return new PricingExperimentAssignmentResult(
            experimentId,
            bucketKey.Trim(),
            variant?.ToDto(),
            assignmentId,
            assignedAt);
    }

    /// <inheritdoc />
    public async Task RecordOutcomeAsync(
        Guid experimentId,
        string bucketKey,
        decimal amount,
        string currency,
        Guid? orderId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketKey))
        {
            throw new InvalidOperationException("A bucket key is required to record an experiment outcome.");
        }

        if (amount < 0)
        {
            throw new InvalidOperationException("Outcome amount cannot be negative.");
        }

        var assignment = await experimentRepository
            .GetAssignmentAsync(experimentId, bucketKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Bucket key is not enrolled in experiment {experimentId}; assign a variant before recording outcomes.");

        var outcome = new PricingExperimentOutcome
        {
            ExperimentId = experimentId,
            VariantId = assignment.VariantId,
            AssignmentId = assignment.Id,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            OrderId = orderId,
            OccurredAt = SystemClock.UtcNow
        };

        await experimentRepository.AddOutcomeAsync(outcome, cancellationToken).ConfigureAwait(false);
        await experimentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PricingExperimentAnalytics> GetAnalyticsAsync(
        Guid experimentId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var experiment = await experimentRepository
            .GetByIdAsync(experimentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Pricing experiment {experimentId} not found.");

        var outcomes = await experimentRepository
            .GetOutcomesAsync(experimentId, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);

        var assignmentCounts = await experimentRepository
            .GetAssignmentCountsAsync(experimentId, cancellationToken)
            .ConfigureAwait(false);

        var orderedVariants = experiment.Variants
            .OrderByDescending(v => v.IsControl)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var control = orderedVariants.FirstOrDefault(v => v.IsControl);
        var controlAssignments = control is null ? 0 : assignmentCounts.GetValueOrDefault(control.Id);
        var controlRevenuePerAssignment = control is not null && controlAssignments > 0
            ? outcomes.Where(o => o.VariantId == control.Id).Sum(o => o.Amount) / controlAssignments
            : (decimal?)null;

        var rows = new List<PricingExperimentVariantAnalytics>();
        foreach (var variant in orderedVariants)
        {
            var variantOutcomes = outcomes.Where(o => o.VariantId == variant.Id).ToList();
            var conversions = variantOutcomes.Count;
            var revenue = variantOutcomes.Sum(o => o.Amount);
            var assignments = assignmentCounts.GetValueOrDefault(variant.Id);

            decimal? revenuePerAssignment = assignments > 0 ? revenue / assignments : null;
            decimal? lift = null;
            if (!variant.IsControl &&
                revenuePerAssignment.HasValue &&
                controlRevenuePerAssignment is > 0)
            {
                lift = decimal.Round(
                    (revenuePerAssignment.Value - controlRevenuePerAssignment.Value) / controlRevenuePerAssignment.Value * 100,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            rows.Add(new PricingExperimentVariantAnalytics(
                variant.Id,
                variant.Name,
                variant.IsControl,
                assignments,
                conversions,
                assignments > 0 ? decimal.Round((decimal)conversions / assignments, 4, MidpointRounding.AwayFromZero) : null,
                decimal.Round(revenue, 2, MidpointRounding.AwayFromZero),
                conversions > 0 ? decimal.Round(revenue / conversions, 2, MidpointRounding.AwayFromZero) : null,
                revenuePerAssignment.HasValue ? decimal.Round(revenuePerAssignment.Value, 2, MidpointRounding.AwayFromZero) : null,
                lift));
        }

        return new PricingExperimentAnalytics(
            experimentId,
            experiment.Name,
            experiment.Status,
            fromUtc,
            toUtc,
            outcomes.Count,
            decimal.Round(outcomes.Sum(o => o.Amount), 2, MidpointRounding.AwayFromZero),
            outcomes.Count > 0 ? outcomes[0].Currency : "USD",
            rows);
    }

    /// <inheritdoc />
    public async Task<PricingScheduleSweepResult> RunSchedulingSweepAsync(
        DateTime? now = null,
        CancellationToken cancellationToken = default)
    {
        var ranAt = now ?? SystemClock.UtcNow;
        var rulesActivated = 0;
        var rulesDeactivated = 0;
        var experimentsStarted = 0;
        var experimentsCompleted = 0;

        // ---- Opt-in scheduled pricing rules: IsActive mirrors the date window ----
        var scheduledRules = await pricingRuleRepository
            .GetScheduledRulesAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var rule in scheduledRules)
        {
            var shouldBeActive = (!rule.StartDate.HasValue || rule.StartDate.Value <= ranAt) &&
                                 (!rule.EndDate.HasValue || rule.EndDate.Value > ranAt);

            if (shouldBeActive && !rule.IsActive)
            {
                rule.IsActive = true;
                await pricingRuleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
                rulesActivated++;
            }
            else if (!shouldBeActive && rule.IsActive)
            {
                rule.IsActive = false;
                await pricingRuleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
                rulesDeactivated++;
            }
        }

        // ---- Date-bounded experiments: start at StartDate, complete at EndDate ----
        var experiments = await experimentRepository
            .GetPagedAsync(take: 200, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var experiment in experiments.Items)
        {
            if (experiment.Status == PricingExperimentStatus.Draft &&
                experiment.StartDate.HasValue &&
                experiment.StartDate.Value <= ranAt)
            {
                experiment.Status = PricingExperimentStatus.Running;
                await experimentRepository.UpdateAsync(experiment, cancellationToken).ConfigureAwait(false);
                experimentsStarted++;
            }
            else if (experiment.Status == PricingExperimentStatus.Running &&
                     experiment.EndDate.HasValue &&
                     experiment.EndDate.Value <= ranAt)
            {
                experiment.Status = PricingExperimentStatus.Completed;
                await experimentRepository.UpdateAsync(experiment, cancellationToken).ConfigureAwait(false);
                experimentsCompleted++;
            }
        }

        return new PricingScheduleSweepResult(
            rulesActivated,
            rulesDeactivated,
            experimentsStarted,
            experimentsCompleted,
            ranAt);
    }

    /// <summary>
    ///     Returns the arm already assigned to the key, or allocates and persists a new sticky
    ///     assignment. The variant is null when the key hashes into the traffic holdout.
    /// </summary>
    private async Task<(PricingExperimentVariant? Variant, Guid? AssignmentId, DateTime AssignedAt)> ResolveOrCreateAssignmentAsync(
        PricingExperiment experiment,
        string bucketKey,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var existing = await experimentRepository
            .GetAssignmentAsync(experiment.Id, bucketKey, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return (existing.Variant, existing.Id, existing.AssignedAt);
        }

        var variants = experiment.Variants.ToList();
        var variant = PricingExperimentAllocator.Allocate(experiment, variants, bucketKey);
        if (variant is null)
        {
            return (null, null, SystemClock.UtcNow); // held out by the traffic gate
        }

        var assignedAt = SystemClock.UtcNow;
        var assignment = new PricingExperimentAssignment
        {
            ExperimentId = experiment.Id,
            VariantId = variant.Id,
            BucketKey = bucketKey.Trim(),
            UserId = userId,
            AssignedAt = assignedAt
        };

        await experimentRepository.AddAssignmentAsync(assignment, cancellationToken).ConfigureAwait(false);
        await experimentRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (variant, assignment.Id, assignedAt);
    }
}
