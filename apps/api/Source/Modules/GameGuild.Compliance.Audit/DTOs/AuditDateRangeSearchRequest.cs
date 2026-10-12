using System.ComponentModel.DataAnnotations;

namespace GameGuild.Compliance.Audit;

/// <summary>Flexible, timezone-aware filters for searching audit records by date range.</summary>
public sealed class AuditDateRangeSearchRequest
{
    /// <summary>ISO-8601, Unix seconds/milliseconds, or relative expression such as now-7d.</summary>
    [MaxLength(80)]
    public string? Start { get; set; }

    /// <summary>ISO-8601, Unix seconds/milliseconds, or relative expression such as now.</summary>
    [MaxLength(80)]
    public string? End { get; set; }

    /// <summary>Shortcut: last24h, last7d, last30d, today, thisWeek, or thisMonth.</summary>
    [MaxLength(32)]
    public string? Period { get; set; }

    /// <summary>Timezone used to interpret offset-free dates and render local bucket times.</summary>
    [MaxLength(80)]
    public string? TimeZoneId { get; set; } = "UTC";

    public AuditActivityBucketSize? BucketSize { get; set; }

    public Guid? UserId { get; set; }

    public Guid? TenantId { get; set; }

    public string? ActionType { get; set; }

    public string? ResourceType { get; set; }

    public AuditCategory? Category { get; set; }

    public AuditRiskLevel? RiskLevel { get; set; }

    public bool? Success { get; set; }

    public string? IpAddress { get; set; }

    [Range(0, int.MaxValue)]
    public int Skip { get; set; }

    [Range(1, 1000)]
    public int Take { get; set; } = 100;
}

/// <summary>Resolved, UTC-normalized bounds and the display timezone requested by the caller.</summary>
public sealed record ResolvedAuditDateRange(DateTime StartUtc, DateTime EndUtc, TimeZoneInfo TimeZone);

/// <summary>Result of resolving flexible date inputs into a validated UTC range.</summary>
public sealed record AuditDateRangeResolution(
    ResolvedAuditDateRange? Range,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Range is not null && Errors.Count == 0;
}

/// <summary>Parses flexible date inputs while rejecting ambiguous local wall-clock times.</summary>
public static class AuditDateRangeResolver
{
    private static readonly System.Text.RegularExpressions.Regex UnixOffsetPattern = new(
        @"(?:Z|[+-]\d{2}:?\d{2})$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex NowRelativePattern = new(
        @"^now(?:(?<sign>[+-])(?<amount>\d+)(?<unit>[smhdw]))?$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex LastRelativePattern = new(
        @"^last\s+(?<amount>\d+)\s+(?<unit>seconds?|minutes?|hours?|days?|weeks?)$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex DateOnlyPattern = new(
        @"^\d{4}-\d{2}-\d{2}$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static AuditDateRangeResolution Resolve(
        string? start,
        string? end,
        string? period,
        string? timeZoneId,
        DateTime nowUtc)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var normalizedStart = NormalizeOptional(start);
        var normalizedEnd = NormalizeOptional(end);
        var normalizedPeriod = NormalizeOptional(period);
        var zoneId = NormalizeOptional(timeZoneId) ?? "UTC";

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            AddError(errors, nameof(AuditDateRangeSearchRequest.TimeZoneId), $"'{zoneId}' is not a supported timezone.");
            return new AuditDateRangeResolution(null, ToErrors(errors));
        }

        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        DateTime startUtc;
        DateTime endUtc;

        if (normalizedPeriod is not null)
        {
            if (normalizedStart is not null || normalizedEnd is not null)
            {
                AddError(errors, nameof(AuditDateRangeSearchRequest.Period), "Choose either Period or explicit Start and End values, not both.");
                return new AuditDateRangeResolution(null, ToErrors(errors));
            }

            if (!TryResolvePeriod(normalizedPeriod, nowUtc, timeZone, out startUtc, out endUtc))
            {
                AddError(errors, nameof(AuditDateRangeSearchRequest.Period), "Period must be last24h, last7d, last30d, today, thisWeek, or thisMonth.");
                return new AuditDateRangeResolution(null, ToErrors(errors));
            }
        }
        else
        {
            if (normalizedStart is null || normalizedEnd is null)
            {
                AddError(errors, nameof(AuditDateRangeSearchRequest.Start), "Provide both Start and End values, or select a Period shortcut.");
                AddError(errors, nameof(AuditDateRangeSearchRequest.End), "Provide both Start and End values, or select a Period shortcut.");
                return new AuditDateRangeResolution(null, ToErrors(errors));
            }

            if (!TryParseDate(normalizedStart, timeZone, nowUtc, isEndBound: false, out startUtc, out var startError))
            {
                AddError(errors, nameof(AuditDateRangeSearchRequest.Start), startError!);
            }

            if (!TryParseDate(normalizedEnd, timeZone, nowUtc, isEndBound: true, out endUtc, out var endError))
            {
                AddError(errors, nameof(AuditDateRangeSearchRequest.End), endError!);
            }

            if (errors.Count > 0)
            {
                return new AuditDateRangeResolution(null, ToErrors(errors));
            }
        }

        if (startUtc > endUtc)
        {
            AddError(errors, nameof(AuditDateRangeSearchRequest.Start), "Start must be earlier than or equal to End.");
            AddError(errors, nameof(AuditDateRangeSearchRequest.End), "End must be later than or equal to Start.");
            return new AuditDateRangeResolution(null, ToErrors(errors));
        }

        return new AuditDateRangeResolution(new ResolvedAuditDateRange(startUtc, endUtc, timeZone), ToErrors(errors));
    }

    private static bool TryResolvePeriod(string period, DateTime nowUtc, TimeZoneInfo timeZone, out DateTime startUtc, out DateTime endUtc)
    {
        var normalized = period.Trim().ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        endUtc = nowUtc;

        switch (normalized)
        {
            case "last24h":
            case "last24hours":
                startUtc = nowUtc.AddHours(-24);
                return true;
            case "last7d":
            case "last7days":
            case "lastweek":
                startUtc = nowUtc.AddDays(-7);
                return true;
            case "last30d":
            case "last30days":
            case "lastmonth":
                startUtc = nowUtc.AddDays(-30);
                return true;
            default:
                break;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localStart = normalized switch
        {
            "today" => localNow.Date,
            "thisweek" => localNow.Date.AddDays(-(((int)localNow.DayOfWeek + 6) % 7)),
            "thismonth" => new DateTime(localNow.Year, localNow.Month, 1),
            _ => DateTime.MinValue
        };

        if (localStart == DateTime.MinValue)
        {
            startUtc = default;
            return false;
        }

        return TryConvertLocalToUtc(localStart, timeZone, out startUtc);
    }

    private static bool TryParseDate(string value, TimeZoneInfo timeZone, DateTime nowUtc, bool isEndBound, out DateTime utc, out string? error)
    {
        error = null;
        utc = default;

        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var unixValue))
        {
            try
            {
                utc = unixValue >= 100_000_000_000L || unixValue <= -100_000_000_000L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue).UtcDateTime
                    : DateTimeOffset.FromUnixTimeSeconds(unixValue).UtcDateTime;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                error = "Unix timestamp is outside the supported date range.";
                return false;
            }
        }

        var nowMatch = NowRelativePattern.Match(value);
        if (nowMatch.Success)
        {
            try
            {
                utc = ApplyRelative(nowUtc, nowMatch.Groups["sign"].Value, nowMatch.Groups["amount"].Value, nowMatch.Groups["unit"].Value);
                return true;
            }
            catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
            {
                error = "Relative date expression is outside the supported date range.";
                return false;
            }
        }

        var lastMatch = LastRelativePattern.Match(value);
        if (lastMatch.Success)
        {
            try
            {
                utc = ApplyRelative(nowUtc, "-", lastMatch.Groups["amount"].Value, lastMatch.Groups["unit"].Value);
                return true;
            }
            catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
            {
                error = "Relative date expression is outside the supported date range.";
                return false;
            }
        }

        if (UnixOffsetPattern.IsMatch(value))
        {
            if (DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var offsetDate))
            {
                utc = offsetDate.UtcDateTime;
                return true;
            }

            error = "Date must be a valid ISO-8601 timestamp.";
            return false;
        }

        if (!DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var localDate))
        {
            error = "Date must be ISO-8601, Unix seconds/milliseconds, or a supported relative expression.";
            return false;
        }

        localDate = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        if (isEndBound && DateOnlyPattern.IsMatch(value))
        {
            try
            {
                localDate = localDate.AddDays(1).AddTicks(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                error = "Date is outside the supported range for an inclusive calendar-day bound.";
                return false;
            }
        }

        if (!TryConvertLocalToUtc(localDate, timeZone, out utc))
        {
            error = "Local date falls in an ambiguous or nonexistent daylight-saving time in the selected timezone.";
            return false;
        }

        return true;
    }

    private static DateTime ApplyRelative(DateTime nowUtc, string sign, string amountText, string unit)
    {
        if (string.IsNullOrEmpty(amountText))
        {
            return nowUtc;
        }

        var amount = long.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);
        var multiplier = sign == "+" ? 1d : -1d;
        return unit.ToLowerInvariant() switch
        {
            "s" or "second" or "seconds" => nowUtc.AddSeconds(amount * multiplier),
            "m" or "minute" or "minutes" => nowUtc.AddMinutes(amount * multiplier),
            "h" or "hour" or "hours" => nowUtc.AddHours(amount * multiplier),
            "d" or "day" or "days" => nowUtc.AddDays(amount * multiplier),
            "w" or "week" or "weeks" => nowUtc.AddDays(amount * multiplier * 7),
            _ => throw new FormatException("Unsupported relative date unit.")
        };
    }

    private static bool TryConvertLocalToUtc(DateTime localDate, TimeZoneInfo timeZone, out DateTime utc)
    {
        localDate = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localDate) || timeZone.IsAmbiguousTime(localDate))
        {
            utc = default;
            return false;
        }

        try
        {
            utc = TimeZoneInfo.ConvertTimeToUtc(localDate, timeZone);
            return true;
        }
        catch (ArgumentException)
        {
            utc = default;
            return false;
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddError(IDictionary<string, List<string>> errors, string member, string message)
    {
        if (!errors.TryGetValue(member, out var memberErrors))
        {
            memberErrors = [];
            errors[member] = memberErrors;
        }

        memberErrors.Add(message);
    }

    private static IReadOnlyDictionary<string, string[]> ToErrors(IDictionary<string, List<string>> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
}
