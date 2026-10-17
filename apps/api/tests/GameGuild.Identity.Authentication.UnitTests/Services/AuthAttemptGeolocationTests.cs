using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Login-forensics geolocation enrichment tests (issue #242): resolved country/city lands on
///     the forensics record; unresolvable, missing or throwing providers stay Unknown without
///     failing the pipeline.
/// </summary>
public sealed class AuthAttemptGeolocationTests
{
    private sealed class FakeGeolocationProvider(string? country, string? countryCode, string? city) : IGeolocationProvider
    {
        public Task<GeolocationResult?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(countryCode is null
                ? null
                : new GeolocationResult { Country = country, CountryCode = countryCode, City = city });
        }
    }

    private sealed class ThrowingGeolocationProvider : IGeolocationProvider
    {
        public Task<GeolocationResult?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("geo db locked");
        }
    }

    private sealed class CapturingRepository : IAuthenticationAttemptRepository
    {
        public AuthenticationAttempt? Last { get; private set; }

        public Task<AuthenticationAttempt> CreateAsync(AuthenticationAttempt attempt, CancellationToken cancellationToken = default)
        {
            Last = attempt;
            return Task.FromResult(attempt);
        }

        public Task<AuthenticationAttempt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<AuthenticationAttempt?>(null);

        public Task<List<AuthenticationAttempt>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult(new List<AuthenticationAttempt>());

        public Task<List<AuthenticationAttempt>> GetFailedAttemptsAsync(string identifier, DateTime since, CancellationToken cancellationToken = default) => Task.FromResult(new List<AuthenticationAttempt>());

        public Task<List<AuthenticationAttempt>> GetSuspiciousAttemptsAsync(DateTime since, CancellationToken cancellationToken = default) => Task.FromResult(new List<AuthenticationAttempt>());

        public Task<AuthenticationAttempt?> GetLastSuccessfulAttemptAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<AuthenticationAttempt?>(null);

        public Task<List<AuthenticationAttempt>> GetRecentAttemptsAsync(Guid userId, DateTime since, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new List<AuthenticationAttempt>());

        public Task<List<AuthenticationAttempt>> GetRecentAttemptsByIpAsync(string ipAddress, DateTime since, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult(new List<AuthenticationAttempt>());

        public Task<(int TotalAttempts, int SuccessfulAttempts, int FailedAttempts)> GetUserStatisticsAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default) => Task.FromResult((0, 0, 0));

        public Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static AuthAttemptService CreateService(IAuthenticationAttemptRepository repository, IGeolocationProvider? provider) =>
        new(
            repository,
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance,
            provider,
            null);

    [Fact]
    public async Task ResolvedLocation_EnrichesTheForensicsRecord()
    {
        var sink = new CapturingRepository();
        var service = CreateService(sink, new FakeGeolocationProvider("United States", "US", "Cambridge"));

        await service.RecordSuccessfulAttemptAsync("user@example.com", Guid.NewGuid(), "203.0.113.25", "UnitTestAgent", TimeSpan.FromMilliseconds(5));

        sink.Last.Should().NotBeNull();
        sink.Last!.Country.Should().Be("US");
        sink.Last.City.Should().Be("Cambridge");
        sink.Last.Location.Should().Be("Cambridge, United States");
    }

    [Fact]
    public async Task UnresolvableIp_StaysUnknown()
    {
        var sink = new CapturingRepository();
        var service = CreateService(sink, new FakeGeolocationProvider(null, null, null));

        await service.RecordFailedAttemptAsync("user@example.com", null, "203.0.113.25", "UnitTestAgent", "InvalidCredentials", TimeSpan.FromMilliseconds(5));

        sink.Last.Should().NotBeNull();
        sink.Last!.Country.Should().BeNull();
        sink.Last.City.Should().BeNull();
        sink.Last.Location.Should().BeNull();
    }

    [Fact]
    public async Task NoProviderRegistered_RecordsStayClean()
    {
        var sink = new CapturingRepository();
        var service = CreateService(sink, provider: null);

        await service.RecordSuccessfulAttemptAsync("user@example.com", Guid.NewGuid(), "203.0.113.25", "UnitTestAgent", TimeSpan.FromMilliseconds(5));

        sink.Last!.Country.Should().BeNull();
        sink.Last.Location.Should().BeNull();
    }

    [Fact]
    public async Task ThrowingProvider_FailsClosedWithoutBlockingTheAttempt()
    {
        var sink = new CapturingRepository();
        var service = CreateService(sink, new ThrowingGeolocationProvider());

        var act = () => service.RecordSuccessfulAttemptAsync("user@example.com", Guid.NewGuid(), "203.0.113.25", "UnitTestAgent", TimeSpan.FromMilliseconds(5));
        await act.Should().NotThrowAsync();

        sink.Last.Should().NotBeNull();
        sink.Last!.Country.Should().BeNull();
    }
}
