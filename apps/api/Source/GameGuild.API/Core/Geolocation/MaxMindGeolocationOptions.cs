namespace GameGuild.API.Geolocation;

/// <summary>
///     Host configuration for the offline MaxMind GeoLite2 adapter. Bound to
/// <c>Geolocation:MaxMind</c>. Absent/invalid configuration resolves to no location
/// (fail-closed to Unknown) rather than an error.
/// </summary>
public sealed class MaxMindGeolocationOptions
{
    /// <summary>Section name used by the composition root.</summary>
    public const string SectionName = "Geolocation:MaxMind";

    /// <summary>Path to the GeoLite2-City(.mmdb) database file. Empty or missing file disables enrichment.</summary>
    public string? DatabasePath { get; set; }
}
