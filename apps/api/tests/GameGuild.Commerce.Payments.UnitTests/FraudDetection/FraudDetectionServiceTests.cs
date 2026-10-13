using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

public class FraudDetectionServiceTests
{
    private const int ReviewThreshold = 40;
    private const int BlockThreshold = 70;

    [Fact]
    public async Task AssessPaymentAsync_ShouldAllow_WhenNoSignalsFire()
    {
        var service = CreateService(options => { }, new StubSignalProvider([]));

        var assessment = await service.AssessPaymentAsync(Context(), CancellationToken.None);

        assessment.Decision.Should().Be(FraudDecision.Allow);
        assessment.Score.Should().Be(0);
        assessment.RiskLevel.Should().Be(FraudRiskLevel.Low);
        assessment.Signals.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, FraudDecision.Allow)]
    [InlineData(39, FraudDecision.Allow)]
    [InlineData(40, FraudDecision.Review)] // boundary: review threshold is inclusive
    [InlineData(69, FraudDecision.Review)]
    [InlineData(70, FraudDecision.Block)] // boundary: block threshold is inclusive
    [InlineData(100, FraudDecision.Block)]
    public async Task AssessPaymentAsync_ShouldMapScoreToDecision(int weight, FraudDecision expectedDecision)
    {
        var service = CreateService(options => { }, new StubSignalProvider([new FraudSignal("Test.Single", weight, "{}")]));

        var assessment = await service.AssessPaymentAsync(Context(), CancellationToken.None);

        assessment.Score.Should().Be(weight);
        assessment.Decision.Should().Be(expectedDecision);
    }

    [Theory]
    [InlineData(0, FraudRiskLevel.Low)]
    [InlineData(30, FraudRiskLevel.Low)]
    [InlineData(31, FraudRiskLevel.Medium)]
    [InlineData(60, FraudRiskLevel.Medium)]
    [InlineData(61, FraudRiskLevel.High)]
    [InlineData(85, FraudRiskLevel.High)]
    [InlineData(86, FraudRiskLevel.Critical)]
    [InlineData(100, FraudRiskLevel.Critical)]
    public async Task AssessPaymentAsync_ShouldMapScoreToRiskLevel(int weight, FraudRiskLevel expectedRiskLevel)
    {
        var service = CreateService(options => { }, new StubSignalProvider([new FraudSignal("Test.Single", weight, "{}")]));

        var assessment = await service.AssessPaymentAsync(Context(), CancellationToken.None);

        assessment.RiskLevel.Should().Be(expectedRiskLevel);
    }

    [Fact]
    public async Task AssessPaymentAsync_ShouldClampAggregateScoreTo100()
    {
        var service = CreateService(
            options => { },
            new StubSignalProvider(
            [
                new FraudSignal("Test.First", 80, "{}"),
                new FraudSignal("Test.Second", 80, "{}")
            ]));

        var assessment = await service.AssessPaymentAsync(Context(), CancellationToken.None);

        assessment.Score.Should().Be(100);
        assessment.Decision.Should().Be(FraudDecision.Block);
        assessment.Signals.Should().HaveCount(2);
    }

    [Fact]
    public async Task AssessPaymentAsync_ShouldAllowWithoutSignals_WhenAssessmentIsDisabled()
    {
        var service = CreateService(
            options => options.IsEnabled = false,
            new StubSignalProvider([new FraudSignal("Test.Ignored", 100, "{}")]));

        var assessment = await service.AssessPaymentAsync(Context(), CancellationToken.None);

        assessment.Decision.Should().Be(FraudDecision.Allow);
        assessment.Score.Should().Be(0);
        assessment.Signals.Should().BeEmpty();
        assessment.Reason.Should().Contain("disabled");
    }

    [Fact]
    public async Task AssessPaymentAsync_ShouldUseTenantOverrideThresholds()
    {
        var tenantId = Guid.NewGuid();
        var service = CreateService(
            options => options.TenantOverrides[tenantId.ToString()] = new FraudAssessmentOverride { ReviewThreshold = 10 },
            new StubSignalProvider([new FraudSignal("Test.Single", 15, "{}")]));

        var assessment = await service.AssessPaymentAsync(Context(tenantId), CancellationToken.None);

        assessment.Decision.Should().Be(FraudDecision.Review, "the tenant override lowered the review threshold to 10");
    }

    [Fact]
    public async Task AssessPaymentAsync_ShouldKeepDefaultThresholdsForTenantsWithoutOverride()
    {
        var overrideTenant = Guid.NewGuid();
        var service = CreateService(
            options => options.TenantOverrides[overrideTenant.ToString()] = new FraudAssessmentOverride { ReviewThreshold = 10 },
            new StubSignalProvider([new FraudSignal("Test.Single", 15, "{}")]));

        var assessment = await service.AssessPaymentAsync(Context(), CancellationToken.None);

        assessment.Decision.Should().Be(FraudDecision.Allow, "tenants without an override keep the default threshold of 40");
    }

    [Fact]
    public async Task AssessPaymentAsync_ShouldAggregateSignalsAcrossProviders()
    {
        var service = CreateService(
            options => { },
            new StubSignalProvider([new FraudSignal("Test.First", ReviewThreshold / 2, "{}")]),
            new StubSignalProvider([new FraudSignal("Test.Second", ReviewThreshold / 2, "{}")]));

        var assessment = await service.AssessPaymentAsync(Context(), CancellationToken.None);

        assessment.Score.Should().Be(ReviewThreshold);
        assessment.Decision.Should().Be(FraudDecision.Review);
    }

    [Fact]
    public void ToEvidenceJson_ShouldSerializeBoundedEvidence()
    {
        var assessment = new FraudAssessment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            72,
            FraudRiskLevel.High,
            FraudDecision.Block,
            "Score 72 reached the block threshold 70: Test.Single(72)",
            [new FraudSignal("Test.Single", 72, """{"attempts":9}""")],
            SystemClock.UtcNow);

        var json = assessment.ToEvidenceJson();

        json.Should().Contain("\"score\":72");
        json.Should().Contain("Test.Single");
        json.Length.Should().BeLessOrEqualTo(FraudAssessment.MaxEvidenceJsonLength);
    }

    private static FraudSignalContext Context(Guid? tenantId = null) => new(
        tenantId ?? Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "cus_test",
        "pm_test",
        25m,
        "USD");

    private static FraudDetectionService CreateService(
        Action<FraudAssessmentOptions> configure,
        params StubSignalProvider[] providers)
    {
        var options = new FraudAssessmentOptions();
        configure(options);
        return new FraudDetectionService(
            providers,
            new StaticOptionsMonitor(options),
            NullLogger<FraudDetectionService>.Instance);
    }

    private sealed class StubSignalProvider(IReadOnlyList<FraudSignal> signals) : IFraudSignalProvider
    {
        public string Name => "Stub";

        public Task<IReadOnlyList<FraudSignal>> EvaluateAsync(
            FraudSignalContext signalContext,
            FraudAssessmentOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult(signals);
    }

    private sealed class StaticOptionsMonitor(FraudAssessmentOptions value) : IOptionsMonitor<FraudAssessmentOptions>
    {
        public FraudAssessmentOptions CurrentValue => value;

        public FraudAssessmentOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<FraudAssessmentOptions, string?> listener) => null;
    }
}
