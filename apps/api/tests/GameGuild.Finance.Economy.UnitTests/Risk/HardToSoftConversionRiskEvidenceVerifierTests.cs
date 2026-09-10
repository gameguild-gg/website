using FluentAssertions;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class HardToSoftConversionRiskEvidenceVerifierTests
{
    [Fact]
    public async Task VerifyAsync_RequiresFreshAuditableAllowFromBothIndependentSources()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var observed = DateTimeOffset.UtcNow;
        var financialCrime = new FinancialCrimeSource(AllowFinancialCrime(observed));
        var trustSafety = new TrustSafetySource(AllowTrustSafety(observed));
        var verifier = new HardToSoftConversionRiskEvidenceVerifier(financialCrime, trustSafety);

        var evidence = await verifier.VerifyAsync(actorId, tenantId, CancellationToken.None);

        financialCrime.SubjectReferences.Should().ContainSingle();
        trustSafety.SubjectReferences.Should().Equal(financialCrime.SubjectReferences);
        financialCrime.SubjectReferences[0].Should().HaveLength(64).And.NotContain(actorId.ToString("N"));
        evidence.Should().HaveCount(2).And.OnlyContain(item => item.Outcome == ExternalRiskOutcome.Allow);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerifyAsync_RejectsMissingActorOrTenant(bool missingActor)
    {
        var observed = DateTimeOffset.UtcNow;
        var financialCrime = new FinancialCrimeSource(AllowFinancialCrime(observed));
        var trustSafety = new TrustSafetySource(AllowTrustSafety(observed));
        var verifier = new HardToSoftConversionRiskEvidenceVerifier(financialCrime, trustSafety);
        var actorId = missingActor ? Guid.Empty : Guid.NewGuid();
        var tenantId = missingActor ? Guid.NewGuid() : Guid.Empty;

        var act = () => verifier.VerifyAsync(actorId, tenantId, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.ParamName.Should().Be(missingActor ? "actorId" : "tenantId");
        financialCrime.SubjectReferences.Should().BeEmpty();
        trustSafety.SubjectReferences.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAsync_FailsClosedWhenEitherSourceDoesNotAllowTheOperation()
    {
        var observed = DateTimeOffset.UtcNow;
        var verifier = new HardToSoftConversionRiskEvidenceVerifier(
            new FinancialCrimeSource(AllowFinancialCrime(observed)),
            new TrustSafetySource(AllowTrustSafety(observed) with { Outcome = ExternalRiskOutcome.Review }));

        var act = () => verifier.VerifyAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<ExternalRiskEvidenceException>()
            .WithMessage("*TrustSafety*");
    }

    [Fact]
    public async Task VerifyAsync_RejectsExpiredEvidenceAndHonorsCancellation()
    {
        var observed = DateTimeOffset.UtcNow;
        var verifier = new HardToSoftConversionRiskEvidenceVerifier(
            new FinancialCrimeSource(AllowFinancialCrime(observed) with { ExpiresAt = observed }),
            new TrustSafetySource(AllowTrustSafety(observed)));

        var expired = () => verifier.VerifyAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        await expired.Should().ThrowAsync<ExternalRiskEvidenceException>()
            .WithMessage("*FinancialCrime*");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = () => verifier.VerifyAsync(Guid.NewGuid(), Guid.NewGuid(), cancellation.Token);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void OpaqueReference_IsCanonicalAcrossEconomyAndCompliance_AndRejectsMissingIdentity()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var reference = EconomySubjectReference.ForUser(tenantId, actorId);

        reference.Should().Be(EconomySubjectReference.ForUser(tenantId, actorId));
        reference.Should().HaveLength(64).And.NotContain(actorId.ToString("N"));
        reference.Should().NotBe(EconomySubjectReference.ForUser(tenantId, Guid.NewGuid()));
        reference.Should().NotBe(EconomySubjectReference.ForUser(Guid.NewGuid(), actorId));
        Action noActor = () => EconomySubjectReference.ForUser(tenantId, Guid.Empty);
        Action noTenant = () => EconomySubjectReference.ForUser(Guid.Empty, actorId);
        noActor.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("actorId");
        noTenant.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("tenantId");
    }

    [Fact]
    public async Task TenantAwareRiskSourceOverloadsRequireTenantAndDelegateOpaqueSubject()
    {
        var observed = DateTimeOffset.UtcNow;
        IFinancialCrimeRiskInputSource financial = new FinancialCrimeSource(AllowFinancialCrime(observed));
        ITrustSafetyRiskInputSource trust = new TrustSafetySource(AllowTrustSafety(observed));

        (await financial.ReadAsync(Guid.NewGuid(), "opaque", observed)).Outcome.Should().Be(ExternalRiskOutcome.Allow);
        (await trust.ReadAsync(Guid.NewGuid(), "opaque", observed)).Outcome.Should().Be(ExternalRiskOutcome.Allow);
        await FluentActions.Invoking(() => financial.ReadAsync(Guid.Empty, "opaque", observed).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => trust.ReadAsync(Guid.Empty, "opaque", observed).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    private static FinancialCrimeRiskInput AllowFinancialCrime(DateTimeOffset observed) => new(
        1, observed.AddMinutes(-1), observed.AddMinutes(5), ExternalRiskOutcome.Allow, "financial-evidence", true);

    private static TrustSafetyRiskInput AllowTrustSafety(DateTimeOffset observed) => new(
        1, observed.AddMinutes(-1), observed.AddMinutes(5), ExternalRiskOutcome.Allow, "trust-evidence", true);

    private sealed class FinancialCrimeSource(FinancialCrimeRiskInput result) : IFinancialCrimeRiskInputSource
    {
        public List<string> SubjectReferences { get; } = [];

        public ValueTask<FinancialCrimeRiskInput> ReadAsync(
            string opaqueSubjectReference,
            DateTimeOffset observedAt,
            CancellationToken cancellationToken = default)
        {
            SubjectReferences.Add(opaqueSubjectReference);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TrustSafetySource(TrustSafetyRiskInput result) : ITrustSafetyRiskInputSource
    {
        public List<string> SubjectReferences { get; } = [];

        public ValueTask<TrustSafetyRiskInput> ReadAsync(
            string opaqueSubjectReference,
            DateTimeOffset observedAt,
            CancellationToken cancellationToken = default)
        {
            SubjectReferences.Add(opaqueSubjectReference);
            return ValueTask.FromResult(result);
        }
    }
}
