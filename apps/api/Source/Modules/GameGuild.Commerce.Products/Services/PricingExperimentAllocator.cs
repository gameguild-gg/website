using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Deterministic bucket allocator for pricing A/B experiments (issue #395). A bucket key
///     (typically the acting user id) plus the experiment id are hashed (SHA-256) into a stable
///     bucket in [0, 1); the bucket is first gated by the experiment's traffic percentage and
///     then mapped onto the variant allocation ranges. Pure and side-effect free: the same
///     (experiment, key) pair always lands in the same arm, which makes assignments sticky
///     without coordination and keeps the calculation path idempotent.
/// </summary>
public static class PricingExperimentAllocator
{
    private const int BucketSpace = 1_000_000;

    /// <summary>
    ///     Computes the deterministic bucket value in [0, 1) for a key inside an experiment.
    /// </summary>
    /// <param name="experimentId">Experiment id (salt)</param>
    /// <param name="bucketKey">Stable bucketing key (e.g. user id)</param>
    public static decimal BucketValue(Guid experimentId, string bucketKey)
    {
        if (string.IsNullOrWhiteSpace(bucketKey))
        {
            throw new ArgumentException("Bucket key is required.", nameof(bucketKey));
        }

        var normalized = bucketKey.Trim();
        Span<byte> hash = stackalloc byte[32];
        _ = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{experimentId:N}:{normalized}"),
            hash);

        // First 8 bytes of the digest → a uniform bucket index in [0, BucketSpace).
        var segment = BitConverter.ToUInt64(hash[..8]);
        var bucket = (int)(segment % BucketSpace);

        return bucket / (decimal)BucketSpace;
    }

    /// <summary>
    ///     Allocates a key to one of <paramref name="variants"/> under an experiment's traffic
    ///     percentage. Returns null when the key hashes into the holdout share (above the
    ///     traffic percentage) so the caller prices without any experiment adjustment.
    /// </summary>
    /// <param name="experiment">Experiment (traffic gate)</param>
    /// <param name="variants">Arms with allocation weights (need not sum to 100)</param>
    /// <param name="bucketKey">Stable bucketing key</param>
    public static PricingExperimentVariant? Allocate(
        PricingExperiment experiment,
        IReadOnlyList<PricingExperimentVariant> variants,
        string bucketKey)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ArgumentNullException.ThrowIfNull(variants);

        if (variants.Count == 0)
        {
            return null;
        }

        var trafficShare = Math.Clamp(experiment.TrafficPercentage, 0, 100) / 100m;
        var bucket = BucketValue(experiment.Id, bucketKey);

        if (trafficShare <= 0 || bucket >= trafficShare)
        {
            return null; // held out
        }

        // Normalize the bucket into the enrolled share, then walk cumulative allocation
        // weights in a stable order (control first, then by name) to find the arm.
        var enrolledPosition = bucket / trafficShare;
        var ordered = variants
            .OrderByDescending(v => v.IsControl)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalWeight = ordered.Sum(v => Math.Max(0, v.AllocationPercentage));
        if (totalWeight <= 0)
        {
            return ordered[0];
        }

        var cumulative = 0m;
        foreach (var variant in ordered)
        {
            cumulative += Math.Max(0, variant.AllocationPercentage) / (decimal)totalWeight;
            if (enrolledPosition < cumulative)
            {
                return variant;
            }
        }

        return ordered[^1];
    }

    /// <summary>
    ///     Computes the per-unit price of the effective price under a variant arm: a fixed
    ///     price pins the unit price (never above the effective price), a discount percentage
    ///     shaves it, and the control arm leaves it untouched. The result is floored at zero.
    /// </summary>
    /// <param name="variant">Arm to apply (null = no adjustment)</param>
    /// <param name="effectiveUnitPrice">Effective per-unit price before the experiment</param>
    public static decimal ApplyVariant(PricingExperimentVariant? variant, decimal effectiveUnitPrice)
    {
        if (variant is null || variant.IsControl)
        {
            return effectiveUnitPrice;
        }

        if (variant.FixedPrice.HasValue)
        {
            return Math.Max(0, Math.Min(effectiveUnitPrice, variant.FixedPrice.Value));
        }

        if (variant.DiscountPercentage.HasValue)
        {
            var discount = effectiveUnitPrice * (variant.DiscountPercentage.Value / 100m);
            return Math.Max(0, effectiveUnitPrice - discount);
        }

        return effectiveUnitPrice;
    }
}
