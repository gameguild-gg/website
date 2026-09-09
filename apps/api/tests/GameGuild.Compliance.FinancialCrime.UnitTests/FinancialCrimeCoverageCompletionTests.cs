using System.Reflection;
using FluentAssertions;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Compliance.FinancialCrime.UnitTests;

public sealed class FinancialCrimeCoverageCompletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task RiskInputSourceMapsEveryDurableOutcomeAndFailsClosed()
    {
        Action nullReader = () => new PostgreSqlFinancialCrimeRiskInputSource(null!);
        nullReader.Should().Throw<ArgumentNullException>();
        var reader = new StubEvidence();
        var source = new PostgreSqlFinancialCrimeRiskInputSource(reader);

        await FluentActions.Awaiting(() => source.ReadAsync(Guid.Empty, "subject", Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => source.ReadAsync(TenantId, " ", Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        (await source.ReadAsync(TenantId, "subject", Now)).Outcome.Should().Be(ExternalRiskOutcome.Unavailable);

        foreach (var (result, expected) in new[]
                 {
                     (ComplianceEvidenceResult.Approved, ExternalRiskOutcome.Allow),
                     (ComplianceEvidenceResult.Rejected, ExternalRiskOutcome.Deny),
                     (ComplianceEvidenceResult.NeedsReview, ExternalRiskOutcome.Review),
                     (ComplianceEvidenceResult.Unavailable, ExternalRiskOutcome.Unavailable)
                 })
        {
            reader.Current = Evidence(result, signatureVerified: true, evidenceHash: "hash");
            var input = await source.ReadAsync(TenantId, "subject", Now);
            input.Outcome.Should().Be(expected);
            input.IsAuditable.Should().BeTrue();
        }

        reader.Current = Evidence(ComplianceEvidenceResult.Approved, signatureVerified: false, evidenceHash: "hash");
        (await source.ReadAsync(TenantId, "subject", Now)).IsAuditable.Should().BeFalse();
        reader.Current = Evidence(ComplianceEvidenceResult.Approved, signatureVerified: true, evidenceHash: " ");
        (await source.ReadAsync(TenantId, "subject", Now)).IsAuditable.Should().BeFalse();
    }

    [Fact]
    public void ConstructorAndEveryValidationPredicateAreExplicitlyCovered()
    {
        var evidence = new StubEvidence();
        var holds = new StubHolds();
        using var relational = new ValidationDbContext(new DbContextOptionsBuilder<ValidationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1")
            .Options);
        Action nullContext = () => new PostgreSqlFinancialCrimeControlPlane(null!, evidence, evidence, holds);
        Action nonDb = () => new PostgreSqlFinancialCrimeControlPlane(new NonDbContext(), evidence, evidence, holds);
        Action nullStore = () => new PostgreSqlFinancialCrimeControlPlane(relational, null!, evidence, holds);
        Action nullReader = () => new PostgreSqlFinancialCrimeControlPlane(relational, evidence, null!, holds);
        Action nullHolds = () => new PostgreSqlFinancialCrimeControlPlane(relational, evidence, evidence, null!);
        nullContext.Should().Throw<ArgumentNullException>();
        nonDb.Should().Throw<InvalidOperationException>();
        nullStore.Should().Throw<ArgumentNullException>();
        nullReader.Should().Throw<ArgumentNullException>();
        nullHolds.Should().Throw<ArgumentNullException>();

        var screeningValidator = Private("ValidateScreening", typeof(FinancialCrimeScreening));
        Capture(screeningValidator, (object?)null).Should().BeOfType<ArgumentNullException>();
        var screening = Screening();
        screeningValidator.Invoke(null, [screening]);
        foreach (var invalid in new[]
                 {
                     screening with { Id = Guid.Empty }, screening with { TenantId = Guid.Empty },
                     screening with { Provider = " " }, screening with { Environment = " " },
                     screening with { ProviderEventId = " " }, screening with { SubjectHash = " " },
                     screening with { PayloadHash = " " }, screening with { EvidenceHash = " " },
                     screening with { RawObjectReference = " " }, screening with { SignatureVerified = false },
                     screening with { Version = 0 }, screening with { PolicyVersion = 0 },
                     screening with { ExpiresAt = Now }, screening with { NextScreenAt = Now },
                     screening with { ReceivedAt = Now.AddSeconds(-1) }
                 })
            Capture(screeningValidator, invalid).Should().BeOfType<ArgumentException>();

        var signalValidator = Private("ValidateSignal", typeof(FinancialCrimeTransactionSignal));
        Capture(signalValidator, (object?)null).Should().BeOfType<ArgumentNullException>();
        var signal = Signal();
        signalValidator.Invoke(null, [signal]);
        foreach (var invalid in new[]
                 {
                     signal with { Id = Guid.Empty }, signal with { TenantId = Guid.Empty },
                     signal with { SubjectHash = " " }, signal with { OperationFingerprint = " " },
                     signal with { SignalType = " " }, signal with { EvidenceHash = " " },
                     signal with { IdempotencyKey = " " }, signal with { Score = -1 },
                     signal with { Score = 1_000_001 }, signal with { HoldExpiresAt = Now }
                 })
            Capture(signalValidator, invalid).Should().BeOfType<ArgumentException>();

        var decisionValidator = Private("ValidateDecision", typeof(FinancialCrimeCaseDecision));
        Capture(decisionValidator, (object?)null).Should().BeOfType<ArgumentNullException>();
        var decision = Decision();
        decisionValidator.Invoke(null, [decision]);
        foreach (var invalid in new[]
                 {
                     decision with { Id = Guid.Empty }, decision with { CaseId = Guid.Empty },
                     decision with { TenantId = Guid.Empty }, decision with { DecidedBy = Guid.Empty },
                     decision with { SubjectHash = " " }, decision with { ReasonCode = " " },
                     decision with { EvidenceHash = " " }, decision with { RawObjectReference = " " },
                     decision with { Version = 0 }, decision with { PolicyVersion = 0 },
                     decision with { ExpiresAt = Now }
                 })
            Capture(decisionValidator, invalid).Should().BeOfType<ArgumentException>();

        var tenantActor = Private("ValidateTenantActor", typeof(Guid), typeof(Guid));
        Capture(tenantActor, Guid.Empty, ActorId).Should().BeOfType<ArgumentException>();
        Capture(tenantActor, TenantId, Guid.Empty).Should().BeOfType<ArgumentException>();
        tenantActor.Invoke(null, [TenantId, ActorId]);

        var map = Private("Map", typeof(FinancialCrimeOutcome));
        foreach (var outcome in Enum.GetValues<FinancialCrimeOutcome>()) map.Invoke(null, [outcome]).Should().NotBeNull();
        var reference = new FinancialCrimeRegulatoryReference(
            Guid.NewGuid(), Guid.NewGuid(), "SAR", "BR", "hash", ActorId, Now);
        reference.Id.Should().NotBeEmpty();
        new FinancialCrimeConflictException("conflict").Message.Should().Be("conflict");
    }

    private static MethodInfo Private(string name, params Type[] parameters) =>
        typeof(PostgreSqlFinancialCrimeControlPlane).GetMethod(
            name, BindingFlags.Static | BindingFlags.NonPublic, null, parameters, null)
        ?? throw new InvalidOperationException($"Private method {name} was not found.");

    private static Exception Capture(MethodInfo method, params object?[] arguments)
    {
        try
        {
            method.Invoke(null, arguments);
            throw new InvalidOperationException("Expected validation to reject the input.");
        }
        catch (TargetInvocationException exception)
        {
            return exception.InnerException!;
        }
    }

    private static FinancialCrimeScreening Screening() => new(
        Guid.NewGuid(), "sumsub", "sandbox", "event", TenantId, "subject", 1,
        FinancialCrimeOutcome.Approved, false, false, false, 7, "payload", "evidence",
        "s3://raw", true, Now, Now.AddHours(1), Now.AddMinutes(30), Now.AddSeconds(1));

    private static FinancialCrimeTransactionSignal Signal() => new(
        Guid.NewGuid(), TenantId, "subject", "operation", "velocity", 100,
        "evidence", "idempotency", Now, Now.AddHours(1));

    private static FinancialCrimeCaseDecision Decision() => new(
        Guid.NewGuid(), Guid.NewGuid(), TenantId, "subject", 1, FinancialCrimeOutcome.Approved,
        7, "reason", "evidence", "s3://raw", ActorId, Now, Now.AddHours(1));

    private static DurableComplianceEvidence Evidence(
        ComplianceEvidenceResult result,
        bool signatureVerified,
        string evidenceHash) => new(
        "provider", "sandbox", "event", TenantId, "subject", ComplianceEvidenceKinds.FinancialCrime,
        1, result, 7, evidenceHash, signatureVerified, Now, Now.AddHours(1));

    private sealed class StubEvidence : IComplianceEvidenceStore, IComplianceEvidenceReader
    {
        public DurableComplianceEvidence? Current { get; set; }

        public ValueTask<ComplianceEvidenceIngestionResult> IngestAsync(
            ComplianceEvidenceEnvelope envelope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ComplianceEvidenceIngestionResult(ComplianceEvidenceIngestionStatus.Published, Guid.NewGuid()));

        public ValueTask<DurableComplianceEvidence?> ReadLatestAsync(
            Guid tenantId,
            string subjectHash,
            string evidenceKind,
            CancellationToken cancellationToken) => ValueTask.FromResult(Current);
    }

    private sealed class StubHolds : IComplianceHoldStore
    {
        public ValueTask<ComplianceHold> ActivateAsync(ComplianceHoldActivation activation, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public ValueTask<ComplianceHold> ReleaseAsync(Guid holdId, Guid actorId, string evidenceHash, DateTimeOffset releasedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public ValueTask<bool> IsActiveAsync(ComplianceHoldScope scope, DateTimeOffset now, CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);
    }

    private sealed class ValidationDbContext(DbContextOptions<ValidationDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NonDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
