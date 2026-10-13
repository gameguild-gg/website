namespace GameGuild.Identity.Authentication;

/// <summary>
///     Geo-velocity math for session security analysis: great-circle distance (Haversine) and
///     implied travel speed between two populated locations. Pure and dependency-free; callers
///     must verify both locations carry coordinates before assessing velocity.
/// </summary>
public static class GeoVelocity
{
    /// <summary>
    ///     The default impossible-travel speed threshold, in kilometers per hour. Commercial
    ///     airliners cruise near 900 km/h, so sustained speeds above this cannot be explained by
    ///     legitimate travel between two consecutive sign-ins.
    /// </summary>
    public const double DefaultImpossibleTravelSpeedKmh = 900;

    private const double EarthRadiusKm = 6371.0;

    /// <summary>Returns the great-circle distance in kilometers between two coordinate pairs.</summary>
    public static double DistanceKm(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        var latRadA = DegreesToRadians(latitudeA);
        var latRadB = DegreesToRadians(latitudeB);
        var deltaLat = DegreesToRadians(latitudeB - latitudeA);
        var deltaLon = DegreesToRadians(longitudeB - longitudeA);

        var haversine = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                        Math.Cos(latRadA) * Math.Cos(latRadB) * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var centralAngle = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
        return EarthRadiusKm * centralAngle;
    }

    /// <summary>
    ///     Returns the implied travel speed in km/h between two populated locations over the
    ///     elapsed time, or <c>null</c> when velocity cannot be assessed (missing coordinates,
    ///     non-positive elapsed time, or identical points).
    /// </summary>
    public static double? SpeedKmh(LocationInfo origin, LocationInfo destination, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        if (origin.Latitude is not { } latA || origin.Longitude is not { } lonA ||
            destination.Latitude is not { } latB || destination.Longitude is not { } lonB ||
            elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        var distance = DistanceKm(latA, lonA, latB, lonB);
        if (distance < 1)
        {
            return null;
        }

        return distance / elapsed.TotalHours;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
