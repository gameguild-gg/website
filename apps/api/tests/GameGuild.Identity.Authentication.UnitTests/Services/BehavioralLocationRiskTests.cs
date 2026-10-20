using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class BehavioralLocationRiskTests
{
    private const string UnfamiliarLocation = "Authentication from unfamiliar location";

    [Theory]
    [InlineData("structured", "US", "New York")]
    [InlineData("structured", " us ", " new YORK ")]
    [InlineData("structured", "United States", "New York")]
    [InlineData("structured-name", " united STATES ", "New York")]
    [InlineData("legacy", "US", "NYC")]
    [InlineData("legacy-hyphen", "US", "Winston-Salem")]
    [InlineData("legacy-case", "US", "New York")]
    [InlineData("structured-priority", "US", "New York")]
    [InlineData("structured-space", "United States", "New York")]
    public async Task KnownLocationsDoNotGainRiskFromStorageFormat(string history, string country, string city)
    {
        var result = await Analyze(history, country, city);
        Assert.DoesNotContain(UnfamiliarLocation, result.DetectedAnomalies);
        Assert.Equal(0, result.RiskScore);
        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.True(result.MatchesTypicalBehavior);
    }

    [Theory]
    [InlineData("structured", "GB", "New York")]
    [InlineData("structured", "US", "Boston")]
    [InlineData("structured", "Brazil", "New York")]
    [InlineData("structured-space", "Brazil", "New York")]
    public async Task AComparableDifferentLocationStillContributesRisk(string history, string country, string city)
    {
        var result = await Analyze(history, country, city);
        Assert.Contains(UnfamiliarLocation, result.DetectedAnomalies);
        Assert.Equal(25, result.RiskScore);
        Assert.Equal(RiskLevel.Medium, result.RiskLevel);
        Assert.False(result.MatchesTypicalBehavior);
    }

    [Theory]
    [InlineData("structured", null, "New York")]
    [InlineData("structured", "US", null)]
    [InlineData("structured", null, null)]
    [InlineData("unknown", "US", "New York")]
    [InlineData("incomplete", "US", "New York")]
    [InlineData("ambiguous-display", "US", "New York")]
    [InlineData("incomparable", "United States", "New York")]
    [InlineData("malformed-legacy", "US", "New York")]
    public async Task UnknownOrIncomparableLocationDataDoesNotInventAnAnomaly(string history, string? country, string? city)
    {
        var result = await Analyze(history, country, city);
        Assert.DoesNotContain(UnfamiliarLocation, result.DetectedAnomalies);
        Assert.Equal(0, result.RiskScore);
        Assert.True(result.MatchesTypicalBehavior);
    }

    [Theory]
    [InlineData("structured", "United States", "US", "New York", 0)]
    [InlineData("incomparable", "United States", "US", "New York", 0)]
    [InlineData("structured", null, "US", "New York", 0)]
    [InlineData("legacy", "United States", "US", "NYC", 0)]
    [InlineData("structured", "United Kingdom", "US", "New York", 0)]
    [InlineData("structured", "United States", "GB", "New York", 25)]
    public async Task ExplicitCountryCodesTakePriorityOverDisplayNames(string history, string? country, string code, string city, int score)
    {
        var result = await Analyze(history, country, city, code);
        Assert.Equal(score, result.RiskScore);
        Assert.Equal(score > 0, result.DetectedAnomalies.Contains(UnfamiliarLocation));
    }

    [Theory]
    [InlineData("US", "New York")]
    [InlineData(null, "New York")]
    [InlineData(null, "Washington, D.C.")]
    [InlineData(null, "Winston-Salem")]
    public async Task ActualAuthenticationWriterOutputDoesNotCreateALocationAnomaly(string? code, string city)
    {
        var history = new List<AuthenticationAttempt>();
        var user = Guid.NewGuid();
        var repository = new Mock<IAuthenticationAttemptRepository>(MockBehavior.Strict);
        repository.Setup(port => port.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<AuthenticationAttempt, CancellationToken>((attempt, _) =>
            {
                attempt.AttemptedAt = DateTime.UtcNow.Date.AddDays(-1).AddHours(history.Count);
                history.Add(attempt);
            }).ReturnsAsync((AuthenticationAttempt attempt, CancellationToken _) => attempt);
        repository.Setup(port => port.GetRecentAttemptsAsync(user, It.IsAny<DateTime>(), 1000, It.IsAny<CancellationToken>())).ReturnsAsync(history);
        var geography = new Mock<IGeolocationProvider>(MockBehavior.Strict);
        geography.Setup(provider => provider.ResolveAsync("198.51.100.72", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeolocationResult { Country = "United States", CountryCode = code, City = city });
        var writer = new AuthAttemptService(repository.Object, Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance, geography.Object);
        for (var hour = 0; hour < 24; hour++)
        {
            await writer.RecordSuccessfulAttemptAsync("synthetic-writer@example.test", user, "198.51.100.72", "Synthetic behavioral fixture", TimeSpan.Zero);
        }
        var service = new BehavioralAnalysisService(repository.Object, NullLogger<BehavioralAnalysisService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
        var result = await service.AnalyzeBehavioralPatternsAsync(user, new AuthenticationAttemptContext
        {
            IpAddress = "198.51.100.72", UserAgent = "Synthetic behavioral fixture",
            Location = new LocationInfo { Country = "United States", CountryCode = code, City = city }
        });
        Assert.Equal(0, result.RiskScore);
        Assert.DoesNotContain(UnfamiliarLocation, result.DetectedAnomalies);
        Assert.All(history, attempt =>
        {
            Assert.Equal(code, attempt.Country); Assert.Equal(city, attempt.City);
            Assert.Equal(city + ", United States", attempt.Location);
        });
    }

    private static async Task<BehavioralAnalysisResult> Analyze(string format, string? country, string? city, string? countryCode = null)
    {
        // One observation per hour leaves no hour above the ten-percent threshold. The
        // clock cannot introduce an unrelated time-of-day score into these location cases.
        var history = Enumerable.Range(0, 24).Select(hour => Attempt(format, hour)).ToList();
        var repository = new Mock<IAuthenticationAttemptRepository>(MockBehavior.Strict);
        repository.Setup(port => port.GetRecentAttemptsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);
        var service = new BehavioralAnalysisService(repository.Object, NullLogger<BehavioralAnalysisService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
        var context = new AuthenticationAttemptContext
        {
            IpAddress = "198.51.100.72", UserAgent = "Synthetic behavioral fixture",
            Location = country is null && city is null && countryCode is null ? null :
                new LocationInfo { Country = country, CountryCode = countryCode, City = city }
        };
        var result = await service.AnalyzeBehavioralPatternsAsync(Guid.NewGuid(), context);
        repository.Verify(port => port.GetRecentAttemptsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), 1000, It.IsAny<CancellationToken>()), Times.Once);
        return result;
    }

    private static AuthenticationAttempt Attempt(string format, int hour)
    {
        var attempt = new AuthenticationAttempt
        {
            Email = "synthetic-behavior@example.test", IpAddress = "198.51.100.72", UserAgent = "Synthetic behavioral fixture",
            IsSuccessful = true, AttemptedAt = DateTime.UtcNow.Date.AddDays(-1).AddHours(hour)
        };
        (attempt.Country, attempt.City, attempt.Location) = format switch
        {
            "structured" => ("US", "New York", "New York, United States"),
            "structured-name" => ("United States", "New York", "New York, United States"),
            "structured-priority" => ("US", "New York", "GB-London"),
            "structured-space" => (" us ", " New York ", " New York , United States "),
            "legacy" => (null, null, "US-NYC"),
            "legacy-hyphen" => (null, null, "US-Winston-Salem"),
            "legacy-case" => (null, null, "us-new york"),
            "unknown" => (null, null, null),
            "incomplete" => ("US", null, "United States"),
            "ambiguous-display" => (null, null, "New York, United States"),
            "incomparable" => ("US", "New York", null),
            "malformed-legacy" => (null, null, "US-"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        return attempt;
    }
}
