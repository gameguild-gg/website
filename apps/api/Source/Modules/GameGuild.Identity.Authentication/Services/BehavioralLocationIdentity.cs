namespace GameGuild.Identity.Authentication;

/// <summary>Comparable coarse locations without treating display formats or missing data as risk.</summary>
internal static class BehavioralLocationIdentity
{
    internal readonly record struct Key(bool UsesCountryCode, string Country, string City);

    internal readonly record struct Context(string? CountryCode, string? CountryName, string City)
    {
        internal Key CurrentKey => new(CountryCode is not null, CountryCode ?? CountryName!, City);

        internal Key? HistoricalKey(AuthenticationAttempt attempt)
        {
            if (ReadHistory(attempt) is not { } history)
            {
                return null;
            }
            if (CountryCode is { } currentCode)
            {
                if (history.CountryCode is { } historicalCode)
                {
                    return new Key(true, historicalCode, history.City);
                }
                if (CountryName is { } currentName && history.CountryName is { } historicalName)
                {
                    return currentName == historicalName
                        ? new Key(true, currentCode, history.City)
                        : new Key(false, historicalName, history.City);
                }
                return null;
            }
            return CountryName is not null && history.CountryName is { } name
                ? new Key(false, name, history.City)
                : null;
        }
    }

    internal static Context? Capture(LocationInfo? location)
    {
        var context = CaptureCountry(location);
        return context is { City.Length: > 0 } ? context : null;
    }

    internal static bool TryComparableCountries(LocationInfo current, LocationInfo previous,
        out string currentCountry, out string previousCountry)
    {
        currentCountry = string.Empty; previousCountry = string.Empty;
        if (CaptureCountry(current) is not { } left || CaptureCountry(previous) is not { } right)
        {
            return false;
        }
        if (left.CountryCode is { } leftCode && right.CountryCode is { } rightCode)
        {
            currentCountry = leftCode; previousCountry = rightCode;
            return true;
        }
        if (left.CountryName is { } leftName && right.CountryName is { } rightName)
        {
            currentCountry = leftName; previousCountry = rightName;
            return true;
        }
        return false;
    }

    internal static LocationInfo? FromHistory(AuthenticationAttempt attempt)
    {
        var history = ReadHistory(attempt);
        if (history is null && Normalize(attempt.Country) is { } explicitCountry)
        {
            var code = NormalizeCountryCode(explicitCountry);
            history = new Context(code, code is null ? explicitCountry : null, Normalize(attempt.City) ?? string.Empty);
        }
        if (history is null && string.IsNullOrWhiteSpace(attempt.Country) && string.IsNullOrWhiteSpace(attempt.City) &&
            NormalizeCountryCode(attempt.Location) is { } legacyCode)
        {
            history = new Context(legacyCode, null, string.Empty);
        }
        if (history is not { } value)
        {
            return null;
        }
        var city = attempt.City?.Trim();
        if (string.IsNullOrEmpty(city) && value.City.Length > 0)
        {
            var display = attempt.Location!.Trim();
            city = display[3..].Trim();
        }
        return new LocationInfo { Country = value.CountryName ?? value.CountryCode, CountryCode = value.CountryCode, City = city };
    }

    private static Context? CaptureCountry(LocationInfo? location)
    {
        if (location is null) { return null; }
        // Country previously also carried ISO codes; the explicit code field takes priority.
        var code = NormalizeCountryCode(location.CountryCode) ?? NormalizeCountryCode(location.Country);
        var name = NormalizeCountryCode(location.Country) is null ? Normalize(location.Country) : null;
        return code is null && name is null ? null : new Context(code, name, Normalize(location.City) ?? string.Empty);
    }

    private static Context? ReadHistory(AuthenticationAttempt attempt)
    {
        var code = NormalizeCountryCode(attempt.Country);
        var name = code is null ? Normalize(attempt.Country) : null;
        var city = Normalize(attempt.City);
        if (city is not null)
        {
            if (name is null && !string.IsNullOrWhiteSpace(attempt.Location))
            {
                // The real writer knows City and emits exactly "City, Country name". Use
                // that known prefix; splitting arbitrary display strings would guess a city.
                var prefix = attempt.City!.Trim();
                var display = attempt.Location.Trim();
                if (display.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var remainder = display[prefix.Length..].TrimStart();
                    if (remainder.Length > 0 && remainder[0] == ',')
                    {
                        var label = Normalize(remainder[1..]);
                        code ??= NormalizeCountryCode(label);
                        name = NormalizeCountryCode(label) is null ? label : null;
                    }
                }
            }
            if (code is not null || name is not null)
            {
                return new Context(code, name, city);
            }
        }
        if (!string.IsNullOrWhiteSpace(attempt.Country) || !string.IsNullOrWhiteSpace(attempt.City))
        {
            return null;
        }
        var legacy = attempt.Location?.Trim();
        if (legacy is { Length: > 3 } && legacy[2] == '-' && NormalizeCountryCode(legacy[..2]) is { } legacyCode &&
            Normalize(legacy[3..]) is { } legacyCity)
        {
            // Only the first delimiter belongs to the legacy country code. Preserve city hyphens.
            return new Context(legacyCode, null, legacyCity);
        }
        return null;
    }

    private static string? NormalizeCountryCode(string? value) =>
        Normalize(value) is { Length: 2 } code && code.All(char.IsAsciiLetter) ? code : null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
