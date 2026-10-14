using System.Net;
using System.Net.Sockets;
using GameGuild;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Geolocation;

/// <summary>
///     Offline MaxMind GeoLite2 adapter for <see cref="IGeolocationProvider"/>. The database
///     reader is opened once from <c>Geolocation:MaxMind:DatabasePath</c> and is thread-safe for
///     concurrent lookups. Fail-closed contract: missing/unreadable database, private/loopback
///     addresses and lookup misses resolve to <c>null</c> (Unknown) — enrichment degrades
///     gracefully and never fails the login pipeline.
/// </summary>
public sealed class MaxMindGeolocationProvider : IGeolocationProvider, IDisposable
{
    private readonly DatabaseReader? reader;
    private readonly ILogger<MaxMindGeolocationProvider> logger;
    private bool disposed;

    public MaxMindGeolocationProvider(IOptions<MaxMindGeolocationOptions> options, ILogger<MaxMindGeolocationProvider> logger)
        : this(options.Value.DatabasePath, logger)
    {
    }

    internal MaxMindGeolocationProvider(string? databasePath, ILogger<MaxMindGeolocationProvider> logger)
    {
        this.logger = logger;

        if (string.IsNullOrWhiteSpace(databasePath))
        {
            logger.LogInformation("MaxMind geolocation is not configured; location enrichment stays disabled (Unknown)");
            return;
        }

        if (!File.Exists(databasePath))
        {
            logger.LogWarning("MaxMind geolocation database not found at {DatabasePath}; location enrichment stays disabled (Unknown)", databasePath);
            return;
        }

        try
        {
            reader = new DatabaseReader(databasePath);
            logger.LogInformation("MaxMind geolocation database loaded from {DatabasePath}", databasePath);
        }
        catch (Exception ex)
        {
            // Fail closed: an unreadable database must disable enrichment, not startup.
            logger.LogError(ex, "MaxMind geolocation database at {DatabasePath} could not be opened; location enrichment stays disabled (Unknown)", databasePath);
        }
    }

    /// <inheritdoc />
    public Task<GeolocationResult?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (reader is null || string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var address))
        {
            return Task.FromResult<GeolocationResult?>(null);
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.IsPrivate())
        {
            return Task.FromResult<GeolocationResult?>(null);
        }

        try
        {
            var response = reader.City(address);
            if (response is null)
            {
                return Task.FromResult<GeolocationResult?>(null);
            }

            var result = new GeolocationResult
            {
                Country = NullIfEmpty(response.Country?.Name),
                CountryCode = NullIfEmpty(response.Country?.IsoCode),
                Region = NullIfEmpty(response.MostSpecificSubdivision?.Name),
                City = NullIfEmpty(response.City?.Name),
                PostalCode = NullIfEmpty(response.Postal?.Code),
                Latitude = response.Location?.Latitude,
                Longitude = response.Location?.Longitude,
                Timezone = NullIfEmpty(response.Location?.TimeZone)
            };

            // An address with no country data at all is a miss, not a partial result.
            return Task.FromResult<GeolocationResult?>(result.CountryCode is null ? null : result);
        }
        catch (Exception ex) when (ex is AddressNotFoundException or GeoIP2Exception)
        {
            return Task.FromResult<GeolocationResult?>(null);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        reader?.Dispose();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>IPAddress extension kept next to its only consumer.</summary>
file static class IPAddressExtensions
{
    public static bool IsPrivate(this IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || bytes[0] == 127;
        }

        // IPv6: unique-local (fc00::/7) and link-local (fe80::/10).
        var ipv6 = address.GetAddressBytes();
        return (ipv6[0] & 0xFE) == 0xFC || (ipv6[0] == 0xFE && (ipv6[1] & 0xC0) == 0x80);
    }
}
