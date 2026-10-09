using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Tests that the adaptive (online statistical learning) anomaly scorer is composed into
///     the login attempt risk analysis alongside the fixed-weight heuristics: learned
///     deviations raise the risk score and appear as detected anomaly labels, while
///     cold-start abstentions leave the heuristic score untouched.
/// </summary>
public class LoginAttemptAnalysisAdaptiveIntegrationTests
{
    private readonly Mock<IAuthenticationAttemptRepository> _attemptRepoMock = new();
    private readonly Mock<IThreatDetectionService> _threatDetectionMock = new();
    private readonly Mock<ISiemIntegrationService> _siemServiceMock = new();
    private readonly Mock<IAdaptiveAnomalyDetectionService> _adaptiveMock = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    public LoginAttemptAnalysisAdaptiveIntegrationTests()
    {
        _attemptRepoMock
            .Setup(repo => repo.GetRecentAttemptsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _attemptRepoMock
            .Setup(repo => repo.GetLastSuccessfulAttemptAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt?)null);
        _attemptRepoMock
            .Setup(repo => repo.GetFailedAttemptsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _threatDetectionMock
            .Setup(service => service.DetectBruteForceAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(false);
    }

    private LoginAttemptAnalysisService CreateSut() => new(
        _attemptRepoMock.Object,
        _threatDetectionMock.Object,
        NullLogger<LoginAttemptAnalysisService>.Instance,
        _configuration,
        _siemServiceMock.Object,
        threatIntelligenceProvider: null,
        auditEventSink: null,
        adaptiveAnomalyDetectionService: _adaptiveMock.Object);

    private static AuthenticationAttemptContext MiddayAttempt() => new()
    {
        UserId = Guid.NewGuid(),
        Identifier = "user@example.com",
        AuthenticationMethod = "Local",
        IpAddress = "203.0.113.10",
        UserAgent = "unit-test-agent/1.0",
        AttemptedAt = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_AppendsLearnedDeviationLabelsAndRiskScore()
    {
        _adaptiveMock
            .Setup(service => service.AssessAsync(It.IsAny<AuthenticationAttemptContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdaptiveAnomalyAssessment
            {
                Origin = AdaptiveAnomalyOrigin.LearnedBaseline,
                IsLearnedDeviation = true,
                CombinedZScore = 7.4,
                DeviationLabels = ["Learned:HourOfDayDeviation", "Learned:IpNovelty"],
                LearnedRiskScoreContribution = 12,
                BaselineSampleCount = 42
            });
        var sut = CreateSut();

        var result = await sut.AnalyzeLoginAttemptAsync(MiddayAttempt());

        // Heuristic baseline for an unknown subject is +10 (FirstAttemptOrLongAbsence); the learned
        // deviation adds its configured contribution on top and surfaces its feature labels.
        result.RiskScore.Should().Be(22);
        result.DetectedAnomalies.Should().Contain("FirstAttemptOrLongAbsence");
        result.DetectedAnomalies.Should().Contain("Learned:HourOfDayDeviation").And.Contain("Learned:IpNovelty");
        _adaptiveMock.Verify(
            service => service.AssessAsync(It.IsAny<AuthenticationAttemptContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_ColdStartAbstentionLeavesTheHeuristicScoreUntouched()
    {
        _adaptiveMock
            .Setup(service => service.AssessAsync(It.IsAny<AuthenticationAttemptContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdaptiveAnomalyAssessment.ColdStartFallback(sampleCount: 3));
        var sut = CreateSut();

        var result = await sut.AnalyzeLoginAttemptAsync(MiddayAttempt());

        result.RiskScore.Should().Be(10);
        result.DetectedAnomalies.Should().Contain("FirstAttemptOrLongAbsence");
        result.DetectedAnomalies.Should().NotContain(label => label.StartsWith("Learned:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_AdaptiveFailuresDoNotBreakTheHeuristicAnalysis()
    {
        _adaptiveMock
            .Setup(service => service.AssessAsync(It.IsAny<AuthenticationAttemptContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdaptiveAnomalyAssessment
            {
                Origin = AdaptiveAnomalyOrigin.AssessmentFailed,
                IsLearnedDeviation = false,
                CombinedZScore = 0,
                DeviationLabels = [],
                LearnedRiskScoreContribution = 0,
                BaselineSampleCount = 0
            });
        var sut = CreateSut();

        var result = await sut.AnalyzeLoginAttemptAsync(MiddayAttempt());

        result.RiskScore.Should().Be(10);
        result.IsAnomalous.Should().BeFalse();
    }
}
