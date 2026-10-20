using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class LoginAttemptAnalysisLoggingTests
{
    private const string Email = "Private.Person@example.test";
    private const string IpAddress = "203.0.113.42";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IAuthenticationAttemptRepository> _repository = new();
    private readonly Mock<IThreatDetectionService> _threatDetection = new();
    private readonly Mock<ISiemIntegrationService> _siem = new();
    private readonly Mock<IThreatIntelligenceProvider> _threatIntelligence = new();
    private readonly Mock<IAuthenticationAuditEventSink> _audit = new();
    private readonly Mock<IAdaptiveAnomalyDetectionService> _adaptive = new();
    private readonly Mock<ILogger<LoginAttemptAnalysisService>> _logger = new();

    public LoginAttemptAnalysisLoggingTests()
    {
        _logger.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _repository.Setup(repository => repository.GetFailedAttemptsAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt>());
        _repository.Setup(repository => repository.GetRecentAttemptsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt> { new() { UserId = _userId } });
        _repository.Setup(repository => repository.GetLastSuccessfulAttemptAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt?)null);
        _repository.Setup(repository => repository.CreateAsync(
                It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt attempt, CancellationToken _) => attempt);
        _threatDetection.Setup(service => service.DetectBruteForceAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(false);
        _threatIntelligence.Setup(provider => provider.CheckIpAddressAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: false));
        _threatIntelligence.Setup(provider => provider.CheckPasswordHashAsync(
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligencePasswordResult(IsMatch: false));
    }

    private LoginAttemptAnalysisService CreateSut(bool includeThreatIntelligence, bool includeAdaptive, bool enforce)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ThreatIntelligence:EnforcementMode"] = enforce ? "Enforce" : "Observation"
            }).Build();
        return new LoginAttemptAnalysisService(_repository.Object, _threatDetection.Object,
            _logger.Object, configuration, _siem.Object,
            includeThreatIntelligence ? _threatIntelligence.Object : null,
            _audit.Object, includeAdaptive ? _adaptive.Object : null);
    }

    private AuthenticationAttemptContext Context() => new()
    {
        UserId = _userId,
        TenantId = _tenantId,
        Identifier = Email,
        IpAddress = IpAddress,
        UserAgent = "privacy-test-agent/1.0",
        AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
    };

    private TestCreateAuthenticationAttemptRequest Request(string ipAddress) => new()
    {
        UserId = _userId,
        TenantId = _tenantId,
        Email = Email,
        IpAddress = ipAddress,
        UserAgent = "privacy-test-agent/1.0",
        IsSuccessful = true,
        ProcessingTime = TimeSpan.FromMilliseconds(100)
    };

    [Theory]
    [InlineData("203.0.113.42", false)]
    [InlineData("2001:db8::42", true)]
    public async Task RecordLoginAttempt_RedactsNormalAndSuspiciousLogs_AndPreservesPersistence(
        string ipAddress, bool suspicious)
    {
        var request = Request(ipAddress);
        if (suspicious)
        {
            request.ProcessingTime = TimeSpan.Zero;
            _repository.Setup(repository => repository.GetFailedAttemptsAsync(
                    It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Range(0, 3)
                    .Select(_ => new AuthenticationAttempt { IpAddress = ipAddress }).ToList());
        }

        var result = await CreateSut(false, false, false).RecordLoginAttemptAsync(request);

        result.IsSuspicious.Should().Be(suspicious);
        result.RiskScore.Should().Be(suspicious ? 35 : 0);
        _repository.Verify(repository => repository.CreateAsync(It.Is<AuthenticationAttempt>(attempt =>
                attempt.Email == Email.ToLowerInvariant() && attempt.IpAddress == ipAddress
                && attempt.UserId == _userId && attempt.TenantId == _tenantId),
            It.IsAny<CancellationToken>()), Times.Once);
        request.Email.Should().Be(Email);
        request.IpAddress.Should().Be(ipAddress);
        var records = Logs().Where(log => log.Properties.ContainsKey("Email")).ToArray();
        records.Should().HaveCount(suspicious ? 2 : 1);
        foreach (var entry in records)
        {
            Assert.IsType<string>(entry.Properties["Email"]).Should().MatchRegex("^email:[0-9a-f]{64}$");
            Assert.IsType<string>(entry.Properties["IpAddress"]).Should().MatchRegex("^ip(?:-network)?:[0-9a-f]{64}$");
        }
        AssertPrivateLogs(ipAddress);
    }

    [Fact]
    public async Task RecordLoginAttempt_RedactsExceptionPayload_AndRethrowsTheOriginalFailure()
    {
        var failure = new InvalidOperationException($"{Email} {IpAddress} {_userId}\r\nforged-entry");
        failure.Data["private"] = Email;
        _repository.Setup(repository => repository.CreateAsync(
                It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(false, false, false).RecordLoginAttemptAsync(Request(IpAddress)));

        caught.Should().BeSameAs(failure);
        var error = Logs().Should().ContainSingle(log => log.Level == LogLevel.Error).Which;
        error.Properties["ErrorType"].Should().Be(typeof(InvalidOperationException).FullName);
        AssertPrivateLogs("forged-entry");
    }

    [Theory]
    [InlineData("Private.Person@example.test")]
    [InlineData("203.0.113.42")]
    [InlineData("private-user\r\nforged-entry")]
    public async Task RecordSuspiciousActivity_RedactsDiagnosticIdentity_AndForwardsTheOriginalEvent(string identifier)
    {
        var activity = new SuspiciousActivity
        {
            UserId = _userId,
            Identifier = identifier,
            ActivityType = "BruteForce",
            RiskLevel = RiskLevel.High
        };

        await CreateSut(false, false, false).RecordSuspiciousActivityAsync(activity);

        _siem.Verify(service => service.SendSuspiciousActivityEventAsync(activity,
            It.IsAny<CancellationToken>()), Times.Once);
        activity.UserId.Should().Be(_userId);
        activity.Identifier.Should().Be(identifier);
        var entry = Logs().Should().ContainSingle().Which;
        Assert.IsType<string>(entry.Properties["UserId"]).Should().MatchRegex("^uid:[0-9a-f]{8}$");
        Assert.IsType<string>(entry.Properties["Identifier"]).Should().MatchRegex("^(?:email|ip-network|username):[0-9a-f]{64}$");
        AssertPrivateLogs(identifier, "forged-entry");
    }

    [Fact]
    public async Task AnalyzeLoginAttempt_RedactsProviderLabelsInLogs_AndRetainsTheFullRiskResult()
    {
        var label = $"Learned:{Email}:{IpAddress}:{_userId}\r\nforged-entry";
        _adaptive.Setup(provider => provider.AssessAsync(
                It.IsAny<AuthenticationAttemptContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdaptiveAnomalyAssessment
            {
                Origin = AdaptiveAnomalyOrigin.LearnedBaseline,
                IsLearnedDeviation = true,
                CombinedZScore = 7.4,
                BaselineSampleCount = 42,
                LearnedRiskScoreContribution = 40,
                DeviationLabels = [label]
            });
        var context = Context();

        var result = await CreateSut(false, true, false).AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().Be(40);
        result.RiskLevel.Should().Be(RiskLevel.Medium);
        result.IsAnomalous.Should().BeTrue();
        result.DetectedAnomalies.Should().ContainSingle().Which.Should().Be(label);
        _adaptive.Verify(provider => provider.AssessAsync(context, It.IsAny<CancellationToken>()), Times.Once);
        var feature = Logs().Single(log => log.Properties.ContainsKey("FeatureCount"));
        feature.Properties["FeatureCount"].Should().Be(1);
        var anomaly = Logs().Single(log => log.Properties.ContainsKey("AnomalyCount"));
        anomaly.Properties["AnomalyCount"].Should().Be(1);
        Assert.IsType<string>(anomaly.Properties["UserId"]).Should().MatchRegex("^uid:[0-9a-f]{8}$");
        Assert.IsType<string>(anomaly.Properties["IpAddress"]).Should().MatchRegex("^ip-network:[0-9a-f]{64}$");
        context.Identifier.Should().Be(Email);
        context.IpAddress.Should().Be(IpAddress);
        AssertPrivateLogs(label, "forged-entry");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnalyzeLoginAttempt_RedactsThreatDiagnostics_AndPreservesAuditAndEnforcement(bool enforce)
    {
        _threatIntelligence.Setup(provider => provider.CheckIpAddressAsync(
                IpAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: true, MatchedCidr: "203.0.113.0/24"));
        var context = Context();

        var result = await CreateSut(true, false, enforce).AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().Be(enforce ? 60 : 0);
        result.IsAnomalous.Should().Be(enforce);
        result.DetectedAnomalies.Should().ContainSingle().Which.Should().Be("ThreatIntel:MaliciousIp");
        _audit.Verify(sink => sink.RecordAsync(It.Is<AuthenticationAuditEvent>(audit =>
                audit.UserId == _userId && audit.TenantId == _tenantId && audit.IpAddress == IpAddress
                && audit.ErrorMessage == "ThreatIntel:MaliciousIp"
                && audit.AssessedRiskLevel == (enforce ? RiskLevel.High : RiskLevel.Medium)),
            It.IsAny<CancellationToken>()), Times.Once);
        var match = Logs().Single(log => log.Properties.ContainsKey("Signal"));
        match.Properties["Signal"].Should().Be("ThreatIntel:MaliciousIp");
        Assert.IsType<string>(match.Properties["UserId"]).Should().MatchRegex("^uid:[0-9a-f]{8}$");
        Assert.IsType<string>(match.Properties["IpAddress"]).Should().MatchRegex("^ip-network:[0-9a-f]{64}$");
        AssertPrivateLogs("203.0.113.0/24");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnalyzeLoginAttempt_RedactsProviderAndAuditFailures_AndPreservesRecovery(bool auditFails)
    {
        var failure = new InvalidOperationException($"{Email} {IpAddress} {_userId}\r\nforged-entry");
        if (auditFails)
        {
            _threatIntelligence.Setup(provider => provider.CheckIpAddressAsync(
                    IpAddress, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ThreatIntelligenceIpResult(IsMatch: true));
            _audit.Setup(sink => sink.RecordAsync(It.IsAny<AuthenticationAuditEvent>(),
                It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        }
        else
        {
            _threatIntelligence.Setup(provider => provider.CheckIpAddressAsync(
                IpAddress, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        }

        var result = await CreateSut(true, false, true).AnalyzeLoginAttemptAsync(Context());

        result.RiskScore.Should().Be(auditFails ? 60 : 0);
        result.IsAnomalous.Should().Be(auditFails);
        result.DetectedAnomalies.Should().HaveCount(auditFails ? 1 : 0);
        var failureLog = Logs().Should().ContainSingle(log => log.Properties.ContainsKey("ErrorType")).Which;
        failureLog.Level.Should().Be(auditFails ? LogLevel.Error : LogLevel.Warning);
        failureLog.Properties["ErrorType"].Should().Be(typeof(InvalidOperationException).FullName);
        AssertPrivateLogs("forged-entry");
    }

    [Fact]
    public async Task AnalyzeLoginAttempt_RedactsRepositoryFailure_AndRethrowsTheOriginalException()
    {
        var failure = new InvalidOperationException($"{Email} {IpAddress} {_userId}\r\nforged-entry");
        _repository.Setup(repository => repository.GetRecentAttemptsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(false, false, false).AnalyzeLoginAttemptAsync(Context()));

        caught.Should().BeSameAs(failure);
        Logs().Should().ContainSingle().Which.Properties["ErrorType"].Should()
            .Be(typeof(InvalidOperationException).FullName);
        AssertPrivateLogs("forged-entry");
    }

    private IReadOnlyList<CapturedLog> Logs() => _logger.Invocations
        .Where(call => call.Method.Name == nameof(ILogger.Log))
        .Select(call => new CapturedLog((LogLevel)call.Arguments[0],
            call.Arguments[2].ToString() ?? string.Empty, (Exception?)call.Arguments[3],
            Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(call.Arguments[2])
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)))
        .ToArray();

    private void AssertPrivateLogs(params string[] additionalSensitiveValues)
    {
        var sensitive = new[] { Email, IpAddress, _userId.ToString("D"), _userId.ToString("N"),
            _tenantId.ToString("D"), _tenantId.ToString("N") }.Concat(additionalSensitiveValues).ToArray();
        var logs = Logs();
        logs.Should().NotBeEmpty();
        foreach (var log in logs)
        {
            log.Exception.Should().BeNull("exception messages and data can contain private values");
            log.Message.Should().NotMatchRegex("[\\x00-\\x1F\\x7F-\\x9F\\u2028\\u2029]");
            var properties = string.Join(" ", log.Properties.Select(pair => pair.Value?.ToString()));
            foreach (var value in sensitive)
            {
                log.Message.Should().NotContainEquivalentOf(value);
                properties.Should().NotContainEquivalentOf(value);
            }
        }
    }

    private sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);
}
