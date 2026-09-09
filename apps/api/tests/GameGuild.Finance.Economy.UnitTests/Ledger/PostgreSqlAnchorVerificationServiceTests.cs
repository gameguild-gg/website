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

public sealed class PostgreSqlAnchorVerificationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PeriodicReadBackVerifiesHistoricalKeyAndActivatesKillSwitchOnLaterMutation()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("anchor_periodic_verify");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 0, Hash = JournalChain.GenesisHash, UpdatedAt = Now
        });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 0, ToSequence = 0,
            PreviousHash = JournalChain.GenesisHash, CurrentHash = JournalChain.GenesisHash,
            IsValid = true, FencingToken = 1, StartedAt = Now, CompletedAt = Now
        });
        await context.SaveChangesAsync();
        var cryptography = new DeterministicCryptography();
        var worm = new MutableWormStore();
        var publisher = new PostgreSqlEconomyAnchorPublisher(context, cryptography, cryptography, worm);
        await publisher.PublishIfDueAsync(Now, true, null, CancellationToken.None);
        var killSwitch = new RecordingKillSwitchStore();
        var service = new PostgreSqlAnchorVerificationService(context, worm, cryptography, killSwitch);

        var valid = await service.VerifyPublishedAnchorsAsync(Now.AddMinutes(1), CancellationToken.None);

        valid.Verified.Should().Be(1);
        valid.Failed.Should().Be(0);
        killSwitch.Reasons.Should().BeEmpty();
        worm.ObjectMatches = false;

        var invalid = await service.VerifyPublishedAnchorsAsync(Now.AddMinutes(2), CancellationToken.None);

        invalid.Failed.Should().Be(1);
        killSwitch.Reasons.Should().ContainSingle().Which.Should().Contain("anchor-object-or-signature-invalid");
        var latest = await context.Set<EconomyAnchorVerificationRow>()
            .OrderByDescending(row => row.VerifiedAt).FirstAsync();
        latest.ObjectMatches.Should().BeFalse();
    }

    private static VerificationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<VerificationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class VerificationDbContext(DbContextOptions<VerificationDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class DeterministicCryptography : ICapabilityReceiptSigner, ICapabilityPolicySignatureVerifier
    {
        public ValueTask<CapabilityReceiptSignature> SignAsync(string canonicalPayload, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CapabilityReceiptSignature("historical-key-v1", Sign(canonicalPayload)));

        public ValueTask<bool> VerifyAsync(
            string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken) =>
            ValueTask.FromResult(keyId == "historical-key-v1" && signature == Sign(canonicalPayload));

        private static string Sign(string value) => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class MutableWormStore : IWormAnchorStore, IWormAnchorVerifier
    {
        private WormAnchorPublication? _publication;
        private string? _payload;
        public bool ObjectMatches { get; set; } = true;

        public ValueTask<WormAnchorPublication> PublishAsync(WormAnchorObject anchor, CancellationToken cancellationToken)
        {
            _payload = anchor.CanonicalPayload;
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(_payload)));
            _publication = new WormAnchorPublication(
                $"anchors/{anchor.AnchorId:N}.json", "version-1", "etag-1",
                anchor.PublishedAt.AddYears(7), hash, true);
            return ValueTask.FromResult(_publication);
        }

        public ValueTask<WormAnchorReadBack> ReadAndVerifyAsync(
            WormAnchorReference reference, CancellationToken cancellationToken)
        {
            var publication = _publication!;
            return ValueTask.FromResult(new WormAnchorReadBack(
                _payload!, publication.ObjectVersion, publication.ETag, publication.RetainUntil,
                publication.ObjectHash, ObjectMatches));
        }
    }

    private sealed class RecordingKillSwitchStore : IEconomyKillSwitchStore
    {
        public List<string> Reasons { get; } = [];

        public ValueTask<EconomyKillSwitchState> ActivateAsync(
            Guid activationId, EconomyKillSwitchScope scope, string reason, Guid actorId,
            DateTimeOffset activatedAt, CancellationToken cancellationToken)
        {
            Reasons.Add(reason);
            return ValueTask.FromResult(new EconomyKillSwitchState(
                activationId, scope, 1, true, reason, actorId, activatedAt, null, null, [], null));
        }

        public ValueTask<EconomyKillSwitchState> ProposeReleaseAsync(Guid killSwitchId, Guid actorId, string reauthenticationHash, DateTimeOffset proposedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyKillSwitchState> ApproveReleaseAsync(Guid killSwitchId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyKillSwitchState> TryReleaseAsync(Guid killSwitchId, DateTimeOffset releasedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
