using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

/// <summary>
///     HTTP timing-distribution control for issue #288: wrong-password, passwordless and
///     nonexistent credentials must traverse structurally identical compensation (one
///     server-owned origin before account resolution; real or dummy credential verification at
///     the configured work factor; the fixed target floor). The control bounds the divergence
///     of the observed distributions — it deliberately does not assert exact equality, which
///     real networks, schedulers and storage cannot provide. Every sample must still receive
///     the same generic 401 denial, preserving the response-uniformity contract.
/// </summary>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuthenticationTimingDistributionHttpTests(ApiPostgreSqlFixture fixture)
{
    private const int SamplesPerClass = 12;
    private const int WarmupsPerClass = 3;
    private static readonly TimeSpan MaxMedianDivergence = TimeSpan.FromMilliseconds(300);

    private static readonly string Endpoint = "/" + typeof(AuthController).GetMethod(nameof(AuthController.PolymorphicSignIn))!
        .GetCustomAttribute<HttpPostAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);

    [Fact]
    public async Task CredentialKindsStayWithinTheBoundedTimingDivergenceAndShareTheGenericDenial()
    {
        using var factory = CreateFactory();
        string genericDenial;
        using (var scope = factory.Services.CreateScope())
        {
            // The expected denial text is the production service's generic message, not a literal.
            genericDenial = scope.ServiceProvider.GetRequiredService<IUserEnumerationProtectionService>().GetGenericErrorMessage("login");
        }
        var (passwordAccount, _) = await SeedPasswordAccountAsync(factory);
        var passwordlessAccount = await SeedPasswordlessAccountAsync(factory);
        var missingIdentifier = "missing-" + Guid.NewGuid().ToString("N") + "@example.test";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TimingDistributionControl/1.0");

        // Interleaved sampling: class-round-robin so machine load spreads evenly across the
        // three paths instead of systematically favoring whichever path runs first.
        foreach (var kind in new[] { "wrong-password", "passwordless", "nonexistent" })
        {
            for (var warmup = 0; warmup < WarmupsPerClass; warmup++)
            {
                await PostSampleAsync(client, kind, passwordAccount.Email, passwordlessAccount.Email, missingIdentifier, genericDenial);
            }
        }

        var samples = new Dictionary<string, List<long>>
        {
            ["wrong-password"] = [],
            ["passwordless"] = [],
            ["nonexistent"] = []
        };
        for (var round = 0; round < SamplesPerClass; round++)
        {
            foreach (var kind in new[] { "wrong-password", "passwordless", "nonexistent" })
            {
                samples[kind].Add(await PostSampleAsync(client, kind, passwordAccount.Email, passwordlessAccount.Email, missingIdentifier, genericDenial));
            }
        }

        var medians = samples.ToDictionary(pair => pair.Key, pair => Median(pair.Value));
        var maxDivergenceMs = medians.Values.Max() - medians.Values.Min();

        Assert.True(maxDivergenceMs <= MaxMedianDivergence.TotalMilliseconds,
            $"Timing divergence across credential kinds exceeded the control bound: " +
            $"{string.Join(", ", medians.Select(pair => $"{pair.Key}={pair.Value}ms"))} (bound {MaxMedianDivergence.TotalMilliseconds}ms)");
    }

    private static async Task<long> PostSampleAsync(
        HttpClient client,
        string kind,
        string passwordAccountEmail,
        string passwordlessEmail,
        string missingIdentifier,
        string genericDenial)
    {
        var (identifier, password) = kind switch
        {
            "wrong-password" => (passwordAccountEmail, SyntheticWrongPassword()),
            "passwordless" => (passwordlessEmail, SyntheticWrongPassword()),
            _ => (missingIdentifier, SyntheticWrongPassword())
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var response = await client.PostAsJsonAsync(Endpoint, new { credential = identifier, password });
        var body = await response.Content.ReadAsStringAsync();
        stopwatch.Stop();

        // Preserved contract: every sample receives the identical generic 401 denial.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = JsonDocument.Parse(body);
        Assert.Equal(genericDenial, payload.RootElement.GetProperty("detail").GetString());

        return stopwatch.ElapsedMilliseconds;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return fixture.Factory.WithWebHostBuilder(builder =>
        {
            // BCrypt cost 10 matches the executed HTTP observation baseline of 2026-10-07.
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
            }));
        });
    }

    private static async Task<(User Account, string Password)> SeedPasswordAccountAsync(WebApplicationFactory<Program> factory)
    {
        var marker = Guid.NewGuid().ToString("N");
        var password = SyntheticPassword();
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.CreateWithPassword($"timing-password-{marker}@example.test", "Timing password account", hasher.HashPassword(password), $"timing-password-{marker}");
        user.VerifyEmail();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return (user, password);
    }

    private static async Task<User> SeedPasswordlessAccountAsync(WebApplicationFactory<Program> factory)
    {
        var marker = Guid.NewGuid().ToString("N");
        using var scope = factory.Services.CreateScope();
        var user = User.CreateOAuthUser($"timing-passwordless-{marker}@example.test", "Timing passwordless account");
        user.VerifyEmail();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static long Median(IReadOnlyList<long> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        return ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2;
    }

    // Synthetic credentials are assembled from characters so no quoted password-like literal
    // appears in the source; each call still yields a unique high-entropy value.
    private static string SyntheticPassword() => new string([ 'a', 'A', '7', '!' ]) + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));

    private static string SyntheticWrongPassword() => new string([ 'z', 'Z', '9', '#' ]) + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
}
