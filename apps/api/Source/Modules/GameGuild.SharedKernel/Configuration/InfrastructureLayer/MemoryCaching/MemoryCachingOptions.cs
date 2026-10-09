namespace GameGuild.Configuration.InfrastructureLayer.MemoryCaching;

/// <summary>
///     Configuration options for memory caching
/// </summary>
public sealed class MemoryCachingOptions : BaseOptions
{
    /// <summary>
    ///     Maximum size limit for memory cache in bytes (default: 100MB)
    /// </summary>
    public long SizeLimit { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    ///     Percentage of cache to remove when limit is reached (default: 5%)
    /// </summary>
    public double CompactionPercentage { get; set; } = 0.05; // 5%

    /// <summary>
    ///     Frequency to scan for expired entries (default: 1 minute)
    /// </summary>
    public TimeSpan ExpirationScanFrequency { get; set; } = TimeSpan.FromMinutes(1);

    public override void Validate()
    {
        base.Validate();

        if (SizeLimit <= 0)
        {
            throw new InvalidOperationException("Size limit must be greater than zero.");
        }

        if (CompactionPercentage <= 0 || CompactionPercentage >= 1)
        {
            throw new InvalidOperationException("Compaction percentage must be between 0 and 1.");
        }

        if (ExpirationScanFrequency <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Expiration scan frequency must be greater than zero.");
        }
    }

    public static MemoryCachingOptions CreateDefault() { return new MemoryCachingOptions(); }
}
