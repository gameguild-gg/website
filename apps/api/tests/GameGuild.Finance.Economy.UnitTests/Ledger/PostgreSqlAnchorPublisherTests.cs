using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class PostgreSqlAnchorPublisherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PublishesSignedVerifiedAnchorAndPersistsWormEvidence()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("anchor_publish");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedVerifiedHeadAsync(context, sequence: 42, hash: "verified-head");
        var signer = new DeterministicSigner();
        var worm = new RecordingWormStore();
        var publisher = new PostgreSqlEconomyAnchorPublisher(context, signer, signer, worm);

        var result = await publisher.PublishIfDueAsync(Now, true, "dispatch-snapshot", CancellationToken.None);

        result.Should().NotBeNull();
        result!.JournalSequence.Should().Be(42);
        result.KeyId.Should().Be("kms-key-v1");
        worm.Published.Should().ContainSingle();
        (await context.Set<EconomyExternalAnchorRow>().SingleAsync()).DispatchSnapshotHash
            .Should().Be("dispatch-snapshot");
        var verification = await context.Set<EconomyAnchorVerificationRow>().SingleAsync();
        verification.SignatureValid.Should().BeTrue();
        verification.ObjectMatches.Should().BeTrue();
        verification.ObjectVersion.Should().Be("version-1");
        verification.RetainUntil.Should().BeAfter(Now);
    }

    [Fact]
    public async Task ScheduledPublicationWaitsUntilEntryOrTimeThreshold()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("anchor_schedule");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedVerifiedHeadAsync(context, sequence: 1_050, hash: "head");
        context.Set<EconomyExternalAnchorRow>().Add(new EconomyExternalAnchorRow
        {
            Id = Guid.NewGuid(), JournalSequence = 1_000, JournalHash = "old", Signature = "signature",
            WormReference = "old/key", Provider = "s3-object-lock", ProviderReference = "old-version",
            AnchoredAt = Now.AddMinutes(-1)
        });
        await context.SaveChangesAsync();
        var signer = new DeterministicSigner();
        var worm = new RecordingWormStore();
        var publisher = new PostgreSqlEconomyAnchorPublisher(context, signer, signer, worm);

        var result = await publisher.PublishIfDueAsync(Now, false, null, CancellationToken.None);

        result.Should().BeNull();
        worm.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task BlocksPublicationUnlessCurrentHeadHasValidCheckpoint()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("anchor_unverified");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 8, Hash = "unverified", UpdatedAt = Now
        });
        await context.SaveChangesAsync();
        var signer = new DeterministicSigner();
        var publisher = new PostgreSqlEconomyAnchorPublisher(context, signer, signer, new RecordingWormStore());

        var act = () => publisher.PublishIfDueAsync(Now, true, null, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<AnchorPublicationBlockedException>()
            .WithMessage("*not passed integrity verification*");
    }

    [Fact]
    public async Task InvalidKmsSignatureNeverReachesWormStorage()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("anchor_bad_signature");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedVerifiedHeadAsync(context, sequence: 7, hash: "head");
        var signer = new DeterministicSigner { IsValid = false };
        var worm = new RecordingWormStore();
        var publisher = new PostgreSqlEconomyAnchorPublisher(context, signer, signer, worm);

        var act = () => publisher.PublishIfDueAsync(Now, true, null, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<AnchorPublicationBlockedException>().WithMessage("*signature*");
        worm.Published.Should().BeEmpty();
    }

    private static async Task SeedVerifiedHeadAsync(AnchorDbContext context, long sequence, string hash)
    {
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = sequence, Hash = hash, UpdatedAt = Now
        });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 1, ToSequence = sequence,
            PreviousHash = JournalChain.GenesisHash, CurrentHash = hash, IsValid = true,
            FencingToken = 3, StartedAt = Now.AddSeconds(-1), CompletedAt = Now
        });
        await context.SaveChangesAsync();
    }

    private static AnchorDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<AnchorDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class AnchorDbContext(DbContextOptions<AnchorDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class DeterministicSigner :
        ICapabilityReceiptSigner,
        ICapabilityPolicySignatureVerifier
    {
        public bool IsValid { get; init; } = true;

        public ValueTask<CapabilityReceiptSignature> SignAsync(
            string canonicalPayload,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CapabilityReceiptSignature("kms-key-v1", Signature(canonicalPayload)));

        public ValueTask<bool> VerifyAsync(
            string canonicalPayload,
            string keyId,
            string signature,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(IsValid && keyId == "kms-key-v1" && signature == Signature(canonicalPayload));

        private static string Signature(string value) => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class RecordingWormStore : IWormAnchorStore
    {
        public List<WormAnchorObject> Published { get; } = [];

        public ValueTask<WormAnchorPublication> PublishAsync(
            WormAnchorObject anchor,
            CancellationToken cancellationToken)
        {
            Published.Add(anchor);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(anchor.CanonicalPayload)));
            return ValueTask.FromResult(new WormAnchorPublication(
                $"anchors/{anchor.AnchorId:N}.json", "version-1", "etag-1",
                anchor.PublishedAt.AddYears(7), hash, true));
        }
    }
}
