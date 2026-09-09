using System.Reflection;
using FluentAssertions;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.TrustSafety.UnitTests;

public sealed class TrustSafetyCoverageCompletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task RiskSourceAndSignatureAdapterMapAndDelegateEveryOutcome()
    {
        Action nullReader = () => new PostgreSqlTrustSafetyRiskInputSource(null!);
        nullReader.Should().Throw<ArgumentNullException>();
        var evidence = new StubEvidence();
        var source = new PostgreSqlTrustSafetyRiskInputSource(evidence);
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
            evidence.Current = Evidence(result, true, "hash");
            var input = await source.ReadAsync(TenantId, "subject", Now);
            input.Outcome.Should().Be(expected);
            input.IsAuditable.Should().BeTrue();
        }

        evidence.Current = Evidence(ComplianceEvidenceResult.Approved, false, "hash");
        (await source.ReadAsync(TenantId, "subject", Now)).IsAuditable.Should().BeFalse();
        evidence.Current = Evidence(ComplianceEvidenceResult.Approved, true, " ");
        (await source.ReadAsync(TenantId, "subject", Now)).IsAuditable.Should().BeFalse();

        Action nullVerifier = () => new EconomyTrustSafetyEventSignatureVerifier(null!);
        nullVerifier.Should().Throw<ArgumentNullException>();
        var verifier = new StubPolicyVerifier { Result = true };
        var adapter = new EconomyTrustSafetyEventSignatureVerifier(verifier);
        (await adapter.VerifyAsync("canonical", "key", "signature", CancellationToken.None)).Should().BeTrue();
        verifier.LastRequest.Should().Be(("canonical", "key", "signature"));
    }

    [Fact]
    public void ConstructorAndValidationPredicatesAreExplicitlyCovered()
    {
        var evidence = new StubEvidence();
        var signature = new StubSignatureVerifier();
        var holds = new StubHolds();
        using var relational = new ValidationDbContext(new DbContextOptionsBuilder<ValidationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1")
            .Options);
        Action nullContext = () => new PostgreSqlTrustSafetyControlPlane(null!, signature, evidence, holds);
        Action nonDb = () => new PostgreSqlTrustSafetyControlPlane(new NonDbContext(), signature, evidence, holds);
        Action nullSignature = () => new PostgreSqlTrustSafetyControlPlane(relational, null!, evidence, holds);
        Action nullEvidence = () => new PostgreSqlTrustSafetyControlPlane(relational, signature, null!, holds);
        Action nullHolds = () => new PostgreSqlTrustSafetyControlPlane(relational, signature, evidence, null!);
        nullContext.Should().Throw<ArgumentNullException>();
        nonDb.Should().Throw<InvalidOperationException>();
        nullSignature.Should().Throw<ArgumentNullException>();
        nullEvidence.Should().Throw<ArgumentNullException>();
        nullHolds.Should().Throw<ArgumentNullException>();

        var eventValidator = Private("ValidateEvent", typeof(TrustSafetyEvent));
        Capture(eventValidator, (object?)null).Should().BeOfType<ArgumentNullException>();
        var validEvent = Event();
        eventValidator.Invoke(null, [validEvent]);
        foreach (var invalid in new[]
                 {
                     validEvent with { Id = Guid.Empty }, validEvent with { TenantId = Guid.Empty },
                     validEvent with { EventId = " " }, validEvent with { SubjectHash = " " },
                     validEvent with { PayloadHash = " " }, validEvent with { EvidenceHash = " " },
                     validEvent with { RawObjectReference = " " }, validEvent with { KeyId = " " },
                     validEvent with { Signature = " " }, validEvent with { Kind = (TrustSafetyEventKind)999 },
                     validEvent with { Outcome = (TrustSafetyOutcome)999 }, validEvent with { Version = 0 },
                     validEvent with { PolicyVersion = 0 }, validEvent with { ExpiresAt = Now },
                     validEvent with { ReceivedAt = Now.AddTicks(-1) }
                 })
            Capture(eventValidator, invalid).Should().BeOfType<ArgumentException>();

        var appealValidator = Private("ValidateAppeal", typeof(TrustSafetyAppeal));
        Capture(appealValidator, (object?)null).Should().BeOfType<ArgumentNullException>();
        var appeal = Appeal();
        appealValidator.Invoke(null, [appeal]);
        foreach (var invalid in new[]
                 {
                     appeal with { Id = Guid.Empty }, appeal with { TenantId = Guid.Empty },
                     appeal with { SubmittedBy = Guid.Empty }, appeal with { State = TrustSafetyAppealState.Assigned },
                     appeal with { Version = 2 }, appeal with { AssignedTo = ActorId },
                     appeal with { DecidedBy = ActorId }, appeal with { DecidedAt = Now },
                     appeal with { SubjectHash = " " }, appeal with { RestrictionReferenceHash = " " },
                     appeal with { SubmissionEvidenceHash = " " }
                 })
            Capture(appealValidator, invalid).Should().BeOfType<ArgumentException>();

        var tenantActor = Private("ValidateTenantActor", typeof(Guid), typeof(Guid));
        Capture(tenantActor, Guid.Empty, ActorId).Should().BeOfType<ArgumentException>();
        Capture(tenantActor, TenantId, Guid.Empty).Should().BeOfType<ArgumentException>();
        tenantActor.Invoke(null, [TenantId, ActorId]);

        var map = Private("Map", typeof(TrustSafetyOutcome));
        foreach (var outcome in Enum.GetValues<TrustSafetyOutcome>()) map.Invoke(null, [outcome]).Should().NotBeNull();
        PostgreSqlTrustSafetyControlPlane.Canonicalize(validEvent).Should().Contain(validEvent.EventId);
        new TrustSafetyConflictException("conflict").Message.Should().Be("conflict");
    }

    private static MethodInfo Private(string name, params Type[] parameters) =>
        typeof(PostgreSqlTrustSafetyControlPlane).GetMethod(
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

    private static TrustSafetyEvent Event() => new(
        Guid.NewGuid(), "event", TenantId, "subject", TrustSafetyEventKind.Moderation,
        1, TrustSafetyOutcome.Approved, 7, "payload", "evidence", "s3://raw",
        "key", "signature", Now, Now.AddHours(1), Now);

    private static TrustSafetyAppeal Appeal() => new(
        Guid.NewGuid(), TenantId, "subject", "restriction", TrustSafetyAppealState.Submitted,
        ActorId, null, null, "submission", null, null, Now, null, 1);

    private static DurableComplianceEvidence Evidence(
        ComplianceEvidenceResult result,
        bool verified,
        string hash) => new(
        "internal", "production", "event", TenantId, "subject", ComplianceEvidenceKinds.TrustSafety,
        1, result, 7, hash, verified, Now, Now.AddHours(1));

    private sealed class StubEvidence : IComplianceEvidenceStore, IComplianceEvidenceReader
    {
        public DurableComplianceEvidence? Current { get; set; }
        public ValueTask<ComplianceEvidenceIngestionResult> IngestAsync(ComplianceEvidenceEnvelope envelope, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ComplianceEvidenceIngestionResult(ComplianceEvidenceIngestionStatus.Published, Guid.NewGuid()));
        public ValueTask<DurableComplianceEvidence?> ReadLatestAsync(Guid tenantId, string subjectHash, string evidenceKind, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Current);
    }

    private sealed class StubPolicyVerifier : ICapabilityPolicySignatureVerifier
    {
        public bool Result { get; set; }
        public (string Payload, string Key, string Signature)? LastRequest { get; private set; }
        public ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken)
        {
            LastRequest = (canonicalPayload, keyId, signature);
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubSignatureVerifier : ITrustSafetyEventSignatureVerifier
    {
        public ValueTask<bool> VerifyAsync(string canonicalPayload, string keyId, string signature, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }

    private sealed class StubHolds : IComplianceHoldStore
    {
        public ValueTask<ComplianceHold> ActivateAsync(ComplianceHoldActivation activation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ComplianceHold> ReleaseAsync(Guid holdId, Guid actorId, string evidenceHash, DateTimeOffset releasedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<bool> IsActiveAsync(ComplianceHoldScope scope, DateTimeOffset now, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    }

    private sealed class ValidationDbContext(DbContextOptions<ValidationDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NonDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
