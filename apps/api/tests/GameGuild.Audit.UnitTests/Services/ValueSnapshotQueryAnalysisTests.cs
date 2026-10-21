using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Searchable value history, value trend analysis, and change impact analysis over the
///     mutation value snapshot store (issue #176).
/// </summary>
public sealed class ValueSnapshotQueryAnalysisTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IActorContextAccessor> _actors = new();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();

    public ValueSnapshotQueryAnalysisTests()
    {
        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ValueSnapshots_{Guid.NewGuid()}", new InMemoryDatabaseRoot())
            .Options;
        _context = new TestApplicationDbContext(_contextOptions);
        _actors.SetupGet(accessor => accessor.ActorContext).Returns(AdminActor());
    }

    public void Dispose() => _context.Dispose();

    private ActorContext AdminActor() => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = _admin.ToString(),
        TenantId = _tenant,
        IsAuthenticated = true,
        Roles = new HashSet<string> { "TenantAdmin" },
        Permissions = new HashSet<string>()
    };

    private ValueSnapshotQueryService CreateQueryService() => new(
        _actors.Object, _context, NullLogger<ValueSnapshotQueryService>.Instance);

    private ValueSnapshotAnalysisService CreateAnalysisService() => new(
        _actors.Object, _context, NullLogger<ValueSnapshotAnalysisService>.Instance);

    private MutationValueSnapshot SeedSnapshot(
        string entityName,
        string keyId,
        string changeKind,
        string? before,
        string? after,
        DateTimeOffset occurredAt,
        string? correlationId = null,
        Guid? actorId = null)
    {
        var snapshot = new MutationValueSnapshotFactory().Create(entityName, keyId, changeKind, before, after, _tenant, actorId, correlationId, occurredAt);
        _context.Set<MutationValueSnapshot>().Add(snapshot!);
        _context.SaveChanges();
        return snapshot!;
    }

    [Fact]
    public async Task Search_FiltersByEntityFieldAndValuePattern()
    {
        var t0 = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        SeedSnapshot("Project", "k1", "Added", null, """{"Title":"Alpha","Status":"draft"}""", t0);
        SeedSnapshot("Project", "k2", "Modified", """{"Title":"Beta"}""", """{"Title":"Gamma"}""", t0.AddDays(1));

        var service = CreateQueryService();
        var byField = await service.SearchAsync(new ValueSnapshotSearchRequest { EntityName = "Project", Field = "Title" }, CancellationToken.None);
        byField.TotalCount.Should().Be(2);

        var byPattern = await service.SearchAsync(new ValueSnapshotSearchRequest { EntityName = "Project", ValuePattern = "Gamma" }, CancellationToken.None);
        byPattern.TotalCount.Should().Be(1);
        byPattern.Items[0].KeyId.Should().Be("k2");
        byPattern.Items[0].FieldDiffs.Should().Contain(d => d.Field == "Title" && d.After == "\"Gamma\"");

        var byKind = await service.SearchAsync(new ValueSnapshotSearchRequest { EntityName = "Project", ChangeKind = "Modified" }, CancellationToken.None);
        byKind.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_DeniesNonAdministrators()
    {
        _actors.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });

        var act = () => CreateQueryService().SearchAsync(new ValueSnapshotSearchRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetHistory_TracksFieldValueAcrossCreateUpdateDelete()
    {
        var t0 = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        SeedSnapshot("Project", "k1", "Added", null, """{"Title":"First"}""", t0);
        SeedSnapshot("Project", "k1", "Modified", """{"Title":"First"}""", """{"Title":"Second"}""", t0.AddDays(1));
        SeedSnapshot("Project", "k1", "Modified", """{"Other":1}""", """{"Other":2}""", t0.AddDays(2));
        SeedSnapshot("Project", "k1", "Deleted", """{"Title":"Second","Other":2}""", null, t0.AddDays(3));

        var history = await CreateQueryService().GetHistoryAsync("Project", "k1", "Title", CancellationToken.None);

        history!.Entries.Should().HaveCount(4);
        history.Entries[0].Value.Should().Be("First");
        history.Entries[1].Value.Should().Be("Second");
        history.Entries[2].Value.Should().Be("Second", "the untouched field carries its last value forward");
        history.Entries[3].Value.Should().Be("Second", "the delete entry reports the last known value");
    }

    [Fact]
    public async Task GetTrend_ComputesStatisticsOverTheChangeSeries()
    {
        var t0 = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        SeedSnapshot("Meter", "k1", "Added", null, """{"Level":10}""", t0);
        SeedSnapshot("Meter", "k1", "Modified", """{"Level":10}""", """{"Level":20}""", t0.AddDays(1));
        SeedSnapshot("Meter", "k1", "Modified", """{"Level":20}""", """{"Level":15}""", t0.AddDays(2));

        var trend = await CreateAnalysisService().GetTrendAsync("Meter", "k1", "Level",
            t0.UtcDateTime, t0.UtcDateTime.AddDays(3), CancellationToken.None);

        trend!.Series.Should().HaveCount(3);
        trend.Statistics.ChangeCount.Should().Be(3);
        trend.Statistics.MinNumeric.Should().Be(10);
        trend.Statistics.MaxNumeric.Should().Be(20);
        trend.Statistics.AverageNumeric.Should().Be(15);
        trend.Statistics.NetChange.Should().Be(5);
        trend.Statistics.DistinctValues.Should().Be(3);
    }

    [Fact]
    public async Task GetImpact_GroupsCascadeByCorrelationAndLineageByKey()
    {
        var t0 = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var correlation = Guid.NewGuid().ToString();
        var anchor = SeedSnapshot("Order", "o1", "Modified", """{"Total":1}""", """{"Total":2}""", t0, correlation);
        SeedSnapshot("Ledger", "l1", "Added", null, """{"Amount":2}""", t0.AddSeconds(1), correlation);
        SeedSnapshot("Order", "o1", "Modified", """{"Total":2}""", """{"Total":3}""", t0.AddDays(1), correlation);

        var impact = await CreateAnalysisService().GetImpactAsync(anchor.Id, CancellationToken.None);

        impact!.SameOperationChangeCount.Should().Be(3, "every change staged in the same operation cascades together");
        impact.EntitiesTouched.Should().Be(2);
        impact.EntityHistoryChangeCount.Should().Be(3, "the entity lineage includes every recorded change for the key");
        impact.SameOperationChanges.Should().ContainSingle(c => c.IsAnchor && c.EntityName == "Order");
        impact.EntityHistory.Should().OnlyContain(c => c.EntityName == "Order" && c.KeyId == "o1");
    }

    [Fact]
    public async Task GetStorageOverview_ReportsDeltaFullTruncatedAndDeduplicationFigures()
    {
        SeedSnapshot("A", "k1", "Added", null, """{"Title":"Alpha"}""", DateTimeOffset.UtcNow);
        SeedSnapshot("A", "k2", "Modified", """{"Title":"x"}""", """{"Title":"y"}""", DateTimeOffset.UtcNow);
        _context.Set<MutationValuePayloadChunk>().Add(new MutationValuePayloadChunk
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant,
            ContentSha256 = new string('a', 64),
            Content = new string('a', 1_000),
            LengthBytes = 1_000,
            RefCount = 4
        });
        _context.SaveChanges();

        var overview = await CreateQueryService().GetStorageOverviewAsync(CancellationToken.None);

        overview.TotalSnapshots.Should().Be(2);
        overview.DeltaSnapshots.Should().Be(1);
        overview.FullSnapshots.Should().Be(1);
        overview.ChunkCount.Should().Be(1);
        overview.ChunkReferences.Should().Be(4);
        overview.DeduplicatedBytesSaved.Should().Be(3_000);
    }

    [Fact]
    public async Task Search_RejectsInvalidPaging()
    {
        var act = () => CreateQueryService().SearchAsync(new ValueSnapshotSearchRequest { Take = 0 }, CancellationToken.None);

        await act.Should().ThrowAsync<ValueSnapshotValidationException>();
    }
}
