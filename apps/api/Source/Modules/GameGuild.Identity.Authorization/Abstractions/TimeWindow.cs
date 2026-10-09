using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Represents a time window with timezone support for ABAC environment constraints.
/// </summary>
[JsonConverter(typeof(TimeWindowJsonConverter))]
public sealed class TimeWindow
{
    /// <summary>
    ///     Gets or sets the start time of the window.
    /// </summary>
    public TimeOnly Start { get; init; }

    /// <summary>
    ///     Gets or sets the end time of the window.
    /// </summary>
    public TimeOnly End { get; init; }

    /// <summary>
    ///     Gets or sets the timezone ID (e.g., "America/New_York", "UTC", "Europe/London").
    ///     If null or empty, UTC is assumed.
    /// </summary>
    public string? TimeZoneId { get; init; }

    /// <summary>
    ///     Gets the resolved TimeZoneInfo for this window.
    /// </summary>
    [JsonIgnore]
    public TimeZoneInfo TimeZone => string.IsNullOrEmpty(TimeZoneId)
        ? TimeZoneInfo.Utc
        : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    /// <summary>
    ///     Creates a time window from a string in format "HH:mm-HH:mm" or "HH:mm-HH:mm@TimeZoneId".
    /// </summary>
    /// <param name="windowString">The time window string.</param>
    /// <returns>The parsed time window.</returns>
    public static TimeWindow? Parse(string? windowString)
    {
        if (string.IsNullOrWhiteSpace(windowString))
        {
            return null;
        }

        string? timeZoneId = null;
        var timePart = windowString;

        // Check for timezone suffix (e.g., "09:00-17:00@America/New_York")
        var atIndex = windowString.IndexOf('@');
        if (atIndex > 0)
        {
            timePart = windowString[..atIndex];
            timeZoneId = windowString[(atIndex + 1)..];
        }

        var parts = timePart.Split('-');
        if (parts.Length != 2)
        {
            return null;
        }

        if (!TimeOnly.TryParse(parts[0].Trim(), out var start) ||
            !TimeOnly.TryParse(parts[1].Trim(), out var end))
        {
            return null;
        }

        return new TimeWindow
        {
            Start = start,
            End = end,
            TimeZoneId = timeZoneId
        };
    }

    /// <summary>
    ///     Checks if the given time is within this window.
    /// </summary>
    /// <param name="utcTime">The UTC time to check.</param>
    /// <returns>True if the time is within the window.</returns>
    public bool Contains(DateTimeOffset utcTime)
    {
        var localTime = TimeZoneInfo.ConvertTime(utcTime, TimeZone);
        var currentTimeOfDay = TimeOnly.FromDateTime(localTime.DateTime);

        return IsTimeInWindow(currentTimeOfDay);
    }

    /// <summary>
    ///     Checks if the given time of day is within this window.
    /// </summary>
    /// <param name="time">The time of day to check.</param>
    /// <returns>True if the time is within the window.</returns>
    public bool IsTimeInWindow(TimeOnly time)
    {
        if (Start <= End)
        {
            // Same day window (e.g., 09:00-17:00)
            return time >= Start && time <= End;
        }

        // Overnight window (e.g., 22:00-06:00)
        return time >= Start || time <= End;
    }

    /// <summary>
    ///     Returns string representation in format "HH:mm-HH:mm@TimeZoneId".
    /// </summary>
    public override string ToString()
    {
        var result = $"{Start:HH:mm}-{End:HH:mm}";
        if (!string.IsNullOrEmpty(TimeZoneId))
        {
            result += $"@{TimeZoneId}";
        }

        return result;
    }
}

/// <summary>
///     JSON converter that supports both string format ("HH:mm-HH:mm@TZ") and object format for TimeWindow.
/// </summary>
public sealed class TimeWindowJsonConverter : JsonConverter<TimeWindow>
{
    public override TimeWindow? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            // Parse from string format "HH:mm-HH:mm" or "HH:mm-HH:mm@TimeZoneId"
            var stringValue = reader.GetString();
            return TimeWindow.Parse(stringValue);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Parse from object format { "Start": "09:00", "End": "17:00", "TimeZoneId": "UTC" }
            TimeOnly? start = null;
            TimeOnly? end = null;
            string? timeZoneId = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString()!;
                    reader.Read();

                    switch (propertyName.ToLowerInvariant())
                    {
                        case "start":
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                var startStr = reader.GetString();
                                if (TimeOnly.TryParse(startStr, out var s))
                                {
                                    start = s;
                                }
                            }
                            break;
                        case "end":
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                var endStr = reader.GetString();
                                if (TimeOnly.TryParse(endStr, out var e))
                                {
                                    end = e;
                                }
                            }
                            break;
                        case "timezoneid":
                        case "timezone":
                            timeZoneId = reader.GetString();
                            break;
                    }
                }
            }

            if (start.HasValue && end.HasValue)
            {
                return new TimeWindow
                {
                    Start = start.Value,
                    End = end.Value,
                    TimeZoneId = timeZoneId
                };
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, TimeWindow value, JsonSerializerOptions options)
    {
        // Write as string format for compact JSON
        writer.WriteStringValue(value.ToString());
    }
}
