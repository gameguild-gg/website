using FluentAssertions;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.TrustSafety.UnitTests;

public sealed class PostgreSqlTrustSafetyControlPlaneTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task SignedEventsAreOrderedIdempotentAndDriveEvidenceAndHolds()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("trust_safety_events");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var control = CreateControl(context);
        var review = Event(1, TrustSafetyOutcome.NeedsReview, "event-1");

        var first = await control.IngestAsync(review, CancellationToken.None);

        first.Status.Should().Be(ComplianceEvidenceIngestionStatus.Published);
        var holdStore = new PostgreSqlComplianceHoldStore(context);
        (await holdStore.IsActiveAsync(
            new ComplianceHoldScope(TenantId, "subject-hash", null), Now.AddMinutes(1), CancellationToken.None)).Should().BeTrue();
        (await control.IngestAsync(review, CancellationToken.None)).Status.Should().Be(ComplianceEvidenceIngestionStatus.Duplicate);
        await FluentActions.Awaiting(() => control.IngestAsync(
                review with { PayloadHash = "different" }, CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();

        var stale = await control.IngestAsync(
            Event(3, TrustSafetyOutcome.Approved, "event-3"), CancellationToken.None);
        stale.Status.Should().Be(ComplianceEvidenceIngestionStatus.Deferred);
        (await control.IngestAsync(
            Event(3, TrustSafetyOutcome.Approved, "event-3"), CancellationToken.None)).Status
            .Should().Be(ComplianceEvidenceIngestionStatus.Deferred);

        var approved = await control.IngestAsync(
            Event(2, TrustSafetyOutcome.Approved, "event-2") with
            {
                IssuedAt = Now.AddMinutes(2), ReceivedAt = Now.AddMinutes(2), ExpiresAt = Now.AddHours(2)
            }, CancellationToken.None);

        approved.Status.Should().Be(ComplianceEvidenceIngestionStatus.Published);
        (await holdStore.IsActiveAsync(
            new ComplianceHoldScope(TenantId, "subject-hash", null), Now.AddMinutes(3), CancellationToken.None)).Should().BeFalse();
        var evidence = await new PostgreSqlComplianceEvidenceStore(context).ReadLatestAsync(
            TenantId, "subject-hash", ComplianceEvidenceKinds.TrustSafety, CancellationToken.None);
        evidence!.Version.Should().Be(2);
        evidence.Result.Should().Be(ComplianceEvidenceResult.Approved);
    }

    [Fact]
    public async Task InvalidSignatureIsPersistedButCannotPublishEvidence()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("trust_safety_signature");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var control = CreateControl(context);

        var result = await control.IngestAsync(
            Event(1, TrustSafetyOutcome.Approved, "invalid") with { Signature = "invalid" },
            CancellationToken.None);

        result.Status.Should().Be(ComplianceEvidenceIngestionStatus.Rejected);
        (await control.IngestAsync(
            Event(1, TrustSafetyOutcome.Approved, "invalid") with { Signature = "invalid" },
            CancellationToken.None)).Status.Should().Be(ComplianceEvidenceIngestionStatus.Rejected);
        var inbox = await context.Set<TrustSafetyEventInboxRow>().SingleAsync();
        inbox.SignatureVerified.Should().BeFalse();
        inbox.ProcessingError.Should().Be("invalid-signature");
        (await new PostgreSqlComplianceEvidenceStore(context).ReadLatestAsync(
            TenantId, "subject-hash", ComplianceEvidenceKinds.TrustSafety, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task AppealsRequireAssignmentAndOptimisticVersionBeforeDecision()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("trust_safety_appeals");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var control = CreateControl(context);
        var appeal = new TrustSafetyAppeal(
            Guid.NewGuid(), TenantId, "subject-hash", "restriction-hash", TrustSafetyAppealState.Submitted,
            ActorId, null, null, "submission-hash", null, null, Now, null, 1);

        var submitted = await control.SubmitAppealAsync(appeal, CancellationToken.None);
        (await control.SubmitAppealAsync(appeal, CancellationToken.None)).Id.Should().Be(submitted.Id);
        var reviewer = Guid.NewGuid();
        var assigned = await control.AssignAppealAsync(
            TenantId, appeal.Id, reviewer, 1, Now.AddMinutes(1), CancellationToken.None);
        var decided = await control.DecideAppealAsync(
            TenantId, appeal.Id, reviewer, assigned.Version, true, "restriction-cleared",
            "decision-hash", Now.AddMinutes(2), CancellationToken.None);

        decided.State.Should().Be(TrustSafetyAppealState.Overturned);
        decided.DecidedBy.Should().Be(reviewer);
        decided.Version.Should().Be(3);
        var appeals = await control.ReadAppealsAsync(
            TenantId, TrustSafetyAppealState.Overturned, 10, CancellationToken.None);
        appeals.Should().ContainSingle().Which.Id.Should().Be(appeal.Id);
        (await control.ReadAppealsAsync(
            Guid.NewGuid(), null, 10, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task AlternativeHoldEvidenceAndAppealBranchesRemainFailClosed()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("trust_safety_boundaries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var control = CreateControl(context);

        await control.IngestAsync(
            Event(1, TrustSafetyOutcome.NeedsReview, "timestamp-1") with { SubjectHash = "timestamp-subject" },
            CancellationToken.None);
        var sameTimestamp = await control.IngestAsync(
            Event(2, TrustSafetyOutcome.NeedsReview, "same-timestamp") with
            {
                SubjectHash = "timestamp-subject",
                IssuedAt = Now
            },
            CancellationToken.None);
        sameTimestamp.Status.Should().Be(ComplianceEvidenceIngestionStatus.Deferred);
        await FluentActions.Awaiting(() => control.IngestAsync(
                Event(2, TrustSafetyOutcome.NeedsReview, "same-version-conflict") with
                {
                    SubjectHash = "timestamp-subject"
                },
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();

        await control.IngestAsync(Event(1, TrustSafetyOutcome.NeedsReview, "hold-1"), CancellationToken.None);
        await control.IngestAsync(Event(2, TrustSafetyOutcome.NeedsReview, "hold-2"), CancellationToken.None);
        await control.IngestAsync(Event(3, TrustSafetyOutcome.Rejected, "hold-3"), CancellationToken.None);
        await control.IngestAsync(Event(4, TrustSafetyOutcome.Unavailable, "hold-4"), CancellationToken.None);
        await control.IngestAsync(Event(5, TrustSafetyOutcome.Approved, "hold-5"), CancellationToken.None);

        var duplicateControl = CreateControl(
            context, evidenceStore: new FixedEvidenceStore(ComplianceEvidenceIngestionStatus.Duplicate));
        (await duplicateControl.IngestAsync(
            Event(1, TrustSafetyOutcome.Approved, "duplicate-evidence") with { SubjectHash = "duplicate-subject" },
            CancellationToken.None)).Status.Should().Be(ComplianceEvidenceIngestionStatus.Duplicate);
        var deferredControl = CreateControl(
            context, evidenceStore: new FixedEvidenceStore(ComplianceEvidenceIngestionStatus.Deferred));
        await FluentActions.Awaiting(() => deferredControl.IngestAsync(
                Event(1, TrustSafetyOutcome.Approved, "deferred-evidence") with { SubjectHash = "deferred-subject" },
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();

        var appeal = new TrustSafetyAppeal(
            Guid.NewGuid(), TenantId, "subject-hash", "restriction-hash", TrustSafetyAppealState.Submitted,
            ActorId, null, null, "submission-hash", null, null, Now, null, 1);
        await control.SubmitAppealAsync(appeal, CancellationToken.None);
        await FluentActions.Awaiting(() => control.SubmitAppealAsync(
                appeal with { SubmissionEvidenceHash = "different" }, CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();
        var reviewer = Guid.NewGuid();
        await FluentActions.Awaiting(() => control.DecideAppealAsync(
                TenantId, appeal.Id, reviewer, 1, false, "upheld", "decision", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();
        await FluentActions.Awaiting(() => control.AssignAppealAsync(
                TenantId, appeal.Id, reviewer, 999, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();
        var assigned = await control.AssignAppealAsync(
            TenantId, appeal.Id, reviewer, 1, Now.AddMinutes(1), CancellationToken.None);
        await FluentActions.Awaiting(() => control.DecideAppealAsync(
                TenantId, appeal.Id, Guid.NewGuid(), assigned.Version, false, "upheld", "decision", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();
        await FluentActions.Awaiting(() => control.DecideAppealAsync(
                TenantId, appeal.Id, reviewer, 999, false, "upheld", "decision", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();
        var upheld = await control.DecideAppealAsync(
            TenantId, appeal.Id, reviewer, assigned.Version, false, "upheld", "decision", Now.AddMinutes(2), CancellationToken.None);
        upheld.State.Should().Be(TrustSafetyAppealState.Upheld);

        var corruptAppealId = Guid.NewGuid();
        context.Set<TrustSafetyAppealRow>().Add(new TrustSafetyAppealRow
        {
            Id = corruptAppealId,
            TenantId = TenantId,
            SubjectHash = "corrupt-subject",
            RestrictionReferenceHash = "corrupt-restriction",
            State = TrustSafetyAppealState.Assigned,
            SubmittedBy = ActorId,
            AssignedTo = null,
            SubmissionEvidenceHash = "corrupt-submission",
            SubmittedAt = Now,
            Version = 1
        });
        await context.SaveChangesAsync();
        await FluentActions.Awaiting(() => control.DecideAppealAsync(
                TenantId, corruptAppealId, reviewer, 1, false, "upheld", "decision", Now,
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();

        await FluentActions.Awaiting(() => control.AssignAppealAsync(
                TenantId, appeal.Id, reviewer, upheld.Version, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<TrustSafetyConflictException>();
        await FluentActions.Awaiting(() => control.AssignAppealAsync(
                TenantId, Guid.NewGuid(), reviewer, 1, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            control.ReadAppealsAsync(Guid.Empty, null, 1, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadAppealsAsync(TenantId, (TrustSafetyAppealState)999, 1, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadAppealsAsync(TenantId, null, 0, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadAppealsAsync(TenantId, null, 501, CancellationToken.None).AsTask());
    }

    private static TrustSafetyEvent Event(long version, TrustSafetyOutcome outcome, string eventId) => new(
        Guid.NewGuid(), eventId, TenantId, "subject-hash", TrustSafetyEventKind.AccountRestriction,
        version, outcome, 7, $"payload-{eventId}", $"evidence-{eventId}", $"s3://trust/{eventId}",
        "kms-key", "signature", Now.AddMinutes(version - 1), Now.AddHours(1), Now.AddMinutes(version - 1));

    private static PostgreSqlTrustSafetyControlPlane CreateControl(
        TestDbContext context,
        ITrustSafetyEventSignatureVerifier? signatureVerifier = null,
        IComplianceEvidenceStore? evidenceStore = null)
    {
        var evidence = new PostgreSqlComplianceEvidenceStore(context);
        return new PostgreSqlTrustSafetyControlPlane(
            context, signatureVerifier ?? new StubSignatureVerifier(), evidenceStore ?? evidence,
            new PostgreSqlComplianceHoldStore(context));
    }

    private sealed class FixedEvidenceStore(ComplianceEvidenceIngestionStatus status) : IComplianceEvidenceStore
    {
        public ValueTask<ComplianceEvidenceIngestionResult> IngestAsync(
            ComplianceEvidenceEnvelope envelope,
            CancellationToken cancellationToken) => ValueTask.FromResult(
            new ComplianceEvidenceIngestionResult(
                status,
                status is ComplianceEvidenceIngestionStatus.Published or ComplianceEvidenceIngestionStatus.Duplicate
                    ? Guid.NewGuid()
                    : null));
    }

    private static TestDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(connectionString).Options);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new EconomyModelConfiguration().Configure(modelBuilder);
            new TrustSafetyModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class StubSignatureVerifier : ITrustSafetyEventSignatureVerifier
    {
        public ValueTask<bool> VerifyAsync(
            string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken) =>
            ValueTask.FromResult(signature == "signature");
    }
}
