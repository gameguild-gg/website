using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuthenticationTimingPostgreSqlHttpTests(ApiPostgreSqlFixture fixture, ITestOutputHelper output)
{
    [Theory]
    [InlineData("wrong-current", false)]
    [InlineData("wrong-legacy", false)]
    [InlineData("wrong-slow", false)]
    [InlineData("wrong-long", false)]
    [InlineData("malformed", true)]
    [InlineData("malformed-long", true)]
    [InlineData("oversized-legacy", true)]
    [InlineData("passwordless", true)]
    [InlineData("absent", true)]
    public async Task CredentialClassesUseTheGenericDenialFloorAndActualCredentialWork(string scenario, bool needsDummyWork)
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory, scenario);
        using var client = factory.CreateClient();
        await AssertDenialAsync(factory, client, "/v1/auth/sign-in",
            new { email = account.Email, password = scenario == "oversized-legacy" ? new string('p', 73) : "Synthetic-Wrong-1!" },
            needsDummyWork, scenario);
    }

    [Theory]
    [InlineData(CredentialType.Email, "wrong-current", false)]
    [InlineData(CredentialType.Username, "wrong-current", false)]
    [InlineData(CredentialType.Phone, "wrong-current", false)]
    [InlineData(CredentialType.Email, "passwordless", true)]
    [InlineData(CredentialType.Username, "passwordless", true)]
    [InlineData(CredentialType.Phone, "passwordless", true)]
    [InlineData(CredentialType.Email, "absent", true)]
    [InlineData(CredentialType.Username, "absent", true)]
    [InlineData(CredentialType.Phone, "absent", true)]
    public async Task ActualPolymorphicResolutionPreservesTheDenialFloorAndCredentialWork(CredentialType type, string scenario, bool needsDummyWork)
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory, scenario);
        var identifier = type switch
        {
            CredentialType.Email => account.Email,
            CredentialType.Phone => account.PhoneNumber!,
            _ => account.Username!
        };
        using var client = factory.CreateClient();
        await AssertDenialAsync(factory, client, "/v1/auth/polymorphic",
            new { credential = identifier, credentialType = type, password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) },
            needsDummyWork, scenario + ":" + type);
    }

    private async Task AssertDenialAsync(WebApplicationFactory<Program> factory, HttpClient client, string endpoint, object request, bool needsDummyWork, string scenario)
    {
        var recorder = factory.Services.GetRequiredService<CredentialWorkRecorder>();
        var before = recorder.Costs.Count;
        var elapsed = Stopwatch.StartNew();
        using var response = await client.PostAsJsonAsync(endpoint, request);
        var body = await response.Content.ReadAsStringAsync();
        elapsed.Stop();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var result = JsonDocument.Parse(body);
        Assert.Equal("Unauthorized", result.RootElement.GetProperty("title").GetString());
        Assert.Equal("Invalid credentials. Please check your email and password.", result.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(400), $"The total denial floor was missed: {elapsed.Elapsed}.");
        var costs = recorder.Costs.Skip(before).ToArray();
        Assert.Equal(needsDummyWork ? 1 : 0, costs.Length);
        Assert.All(costs, cost => Assert.Equal(10, cost));
        output.WriteLine("AUTHENTICATION_TIMING_OBSERVATION=" + JsonSerializer.Serialize(new
        {
            Scenario = scenario, Endpoint = endpoint, ElapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds,
            CompletedDummyCosts = costs,
            Transport = factory.Services.GetRequiredService<IServer>().GetType().Namespace?.StartsWith("Microsoft.AspNetCore.Server.Kestrel", StringComparison.Ordinal) == true
                ? "Actual Kestrel loopback HTTP and real migrated PostgreSQL"
                : "in-process TestServer and real migrated PostgreSQL",
            UniversalConstantTimeClaim = false
        }));
    }

    [Theory]
    [InlineData("wrong-current")]
    [InlineData("passwordless")]
    [InlineData("absent")]
    public async Task AdvisoryLockAdmissionRetainsGenericDenialAndRealCredentialWorkOverKestrel(string scenario)
    {
        using var factory = CreateFactory();
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var account = await SeedAsync(factory, scenario);
        var server = factory.Services.GetRequiredService<IServer>();
        Assert.StartsWith("Microsoft.AspNetCore.Server.Kestrel", server.GetType().Namespace!);
        var address = new Uri(Assert.Single(server.Features.Get<IServerAddressesFeature>()!.Addresses));
        Assert.True(address.IsLoopback);
        Assert.NotEqual(0, address.Port);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = address, AllowAutoRedirect = false });
        Assert.Equal(address.Port, client.BaseAddress!.Port);

        // Hold the existing account-admission advisory-lock protocol on a separate real
        // PostgreSQL session. A denial must retain the lock and complete real dummy work.
        var identifier = "gameguild:auth:local-sign-in-lockout:v1:" + account.Email.ToLowerInvariant();
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(identifier)));
        await using var heldConnection = new NpgsqlConnection(fixture.ConnectionString);
        await heldConnection.OpenAsync();
        await using var acquire = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", heldConnection);
        acquire.Parameters.AddWithValue("key", lockKey);
        Assert.True(Assert.IsType<bool>(await acquire.ExecuteScalarAsync()));
        try
        {
            await AssertDenialAsync(factory, client, "/v1/auth/sign-in",
                new { email = account.Email, password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) }, needsDummyWork: true, "advisory-lock:" + scenario);
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", heldConnection);
            release.Parameters.AddWithValue("key", lockKey);
            Assert.True(Assert.IsType<bool>(await release.ExecuteScalarAsync()));
        }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var risk = new Mock<IAuthenticationAnomalyDetectionService>();
        risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        risk.Setup(service => service.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = RiskLevel.Low });
        return fixture.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<CredentialWorkRecorder>();
                services.RemoveAll<ILogger<UserEnumerationProtectionService>>();
                services.AddSingleton<ILogger<UserEnumerationProtectionService>>(provider => provider.GetRequiredService<CredentialWorkRecorder>());
                services.RemoveAll<ILogger<PasswordHasher>>();
                services.AddSingleton<ILogger<PasswordHasher>>(provider => provider.GetRequiredService<CredentialWorkRecorder>());
                services.RemoveAll<IAuthenticationAnomalyDetectionService>();
                services.AddSingleton(risk.Object);
            });
        });
    }

    private static async Task<User> SeedAsync(WebApplicationFactory<Program> factory, string scenario)
    {
        var marker = Guid.NewGuid().ToString("N");
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.IsAssignableFrom<IPasswordVerificationWork>(hasher);
        Assert.IsAssignableFrom<IAuthenticationTimingProtection>(scope.ServiceProvider.GetRequiredService<IUserEnumerationProtectionService>());
        var stored = scenario switch
        {
            "wrong-legacy" => BCrypt.Net.BCrypt.HashPassword("Synthetic-Correct-1!", 4),
            "wrong-slow" => BCrypt.Net.BCrypt.HashPassword("Synthetic-Correct-1!", 13),
            "wrong-long" => hasher.HashPassword(new string('p', 80)),
            "malformed" => "malformed-legacy-hash",
            "malformed-long" => "pbkdf2-sha256$600000$invalid$invalid",
            _ => hasher.HashPassword("Synthetic-Correct-1!")
        };
        var user = scenario == "passwordless"
            ? User.CreateOAuthUser("timing-" + marker + "@example.test", "Synthetic timing account")
            : User.CreateWithPassword("timing-" + marker + "@example.test", "Synthetic timing account", stored);
        user.Username = "timing-" + marker;
        user.PhoneNumber = "+1" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (scenario != "absent")
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<User>().Add(user);
            await context.SaveChangesAsync();
        }
        return user;
    }

    private sealed class CredentialWorkRecorder : ILogger<UserEnumerationProtectionService>, ILogger<PasswordHasher>
    {
        public ConcurrentQueue<int> Costs { get; } = new();
        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            _ = state;
            return null;
        }
        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            _ = logLevel;
            return true;
        }
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // The interface requires these arguments; this recorder observes only structured state.
            _ = logLevel;
            _ = eventId;
            _ = exception;
            _ = formatter;
            if (state is not IEnumerable<KeyValuePair<string, object?>> values) { return; }
            foreach (var pair in values)
            {
                if (pair.Key == "WorkFactor" && pair.Value is int cost) { Costs.Enqueue(cost); }
            }
        }
    }
}
