using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Symmetric JSON (de)serialization for the <see cref="UserSession.DeviceInfo"/> and
///     <see cref="UserSession.Location"/> metadata columns. Null members are omitted so the
///     payloads stay within the column limits; both directions share the same options so a
///     payload written by the session service round-trips through the API DTOs.
/// </summary>
public static class SessionEnrichmentJson
{
    private const int DeviceInfoMaxLength = 2000;
    private const int LocationMaxLength = 500;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Serializes device metadata, dropping the raw user agent first if the column limit would be exceeded.</summary>
    public static string? SerializeDeviceInfo(DeviceInfo? deviceInfo)
    {
        if (deviceInfo is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(deviceInfo, Options);
        if (json.Length <= DeviceInfoMaxLength)
        {
            return json;
        }

        // The raw agent is the only unbounded member; enrichment must never fail persistence.
        deviceInfo.UserAgent = null;
        json = JsonSerializer.Serialize(deviceInfo, Options);
        return json.Length <= DeviceInfoMaxLength ? json : null;
    }

    /// <summary>Serializes location metadata, degrading to a minimal payload if the column limit would be exceeded.</summary>
    public static string? SerializeLocation(LocationInfo? location)
    {
        if (location is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(location, Options);
        if (json.Length <= LocationMaxLength)
        {
            return json;
        }

        var minimal = new LocationInfo
        {
            IpAddress = location.IpAddress,
            Country = location.Country,
            CountryCode = location.CountryCode,
            City = location.City
        };
        json = JsonSerializer.Serialize(minimal, Options);
        return json.Length <= LocationMaxLength ? json : null;
    }

    /// <summary>Null-safe device metadata deserialization; malformed payloads yield <c>null</c>.</summary>
    public static DeviceInfo? DeserializeDeviceInfo(string? json)
    {
        return Deserialize<DeviceInfo>(json);
    }

    /// <summary>Null-safe location metadata deserialization; malformed payloads yield <c>null</c>.</summary>
    public static LocationInfo? DeserializeLocation(string? json)
    {
        return Deserialize<LocationInfo>(json);
    }

    /// <summary>Maps a resolved geolocation onto the session location metadata shape.</summary>
    public static LocationInfo? ToLocationInfo(GeolocationResult? result, string ipAddress)
    {
        if (result is null)
        {
            return null;
        }

        return new LocationInfo
        {
            IpAddress = ipAddress,
            Country = result.Country,
            CountryCode = result.CountryCode,
            Region = result.Region,
            City = result.City,
            PostalCode = result.PostalCode,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            Timezone = result.Timezone
        };
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
