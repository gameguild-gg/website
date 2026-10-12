using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditExportCronScheduleTests
{
    private readonly AuditExportCronSchedule _schedule = new();

    [Fact]
    public void GetNextRunUtc_AdvancesToNextMatchingMinute()
    {
        var after = new DateTime(2025, 1, 1, 10, 7, 18, DateTimeKind.Utc);

        var next = _schedule.GetNextRunUtc("*/15 * * * *", "UTC", after);

        Assert.Equal(new DateTime(2025, 1, 1, 10, 15, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void GetNextRunUtc_UsesConfiguredLocalTimeZone()
    {
        var after = new DateTime(2025, 7, 1, 6, 0, 0, DateTimeKind.Utc);

        var next = _schedule.GetNextRunUtc("30 9 * * 1-5", GetTimeZoneId("Europe/Berlin", "W. Europe Standard Time"), after);

        Assert.Equal(new DateTime(2025, 7, 1, 7, 30, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void GetNextRunUtc_RunsAmbiguousLocalTimeOnlyOnItsFirstUtcOccurrence()
    {
        var timezone = GetTimeZoneId("America/New_York", "Eastern Standard Time");
        var beforeFirstOccurrence = new DateTime(2025, 11, 2, 4, 0, 0, DateTimeKind.Utc);
        var betweenRepeatedOccurrences = new DateTime(2025, 11, 2, 5, 31, 0, DateTimeKind.Utc);

        var firstOccurrence = _schedule.GetNextRunUtc("30 1 * * *", timezone, beforeFirstOccurrence);
        var nextCalendarOccurrence = _schedule.GetNextRunUtc("30 1 * * *", timezone, betweenRepeatedOccurrences);

        Assert.Equal(new DateTime(2025, 11, 2, 5, 30, 0, DateTimeKind.Utc), firstOccurrence);
        Assert.Equal(new DateTime(2025, 11, 3, 6, 30, 0, DateTimeKind.Utc), nextCalendarOccurrence);
    }

    [Theory]
    [InlineData("0 0 * *")]
    [InlineData("60 * * * *")]
    [InlineData("*/0 * * * *")]
    [InlineData("5-2 * * * *")]
    public void GetNextRunUtc_RejectsInvalidCronExpressions(string expression)
    {
        Assert.Throws<FormatException>(() =>
            _schedule.GetNextRunUtc(expression, "UTC", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    private static string GetTimeZoneId(string ianaId, string windowsId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            return ianaId;
        }
        catch (TimeZoneNotFoundException)
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            return windowsId;
        }
    }
}
