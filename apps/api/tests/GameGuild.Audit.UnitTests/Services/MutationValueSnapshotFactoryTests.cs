using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Staging factory tests (issue #176): automatic before/after snapshot rows for CREATE,
/// UPDATE, and DELETE operations, delta compression for updates, full-state storage for
/// add/delete, checksums of full and stored payloads, and truncation markers.
/// </summary>
public sealed class MutationValueSnapshotFactoryTests
{
    private readonly MutationValueSnapshotFactory _factory = new(new ValueSnapshotOptions
    {
        MaxPayloadBytes = 256,
        MaxValueBytes = 128
    });

    [Fact]
    public void Create_ForAddedEntity_StoresFullAfterStateWithChecksum()
    {
        const string after = """{"Title":"New entity","Count":1}""";

        var snapshot = _factory.Create("Project", "key-1", "Added", null, after, Guid.NewGuid(), Guid.NewGuid(), "corr-1", DateTimeOffset.UtcNow);

        snapshot.Should().NotBeNull();
        snapshot!.BeforePayload.Should().BeNull();
        snapshot.BeforeIntegritySha256.Should().BeNull();
        snapshot.AfterPayload.Should().Be(after);
        snapshot.AfterIntegritySha256.Should().Be(ValueSnapshotDiffEngine.ComputeSha256(after));
        snapshot.AfterStoredSha256.Should().HaveLength(64);
        snapshot.StorageMode.Should().Be("Full");
        snapshot.ChangeKind.Should().Be("Added");
        snapshot.DiffCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Create_ForDeletedEntity_StoresFullBeforeState()
    {
        const string before = """{"Title":"Doomed"}""";

        var snapshot = _factory.Create("Project", "key-1", "Deleted", before, null, null, null, null, DateTimeOffset.UtcNow);

        snapshot!.BeforePayload.Should().Be(before);
        snapshot.AfterPayload.Should().BeNull();
        snapshot.StorageMode.Should().Be("Full");
    }

    [Fact]
    public void Create_ForModifiedEntity_StoresDeltaAndMarksDeltaMode()
    {
        const string before = """{"Title":"Old"}""";
        const string after = """{"Title":"New"}""";

        var snapshot = _factory.Create("Project", "key-1", "Modified", before, after, null, null, null, DateTimeOffset.UtcNow);

        snapshot!.StorageMode.Should().Be("Delta");
        snapshot.AfterPayload.Should().Be(after);
        var diffs = ValueSnapshotDiffEngine.DeserializeDiffs(snapshot.StructuredDiffJson);
        diffs.Should().ContainSingle(d => d.Field == "Title" && d.ChangeKind == "Modified");
    }

    [Fact]
    public void Create_TruncatesOversizedPayloadsButKeepsIntegrityChecksumOfFullContent()
    {
        var big = $$"""{"Blob":"{{new string('z', 2_000)}}"}""";

        var snapshot = _factory.Create("BlobHolder", "key-1", "Added", null, big, null, null, null, DateTimeOffset.UtcNow);

        snapshot!.AfterPayloadTruncated.Should().BeTrue();
        snapshot.AfterPayload.Should().NotBe(big);
        snapshot.AfterBytes.Should().BeGreaterThan(snapshot.AfterPayload!.Length);
        snapshot.AfterIntegritySha256.Should().Be(ValueSnapshotDiffEngine.ComputeSha256(big));
    }

    [Fact]
    public void Create_IdenticalPayloads_ProduceIdenticalStoredHashesForDeduplication()
    {
        const string payload = """{"Title":"Same everywhere","Owner":"team"}""";

        var first = _factory.Create("Project", "key-1", "Added", null, payload, null, null, null, DateTimeOffset.UtcNow);
        var second = _factory.Create("OtherEntity", "key-2", "Deleted", payload, null, null, null, null, DateTimeOffset.UtcNow);

        first!.AfterStoredSha256.Should().Be(second!.BeforeStoredSha256,
            "identical stored content must share the content-addressed deduplication key");
    }

    [Fact]
    public void Create_ReturnsNull_WhenEntityNameOrKeyMissing()
    {
        _factory.Create("", "key", "Added", null, "{}", null, null, null, DateTimeOffset.UtcNow).Should().BeNull();
        _factory.Create("Project", " ", "Added", null, "{}", null, null, null, DateTimeOffset.UtcNow).Should().BeNull();
    }
}
