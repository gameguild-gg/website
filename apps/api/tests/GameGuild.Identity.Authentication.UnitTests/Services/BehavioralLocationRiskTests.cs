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

    private static async Task<BehavioralAnalysisResult> Analyze(string format, string? country, string? city)
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
            Location = country is null && city is null ? null : new LocationInfo { Country = country ?? string.Empty, City = city ?? string.Empty }
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
