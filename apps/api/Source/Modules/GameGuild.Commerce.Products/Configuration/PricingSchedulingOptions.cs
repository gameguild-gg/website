namespace GameGuild.Commerce.Products;

/// <summary>
///     Options for the automated pricing scheduling worker (issue #395).
/// </summary>
public sealed class PricingSchedulingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PricingScheduling";

    /// <summary>Whether the background scheduler runs. Defaults to true; scheduling itself is per-rule opt-in.</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>Minutes between scheduling passes (minimum 1).</summary>
    public int WorkerIntervalMinutes { get; set; } = 5;
}
