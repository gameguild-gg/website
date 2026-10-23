using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Field-level change tracking, precise value comparison, structured data comparison
///     (complex objects, arrays, nested structures), and large-object handling with checksums
///     and truncation (issue #176).
/// </summary>
public sealed class ValueSnapshotDiffEngineTests
{
    [Fact]
    public void ComputeDiff_ReportsModifiedAddedAndRemovedFieldsWithPreciseValues()
    {
        const string before = """{"Title":"Old","Slug":"same","Tags":["a","b"],"Count":10}""";
        const string after = """{"Title":"New","Slug":"same","Tags":["a","b"],"Count":25,"NewField":true}""";

        var diffs = ValueSnapshotDiffEngine.ComputeDiff(before, after);

        diffs.Should().Contain(d => d.Field == "Title" && d.ChangeKind == "Modified" && d.Before == "\"Old\"" && d.After == "\"New\"");
        diffs.Should().Contain(d => d.Field == "Count" && d.NumericDelta == 15);
        diffs.Should().Contain(d => d.Field == "NewField" && d.ChangeKind == "Added" && d.After == "true");
        diffs.Should().NotContain(d => d.Field == "Slug", "unchanged fields are not reported");
        diffs.Should().NotContain(d => d.Field == "Tags", "identical arrays are not reported");
    }

    [Fact]
    public void ComputeDiff_RecursesIntoNestedObjectsWithDottedPaths()
    {
        const string before = """{"Address":{"City":"Lisbon","Zip":"1000"},"Name":"Stable"}""";
        const string after = """{"Address":{"City":"Porto","Zip":"1000"},"Name":"Stable"}""";

        var diffs = ValueSnapshotDiffEngine.ComputeDiff(before, after);

        diffs.Should().ContainSingle().Which.Field.Should().Be("Address.City");
    }

    [Fact]
    public void ComputeDiff_RecursesIntoArraysWithIndexedPaths()
    {
        const string before = """{"Items":["one","two","three"]}""";
        const string after = """{"Items":["one","TWO","three","four"]}""";

        var diffs = ValueSnapshotDiffEngine.ComputeDiff(before, after);

        diffs.Should().Contain(d => d.Field == "Items[1]" && d.ChangeKind == "Modified");
        diffs.Should().Contain(d => d.Field == "Items[3]" && d.ChangeKind == "Added");
        diffs.Should().HaveCount(2);
    }

    [Fact]
    public void ComputeDiff_NumbersCompareByValueSoUnitChangesAreNotReported()
    {
        const string before = """{"Amount":1}""";
        const string after = """{"Amount":1.0}""";

        var diffs = ValueSnapshotDiffEngine.ComputeDiff(before, after);

        diffs.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDiff_NullBeforeReportsAddedAndNullAfterReportsRemoved()
    {
        var added = ValueSnapshotDiffEngine.ComputeDiff(null, """{"A":1}""");
        added.Should().OnlyContain(d => d.ChangeKind == "Added");

        var removed = ValueSnapshotDiffEngine.ComputeDiff("""{"A":1}""", null);
        removed.Should().OnlyContain(d => d.ChangeKind == "Removed");
    }

    [Fact]
    public void ApplyLargeValueHandling_TruncatesOversizedValuesAndKeepsFullChecksum()
    {
        var large = new string('x', 5_000);
        var (stored, truncated, hash) = ValueSnapshotDiffEngine.ApplyLargeValueHandling(large, 1_024);

        truncated.Should().BeTrue();
        stored.Should().StartWith("xxxx");
        stored.Should().Contain("sha256:");
        hash.Should().HaveLength(64).And.Be(ValueSnapshotDiffEngine.ComputeSha256(large));
    }

    [Fact]
    public void ApplyLargeValueHandling_KeepsSmallValuesIntact()
    {
        var (stored, truncated, hash) = ValueSnapshotDiffEngine.ApplyLargeValueHandling("small", 1_024);

        truncated.Should().BeFalse();
        stored.Should().Be("small");
        hash.Should().Be(ValueSnapshotDiffEngine.ComputeSha256("small"));
    }

    [Fact]
    public void ComputeDiff_TruncatesOversizedFieldValuesWithChecksums()
    {
        var bigValue = new string('y', 4_000);
        var before = $$"""{"Blob":"{{bigValue}}"}""";
        var after = """{"Blob":"changed"}""";

        var diffs = ValueSnapshotDiffEngine.ComputeDiff(before, after, valueMaxBytes: 512);

        var blob = diffs.Should().ContainSingle(d => d.Field == "Blob").Which;
        blob.BeforeTruncated.Should().BeTrue();
        blob.BeforeValueSha256.Should().HaveLength(64);
        blob.AfterTruncated.Should().BeFalse();
    }

    [Fact]
    public void SerializeDiffs_RoundTripsThroughDeserializeDiffs()
    {
        var diffs = ValueSnapshotDiffEngine.ComputeDiff("""{"A":1,"N":{"B":2}}""", """{"A":3,"N":{"B":4}}""");
        var json = ValueSnapshotDiffEngine.SerializeDiffs(diffs);

        var restored = ValueSnapshotDiffEngine.DeserializeDiffs(json);

        restored.Should().BeEquivalentTo(diffs);
    }

    [Fact]
    public void DeserializeDiffs_ReturnsEmptyForMalformedJson()
    {
        ValueSnapshotDiffEngine.DeserializeDiffs("not json").Should().BeEmpty();
        ValueSnapshotDiffEngine.DeserializeDiffs(null).Should().BeEmpty();
    }
}
