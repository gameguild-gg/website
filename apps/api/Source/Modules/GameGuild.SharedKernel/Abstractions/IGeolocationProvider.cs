namespace GameGuild;

/// <summary>
///     Resolves a coarse geographic location for an IP address. The contract is intentionally
///     free of any GeoIP implementation detail so platform modules can depend on the abstraction
///     while a host composition root plugs in a concrete provider (e.g. an offline MaxMind
///     database). Implementations must be thread-safe and must never throw for unknown,
///     malformed, private, or loopback addresses — return <c>null</c> instead.
/// </summary>
public interface IGeolocationProvider
{
    /// <summary>
    ///     Attempts to resolve the geographic location of a public IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved geolocation, or <c>null</c> when no data is available.</returns>
    Task<GeolocationResult?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>
///     Coarse geographic data for a resolved IP address. Only the fields the provider actually
///     knows are populated; the rest stay <c>null</c>.
/// </summary>
public sealed record GeolocationResult
{
    /// <summary>Country name.</summary>
    public string? Country { get; init; }

    /// <summary>ISO country code (US, GB, etc.).</summary>
    public string? CountryCode { get; init; }

    /// <summary>State or province name.</summary>
    public string? Region { get; init; }

    /// <summary>City name.</summary>
    public string? City { get; init; }

    /// <summary>Postal/ZIP code.</summary>
    public string? PostalCode { get; init; }

    /// <summary>Geographic latitude coordinate.</summary>
    public double? Latitude { get; init; }

    /// <summary>Geographic longitude coordinate.</summary>
    public double? Longitude { get; init; }

    /// <summary>Timezone identifier (America/New_York, Europe/London, etc.).</summary>
    public string? Timezone { get; init; }
}

/// <summary>
///     Default no-op geolocation provider: location enrichment is unavailable and every lookup
///     resolves to <c>null</c>. Registered as the fallback so session security features degrade
///     gracefully (count-based analysis) until a host registers a real provider.
/// </summary>
public sealed class NoOpGeolocationProvider : IGeolocationProvider
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly NoOpGeolocationProvider Instance = new();

    private NoOpGeolocationProvider() { }

    /// <inheritdoc />
    public Task<GeolocationResult?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<GeolocationResult?>(null);
    }
}
