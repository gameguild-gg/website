using FluentAssertions;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class CooldownAndExternalEvidenceTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProtectedChangeRemainsBlockedUntilTheExactCooldownBoundary()
    {
        var registry = new ProtectedChangeCooldownRegistry();
        var subject = Guid.NewGuid();
        var change = registry.Record(
            subject, ProtectedChangeKind.PayoutDestination, "destination-hash", Time, TimeSpan.FromHours(24));

        registry.Evaluate(subject, ProtectedChangeKind.PayoutDestination, Time.AddHours(24).AddTicks(-1))
            .IsElapsed.Should().BeFalse();
        registry.Evaluate(subject, ProtectedChangeKind.PayoutDestination, Time.AddHours(24))
            .IsElapsed.Should().BeTrue();
        change.Version.Should().Be(1);
        change.ValueHash.Should().Be("destination-hash");
        change.ChangedAt.Should().Be(Time);
        typeof(ProtectedChangeCooldownRegistry).GetMethods().Select(method => method.Name)
            .Should().NotContain(name => name.Contains("Accelerate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NewProtectedChangeRestartsCooldownAndUnknownSubjectFailsClosed()
    {
        var registry = new ProtectedChangeCooldownRegistry();
        var subject = Guid.NewGuid();
        registry.Record(subject, ProtectedChangeKind.PayoutDestination, "first", Time, TimeSpan.FromHours(1));
        var second = registry.Record(
            subject, ProtectedChangeKind.PayoutDestination, "second", Time.AddMinutes(30), TimeSpan.FromHours(1));

        second.Version.Should().Be(2);
        registry.Evaluate(subject, ProtectedChangeKind.PayoutDestination, Time.AddHours(1))
            .IsElapsed.Should().BeFalse();
        FluentActions.Invoking(() => registry.Evaluate(
                Guid.NewGuid(), ProtectedChangeKind.PayoutDestination, Time))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void SubjectLookup_IsEmptyForUnknownSubject_AndRejectsEmptyIdentity()
    {
        var registry = new ProtectedChangeCooldownRegistry();

        registry.ForSubject(Guid.NewGuid()).Should().BeEmpty();
        FluentActions.Invoking(() => registry.ForSubject(Guid.Empty))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExternalEvidenceRequiresFreshAllowFromFinancialCrimeAndTrustSafety()
    {
        var valid = new[]
        {
            Evidence(ExternalRiskSource.FinancialCrime, ExternalRiskOutcome.Allow),
            Evidence(ExternalRiskSource.TrustSafety, ExternalRiskOutcome.Allow)
        };

        ExternalRiskEvidenceValidator.RequireFreshAllow(valid, Time).Should().HaveCount(2);
        FluentActions.Invoking(() => ExternalRiskEvidenceValidator.RequireFreshAllow(valid.Take(1).ToArray(), Time))
            .Should().Throw<ExternalRiskEvidenceException>();
        FluentActions.Invoking(() => ExternalRiskEvidenceValidator.RequireFreshAllow(
                [valid[0], Evidence(ExternalRiskSource.TrustSafety, ExternalRiskOutcome.Deny)], Time))
            .Should().Throw<ExternalRiskEvidenceException>();
        FluentActions.Invoking(() => ExternalRiskEvidenceValidator.RequireFreshAllow(
                [valid[0], valid[1] with { ExpiresAt = Time }], Time))
            .Should().Throw<ExternalRiskEvidenceException>();
        FluentActions.Invoking(() => ExternalRiskEvidenceValidator.RequireFreshAllow(
                [valid[0], valid[1] with { IsAuditable = false }], Time))
            .Should().Throw<ExternalRiskEvidenceException>();

        new FinancialCrimeRiskInput(
                1, Time.AddMinutes(-1), Time.AddMinutes(1), ExternalRiskOutcome.Allow, "financial", true)
            .ToEvidence().Source.Should().Be(ExternalRiskSource.FinancialCrime);
        new TrustSafetyRiskInput(
                1, Time.AddMinutes(-1), Time.AddMinutes(1), ExternalRiskOutcome.Allow, "trust", true)
            .ToEvidence().Source.Should().Be(ExternalRiskSource.TrustSafety);
    }

    private static ExternalRiskEvidence Evidence(ExternalRiskSource source, ExternalRiskOutcome outcome) =>
        new(source, 1, Time.AddMinutes(-1), Time.AddMinutes(5), outcome, $"{source}-evidence");
}
