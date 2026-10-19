using FluentAssertions;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Threat-intelligence signal integration into
///     <see cref="LoginAttemptAnalysisService.AnalyzeLoginAttemptAsync" />:
///     score bumps in enforce mode, log-only observation mode by default,
///     and fail-open when the provider errors.
/// </summary>
public sealed class LoginAttemptAnalysisServiceThreatIntelTests
{
    private const string BreachedDigest = "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8";

    private readonly Mock<IAuthenticationAttemptRepository> _attemptRepoMock = new();
    private readonly Mock<IThreatDetectionService> _threatDetectionMock = new();
    private readonly Mock<ISiemIntegrationService> _siemServiceMock = new();
    private readonly Mock<IThreatIntelligenceProvider> _threatIntelMock = new();
    private readonly Mock<IAuthenticationAuditEventSink> _auditSinkMock = new();

    // A Wednesday afternoon attempt with no known user and no identifier: the baseline
    // analysis contributes 0 risk score, so every point below comes from threat intel.
    private static AuthenticationAttemptContext CleanContext => new()
    {
        UserId = null,
        Identifier = string.Empty,
        IpAddress = "203.0.113.42",
        UserAgent = "TestBrowser/1.0",
        AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
    };

    private LoginAttemptAnalysisService CreateSut(
        IEnumerable<KeyValuePair<string, string?>>? configData = null,
        bool includeProvider = true,
        ILogger<LoginAttemptAnalysisService>? logger = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData ?? [])
            .Build();

        return new LoginAttemptAnalysisService(
            _attemptRepoMock.Object,
            _threatDetectionMock.Object,
            logger ?? NullLogger<LoginAttemptAnalysisService>.Instance,
            configuration,
            _siemServiceMock.Object,
            includeProvider ? _threatIntelMock.Object : null,
            _auditSinkMock.Object);
    }

    // ── Enforce mode: score bump flows into the step-up machinery ─────

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_MaliciousIpInEnforceMode_RaisesScoreToStepUpThreshold()
    {
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync("203.0.113.42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: true, MatchedCidr: "203.0.113.0/24"));
        _threatIntelMock
            .Setup(provider => provider.CheckPasswordHashAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: false));
        var sut = CreateSut([new("ThreatIntelligence:EnforcementMode", "Enforce")]);

        var result = await sut.AnalyzeLoginAttemptAsync(CleanContext);

        result.RiskScore.Should().Be(60);
        result.RiskLevel.Should().Be(RiskLevel.High, "a single enforce-mode match must reach the step-up threshold");
        result.IsAnomalous.Should().BeTrue();
        result.DetectedAnomalies.Should().Contain("ThreatIntel:MaliciousIp");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_BreachedPasswordInEnforceMode_RaisesScoreAndAudits()
    {
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: false));
        _threatIntelMock
            .Setup(provider => provider.CheckPasswordHashAsync(BreachedDigest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: true));
        var sut = CreateSut([new("ThreatIntelligence:EnforcementMode", "Enforce")]);

        var result = await sut.AnalyzeLoginAttemptAsync(new AuthenticationAttemptContext
        {
            UserId = null,
            Identifier = string.Empty,
            IpAddress = "198.51.100.7",
            UserAgent = "TestBrowser/1.0",
            PasswordSha256Hex = BreachedDigest,
            AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
        });

        result.RiskScore.Should().Be(60);
        result.RiskLevel.Should().Be(RiskLevel.High);
        result.DetectedAnomalies.Should().Contain("ThreatIntel:BreachedPassword");

        _auditSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(audit =>
                    audit.ActionType == "Authentication.ThreatIntelligenceMatch"
                    && audit.ErrorMessage == "ThreatIntel:BreachedPassword"
                    && audit.AssessedRiskLevel == RiskLevel.High),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Observation", 35, 0)]
    [InlineData("Observation", 75, 0)]
    [InlineData("Enforce", 35, 35)]
    [InlineData("Enforce", 75, 75)]
    public async Task AnalyzeLoginAttemptAsync_BreachedPasswordMatch_LogsOnlySignalAndScore(
        string mode, int configuredScore, int expectedScore)
    {
        var logger = new Mock<ILogger<LoginAttemptAnalysisService>>();
        AuthenticationAuditEvent? recordedAudit = null;
        _threatIntelMock.Setup(provider => provider.ProviderName).Returns("LocalFile");
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync("203.0.113.42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: false));
        _threatIntelMock
            .Setup(provider => provider.CheckPasswordHashAsync(BreachedDigest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: true));
        _auditSinkMock
            .Setup(sink => sink.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), CancellationToken.None))
            .Callback<AuthenticationAuditEvent, CancellationToken>((audit, _) => recordedAudit = audit)
            .Returns(Task.CompletedTask);
        var context = CleanContext;
        context.PasswordSha256Hex = BreachedDigest;
        var sut = CreateSut(
        [
            new("ThreatIntelligence:EnforcementMode", mode),
            new("ThreatIntelligence:BreachedPasswordRiskScore", configuredScore.ToString(System.Globalization.CultureInfo.InvariantCulture))
        ], logger: logger.Object);

        var result = await sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().Be(expectedScore);
        result.IsAnomalous.Should().Be(expectedScore >= 30);
        result.DetectedAnomalies.Should().BeEquivalentTo(["ThreatIntel:BreachedPassword"]);
        JsonSerializer.Serialize(result).Should().NotContain(BreachedDigest);
        context.PasswordSha256Hex.Should().Be(BreachedDigest);
        _threatIntelMock.Verify(provider => provider.CheckPasswordHashAsync(BreachedDigest, CancellationToken.None), Times.Once);

        var logs = logger.Invocations.Where(invocation => invocation.Method.Name == nameof(ILogger.Log)).ToArray();
        logs.Should().HaveCount(mode == "Enforce" ? 2 : 1);
        foreach (var log in logs)
        {
            log.Arguments[0].Should().Be(LogLevel.Warning);
            log.Arguments[3].Should().BeNull();
            log.Arguments[2].ToString().Should().NotContain(BreachedDigest);
        }

        var matchProperties = ((IEnumerable<KeyValuePair<string, object?>>)logs[0].Arguments[2]).ToDictionary();
        matchProperties.Keys.Should().BeEquivalentTo(["Mode", "Signal", "IpAddress", "UserId", "RiskScoreBump", "{OriginalFormat}"]);
        matchProperties["Mode"].Should().Be(mode);
        matchProperties["Signal"].Should().Be("ThreatIntel:BreachedPassword");
        matchProperties["IpAddress"].Should().Be("203.0.113.42");
        matchProperties["UserId"].Should().BeNull();
        matchProperties["RiskScoreBump"].Should().BeOfType<int>().Which.Should().Be(expectedScore);
        matchProperties["{OriginalFormat}"].Should().Be(
            "Threat intelligence match ({Mode}) - Signal: {Signal}, IP: {IpAddress}, UserId: {UserId}, RiskScoreBump: {RiskScoreBump}");

        if (mode == "Enforce")
        {
            var anomalyProperties = ((IEnumerable<KeyValuePair<string, object?>>)logs[1].Arguments[2]).ToDictionary();
            anomalyProperties.Keys.Should().BeEquivalentTo(["UserId", "IpAddress", "RiskScore", "Anomalies", "{OriginalFormat}"]);
            anomalyProperties["RiskScore"].Should().BeOfType<int>().Which.Should().Be(expectedScore);
            anomalyProperties["Anomalies"].Should().Be("ThreatIntel:BreachedPassword");
            anomalyProperties["UserId"].Should().BeNull();
            anomalyProperties["IpAddress"].Should().Be("203.0.113.42");
            anomalyProperties["{OriginalFormat}"].Should().Be(
                "Anomalous login attempt detected - UserId: {UserId}, IP: {IpAddress}, RiskScore: {RiskScore}, Anomalies: {Anomalies}");
        }

        recordedAudit.Should().NotBeNull();
        recordedAudit!.ActionType.Should().Be("Authentication.ThreatIntelligenceMatch");
        recordedAudit.ErrorMessage.Should().Be("ThreatIntel:BreachedPassword");
        recordedAudit.AssessedRiskLevel.Should().Be(mode == "Enforce" ? RiskLevel.High : RiskLevel.Medium);
        JsonSerializer.Serialize(recordedAudit).Should().NotContain(BreachedDigest);
        var metadata = JsonSerializer.SerializeToElement(recordedAudit.Metadata);
        metadata.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            ["Signal", "EnforcementMode", "MatchedDetail", "Provider"]);
        metadata.GetProperty("Signal").GetString().Should().Be("ThreatIntel:BreachedPassword");
        metadata.GetProperty("EnforcementMode").GetString().Should().Be(mode);
        metadata.GetProperty("MatchedDetail").ValueKind.Should().Be(JsonValueKind.Null);
        metadata.GetProperty("Provider").GetString().Should().Be("LocalFile");
        _auditSinkMock.Verify(sink => sink.RecordAsync(recordedAudit, CancellationToken.None), Times.Once);
        _auditSinkMock.VerifyNoOtherCalls();
        _attemptRepoMock.VerifyNoOtherCalls();
        _threatDetectionMock.VerifyNoOtherCalls();
        _siemServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_CustomRiskScoresAreHonored()
    {
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: true, MatchedCidr: "203.0.113.0/24"));
        _threatIntelMock
            .Setup(provider => provider.CheckPasswordHashAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: false));
        var sut = CreateSut(
        [
            new("ThreatIntelligence:EnforcementMode", "Enforce"),
            new("ThreatIntelligence:MaliciousIpRiskScore", "35")
        ]);

        var result = await sut.AnalyzeLoginAttemptAsync(CleanContext);

        result.RiskScore.Should().Be(35);
        result.RiskLevel.Should().Be(RiskLevel.Medium);
    }

    // ── Observation mode (default): log-only, no score bump ───────────

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_ObservationModeByDefault_AddsLabelWithoutRaisingScore()
    {
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: true, MatchedCidr: "203.0.113.0/24"));
        _threatIntelMock
            .Setup(provider => provider.CheckPasswordHashAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: false));
        var sut = CreateSut(); // no ThreatIntelligence configuration: observation is the default

        var result = await sut.AnalyzeLoginAttemptAsync(CleanContext);

        result.RiskScore.Should().Be(0, "observation mode must not raise the risk score");
        result.RiskLevel.Should().Be(RiskLevel.Low);
        result.IsAnomalous.Should().BeFalse();
        result.DetectedAnomalies.Should().Contain("ThreatIntel:MaliciousIp");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_ObservationModeExplicitlyConfigured_MatchesAreAudited()
    {
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: true, MatchedCidr: "203.0.113.0/24"));
        _threatIntelMock
            .Setup(provider => provider.CheckPasswordHashAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: false));
        var sut = CreateSut([new("ThreatIntelligence:EnforcementMode", "Observation")]);

        await sut.AnalyzeLoginAttemptAsync(CleanContext);

        _auditSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(audit =>
                    audit.ActionType == "Authentication.ThreatIntelligenceMatch"
                    && audit.AssessedRiskLevel == RiskLevel.Medium),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── No-op cases ───────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_NoThreatIntelMatches_LeavesScoreUnchanged()
    {
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: false));
        _threatIntelMock
            .Setup(provider => provider.CheckPasswordHashAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: false));
        var sut = CreateSut([new("ThreatIntelligence:EnforcementMode", "Enforce")]);

        var result = await sut.AnalyzeLoginAttemptAsync(CleanContext);

        result.RiskScore.Should().Be(0);
        result.DetectedAnomalies.Should().NotContain(a => a.StartsWith("ThreatIntel:", StringComparison.Ordinal));
        _auditSinkMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_NullProvider_IsANoOp()
    {
        var sut = CreateSut([new("ThreatIntelligence:EnforcementMode", "Enforce")], includeProvider: false);

        var result = await sut.AnalyzeLoginAttemptAsync(CleanContext);

        result.RiskScore.Should().Be(0);
        result.DetectedAnomalies.Should().BeEmpty();
    }

    // ── Fail-open ─────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_ProviderThrows_FailsOpen()
    {
        _threatIntelMock
            .Setup(provider => provider.CheckIpAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("feed unreadable"));
        var sut = CreateSut([new("ThreatIntelligence:EnforcementMode", "Enforce")]);

        var result = await sut.AnalyzeLoginAttemptAsync(CleanContext);

        result.RiskScore.Should().Be(0);
        result.DetectedAnomalies.Should().BeEmpty("a provider error must not fabricate a match");
        result.IsAnomalous.Should().BeFalse();
    }
}
