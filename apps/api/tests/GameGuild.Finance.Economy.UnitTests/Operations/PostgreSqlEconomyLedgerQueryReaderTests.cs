using FluentAssertions;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Operations;

public sealed class PostgreSqlEconomyLedgerQueryReaderTests
{
    private static readonly Guid TenantId = Guid.Parse("ae000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadsVerificationAnchorAndProjectionHistoriesWithStableCursors()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ledger_queries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var verificationRows = Enumerable.Range(1, 2).Select(Verification).ToArray();
        var anchorRows = Enumerable.Range(1, 2).Select(Anchor).ToArray();
        var projectionRows = Enumerable.Range(1, 2).Select(Projection).ToArray();
        context.Set<EconomyJournalVerificationCheckpointRow>().AddRange(verificationRows);
        context.Set<EconomyExternalAnchorRow>().AddRange(anchorRows);
        context.Set<EconomyProjectionGenerationRow>().AddRange(projectionRows);
        context.Set<EconomyAnchorVerificationRow>().Add(new EconomyAnchorVerificationRow
        {
            Id = Guid.NewGuid(),
            ExternalAnchorId = anchorRows[1].Id,
            KeyId = "kms-key",
            ObjectVersion = "version-2",
            ETag = "etag",
            RetainUntil = Now.AddYears(1),
            ObjectHash = "object-hash",
            SignatureValid = true,
            ObjectMatches = true,
            VerifiedAt = Now
        });
        context.Set<EconomyProjectionGenerationApprovalRow>().Add(new EconomyProjectionGenerationApprovalRow
        {
            Id = Guid.NewGuid(),
            Generation = 2,
            ActorId = Guid.NewGuid(),
            ReauthenticationHash = "approval-evidence",
            ApprovedAt = Now
        });
        await context.SaveChangesAsync();
        var reader = new PostgreSqlEconomyLedgerQueryReader(context);

        var verificationFirst = await reader.ListVerificationsAsync(TenantId, 1, null, default);
        var verificationSecond = await reader.ListVerificationsAsync(
            TenantId, 1, verificationFirst.NextCursor, default);
        var anchorFirst = await reader.ListAnchorsAsync(TenantId, 1, null, default);
        var anchorSecond = await reader.ListAnchorsAsync(TenantId, 1, anchorFirst.NextCursor, default);
        var projectionFirst = await reader.ListProjectionsAsync(TenantId, 1, null, default);
        var projectionSecond = await reader.ListProjectionsAsync(
            TenantId, 1, projectionFirst.NextCursor, default);

        verificationFirst.Items.Single().ToSequence.Should().Be(1);
        verificationSecond.Items.Single().ToSequence.Should().Be(2);
        anchorFirst.Items.Single().Summary.JournalSequence.Should().Be(1);
        anchorSecond.Items.Single().Summary.JournalSequence.Should().Be(2);
        projectionFirst.Items.Single().Summary.Generation.Should().Be(2);
        projectionSecond.Items.Single().Summary.Generation.Should().Be(1);
    }

    [Fact]
    public async Task ReadsDetailsAndAppendOnlyVerificationAndApprovalEvidence()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ledger_query_details");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var verification = Verification(1);
        var anchor = Anchor(1);
        var projection = Projection(1);
        var approver = Guid.NewGuid();
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(verification);
        context.Set<EconomyExternalAnchorRow>().Add(anchor);
        context.Set<EconomyProjectionGenerationRow>().Add(projection);
        context.Set<EconomyAnchorVerificationRow>().Add(new EconomyAnchorVerificationRow
        {
            Id = Guid.NewGuid(), ExternalAnchorId = anchor.Id, KeyId = "kms-key",
            ObjectVersion = "version-1", ETag = "etag", RetainUntil = Now.AddYears(1),
            ObjectHash = "object-hash", SignatureValid = true, ObjectMatches = true, VerifiedAt = Now
        });
        context.Set<EconomyProjectionGenerationApprovalRow>().Add(new EconomyProjectionGenerationApprovalRow
        {
            Id = Guid.NewGuid(), Generation = 1, ActorId = approver,
            ReauthenticationHash = "approval-evidence", ApprovedAt = Now
        });
        await context.SaveChangesAsync();
        var reader = new PostgreSqlEconomyLedgerQueryReader(context);

        (await reader.FindVerificationAsync(TenantId, verification.Id, default))!.CurrentHash
            .Should().Be("hash-1");
        (await reader.FindAnchorAsync(TenantId, anchor.Id, default))!.Summary.Provider
            .Should().Be("s3-object-lock");
        (await reader.ReadAnchorVerificationsAsync(TenantId, anchor.Id, default)).Should().ContainSingle();
        (await reader.FindProjectionAsync(TenantId, 1, default))!.Summary.ProjectionHash
            .Should().Be("projection-1");
        (await reader.ReadProjectionAuditAsync(TenantId, 1, default)).Should().ContainSingle()
            .Which.ActorId.Should().Be(approver);
        await FluentActions.Awaiting(() => reader.ListAnchorsAsync(
                TenantId, 20, "invalid", default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListProjectionsAsync(
                TenantId, 20, "invalid", default).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        var identifier = Guid.NewGuid().ToString("N");
        foreach (var invalidCursor in new[]
                 {
                     $"ZZZZZZZZZZZZZZZZ{identifier}",
                     $"FFFFFFFFFFFFFFFF{identifier}",
                     $"7FFFFFFFFFFFFFFF{identifier}",
                     $"0000000000000001{new string('Z', 32)}"
                 })
        {
            await FluentActions.Awaiting(() => reader.ListAnchorsAsync(
                    TenantId, 20, invalidCursor, default).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }

        foreach (var invalidCursor in new[]
                 {
                     $"ZZZZZZZZZZZZZZZZ{identifier}",
                     $"0000000000000000{identifier}",
                     $"0000000000000001{new string('Z', 32)}"
                 })
        {
            await FluentActions.Awaiting(() => reader.ListProjectionsAsync(
                    TenantId, 20, invalidCursor, default).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }

        await FluentActions.Awaiting(() => reader.ListVerificationsAsync(
                TenantId, 0, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => reader.ListVerificationsAsync(
                TenantId, 101, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => reader.FindVerificationAsync(
                TenantId, Guid.Empty, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.FindVerificationAsync(
                Guid.Empty, verification.Id, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.FindProjectionAsync(
                TenantId, 0, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => reader.ReadProjectionAuditAsync(
                TenantId, 0, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static EconomyJournalVerificationCheckpointRow Verification(int sequence) => new()
    {
        Id = Guid.NewGuid(),
        FromSequence = sequence,
        ToSequence = sequence,
        PreviousHash = JournalChain.GenesisHash,
        CurrentHash = $"hash-{sequence}",
        IsValid = true,
        FencingToken = sequence,
        StartedAt = Now.AddMinutes(-sequence - 1),
        CompletedAt = Now.AddMinutes(-sequence)
    };

    private static EconomyExternalAnchorRow Anchor(int sequence) => new()
    {
        Id = Guid.NewGuid(),
        JournalSequence = sequence,
        JournalHash = $"hash-{sequence}",
        Signature = "signature",
        WormReference = $"worm/{sequence}",
        Provider = "s3-object-lock",
        ProviderReference = $"version-{sequence}",
        AnchoredAt = Now.AddMinutes(-sequence)
    };

    private static EconomyProjectionGenerationRow Projection(int generation) => new()
    {
        Id = Guid.NewGuid(),
        Generation = generation,
        FromSequence = 0,
        ToSequence = generation,
        ProjectionHash = $"projection-{generation}",
        JournalHash = $"hash-{generation}",
        State = "Ready",
        ProposedBy = Guid.NewGuid(),
        StartedAt = Now.AddMinutes(-generation - 1),
        CompletedAt = Now.AddMinutes(-generation)
    };

    private static QueryDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<QueryDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class QueryDbContext(DbContextOptions<QueryDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }
}
