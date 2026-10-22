using System.Net;
using System.Text;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.API.UnitTests.Core;

public sealed class TrustedProxyPresentationRegistrationTests
{
    private const string Scheme = "proxy-fixture-basic";

    [Theory]
    [InlineData(false, false, false, "192.0.2.50", "http", "https", false)]
    [InlineData(false, false, true, "192.0.2.50", "http", "https", true)]
    [InlineData(false, false, true, "192.0.2.51", "http", "https", false)]
    [InlineData(false, false, false, "192.0.2.51", "https", null, true)]
    [InlineData(false, true, false, "192.0.2.50", "http", "https", false)]
    [InlineData(false, true, true, "192.0.2.50", "http", "https", true)]
    [InlineData(false, true, true, "192.0.2.51", "http", "https", false)]
    [InlineData(false, true, false, "192.0.2.51", "https", null, true)]
    [InlineData(true, false, false, "192.0.2.50", "http", "https", false)]
    [InlineData(true, false, true, "192.0.2.50", "http", "https", true)]
    [InlineData(true, false, true, "192.0.2.51", "http", "https", false)]
    [InlineData(true, false, false, "192.0.2.51", "https", null, true)]
    [InlineData(true, true, false, "192.0.2.50", "http", "https", false)]
    [InlineData(true, true, true, "192.0.2.50", "http", "https", true)]
    [InlineData(true, true, true, "192.0.2.51", "http", "https", false)]
    [InlineData(true, true, false, "192.0.2.51", "https", null, true)]
    public async Task BasicAuthentication_UsesExplicitProxyTrustWithEitherRateLimitingSetting(
        bool rateLimitingEnabled,
        bool nestedOptions,
        bool configureProxy,
        string remoteAddress,
        string originalScheme,
        string? forwardedScheme,
        bool expectedHttps)
    {
        var settingsPrefix = nestedOptions ? "PresentationLayer:RateLimiting:" : "RateLimiting:";
        var settings = new Dictionary<string, string?>
        {
            [settingsPrefix + "TrustedProxyForwardLimit"] = "1"
        };
        if (configureProxy)
        {
            settings[settingsPrefix + "TrustedProxyAddresses:0"] = "192.0.2.50";
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var user = User.Create("proxy-fixture@example.invalid", "Proxy fixture");
        var storedHash = Guid.NewGuid().ToString("N");
        var credential = Guid.NewGuid().ToString("N");
        user.SetPasswordHash(storedHash);
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(passwordHasher => passwordHasher.VerifyPassword(storedHash, credential)).Returns(true);
        var mfa = new Mock<IUserMfaConfigurationRepository>();
        mfa.Setup(repository => repository.IsMfaEnabledAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost.UseTestServer()
                .ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.All;
                        options.KnownProxies.Clear();
                        options.KnownIPNetworks.Clear();
                        options.ForwardLimit = null;
                    });
                    services.AddPresentationLayer(configuration, NullLogger.Instance,
                        options => ConfigurePresentationFixture(options, rateLimitingEnabled));
                    services.AddSingleton(users.Object);
                    services.AddSingleton(hasher.Object);
                    services.AddSingleton(mfa.Object);
                    services.AddAuthentication(Scheme).AddBasicAuthentication(Scheme);
                })
                .Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.UseAuthentication();
                    app.Run(async context =>
                    {
                        var result = await context.AuthenticateAsync(Scheme);
                        context.Response.StatusCode = result.Succeeded ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized;
                    });
                }))
            .StartAsync();

        var context = await host.GetTestServer().SendAsync(httpContext =>
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
            httpContext.Request.Scheme = originalScheme;
            httpContext.Request.Headers.Authorization = "Basic " +
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.Email}:{credential}"));
            if (forwardedScheme is not null)
            {
                httpContext.Request.Headers["X-Forwarded-Proto"] = forwardedScheme;
                httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.25";
            }
        });

        context.Request.IsHttps.Should().Be(expectedHttps);
        context.Response.StatusCode.Should().Be(expectedHttps ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized);
        (context.User.Identity?.IsAuthenticated ?? false).Should().Be(expectedHttps);
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse(
            expectedHttps && forwardedScheme is not null ? "203.0.113.25" : remoteAddress));
        users.Verify(repository => repository.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()),
            expectedHttps ? Times.Once() : Times.Never());
        hasher.Verify(passwordHasher => passwordHasher.VerifyPassword(storedHash, credential),
            expectedHttps ? Times.Once() : Times.Never());
        mfa.Verify(repository => repository.IsMfaEnabledAsync(user.Id, It.IsAny<CancellationToken>()),
            expectedHttps ? Times.Once() : Times.Never());
        host.Services.GetServices<IConfigureOptions<RateLimiterOptions>>().Any().Should().Be(rateLimitingEnabled);
        var forwarding = host.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        forwarding.ForwardLimit.Should().Be(1);
        forwarding.KnownIPNetworks.Should().BeEmpty();
        forwarding.KnownProxies.Should().HaveCount(configureProxy ? 1 : 0);
        forwarding.ForwardedHeaders.Should().Be(configureProxy
            ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            : ForwardedHeaders.None);
        if (configureProxy)
        {
            forwarding.KnownProxies.Should().Contain(IPAddress.Parse("192.0.2.50"));
        }
    }

    [Fact]
    public void DisabledRateLimiting_DoesNotValidateUnusedThrottlingBudgets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:RequestsPerMinute"] = "0"
        }).Build();
        var services = new ServiceCollection();
        services.AddPresentationLayer(configuration, NullLogger.Instance,
            options => ConfigurePresentationFixture(options, false));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value.ForwardedHeaders
            .Should().Be(ForwardedHeaders.None);
        provider.GetServices<IConfigureOptions<RateLimiterOptions>>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("RateLimiting:TrustedProxyAddresses:0", "invalid-proxy")]
    [InlineData("RateLimiting:TrustedProxyForwardLimit", "0")]
    public void DisabledRateLimiting_StillRejectsInvalidProxyTrust(string setting, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:RequestsPerMinute"] = "0",
            [setting] = value
        }).Build();
        var act = () => new ServiceCollection().AddPresentationLayer(configuration, NullLogger.Instance,
            options => ConfigurePresentationFixture(options, false));

        act.Should().Throw<InvalidOperationException>().WithMessage("*trusted proxy*");
    }

    private static void ConfigurePresentationFixture(PresentationLayerOptions options, bool rateLimitingEnabled)
    {
        options.EnableRateLimiting = rateLimitingEnabled;
        options.EnableOpenApi = false;
        options.EnableApiVersioning = false;
        options.EnableApiExplorer = false;
        options.EnableCors = false;
        options.EnableAuthentication = false;
        options.EnableAuthorization = false;
        options.EnableResponseCompression = false;
        options.EnableHttpLogging = false;
        options.EnableProblemDetails = false;
        options.EnableLocalization = false;
        options.EnableModelValidation = false;
        options.EnableHealthChecks = false;
        options.EnableRequestContext = false;
        options.EnableResponseCaching = false;
        options.EnableMemoryCaching = false;
        options.EnableSignalR = false;
        options.EnableGraphQL = false;
        options.EnableFeatureFlags = false;
        options.EnableControllers = false;
        options.EnableEndpoints = false;
    }
}
