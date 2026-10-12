using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Models;

public class AuditDateRangeResolverTests
{
    private static readonly DateTime NowUtc = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resolve_ShouldConvertIso8601OffsetsToUtc()
    {
        var result = AuditDateRangeResolver.Resolve(
            "2026-09-20T10:00:00+02:00",
            "2026-09-20T12:00:00+02:00",
            null,
            "UTC",
            NowUtc);

        result.IsValid.Should().BeTrue();
        result.Range!.StartUtc.Should().Be(new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc));
        result.Range.EndUtc.Should().Be(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Resolve_ShouldAcceptUnixSecondsAndMilliseconds()
    {
        var start = new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

        var result = AuditDateRangeResolver.Resolve(
            start.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            end.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            null,
            "UTC",
            NowUtc);

        result.IsValid.Should().BeTrue();
        result.Range!.StartUtc.Should().Be(start.UtcDateTime);
        result.Range.EndUtc.Should().Be(end.UtcDateTime);
    }

    [Fact]
    public void Resolve_ShouldSupportRelativeBoundsAndPeriodShortcuts()
    {
        var relative = AuditDateRangeResolver.Resolve("last 2 days", "now", null, "UTC", NowUtc);
        var shortcut = AuditDateRangeResolver.Resolve(null, null, "last7d", "UTC", NowUtc);

        relative.IsValid.Should().BeTrue();
        relative.Range!.StartUtc.Should().Be(NowUtc.AddDays(-2));
        relative.Range.EndUtc.Should().Be(NowUtc);
        shortcut.IsValid.Should().BeTrue();
        shortcut.Range!.StartUtc.Should().Be(NowUtc.AddDays(-7));
        shortcut.Range.EndUtc.Should().Be(NowUtc);
    }

    [Theory]
    [InlineData("today", "UTC", 20, 0)]
    [InlineData("thisWeek", "UTC", 14, 0)]
    [InlineData("thisMonth", "UTC", 1, 0)]
    [InlineData("today", "America/Sao_Paulo", 20, 3)]
    [InlineData("this_week", "America/Sao_Paulo", 14, 3)]
    [InlineData("this-month", "America/Sao_Paulo", 1, 3)]
    public void Resolve_ShouldContinueToCalendarPeriodsAfterRollingPeriodSwitch(
        string period, string timeZoneId, int startDay, int startHour)
    {
        var result = AuditDateRangeResolver.Resolve(null, null, period, timeZoneId, NowUtc);

        result.IsValid.Should().BeTrue();
        result.Range!.StartUtc.Should().Be(new DateTime(2026, 9, startDay, startHour, 0, 0, DateTimeKind.Utc));
        result.Range.EndUtc.Should().Be(NowUtc);
    }

    [Fact]
    public void Resolve_ShouldInterpretOffsetFreeValuesInRequestedTimezone()
    {
        var result = AuditDateRangeResolver.Resolve(
            "2026-09-20T08:00:00",
            "2026-09-20T09:00:00",
            null,
            "America/Sao_Paulo",
            NowUtc);

        result.IsValid.Should().BeTrue();
        result.Range!.StartUtc.Should().Be(new DateTime(2026, 9, 20, 11, 0, 0, DateTimeKind.Utc));
        result.Range.EndUtc.Should().Be(new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Resolve_ShouldIncludeTheWholeDayForDateOnlyEndBounds()
    {
        var result = AuditDateRangeResolver.Resolve("2026-09-01", "2026-09-02", null, "UTC", NowUtc);

        result.IsValid.Should().BeTrue();
        result.Range!.StartUtc.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        result.Range.EndUtc.Should().Be(new DateTime(2026, 9, 2, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999));
    }

    [Fact]
    public void Resolve_ShouldRejectReversedOrConflictingRanges()
    {
        var reversed = AuditDateRangeResolver.Resolve("2026-09-21", "2026-09-20", null, "UTC", NowUtc);
        var conflicting = AuditDateRangeResolver.Resolve("2026-09-19", "2026-09-20", "last7d", "UTC", NowUtc);

        reversed.IsValid.Should().BeFalse();
        reversed.Errors.Should().ContainKey("Start").And.ContainKey("End");
        conflicting.IsValid.Should().BeFalse();
        conflicting.Errors.Should().ContainKey("Period");
    }

    [Theory]
    [InlineData("2026-03-08T02:30:00")]
    [InlineData("2026-11-01T01:30:00")]
    public void Resolve_ShouldRejectAmbiguousOrNonexistentLocalTimes(string localTime)
    {
        var result = AuditDateRangeResolver.Resolve(
            localTime,
            "2026-11-02T12:00:00",
            null,
            "America/New_York",
            NowUtc);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("Start");
    }

    [Fact]
    public void Resolve_ShouldRejectUnknownTimezonesAndUnsupportedPeriods()
    {
        var invalidZone = AuditDateRangeResolver.Resolve(null, null, "today", "No/Such_Zone", NowUtc);
        var invalidPeriod = AuditDateRangeResolver.Resolve(null, null, "last90d", "UTC", NowUtc);

        invalidZone.IsValid.Should().BeFalse();
        invalidZone.Errors.Should().ContainKey("TimeZoneId");
        invalidPeriod.IsValid.Should().BeFalse();
        invalidPeriod.Errors.Should().ContainKey("Period");
    }

    [Fact]
    public void Resolve_ShouldReportDateOnlyOverflowAsValidationError()
    {
        var result = AuditDateRangeResolver.Resolve("9999-12-30", "9999-12-31", null, "UTC", NowUtc);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainKey("End");
    }
}
