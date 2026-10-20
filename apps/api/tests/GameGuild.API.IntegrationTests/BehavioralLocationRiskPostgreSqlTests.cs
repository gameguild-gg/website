using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class BehavioralLocationRiskPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    private const string SourceAddress = "198.51.100.72";
    private const string Agent = "Synthetic persisted behavioral fixture";

    [Theory]
    [InlineData("United States", "US", "New York", 0)]
    [InlineData("United States", null, "New York", 0)]
    [InlineData(null, "US", "New York", 0)]
    [InlineData("United States", "US", "Boston", 25)]
    [InlineData("Brazil", "BR", "New York", 25)]
    public async Task ProductionServicesScoreActualPersistedWriterLocations(string? country, string? code, string city, int score)
    {
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGeolocationProvider>();
            services.AddSingleton<IGeolocationProvider>(new ControlledGeolocation());
        }));
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create(marker + "@example.test", "Synthetic persisted location account");
        user.Username = marker;
        await using (var writerScope = factory.Services.CreateAsyncScope())
        {
            var db = writerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(user); await db.SaveChangesAsync();
            var recorder = writerScope.ServiceProvider.GetRequiredService<IAuthAttemptService>();
            Assert.IsType<AuthAttemptService>(recorder);
            for (var hour = 0; hour < 24; hour++)
            {
                await recorder.RecordSuccessfulAttemptAsync(user.Email, user.Id, SourceAddress, Agent, TimeSpan.Zero);
            }
            var history = await db.Set<AuthenticationAttempt>().Where(attempt => attempt.UserId == user.Id).ToListAsync();
            Assert.Equal(24, history.Count);
            for (var hour = 0; hour < history.Count; hour++)
            {
                // Isolate location scoring from the wall clock while retaining actual persisted writer data.
                history[hour].AttemptedAt = DateTime.UtcNow.Date.AddDays(-1).AddHours(hour);
                Assert.Equal("US", history[hour].Country);
                Assert.Equal("New York", history[hour].City);
                Assert.Equal("New York, United States", history[hour].Location);
            }
            await db.SaveChangesAsync();
        }
        await using var readScope = factory.Services.CreateAsyncScope();
        var repository = readScope.ServiceProvider.GetRequiredService<IAuthenticationAttemptRepository>();
        Assert.IsType<AuthenticationAttemptRepository>(repository);
        Assert.Equal(24, (await repository.GetRecentAttemptsAsync(user.Id, DateTime.UtcNow.AddDays(-30), 1000)).Count);
        var service = readScope.ServiceProvider.GetRequiredService<IBehavioralAnalysisService>();
        Assert.IsType<BehavioralAnalysisService>(service);
        var facade = readScope.ServiceProvider.GetRequiredService<IAuthenticationAnomalyDetectionService>();
        Assert.IsType<AuthenticationAnomalyDetectionService>(facade);
        var attemptContext = new AuthenticationAttemptContext
        {
            UserId = user.Id, IpAddress = SourceAddress, UserAgent = Agent,
            Location = new LocationInfo { Country = country, CountryCode = code, City = city }
        };
        var result = await facade.AnalyzeBehavioralPatternsAsync(user.Id, attemptContext);
        Assert.Equal(score, result.RiskScore);
        Assert.Equal(score > 0, result.DetectedAnomalies.Contains("Authentication from unfamiliar location"));
        Assert.Equal(score == 0, result.MatchesTypicalBehavior);
        Assert.Equal(score == 0 ? RiskLevel.Low : RiskLevel.Medium, result.RiskLevel);
        var unseenUser = Guid.NewGuid();
        attemptContext.UserId = unseenUser;
        var isolated = await facade.AnalyzeBehavioralPatternsAsync(unseenUser, attemptContext);
        Assert.Equal(0, isolated.RiskScore);
        Assert.Equal(0.2, isolated.Confidence);
        Assert.Contains("Insufficient historical data for behavioral analysis", isolated.DetectedAnomalies);
    }

    private sealed class ControlledGeolocation : IGeolocationProvider
    {
        public Task<GeolocationResult?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<GeolocationResult?>(ipAddress == SourceAddress
                ? new GeolocationResult { Country = "United States", CountryCode = "US", City = "New York" }
                : null);
        }
    }
}
