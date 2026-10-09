using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Unit tests for the durable local spool of the security event pipeline:
/// append/read round-trips, delivered-record removal, and resilience to partial lines.
/// </summary>
public sealed class SecurityEventFileSpoolTests : IDisposable
{
    private readonly string _spoolDirectory =
        Path.Combine(Path.GetTempPath(), "gg-security-event-spool-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Append_ThenReadAll_RoundTripsEveryRecord()
    {
        var spool = CreateSpool();
        var first = new SecurityEventSpoolRecord(Guid.NewGuid(), new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc),
            """{"actionType":"LoginFailed","success":false}""");
        var second = new SecurityEventSpoolRecord(Guid.NewGuid(), new DateTime(2026, 10, 8, 10, 1, 0, DateTimeKind.Utc),
            """{"actionType":"MfaFailed","success":false}""");

        spool.Append(first);
        spool.Append(second);

        var records = spool.ReadAll();
        records.Should().HaveCount(2);
        records[0].Should().Be(first);
        records[1].Should().Be(second);

        var stats = spool.GetStats();
        stats.PendingCount.Should().Be(2);
        stats.OldestOccurredAtUtc.Should().Be(first.OccurredAtUtc);
    }

    [Fact]
    public void ReadAll_SkipsPartiallyWrittenLines()
    {
        var spool = CreateSpool();
        spool.Append(new SecurityEventSpoolRecord(Guid.NewGuid(), SystemClock.UtcNow, "{}"));
        File.AppendAllText(
            Path.Combine(_spoolDirectory, "security-events.spool.jsonl"),
            "{ not valid json" + Environment.NewLine,
            System.Text.Encoding.UTF8);

        var records = spool.ReadAll();

        records.Should().ContainSingle();
    }

    [Fact]
    public void Remove_KeepsUndeliveredRecordsVerbatim()
    {
        var spool = CreateSpool();
        var delivered = new SecurityEventSpoolRecord(Guid.NewGuid(), SystemClock.UtcNow, """{"a":1}""");
        var pending = new SecurityEventSpoolRecord(Guid.NewGuid(), SystemClock.UtcNow, """{"b":2}""");
        spool.Append(delivered);
        spool.Append(pending);

        spool.Remove([delivered.EventId]);

        var remaining = spool.ReadAll();
        remaining.Should().ContainSingle().Which.Should().Be(pending);
    }

    [Fact]
    public void Remove_KeepsUnparseableLinesForOperatorInspection()
    {
        var spool = CreateSpool();
        var delivered = new SecurityEventSpoolRecord(Guid.NewGuid(), SystemClock.UtcNow, """{"a":1}""");
        spool.Append(delivered);
        var spoolPath = Path.Combine(_spoolDirectory, "security-events.spool.jsonl");
        File.AppendAllText(spoolPath, "corrupt-line" + Environment.NewLine, System.Text.Encoding.UTF8);

        spool.Remove([delivered.EventId]);

        File.Exists(spoolPath).Should().BeTrue();
        spool.ReadAll().Should().BeEmpty();
        var rawLines = File.ReadAllLines(spoolPath);
        rawLines.Should().Contain("corrupt-line");
    }

    [Fact]
    public void ReadAll_EmptySpool_ReturnsNoRecords()
    {
        var spool = CreateSpool();

        spool.ReadAll().Should().BeEmpty();
        spool.GetStats().PendingCount.Should().Be(0);
        spool.GetStats().OldestOccurredAtUtc.Should().BeNull();
    }

    [Fact]
    public void Append_EmptyPayload_IsRejected()
    {
        var spool = CreateSpool();
        var act = () => spool.Append(new SecurityEventSpoolRecord(Guid.NewGuid(), SystemClock.UtcNow, " "));
        act.Should().Throw<ArgumentException>();
    }

    private SecurityEventFileSpool CreateSpool()
    {
        var options = new SecurityEventPipelineOptions { SpoolDirectoryPath = _spoolDirectory };
        return new SecurityEventFileSpool(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_spoolDirectory))
        {
            Directory.Delete(_spoolDirectory, recursive: true);
        }
    }
}
