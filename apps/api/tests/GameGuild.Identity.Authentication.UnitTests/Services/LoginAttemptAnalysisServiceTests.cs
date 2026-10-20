using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
/// Concrete subclass of the abstract CreateAuthenticationAttemptRequest for testing
/// </summary>
internal class TestCreateAuthenticationAttemptRequest : CreateAuthenticationAttemptRequest { }

public class LoginAttemptAnalysisServiceTests
{
    private readonly Mock<IAuthenticationAttemptRepository> _attemptRepoMock = new();
    private readonly Mock<IThreatDetectionService> _threatDetectionMock = new();
    private readonly Mock<ISiemIntegrationService> _siemServiceMock = new();
    private readonly IConfiguration _configuration;
    private readonly LoginAttemptAnalysisService _sut;

    public LoginAttemptAnalysisServiceTests()
    {
        var configData = new Dictionary<string, string?>
        {
            { "Authentication:Anomaly:SuspiciousThreshold", "3" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new LoginAttemptAnalysisService(
            _attemptRepoMock.Object,
            _threatDetectionMock.Object,
            NullLogger<LoginAttemptAnalysisService>.Instance,
            _configuration,
            _siemServiceMock.Object
        );
    }

    // ── RecordSuspiciousActivityAsync ──────────────────────────

    [Fact]
    public async Task RecordSuspiciousActivityAsync_SendsToSiemService()
    {
        var activity = new SuspiciousActivity
        {
            ActivityType = "BruteForce",
            UserId = Guid.NewGuid(),
            Identifier = "user@test.com",
            RiskLevel = RiskLevel.High
        };

        await _sut.RecordSuspiciousActivityAsync(activity);

        _siemServiceMock.Verify(
            x => x.SendSuspiciousActivityEventAsync(activity, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── RecordLoginAttemptAsync ────────────────────────────────

    [Fact]
    public async Task RecordLoginAttemptAsync_PersistsAttemptToRepository()
    {
        var request = new TestCreateAuthenticationAttemptRequest
        {
            Email = "test@example.com",
            IpAddress = "192.168.1.1",
            IsSuccessful = true,
            UserAgent = "TestBrowser/1.0 (Windows NT 10.0)"
        };

        _attemptRepoMock
            .Setup(x => x.GetFailedAttemptsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt>());

        _attemptRepoMock
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt a, CancellationToken _) => a);

        var result = await _sut.RecordLoginAttemptAsync(request);

        _attemptRepoMock.Verify(
            x => x.CreateAsync(It.Is<AuthenticationAttempt>(a => a.Email == "test@example.com"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordLoginAttemptAsync_ReturnsAnalysis()
    {
        var request = new TestCreateAuthenticationAttemptRequest
        {
            Email = "test@example.com",
            IpAddress = "192.168.1.1",
            IsSuccessful = true,
            UserAgent = "TestBrowser/1.0 (Windows NT 10.0)"
        };

        _attemptRepoMock
            .Setup(x => x.GetFailedAttemptsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt>());

        _attemptRepoMock
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt a, CancellationToken _) => a);

        var result = await _sut.RecordLoginAttemptAsync(request);

        result.Should().NotBeNull();
        result.RiskScore.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RecordLoginAttemptAsync_WithMultipleFailedIpAttempts_FlagsSuspicious()
    {
        var request = new TestCreateAuthenticationAttemptRequest
        {
            Email = "test@example.com",
            IpAddress = "10.0.0.1",
            IsSuccessful = false,
            FailureReason = "InvalidCredentials",
            UserAgent = "TestBrowser/1.0 (Windows NT 10.0)"
        };

        // Simulate 3+ failed attempts from same IP
        var failedAttempts = Enumerable.Range(0, 4).Select(_ => new AuthenticationAttempt
        {
            Email = "test@example.com",
            IpAddress = "10.0.0.1",
            IsSuccessful = false,
            AttemptedAt = DateTime.UtcNow.AddMinutes(-10)
        }).ToList();

        _attemptRepoMock
            .Setup(x => x.GetFailedAttemptsAsync("test@example.com", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedAttempts);

        _attemptRepoMock
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt a, CancellationToken _) => a);

        var result = await _sut.RecordLoginAttemptAsync(request);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(20);
        result.RiskFactors.Should().Contain(f => f.Contains("Multiple failed attempts from IP"));
    }

    [Fact]
    public async Task RecordLoginAttemptAsync_FastAttempt_IncreasesRiskScore()
    {
        var request = new TestCreateAuthenticationAttemptRequest
        {
            Email = "test@example.com",
            IpAddress = "192.168.1.1",
            IsSuccessful = false,
            ProcessingTime = TimeSpan.FromMilliseconds(10), // abnormally fast
            UserAgent = "TestBrowser/1.0 (Windows NT 10.0)"
        };

        _attemptRepoMock
            .Setup(x => x.GetFailedAttemptsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt>());

        _attemptRepoMock
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt a, CancellationToken _) => a);

        var result = await _sut.RecordLoginAttemptAsync(request);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(15);
        result.RiskFactors.Should().Contain(f => f.Contains("fast"));
    }

    [Fact]
    public async Task RecordLoginAttemptAsync_MissingUserAgent_IncreasesRiskScore()
    {
        var request = new TestCreateAuthenticationAttemptRequest
        {
            Email = "test@example.com",
            IpAddress = "192.168.1.1",
            IsSuccessful = false,
            UserAgent = "" // empty
        };

        _attemptRepoMock
            .Setup(x => x.GetFailedAttemptsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt>());

        _attemptRepoMock
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt a, CancellationToken _) => a);

        var result = await _sut.RecordLoginAttemptAsync(request);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(10);
        result.RiskFactors.Should().Contain(f => f.Contains("user agent"));
    }

    [Fact]
    public async Task RecordLoginAttemptAsync_RepositoryThrows_PropagatesException()
    {
        var request = new TestCreateAuthenticationAttemptRequest
        {
            Email = "test@example.com",
            IpAddress = "192.168.1.1"
        };

        _attemptRepoMock
            .Setup(x => x.GetFailedAttemptsAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RecordLoginAttemptAsync(request));
    }

    // ── AnalyzeLoginAttemptAsync (context-based analysis) ──────

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_NoUserId_NoAnomalies_ReturnsLowRisk()
    {
        var context = new AuthenticationAttemptContext
        {
            UserId = null,
            IpAddress = "192.168.1.1",
            UserAgent = "TestBrowser/1.0",
            AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc) // Wednesday afternoon
        };

        _threatDetectionMock
            .Setup(x => x.DetectBruteForceAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskLevel.Should().Be(RiskLevel.Low);
        result.IsAnomalous.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_FirstAttemptOrLongAbsence_IncreasesRisk()
    {
        var userId = Guid.NewGuid();
        var context = new AuthenticationAttemptContext
        {
            UserId = userId,
            IpAddress = "192.168.1.1",
            UserAgent = "TestBrowser/1.0",
            AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
        };

        _attemptRepoMock
            .Setup(x => x.GetRecentAttemptsAsync(userId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt>()); // empty = first attempt

        _attemptRepoMock
            .Setup(x => x.GetLastSuccessfulAttemptAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationAttempt?)null);

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(10);
        result.DetectedAnomalies.Should().Contain("FirstAttemptOrLongAbsence");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_IpAddressChange_IncreasesRisk()
    {
        var userId = Guid.NewGuid();
        var context = new AuthenticationAttemptContext
        {
            UserId = userId,
            IpAddress = "10.0.0.2",
            UserAgent = "TestBrowser/1.0",
            AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
        };

        _attemptRepoMock
            .Setup(x => x.GetRecentAttemptsAsync(userId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt> { new() { IpAddress = "10.0.0.1" } });

        _attemptRepoMock
            .Setup(x => x.GetLastSuccessfulAttemptAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationAttempt { IpAddress = "10.0.0.1", UserAgent = "TestBrowser/1.0", AttemptedAt = DateTime.UtcNow.AddHours(-2) });

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(20);
        result.DetectedAnomalies.Should().Contain("IpAddressChange");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_UserAgentChange_IncreasesRisk()
    {
        var userId = Guid.NewGuid();
        var context = new AuthenticationAttemptContext
        {
            UserId = userId,
            IpAddress = "10.0.0.1",
            UserAgent = "NewBrowser/2.0",
            AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
        };

        _attemptRepoMock
            .Setup(x => x.GetRecentAttemptsAsync(userId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt> { new() { IpAddress = "10.0.0.1" } });

        _attemptRepoMock
            .Setup(x => x.GetLastSuccessfulAttemptAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationAttempt { IpAddress = "10.0.0.1", UserAgent = "OldBrowser/1.0", AttemptedAt = DateTime.UtcNow.AddHours(-1) });

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(15);
        result.DetectedAnomalies.Should().Contain("UserAgentChange");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_BruteForceDetected_IncreasesRisk()
    {
        var context = new AuthenticationAttemptContext
        {
            UserId = null,
            Identifier = "attacker@evil.com",
            IpAddress = "10.0.0.1",
            UserAgent = "Bot/1.0",
            AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
        };

        _threatDetectionMock
            .Setup(x => x.DetectBruteForceAsync("attacker@evil.com", It.IsAny<int>()))
            .ReturnsAsync(true);

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(40);
        result.DetectedAnomalies.Should().Contain("BruteForceDetected");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_WeekendAccess_IncreasesRisk()
    {
        var context = new AuthenticationAttemptContext
        {
            UserId = null,
            IpAddress = "10.0.0.1",
            UserAgent = "TestBrowser/1.0",
            AttemptedAt = new DateTime(2025, 1, 18, 14, 0, 0, DateTimeKind.Utc) // Saturday
        };

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(5);
        result.DetectedAnomalies.Should().Contain("UnusualTimeOfDay");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_EarlyMorningAccess_IncreasesRisk()
    {
        var context = new AuthenticationAttemptContext
        {
            UserId = null,
            IpAddress = "10.0.0.1",
            UserAgent = "TestBrowser/1.0",
            AttemptedAt = new DateTime(2025, 1, 15, 3, 0, 0, DateTimeKind.Utc) // 3 AM
        };

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(5);
        result.DetectedAnomalies.Should().Contain("UnusualTimeOfDay");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_DeviceFingerprintChange_IncreasesRisk()
    {
        var userId = Guid.NewGuid();
        var context = new AuthenticationAttemptContext
        {
            UserId = userId,
            IpAddress = "10.0.0.1",
            UserAgent = "TestBrowser/1.0",
            DeviceFingerprint = "new-fingerprint",
            AttemptedAt = new DateTime(2025, 1, 15, 14, 0, 0, DateTimeKind.Utc)
        };

        _attemptRepoMock
            .Setup(x => x.GetRecentAttemptsAsync(userId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt> { new() { IpAddress = "10.0.0.1" } });

        _attemptRepoMock
            .Setup(x => x.GetLastSuccessfulAttemptAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationAttempt
            {
                IpAddress = "10.0.0.1",
                UserAgent = "TestBrowser/1.0",
                DeviceFingerprint = "old-fingerprint",
                AttemptedAt = DateTime.UtcNow.AddHours(-1)
            });

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(15);
        result.DetectedAnomalies.Should().Contain("DeviceFingerprintChange");
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_ImpossibleTravel_IncreasesRisk()
    {
        var userId = Guid.NewGuid();
        var lastAttemptTime = DateTime.UtcNow.AddMinutes(-30);
        var context = new AuthenticationAttemptContext
        {
            UserId = userId,
            IpAddress = "10.0.0.2",
            UserAgent = "TestBrowser/1.0",
            Location = new LocationInfo { Country = "Brazil", City = "Sao Paulo" },
            AttemptedAt = DateTime.UtcNow
        };

        _attemptRepoMock
            .Setup(x => x.GetRecentAttemptsAsync(userId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt> { new() { IpAddress = "10.0.0.1" } });

        _attemptRepoMock
            .Setup(x => x.GetLastSuccessfulAttemptAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationAttempt
            {
                IpAddress = "10.0.0.1",
                UserAgent = "TestBrowser/1.0",
                Country = "JP",
                City = "Tokyo",
                Location = "Tokyo, Japan",
                AttemptedAt = lastAttemptTime
            });

        _threatDetectionMock
            .Setup(x => x.DetectImpossibleTravelAsync(userId,
                It.IsAny<LocationInfo>(),
                It.IsAny<LocationInfo>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync(true);

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.RiskScore.Should().BeGreaterThanOrEqualTo(50);
        result.DetectedAnomalies.Should().Contain("ImpossibleTravel");
        _threatDetectionMock.Verify(x => x.DetectImpossibleTravelAsync(userId,
            It.IsAny<LocationInfo>(),
            It.Is<LocationInfo>(previous => previous.CountryCode == "JP" && previous.City == "Tokyo"),
            It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_HighRiskScore_MarksAnomalous()
    {
        var context = new AuthenticationAttemptContext
        {
            UserId = null,
            Identifier = "brute@evil.com",
            IpAddress = "10.0.0.1",
            UserAgent = "Bot/1.0",
            AttemptedAt = new DateTime(2025, 1, 18, 3, 0, 0, DateTimeKind.Utc) // Saturday 3AM
        };

        _threatDetectionMock
            .Setup(x => x.DetectBruteForceAsync("brute@evil.com", It.IsAny<int>()))
            .ReturnsAsync(true); // +40

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        result.IsAnomalous.Should().BeTrue();
        result.RiskLevel.Should().BeOneOf(RiskLevel.Medium, RiskLevel.High, RiskLevel.Critical);
    }

    [Fact]
    public async Task AnalyzeLoginAttemptAsync_CriticalRiskScore_ReturnsCriticalLevel()
    {
        var userId = Guid.NewGuid();
        var context = new AuthenticationAttemptContext
        {
            UserId = userId,
            Identifier = "brute@evil.com",
            IpAddress = "10.0.0.2",
            UserAgent = "DifferentBrowser/2.0",
            DeviceFingerprint = "new-fp",
            Location = new LocationInfo { Country = "Australia", City = "Sydney" },
            AttemptedAt = new DateTime(2025, 1, 18, 3, 0, 0, DateTimeKind.Utc) // Saturday 3AM
        };

        _attemptRepoMock
            .Setup(x => x.GetRecentAttemptsAsync(userId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuthenticationAttempt>()); // first attempt +10

        _attemptRepoMock
            .Setup(x => x.GetLastSuccessfulAttemptAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationAttempt
            {
                IpAddress = "10.0.0.1", // ip change +20
                UserAgent = "OldBrowser/1.0", // ua change +15
                DeviceFingerprint = "old-fp", // fp change +15
                Country = "JP",
                City = "Tokyo",
                Location = "Tokyo, Japan",
                AttemptedAt = DateTime.UtcNow.AddMinutes(-10)
            });

        _threatDetectionMock
            .Setup(x => x.DetectBruteForceAsync("brute@evil.com", It.IsAny<int>()))
            .ReturnsAsync(true); // +40

        _threatDetectionMock
            .Setup(x => x.DetectImpossibleTravelAsync(userId, It.IsAny<LocationInfo>(), It.IsAny<LocationInfo>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(true); // +50

        var result = await _sut.AnalyzeLoginAttemptAsync(context);

        // 10+20+15+15+40+50+5 = 155 => Critical
        result.RiskLevel.Should().Be(RiskLevel.Critical);
        result.IsAnomalous.Should().BeTrue();
    }
}
