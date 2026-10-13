using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Session enrichment (issue #278): device/location population on creation and refresh,
///     session renaming, trusted-device listing, and the geo-aware security analysis.
/// </summary>
public class SessionEnrichmentTests
{
    private readonly Mock<ILogger<SessionManagementService>> _loggerMock = new();
    private readonly Mock<IUserSessionRepository> _sessionRepositoryMock = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedDeviceRepositoryMock = new();
    private readonly Mock<IRefreshTokenLineageRepository> _tokenLineageRepositoryMock = new();
    private readonly Mock<IGeolocationProvider> _geolocationMock = new();

    public SessionEnrichmentTests()
    {
        _tokenLineageRepositoryMock.Setup(repository => repository.BindSessionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private SessionManagementService CreateSut(SessionOptions? options = null, IGeolocationProvider? geolocation = null) =>
        new(_loggerMock.Object,
            _sessionRepositoryMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _tokenLineageRepositoryMock.Object,
            options,
            userAgentParser: UserAgentParser.Shared,
            geolocationProvider: geolocation);

    private void AllowCreate()
    {
        _sessionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession s, CancellationToken _) => s);
    }

    // ── DeviceInfo population on creation ───────────────────────────────

    [Fact]
    public async Task CreateSessionAsync_ParsesUserAgentIntoDeviceInfo()
    {
        AllowCreate();
        var sut = CreateSut();

        var session = await sut.CreateSessionAsync(
            Guid.NewGuid(), "203.0.113.10",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        session.DeviceInfo.Should().NotBeNullOrWhiteSpace();
        var device = SessionEnrichmentJson.DeserializeDeviceInfo(session.DeviceInfo);
        device.Should().NotBeNull();
        device!.Browser.Should().Be("Chrome");
        device.OperatingSystem.Should().Be("Windows");
        device.DeviceType.Should().Be("Desktop");
        device.DeviceName.Should().Be("Chrome 120 on Windows");
        device.IpAddress.Should().Be("203.0.113.10");
        device.UserAgent.Should().Contain("Chrome/120");
        device.Fingerprint.Should().Be(session.DeviceFingerprint);
    }

    [Fact]
    public async Task CreateSessionAsync_WithoutGeolocationProvider_LeavesLocationEmpty()
    {
        AllowCreate();
        var sut = CreateSut();

        var session = await sut.CreateSessionAsync(Guid.NewGuid(), "203.0.113.10", "Mozilla/5.0");

        session.Location.Should().BeNull();
        session.DeviceInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSessionAsync_PopulatesLocation_FromGeolocationProvider()
    {
        AllowCreate();
        _geolocationMock
            .Setup(provider => provider.ResolveAsync("203.0.113.10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeolocationResult
            {
                Country = "Brazil", CountryCode = "BR", City = "Sao Paulo",
                Latitude = -23.55, Longitude = -46.63, Timezone = "America/Sao_Paulo"
            });
        var sut = CreateSut(geolocation: _geolocationMock.Object);

        var session = await sut.CreateSessionAsync(Guid.NewGuid(), "203.0.113.10", "Mozilla/5.0");

        session.Location.Should().NotBeNull();
        var location = SessionEnrichmentJson.DeserializeLocation(session.Location);
        location.Should().NotBeNull();
        location!.Country.Should().Be("Brazil");
        location.City.Should().Be("Sao Paulo");
        location.Latitude.Should().Be(-23.55);
        location.IpAddress.Should().Be("203.0.113.10");
    }

    [Fact]
    public async Task CreateSessionAsync_SwallowsGeolocationProviderFailure()
    {
        AllowCreate();
        _geolocationMock
            .Setup(provider => provider.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("geo db locked"));
        var sut = CreateSut(geolocation: _geolocationMock.Object);

        var session = await sut.CreateSessionAsync(Guid.NewGuid(), "203.0.113.10", "Mozilla/5.0");

        session.Location.Should().BeNull();
        session.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSessionAsync_LocationTrackingDisabled_DoesNotCallProvider()
    {
        AllowCreate();
        var sut = CreateSut(new SessionOptions { EnableLocationTracking = false }, _geolocationMock.Object);

        await sut.CreateSessionAsync(Guid.NewGuid(), "203.0.113.10", "Mozilla/5.0");

        _geolocationMock.Verify(provider => provider.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── DeviceInfo/Location population on refresh ───────────────────────

    [Fact]
    public async Task RefreshSessionAsync_NewUserAgent_ReparsesDeviceInfoAndPreservesCustomName()
    {
        var sessionId = Guid.NewGuid();
        var originalAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0 Safari/537.36";
        var session = ActiveSession(sessionId, originalAgent);
        session.DeviceInfo = SessionEnrichmentJson.SerializeDeviceInfo(new DeviceInfo
        {
            Fingerprint = "fp", DeviceName = "My work laptop", Browser = "Chrome", OperatingSystem = "Windows"
        });
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _sessionRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession s, CancellationToken _) => s);

        var newAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_2) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var result = await CreateSut().RefreshSessionAsync(sessionId, "hash", SystemClock.UtcNow.AddDays(1), "203.0.113.99", newAgent);

        result.Should().BeTrue();
        session.UserAgent.Should().Be(newAgent);
        var device = SessionEnrichmentJson.DeserializeDeviceInfo(session.DeviceInfo);
        device!.OperatingSystem.Should().Be("macOS");
        device.Browser.Should().Be("Chrome");
        device.Fingerprint.Should().Be("fp");
        // The user-assigned name survives rotation; only structural fields are re-parsed.
        device.DeviceName.Should().Be("My work laptop");
    }

    [Fact]
    public async Task RefreshSessionAsync_ChangedIp_ResolvesNewLocation()
    {
        var sessionId = Guid.NewGuid();
        var session = ActiveSession(sessionId, "Mozilla/5.0");
        session.IpAddress = "198.51.100.7";
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _sessionRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession s, CancellationToken _) => s);
        _geolocationMock
            .Setup(provider => provider.ResolveAsync("203.0.113.200", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeolocationResult { Country = "Portugal", CountryCode = "PT", City = "Lisbon" });

        var result = await CreateSut(geolocation: _geolocationMock.Object)
            .RefreshSessionAsync(sessionId, "hash", SystemClock.UtcNow.AddDays(1), "203.0.113.200", userAgent: null);

        result.Should().BeTrue();
        session.IpAddress.Should().Be("203.0.113.200");
        var location = SessionEnrichmentJson.DeserializeLocation(session.Location);
        location!.Country.Should().Be("Portugal");
    }

    [Fact]
    public async Task RefreshSessionAsync_SameContext_LeavesEnrichmentUntouched()
    {
        var sessionId = Guid.NewGuid();
        var session = ActiveSession(sessionId, "Mozilla/5.0");
        session.IpAddress = "198.51.100.7";
        session.DeviceInfo = "{\"DeviceName\":\"stable\"}";
        session.Location = null;
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _sessionRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession s, CancellationToken _) => s);

        await CreateSut(geolocation: _geolocationMock.Object)
            .RefreshSessionAsync(sessionId, "hash", SystemClock.UtcNow.AddDays(1), ipAddress: null, userAgent: null);

        session.DeviceInfo.Should().Be("{\"DeviceName\":\"stable\"}");
        _geolocationMock.Verify(provider => provider.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Session custom naming ───────────────────────────────────────────

    [Fact]
    public async Task RenameSessionAsync_StoresCustomNameOnTheCallerSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = ActiveSession(sessionId, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0 Safari/537.36");
        session.UserId = userId;
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _sessionRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession s, CancellationToken _) => s);

        var result = await CreateSut().RenameSessionAsync(userId, sessionId, "  Family desktop  ");

        result.Should().BeTrue();
        var device = SessionEnrichmentJson.DeserializeDeviceInfo(session.DeviceInfo);
        device!.DeviceName.Should().Be("Family desktop");
        device.Browser.Should().Be("Chrome");
    }

    [Fact]
    public async Task RenameSessionAsync_RejectsAnotherUsersSession()
    {
        var sessionId = Guid.NewGuid();
        var session = ActiveSession(sessionId, "Mozilla/5.0");
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var result = await CreateSut().RenameSessionAsync(Guid.NewGuid(), sessionId, "not mine");

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("over-sixty-four-characters-over-sixty-four-characters-over-sixty-four-characters!!")]
    public async Task RenameSessionAsync_RejectsInvalidNames(string name)
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = ActiveSession(sessionId, "Mozilla/5.0");
        session.UserId = userId;
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var result = await CreateSut().RenameSessionAsync(userId, sessionId, name);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RenameSessionAsync_RejectsInactiveSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = ActiveSession(sessionId, "Mozilla/5.0");
        session.UserId = userId;
        session.IsActive = false;
        _sessionRepositoryMock.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var result = await CreateSut().RenameSessionAsync(userId, sessionId, "old session");

        result.Should().BeFalse();
    }

    // ── Trusted device listing ──────────────────────────────────────────

    [Fact]
    public async Task GetTrustedDevicesAsync_FiltersInactiveAndExpiredRows()
    {
        var userId = Guid.NewGuid();
        _trustedDeviceRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TrustedDevice { Id = Guid.NewGuid(), UserId = userId, IsActive = true, ExpiresAt = SystemClock.UtcNow.AddDays(1) },
                new TrustedDevice { Id = Guid.NewGuid(), UserId = userId, IsActive = true, ExpiresAt = SystemClock.UtcNow.AddDays(-1) },
                new TrustedDevice { Id = Guid.NewGuid(), UserId = userId, IsActive = false, ExpiresAt = SystemClock.UtcNow.AddDays(1) },
                new TrustedDevice { Id = Guid.NewGuid(), UserId = userId, IsActive = true }
            ]);

        var devices = await CreateSut().GetTrustedDevicesAsync(userId);

        devices.Should().HaveCount(2);
        devices.Should().OnlyContain(d => d.IsActive && (d.ExpiresAt is null || d.ExpiresAt > SystemClock.UtcNow));
    }

    // ── Geo-aware security analysis ─────────────────────────────────────

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_WithoutLocations_KeepsCountBasedAnalysis()
    {
        var userId = Guid.NewGuid();
        _sessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserSession>());

        var analysis = await CreateSut().AnalyzeSessionSecurityAsync(userId, "203.0.113.10", "Mozilla/5.0");

        analysis.RiskLevel.Should().Be(RiskLevel.Low);
        analysis.RiskScore.Should().Be(0);
        analysis.Metadata["geo_data_available"].Should().Be("false");
        analysis.Metadata["concurrent_locations"].Should().Be("0");
    }

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_FlagsConcurrentLocationsAndAnomalousSessions()
    {
        var userId = Guid.NewGuid();
        var homeSession = ActiveSession(Guid.NewGuid(), "Mozilla/5.0");
        homeSession.UserId = userId;
        homeSession.Location = SessionEnrichmentJson.SerializeLocation(new LocationInfo
        {
            IpAddress = "198.51.100.1", Country = "Brazil", City = "Sao Paulo"
        });
        var awaySession = ActiveSession(Guid.NewGuid(), "Mozilla/5.0");
        awaySession.UserId = userId;
        awaySession.CreatedAt = SystemClock.UtcNow.AddHours(-2);
        awaySession.Location = SessionEnrichmentJson.SerializeLocation(new LocationInfo
        {
            IpAddress = "198.51.100.2", Country = "Japan", City = "Tokyo"
        });
        _sessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([homeSession, awaySession]);

        var analysis = await CreateSut().AnalyzeSessionSecurityAsync(userId, "198.51.100.1", "Mozilla/5.0");

        analysis.Metadata["geo_data_available"].Should().Be("true");
        analysis.Metadata["concurrent_locations"].Should().Be("2");
        analysis.RiskFactors.Should().Contain(f => f.StartsWith("Concurrent locations", StringComparison.Ordinal));
        analysis.Metadata["anomalous_session_ids"].Should().Contain(awaySession.Id.ToString());
        analysis.RiskLevel.Should().Be(RiskLevel.Medium);
    }

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_DetectsImpossibleTravelFromCoordinates()
    {
        var userId = Guid.NewGuid();
        var saoPaulo = ActiveSession(Guid.NewGuid(), "Mozilla/5.0");
        saoPaulo.UserId = userId;
        saoPaulo.LastUsedAt = SystemClock.UtcNow.AddMinutes(-10);
        saoPaulo.Location = SessionEnrichmentJson.SerializeLocation(new LocationInfo
        {
            IpAddress = "198.51.100.1", Country = "Brazil", City = "Sao Paulo", Latitude = -23.55, Longitude = -46.63
        });
        var tokyo = ActiveSession(Guid.NewGuid(), "Mozilla/5.0");
        tokyo.UserId = userId;
        tokyo.LastUsedAt = SystemClock.UtcNow;
        tokyo.Location = SessionEnrichmentJson.SerializeLocation(new LocationInfo
        {
            IpAddress = "198.51.100.2", Country = "Japan", City = "Tokyo", Latitude = 35.68, Longitude = 139.69
        });
        _sessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([saoPaulo, tokyo]);

        var analysis = await CreateSut().AnalyzeSessionSecurityAsync(userId, "198.51.100.2", "Mozilla/5.0");

        // ~18,500 km in <=10 minutes is far above the 900 km/h threshold.
        analysis.RiskFactors.Should().Contain(f => f.StartsWith("Impossible travel", StringComparison.Ordinal));
        analysis.RiskLevel.Should().Be(RiskLevel.High);
        analysis.Metadata["max_observed_velocity_kmh"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_SameLocationTwice_DoesNotFlagVelocity()
    {
        var userId = Guid.NewGuid();
        var first = ActiveSession(Guid.NewGuid(), "Mozilla/5.0");
        first.UserId = userId;
        first.LastUsedAt = SystemClock.UtcNow.AddMinutes(-5);
        first.Location = SessionEnrichmentJson.SerializeLocation(new LocationInfo
        {
            IpAddress = "198.51.100.1", Country = "Brazil", City = "Sao Paulo", Latitude = -23.55, Longitude = -46.63
        });
        var second = ActiveSession(Guid.NewGuid(), "Mozilla/5.0");
        second.UserId = userId;
        second.LastUsedAt = SystemClock.UtcNow;
        second.Location = first.Location;
        _sessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);

        var analysis = await CreateSut().AnalyzeSessionSecurityAsync(userId, "198.51.100.1", "Mozilla/5.0");

        analysis.RiskFactors.Should().NotContain(f => f.StartsWith("Impossible travel", StringComparison.Ordinal));
        analysis.Metadata["max_observed_velocity_kmh"].Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeSessionSecurityAsync_UsesRequestLocationWhenProviderResolvesIt()
    {
        var userId = Guid.NewGuid();
        var home = ActiveSession(Guid.NewGuid(), "Mozilla/5.0");
        home.UserId = userId;
        home.LastUsedAt = SystemClock.UtcNow.AddMinutes(-5);
        home.Location = SessionEnrichmentJson.SerializeLocation(new LocationInfo
        {
            IpAddress = "198.51.100.1", Country = "Brazil", City = "Sao Paulo", Latitude = -23.55, Longitude = -46.63
        });
        _sessionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([home]);
        _geolocationMock
            .Setup(provider => provider.ResolveAsync("203.0.113.50", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeolocationResult
            {
                Country = "Australia", City = "Sydney", Latitude = -33.87, Longitude = 151.21
            });

        var analysis = await CreateSut(geolocation: _geolocationMock.Object)
            .AnalyzeSessionSecurityAsync(userId, "203.0.113.50", "Mozilla/5.0");

        analysis.Metadata["geo_data_available"].Should().Be("true");
        analysis.RiskFactors.Should().Contain(f => f.StartsWith("Impossible travel", StringComparison.Ordinal));
    }

    private static UserSession ActiveSession(Guid id, string userAgent) => new()
    {
        Id = id,
        UserId = Guid.NewGuid(),
        RefreshToken = "hash",
        IpAddress = "198.51.100.1",
        UserAgent = userAgent,
        IsActive = true,
        CreatedAt = SystemClock.UtcNow.AddHours(-1),
        LastUsedAt = SystemClock.UtcNow.AddMinutes(-1),
        ExpiresAt = SystemClock.UtcNow.AddDays(1)
    };
}

/// <summary>Haversine/velocity math backing the geo-velocity analysis.</summary>
public class GeoVelocityTests
{
    [Fact]
    public void DistanceKm_SaoPauloToTokyo_IsApproximatelyEighteenThousandKm()
    {
        var distance = GeoVelocity.DistanceKm(-23.55, -46.63, 35.68, 139.69);

        distance.Should().BeInRange(18_000, 19_000);
    }

    [Fact]
    public void SpeedKmh_MissingCoordinates_ReturnsNull()
    {
        var origin = new LocationInfo { Country = "BR" };
        var destination = new LocationInfo { Country = "JP", Latitude = 35.68, Longitude = 139.69 };

        var speed = GeoVelocity.SpeedKmh(origin, destination, TimeSpan.FromMinutes(30));

        speed.Should().BeNull();
    }

    [Fact]
    public void SpeedKmh_IdenticalPoints_ReturnsNull()
    {
        var origin = new LocationInfo { Latitude = -23.55, Longitude = -46.63 };
        var destination = new LocationInfo { Latitude = -23.55, Longitude = -46.63 };

        var speed = GeoVelocity.SpeedKmh(origin, destination, TimeSpan.FromMinutes(30));

        speed.Should().BeNull();
    }

    [Fact]
    public void SpeedKmh_ZeroElapsed_ReturnsNull()
    {
        var origin = new LocationInfo { Latitude = -23.55, Longitude = -46.63 };
        var destination = new LocationInfo { Latitude = 35.68, Longitude = 139.69 };

        var speed = GeoVelocity.SpeedKmh(origin, destination, TimeSpan.Zero);

        speed.Should().BeNull();
    }

    [Fact]
    public void SpeedKmh_ComputesImpliedTravelSpeed()
    {
        var origin = new LocationInfo { Latitude = -23.55, Longitude = -46.63 };
        var destination = new LocationInfo { Latitude = 35.68, Longitude = 139.69 };

        var speed = GeoVelocity.SpeedKmh(origin, destination, TimeSpan.FromMinutes(30));

        // ~18,500 km in 0.5 h → far above the 900 km/h impossible-travel bar.
        speed.Should().BeGreaterThan(30_000);
    }
}
