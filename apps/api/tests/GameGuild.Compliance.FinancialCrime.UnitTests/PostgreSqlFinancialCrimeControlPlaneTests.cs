using FluentAssertions;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Compliance.FinancialCrime.UnitTests;

public sealed class PostgreSqlFinancialCrimeControlPlaneTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReviewerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ScreeningCaseDecisionAndConsumptionAreDurableAndTenantScoped()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("financial_crime_case");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var control = CreateControl(context);
        var screening = Screening(FinancialCrimeOutcome.NeedsReview);

        var ingested = await control.IngestScreeningAsync(screening, CancellationToken.None);

        ingested.Case.Should().NotBeNull();
        (await context.Set<FinancialCrimeScreeningRow>().CountAsync()).Should().Be(1);
        (await context.Set<FinancialCrimeCaseEventRow>().CountAsync()).Should().Be(1);
        var holdStore = new PostgreSqlComplianceHoldStore(context);
        (await holdStore.IsActiveAsync(
            new ComplianceHoldScope(TenantId, "subject-hash", null), Now.AddMinutes(1), CancellationToken.None)).Should().BeTrue();
        var evidence = await new PostgreSqlComplianceEvidenceStore(context).ReadLatestAsync(
            TenantId, "subject-hash", ComplianceEvidenceKinds.FinancialCrime, CancellationToken.None);
        evidence!.Version.Should().Be(1);
        evidence.Result.Should().Be(ComplianceEvidenceResult.NeedsReview);

        (await control.IngestScreeningAsync(screening, CancellationToken.None)).Case!.Id
            .Should().Be(ingested.Case!.Id);
        await FluentActions.Awaiting(() => control.IngestScreeningAsync(
                screening with { PayloadHash = "different" }, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        var assigned = await control.AssignCaseAsync(
            TenantId, ingested.Case.Id, ReviewerId, 1, Now.AddMinutes(2), CancellationToken.None);
        assigned.State.Should().Be(FinancialCrimeCaseState.Assigned);
        var decision = new FinancialCrimeCaseDecision(
            Guid.NewGuid(), assigned.Id, TenantId, "subject-hash", 1, FinancialCrimeOutcome.Approved,
            7, "cleared", "decision-evidence", "s3://compliance/decision", ReviewerId,
            Now.AddMinutes(3), Now.AddHours(1));

        await control.DecideCaseAsync(decision, assigned.Version, CancellationToken.None);

        (await holdStore.IsActiveAsync(
            new ComplianceHoldScope(TenantId, "subject-hash", null), Now.AddMinutes(4), CancellationToken.None)).Should().BeFalse();
        evidence = await new PostgreSqlComplianceEvidenceStore(context).ReadLatestAsync(
            TenantId, "subject-hash", ComplianceEvidenceKinds.FinancialCrime, CancellationToken.None);
        evidence!.Version.Should().Be(2);
        evidence.Result.Should().Be(ComplianceEvidenceResult.Approved);
        await control.ConsumeDecisionAsync(TenantId, decision.Id, "payout:1", Now.AddMinutes(4), CancellationToken.None);
        await FluentActions.Awaiting(() => control.ConsumeDecisionAsync(
                TenantId, decision.Id, "payout:2", Now.AddMinutes(5), CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        await control.RecordRegulatoryReferenceAsync(
            TenantId, assigned.Id, "SAR", "br", "opaque-reference-hash", ReviewerId, Now.AddMinutes(6), CancellationToken.None);
        var regulatory = await context.Set<FinancialCrimeRegulatoryReferenceRow>().SingleAsync();
        regulatory.ReferenceHash.Should().Be("opaque-reference-hash");
        regulatory.JurisdictionCode.Should().Be("BR");

        var closedCases = await control.ReadCasesAsync(
            TenantId, FinancialCrimeCaseState.Closed, 10, CancellationToken.None);
        closedCases.Should().ContainSingle().Which.Id.Should().Be(assigned.Id);
        (await control.ReadCasesAsync(
            Guid.NewGuid(), null, 10, CancellationToken.None)).Should().BeEmpty();
        var details = await control.ReadCaseDetailsAsync(TenantId, assigned.Id, CancellationToken.None);
        details.Case.State.Should().Be(FinancialCrimeCaseState.Closed);
        details.Events.Select(item => item.Kind).Should().Equal("Opened", "Assigned", "Decision");
        details.Decisions.Should().ContainSingle().Which.Id.Should().Be(decision.Id);
        details.RegulatoryReferences.Should().ContainSingle().Which.ReferenceHash
            .Should().Be("opaque-reference-hash");
        await FluentActions.Awaiting(() => control.ReadCaseDetailsAsync(
                Guid.NewGuid(), assigned.Id, CancellationToken.None).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task TransactionSignalIsIdempotentAndExpiredScreeningsAreReturnedForRescreening()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("financial_crime_signal");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var control = CreateControl(context);
        await control.IngestScreeningAsync(Screening(FinancialCrimeOutcome.Approved) with
        {
            NextScreenAt = Now.AddMinutes(5)
        }, CancellationToken.None);
        var signal = new FinancialCrimeTransactionSignal(
            Guid.NewGuid(), TenantId, "subject-hash", "marketplace:order-1", "wash-trading", 900_000,
            "signal-evidence", "signal-key", Now.AddMinutes(1), Now.AddHours(1));

        var opened = await control.RecordSignalAsync(signal, CancellationToken.None);
        var replay = await control.RecordSignalAsync(signal, CancellationToken.None);

        replay.Id.Should().Be(opened.Id);
        (await context.Set<FinancialCrimeTransactionSignalRow>().CountAsync()).Should().Be(1);
        await FluentActions.Awaiting(() => control.RecordSignalAsync(
                signal with { EvidenceHash = "different" }, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        var due = await control.ReadDueRescreeningsAsync(Now.AddMinutes(6), 10, CancellationToken.None);
        due.Should().ContainSingle(item => item.SubjectHash == "subject-hash");
    }

    [Fact]
    public async Task AlternativeStatesConflictsAndQueryBoundariesRemainFailClosed()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("financial_crime_boundaries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var control = CreateControl(context);
        var approved = Screening(FinancialCrimeOutcome.Approved) with
        {
            ProviderEventId = "approved-event"
        };

        (await control.IngestScreeningAsync(approved, CancellationToken.None)).Case.Should().BeNull();
        (await control.IngestScreeningAsync(approved, CancellationToken.None)).Case.Should().BeNull();
        await FluentActions.Awaiting(() => control.IngestScreeningAsync(
                approved with { Id = Guid.NewGuid(), ProviderEventId = "version-gap", Version = 3 },
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        var duplicateControl = CreateControl(
            context, new FixedEvidenceStore(ComplianceEvidenceIngestionStatus.Duplicate));
        await duplicateControl.IngestScreeningAsync(
            approved with
            {
                Id = Guid.NewGuid(), ProviderEventId = "duplicate-evidence", SubjectHash = "duplicate-subject"
            }, CancellationToken.None);
        var deferredControl = CreateControl(
            context, new FixedEvidenceStore(ComplianceEvidenceIngestionStatus.Deferred));
        await FluentActions.Awaiting(() => deferredControl.IngestScreeningAsync(
                approved with
                {
                    Id = Guid.NewGuid(), ProviderEventId = "deferred-evidence", SubjectHash = "deferred-subject"
                }, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        var review = approved with
        {
            Id = Guid.NewGuid(), ProviderEventId = "review-event", Version = 2,
            Outcome = FinancialCrimeOutcome.NeedsReview, IssuedAt = Now.AddMilliseconds(1),
            ReceivedAt = Now.AddSeconds(2)
        };
        var opened = (await control.IngestScreeningAsync(review, CancellationToken.None)).Case!;
        await control.IngestScreeningAsync(review with
        {
            Id = Guid.NewGuid(), ProviderEventId = "rejected-event", Version = 3,
            Outcome = FinancialCrimeOutcome.Rejected, IssuedAt = Now.AddMilliseconds(2),
            ReceivedAt = Now.AddSeconds(3)
        }, CancellationToken.None);

        await FluentActions.Awaiting(() => control.AssignCaseAsync(
                TenantId, opened.Id, ReviewerId, 999, Now.AddMinutes(1), CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        var assigned = await control.AssignCaseAsync(
            TenantId, opened.Id, ReviewerId, 1, Now.AddMinutes(1), CancellationToken.None);
        var rejectedDecision = new FinancialCrimeCaseDecision(
            Guid.NewGuid(), assigned.Id, TenantId, "subject-hash", 1, FinancialCrimeOutcome.Rejected,
            7, "blocked", "rejected-evidence", "s3://compliance/rejected", ReviewerId,
            Now.AddMinutes(2), Now.AddHours(1));
        await FluentActions.Awaiting(() => control.DecideCaseAsync(
                rejectedDecision with { DecidedBy = Guid.NewGuid() }, assigned.Version, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        await FluentActions.Awaiting(() => control.DecideCaseAsync(
                rejectedDecision, 999, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        await FluentActions.Awaiting(() => control.DecideCaseAsync(
                rejectedDecision with { Version = 2 }, assigned.Version, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        await control.DecideCaseAsync(rejectedDecision, assigned.Version, CancellationToken.None);
        await FluentActions.Awaiting(() => control.ConsumeDecisionAsync(
                TenantId, rejectedDecision.Id, "operation", Now.AddMinutes(3), CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        var reassigned = await control.AssignCaseAsync(
            TenantId, opened.Id, ReviewerId, 3, Now.AddMinutes(3), CancellationToken.None);
        var approvedDecision = rejectedDecision with
        {
            Id = Guid.NewGuid(), Version = 2, Outcome = FinancialCrimeOutcome.Approved,
            ReasonCode = "cleared", EvidenceHash = "approved-evidence", IssuedAt = Now.AddMinutes(4),
            ExpiresAt = Now.AddMinutes(10)
        };
        await control.DecideCaseAsync(approvedDecision, reassigned.Version, CancellationToken.None);
        await FluentActions.Awaiting(() => control.AssignCaseAsync(
                TenantId, opened.Id, ReviewerId, 5, Now.AddMinutes(5), CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        await FluentActions.Awaiting(() => control.DecideCaseAsync(
                approvedDecision with { Id = Guid.NewGuid(), Version = 3 }, 5, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        await FluentActions.Awaiting(() => control.ConsumeDecisionAsync(
                Guid.Empty, approvedDecision.Id, "operation", Now.AddMinutes(5), CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => control.ConsumeDecisionAsync(
                TenantId, approvedDecision.Id, " ", Now.AddMinutes(5), CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => control.ConsumeDecisionAsync(
                TenantId, Guid.NewGuid(), "operation", Now.AddMinutes(5), CancellationToken.None).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => control.ConsumeDecisionAsync(
                TenantId, approvedDecision.Id, "operation", approvedDecision.IssuedAt.AddTicks(-1), CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        await FluentActions.Awaiting(() => control.ConsumeDecisionAsync(
                TenantId, approvedDecision.Id, "operation", approvedDecision.ExpiresAt, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        var signalCase = await control.RecordSignalAsync(new FinancialCrimeTransactionSignal(
            Guid.NewGuid(), TenantId, "signal-only", "treasury:1", "velocity", 900_000,
            "signal-evidence", "signal-only-key", Now.AddMinutes(5), Now.AddHours(1)), CancellationToken.None);
        var unassignedDecision = new FinancialCrimeCaseDecision(
            Guid.NewGuid(), signalCase.Id, TenantId, "signal-only", 1, FinancialCrimeOutcome.Approved,
            7, "cleared", "signal-decision", "s3://compliance/signal", ReviewerId,
            Now.AddMinutes(7), Now.AddHours(2));
        await FluentActions.Awaiting(() => control.DecideCaseAsync(
                unassignedDecision, signalCase.Version, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();
        var signalAssigned = await control.AssignCaseAsync(
            TenantId, signalCase.Id, ReviewerId, signalCase.Version, Now.AddMinutes(6), CancellationToken.None);
        var signalDecision = unassignedDecision with { Id = Guid.NewGuid() };
        await CreateControl(context, new FixedEvidenceStore(ComplianceEvidenceIngestionStatus.Duplicate))
            .DecideCaseAsync(signalDecision, signalAssigned.Version, CancellationToken.None);

        var deferredCase = await control.RecordSignalAsync(new FinancialCrimeTransactionSignal(
            Guid.NewGuid(), TenantId, "deferred-case", "treasury:2", "velocity", 900_000,
            "signal-evidence", "deferred-case-key", Now.AddMinutes(8), Now.AddHours(1)), CancellationToken.None);
        var deferredAssigned = await control.AssignCaseAsync(
            TenantId, deferredCase.Id, ReviewerId, deferredCase.Version, Now.AddMinutes(9), CancellationToken.None);
        var deferredDecision = signalDecision with
        {
            Id = Guid.NewGuid(), CaseId = deferredCase.Id, SubjectHash = "deferred-case",
            IssuedAt = Now.AddMinutes(10), ExpiresAt = Now.AddHours(2)
        };
        await FluentActions.Awaiting(() => CreateControl(
                context, new FixedEvidenceStore(ComplianceEvidenceIngestionStatus.Deferred))
            .DecideCaseAsync(deferredDecision, deferredAssigned.Version, CancellationToken.None).AsTask())
            .Should().ThrowAsync<FinancialCrimeConflictException>();

        await FluentActions.Awaiting(() => control.RecordRegulatoryReferenceAsync(
                TenantId, opened.Id, "INVALID", "BR", "hash", ReviewerId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => control.RecordRegulatoryReferenceAsync(
                TenantId, opened.Id, "STR", " ", "hash", ReviewerId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => control.RecordRegulatoryReferenceAsync(
                TenantId, opened.Id, "STR", "BR", " ", ReviewerId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => control.RecordRegulatoryReferenceAsync(
                TenantId, Guid.NewGuid(), "STR", "BR", "hash", ReviewerId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
        await control.RecordRegulatoryReferenceAsync(
            TenantId, opened.Id, "STR", "us", "str-hash", ReviewerId, Now, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadDueRescreeningsAsync(Now, 0, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadDueRescreeningsAsync(Now, 1001, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            control.ReadCasesAsync(Guid.Empty, null, 1, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadCasesAsync(TenantId, (FinancialCrimeCaseState)999, 1, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadCasesAsync(TenantId, null, 0, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            control.ReadCasesAsync(TenantId, null, 501, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            control.ReadCaseDetailsAsync(Guid.Empty, opened.Id, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            control.ReadCaseDetailsAsync(TenantId, Guid.Empty, CancellationToken.None).AsTask());
    }

    private static FinancialCrimeScreening Screening(FinancialCrimeOutcome outcome) => new(
        Guid.NewGuid(), "sumsub", "sandbox", "screening-1", TenantId, "subject-hash", 1,
        outcome, false, false, false, 7, "payload-hash", "screening-evidence",
        "s3://compliance/screening", true, Now, Now.AddHours(1), Now.AddMinutes(30), Now.AddSeconds(1));

    private static PostgreSqlFinancialCrimeControlPlane CreateControl(
        TestDbContext context,
        IComplianceEvidenceStore? evidenceStore = null)
    {
        var evidence = new PostgreSqlComplianceEvidenceStore(context);
        return new PostgreSqlFinancialCrimeControlPlane(
            context, evidenceStore ?? evidence, evidence, new PostgreSqlComplianceHoldStore(context));
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
            new FinancialCrimeModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }
}
