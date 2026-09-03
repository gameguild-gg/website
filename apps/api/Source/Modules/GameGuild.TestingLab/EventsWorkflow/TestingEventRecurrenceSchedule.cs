namespace GameGuild.TestingLab;

internal static class TestingEventRecurrenceSchedule
{
    private const int MaxOccurrences = 104;

    public static IReadOnlyList<DateTime> Expand(
        DateTime startsAt,
        TestingEventRecurrenceRequest? recurrence,
        string timeZoneId = "UTC")
    {
        if (recurrence == null) return [startsAt];

        var timeZone = ResolveTimeZone(timeZoneId);
        var startsAtUtc = AsUtc(startsAt);
        var localStartsAt = TimeZoneInfo.ConvertTimeFromUtc(startsAtUtc, timeZone);
        Validate(startsAtUtc, recurrence);
        var occurrences = new List<DateTime>();
        switch (recurrence.Frequency)
        {
            case TestingEventRecurrenceFrequency.Daily:
                AddIntervalOccurrences(
                    occurrences,
                    recurrence,
                    timeZone,
                    index => localStartsAt.AddDays(index * recurrence.Interval));
                break;
            case TestingEventRecurrenceFrequency.Weekly:
                AddWeeklyOccurrences(occurrences, localStartsAt, recurrence, timeZone);
                break;
            case TestingEventRecurrenceFrequency.Monthly:
                AddIntervalOccurrences(
                    occurrences,
                    recurrence,
                    timeZone,
                    index => localStartsAt.AddMonths(index * recurrence.Interval));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(recurrence), "Unsupported recurrence frequency.");
        }

        if (occurrences.Count == 0)
            throw new ArgumentException("The recurrence window does not include the event start.", nameof(recurrence));

        if (recurrence.OccurrenceCount == null &&
            occurrences.Count == MaxOccurrences &&
            recurrence.EndsAt.HasValue &&
            AsUtc(recurrence.EndsAt.Value) > occurrences[^1])
            throw new ArgumentException($"A recurrence cannot create more than {MaxOccurrences} events.", nameof(recurrence));

        return occurrences;
    }

    private static void AddIntervalOccurrences(
        ICollection<DateTime> occurrences,
        TestingEventRecurrenceRequest recurrence,
        TimeZoneInfo timeZone,
        Func<int, DateTime> localOccurrenceAt)
    {
        for (var index = 0; occurrences.Count < MaxOccurrences; index++)
        {
            var candidate = ToUtc(localOccurrenceAt(index), timeZone);
            if (recurrence.EndsAt != null && candidate > AsUtc(recurrence.EndsAt.Value)) break;

            occurrences.Add(candidate);
            if (recurrence.OccurrenceCount != null && occurrences.Count == recurrence.OccurrenceCount.Value) break;
        }
    }

    private static void AddWeeklyOccurrences(
        ICollection<DateTime> occurrences,
        DateTime localStartsAt,
        TestingEventRecurrenceRequest recurrence,
        TimeZoneInfo timeZone)
    {
        var daysOfWeek = (recurrence.DaysOfWeek ?? [localStartsAt.DayOfWeek])
            .Distinct()
            .OrderBy(day => day)
            .ToArray();
        var day = localStartsAt.Date;

        while (occurrences.Count < MaxOccurrences)
        {
            var localCandidate = new DateTime(
                day.Year,
                day.Month,
                day.Day,
                localStartsAt.Hour,
                localStartsAt.Minute,
                localStartsAt.Second,
                DateTimeKind.Unspecified);
            var candidate = ToUtc(localCandidate, timeZone);
            if (recurrence.EndsAt != null && candidate > AsUtc(recurrence.EndsAt.Value)) break;

            var weeksSinceStart = (day - localStartsAt.Date).Days / 7;
            if (localCandidate >= localStartsAt &&
                weeksSinceStart % recurrence.Interval == 0 &&
                daysOfWeek.Contains(localCandidate.DayOfWeek))
            {
                occurrences.Add(candidate);
                if (recurrence.OccurrenceCount != null && occurrences.Count == recurrence.OccurrenceCount.Value) break;
            }

            day = day.AddDays(1);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("A valid time zone is required.", nameof(timeZoneId), exception);
        }
    }

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecified))
            throw new ArgumentException("The recurrence falls on a time that does not exist in the selected time zone.");
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }

    private static DateTime AsUtc(DateTime dateTime) => dateTime.Kind switch
    {
        DateTimeKind.Utc => dateTime,
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
    };

    private static void Validate(DateTime startsAt, TestingEventRecurrenceRequest recurrence)
    {
        if (!Enum.IsDefined(recurrence.Frequency))
            throw new ArgumentOutOfRangeException(nameof(recurrence), "A supported recurrence frequency is required.");
        if (recurrence.Interval is < 1 or > 52)
            throw new ArgumentOutOfRangeException(nameof(recurrence), "Recurrence interval must be between 1 and 52.");
        if (recurrence.OccurrenceCount is <= 0 or > MaxOccurrences)
            throw new ArgumentOutOfRangeException(nameof(recurrence), $"Occurrence count must be between 1 and {MaxOccurrences}.");
        if (recurrence.OccurrenceCount == null && recurrence.EndsAt == null)
            throw new ArgumentException("A recurring event requires an end date or occurrence count.", nameof(recurrence));
        if (recurrence.EndsAt != null && AsUtc(recurrence.EndsAt.Value) < startsAt)
            throw new ArgumentException("Recurrence end must not precede the event start.", nameof(recurrence));
        if (recurrence.DaysOfWeek != null && recurrence.DaysOfWeek.Any(day => !Enum.IsDefined(day)))
            throw new ArgumentOutOfRangeException(nameof(recurrence), "Every recurrence day must be valid.");
    }
}
