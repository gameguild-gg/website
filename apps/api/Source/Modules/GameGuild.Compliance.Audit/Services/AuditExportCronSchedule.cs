namespace GameGuild.Compliance.Audit;

/// <summary>Calculates UTC run times for five-field cron schedules in a configured timezone.</summary>
public interface IAuditExportCronSchedule
{
    DateTime GetNextRunUtc(string expression, string timezoneId, DateTime afterUtc);
}

public sealed class AuditExportCronSchedule : IAuditExportCronSchedule
{
    private const int SearchLimitMinutes = 366 * 24 * 60 * 5;

    public DateTime GetNextRunUtc(string expression, string timezoneId, DateTime afterUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(timezoneId);

        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 5)
        {
            throw new FormatException("Cron schedules must contain five fields: minute hour day-of-month month day-of-week.");
        }

        var minute = CronField.Parse(fields[0], 0, 59);
        var hour = CronField.Parse(fields[1], 0, 23);
        var dayOfMonth = CronField.Parse(fields[2], 1, 31);
        var month = CronField.Parse(fields[3], 1, 12);
        var dayOfWeek = CronField.Parse(fields[4], 0, 7, normalizeSunday: true);
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var utc = afterUtc.Kind == DateTimeKind.Local
            ? afterUtc.ToUniversalTime()
            : DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var minuteStart = utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMinute)).AddMinutes(1);

        for (var offset = 0; offset < SearchLimitMinutes; offset++)
        {
            var candidateUtc = minuteStart.AddMinutes(offset);
            var local = TimeZoneInfo.ConvertTimeFromUtc(candidateUtc, timezone);
            if (timezone.IsInvalidTime(local)) { continue; }
            if (timezone.IsAmbiguousTime(local))
            {
                // Run an ambiguous local wall-clock minute once, on its first UTC occurrence.
                var earlierOffset = timezone.GetAmbiguousTimeOffsets(local).Max();
                if (timezone.GetUtcOffset(candidateUtc) != earlierOffset) { continue; }
            }

            if (!minute.Contains(local.Minute)
                || !hour.Contains(local.Hour)
                || !month.Contains(local.Month))
            {
                continue;
            }

            var monthDayMatches = dayOfMonth.Contains(local.Day);
            var weekDayMatches = dayOfWeek.Contains((int)local.DayOfWeek);
            var calendarDayMatches = dayOfMonth.IsWildcard && dayOfWeek.IsWildcard
                || dayOfMonth.IsWildcard && weekDayMatches
                || dayOfWeek.IsWildcard && monthDayMatches
                || !dayOfMonth.IsWildcard && !dayOfWeek.IsWildcard && (monthDayMatches || weekDayMatches);

            if (calendarDayMatches) { return candidateUtc; }
        }

        throw new InvalidOperationException("The cron schedule has no occurrence within the next five years.");
    }

    private sealed class CronField(HashSet<int> values, bool isWildcard)
    {
        public bool IsWildcard { get; } = isWildcard;

        public bool Contains(int value) => values.Contains(value);

        public static CronField Parse(string expression, int minimum, int maximum, bool normalizeSunday = false)
        {
            var values = new HashSet<int>();
            var wildcard = expression == "*";

            foreach (var term in expression.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var stepParts = term.Split('/', StringSplitOptions.TrimEntries);
                if (stepParts.Length > 2) { throw InvalidCron(expression); }

                var step = stepParts.Length == 2 && int.TryParse(stepParts[1], out var parsedStep)
                    ? parsedStep
                    : stepParts.Length == 1 ? 1 : 0;
                if (step < 1) { throw InvalidCron(expression); }

                var range = stepParts[0];
                int start;
                int end;
                if (range == "*")
                {
                    start = minimum;
                    end = maximum;
                }
                else if (range.Contains('-', StringComparison.Ordinal))
                {
                    var bounds = range.Split('-', StringSplitOptions.TrimEntries);
                    if (bounds.Length != 2
                        || !int.TryParse(bounds[0], out start)
                        || !int.TryParse(bounds[1], out end)
                        || start > end)
                    {
                        throw InvalidCron(expression);
                    }

                }
                else if (int.TryParse(range, out start))
                {
                    end = stepParts.Length == 2 ? maximum : start;
                }
                else
                {
                    throw InvalidCron(expression);
                }

                if (start < minimum || end > maximum) { throw InvalidCron(expression); }

                for (var value = start; value <= end; value += step)
                {
                    values.Add(normalizeSunday && value == 7 ? 0 : value);
                }
            }

            if (values.Count == 0) { throw InvalidCron(expression); }
            return new CronField(values, wildcard);
        }

        private static FormatException InvalidCron(string expression) =>
            new($"Invalid cron field '{expression}'. Use '*', a number, a range, a step, or a comma-separated list.");
    }
}
