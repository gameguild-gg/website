using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using GameGuild.API;
using GameGuild.API.Core.Middleware;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using GameGuild.Resources;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Microsoft.AspNetCore.RateLimiting;
using Xunit.Abstractions;

namespace GameGuild.Resources.IntegrationTests.Security;

[CollectionDefinition("Redis rate limiter")]
public sealed class RedisRateLimiterCollection : ICollectionFixture<RedisRateLimiterFixture>
{
}

public sealed class RedisRateLimiterFixture : IAsyncLifetime
{
    private readonly INetwork _network;
    private readonly IContainer _container;

    private readonly List<IConnectionMultiplexer> _connections = [];

    public RedisRateLimiterFixture()
    {
        _network = new NetworkBuilder()
            .WithName($"gameguild-rate-limiter-{Guid.NewGuid():N}")
            .Build();
        _container = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithNetwork(_network)
            .WithNetworkAliases("rate-limit-redis")
            .WithPortBinding(6379, true)
            .WithCleanUp(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(["redis-cli", "ping"]))
            .Build();
    }

    public IReadOnlyList<IConnectionMultiplexer> Connections => _connections;

    public string RedisEndpoint { get; private set; } = string.Empty;

    public string RedisContainerEndpoint => "rate-limit-redis:6379";

    public INetwork Network => _network;

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();
        await _container.StartAsync();

        RedisEndpoint = $"{_container.Hostname}:{_container.GetMappedPublicPort(6379)}";
        for (var index = 0; index < 4; index++)
        {
            _connections.Add(await ConnectionMultiplexer.ConnectAsync(RedisEndpoint));
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        await _container.DisposeAsync();
        await _network.DisposeAsync();
    }
}

[Collection("Redis rate limiter")]
[Trait("Category", "Integration")]
[Trait("Infrastructure", "Redis")]
public sealed class RedisDistributedRateLimiterIntegrationTests(
    RedisRateLimiterFixture fixture,
    ITestOutputHelper output)
{
    private const string ProbeHostAssemblyFileName = "GameGuild.RateLimitingProbeHost.dll";

    [Fact]
    public void ProbeCertificateStaysWithinShortAuthorityValidityWindow()
    {
        var now = DateTimeOffset.UtcNow;
        using var authority = CreateProbeCertificateAuthority(now.AddDays(-2), now.AddMinutes(5));
        using var certificate = CreateProbeServerCertificate(authority);

        Assert.Equal(authority.NotBefore.ToUniversalTime(), certificate.NotBefore.ToUniversalTime());
        Assert.Equal(authority.NotAfter.ToUniversalTime(), certificate.NotAfter.ToUniversalTime());
        Assert.True(certificate.HasPrivateKey);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        Assert.True(chain.Build(certificate), string.Join(", ", chain.ChainStatus.Select(status => status.Status)));
    }

    [Fact]
    public async Task SeparateApiHostsShareRedisLimitUnderConcurrentLoad()
    {
        const int requestLimit = 20;
        const int requestCount = 200;
        var userId = Guid.NewGuid().ToString("N");
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = requestLimit;
        options.Period = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];
        options.EnableProgressivePenalties = false;

        using var firstHost = await StartRateLimitedTestHostAsync(fixture.Connections[0], options);
        using var secondHost = await StartRateLimitedTestHostAsync(fixture.Connections[1], options);
        using var firstClient = firstHost.GetTestClient();
        using var secondClient = secondHost.GetTestClient();

        var stopwatch = Stopwatch.StartNew();
        var measurements = await Task.WhenAll(Enumerable.Range(0, requestCount).Select(async index =>
        {
            var client = index % 2 == 0 ? firstClient : secondClient;
            using var request = new HttpRequestMessage(HttpMethod.Get, "/limited");
            request.Headers.Add("X-Test-User", userId);
            var startedAt = Stopwatch.GetTimestamp();
            var response = await client.SendAsync(request);
            return (Response: response, Latency: Stopwatch.GetElapsedTime(startedAt));
        }));
        stopwatch.Stop();
        var responses = measurements.Select(measurement => measurement.Response).ToArray();

        try
        {
            var accepted = responses.Count(response => response.StatusCode == HttpStatusCode.NoContent);
            var rejected = responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests);
            accepted.Should().Be(requestLimit);
            rejected.Should().Be(requestCount - requestLimit);
            responses.Should().OnlyContain(response =>
                response.StatusCode == HttpStatusCode.NoContent ||
                response.StatusCode == HttpStatusCode.TooManyRequests);

            output.WriteLine(
                "Two TestServer API hosts enforced one Redis limit across {0} concurrent requests in {1:F1} ms ({2:F0} requests/sec; p95 {3:F1} ms, p99 {4:F1} ms).",
                requestCount,
                stopwatch.Elapsed.TotalMilliseconds,
                requestCount / stopwatch.Elapsed.TotalSeconds,
                Percentile(measurements.Select(measurement => measurement.Latency), 0.95).TotalMilliseconds,
                Percentile(measurements.Select(measurement => measurement.Latency), 0.99).TotalMilliseconds);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task SeparateKestrelContainersShareRedisLimitUnderConcurrentLoad()
    {
        const int requestLimit = 20;
        const int requestCount = 200;
        var userId = Guid.NewGuid().ToString("N");
        using var certificateAuthority = CreateProbeCertificateAuthority();
        using var serverCertificate = CreateProbeServerCertificate(certificateAuthority);
        var certificateDirectory = Directory.CreateTempSubdirectory("gameguild-rate-limit-probe-");
        var certificatePath = Path.Combine(certificateDirectory.FullName, "rate-limit-probe.pfx");
        var certificatePassword = Guid.NewGuid().ToString("N");
        var certificateBytes = serverCertificate.Export(X509ContentType.Pfx, certificatePassword);
        try
        {
            await File.WriteAllBytesAsync(certificatePath, certificateBytes);
            CryptographicOperations.ZeroMemory(certificateBytes);

            await using var firstHost = CreateRateLimitingProbeHost(requestLimit, certificatePath, certificatePassword);
            await using var secondHost = CreateRateLimitingProbeHost(requestLimit, certificatePath, certificatePassword);
            await Task.WhenAll(firstHost.StartAsync(), secondHost.StartAsync());

            using var firstClient = CreateProbeHttpClient(firstHost, certificateAuthority);
            using var secondClient = CreateProbeHttpClient(secondHost, certificateAuthority);
            await Task.WhenAll(WaitForProbeHostAsync(firstClient), WaitForProbeHostAsync(secondClient));
            var responses = await Task.WhenAll(Enumerable.Range(0, requestCount).Select(async index =>
            {
                var client = index % 2 == 0 ? firstClient : secondClient;
                using var request = new HttpRequestMessage(HttpMethod.Get, "/limited");
                request.Headers.Add("X-Test-User", userId);
                return await client.SendAsync(request);
            }));

            try
            {
                responses.Count(response => response.StatusCode == HttpStatusCode.NoContent).Should().Be(requestLimit);
                responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests)
                    .Should().Be(requestCount - requestLimit);
                responses.Should().OnlyContain(response =>
                    response.StatusCode == HttpStatusCode.NoContent ||
                    response.StatusCode == HttpStatusCode.TooManyRequests);
                output.WriteLine(
                    "Two independent Kestrel containers enforced one Redis limit across {0} concurrent requests over HTTPS.",
                    requestCount);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(certificateBytes);
            Directory.Delete(certificateDirectory.FullName, recursive: true);
        }
    }

    private IContainer CreateRateLimitingProbeHost(int requestLimit, string certificatePath, string certificatePassword)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var probeHostDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "RateLimitingProbeHost",
            "bin",
            configuration,
            "net10.0"));
        var assemblyPath = Path.Combine(probeHostDirectory, ProbeHostAssemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("The rate-limiting probe host was not built with the integration tests.", assemblyPath);
        }

        return new ContainerBuilder()
            .WithImage("mcr.microsoft.com/dotnet/aspnet:10.0")
            .WithBindMount(probeHostDirectory, "/app", AccessMode.ReadOnly)
            .WithWorkingDirectory("/app")
            .WithEntrypoint(["dotnet"])
            .WithCommand([ProbeHostAssemblyFileName])
            .WithEnvironment("GAMEGUILD_RATE_LIMIT_REDIS_ENDPOINT", fixture.RedisContainerEndpoint)
            .WithEnvironment("GAMEGUILD_RATE_LIMIT_HTTPS_CERTIFICATE_PATH", "/certs/rate-limit-probe.pfx")
            .WithEnvironment("GAMEGUILD_RATE_LIMIT_HTTPS_CERTIFICATE_PASSWORD", certificatePassword)
            .WithEnvironment("GAMEGUILD_RATE_LIMIT_HTTPS_PORT", "8443")
            .WithEnvironment("GAMEGUILD_RATE_LIMIT_HTTPS_HOST", "0.0.0.0")
            .WithEnvironment(
                "GAMEGUILD_RATE_LIMIT_REQUEST_LIMIT",
                requestLimit.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithBindMount(certificatePath, "/certs/rate-limit-probe.pfx", AccessMode.ReadOnly)
            .WithPortBinding(8443, true)
            .WithNetwork(fixture.Network)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8443))
            .WithCleanUp(true)
            .Build();
    }

    private static HttpClient CreateProbeHttpClient(IContainer host, X509Certificate2 certificateAuthority)
    {
        var chainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck
        };
        chainPolicy.CustomTrustStore.Add(certificateAuthority);
        chainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));

        var handler = new SocketsHttpHandler();
        handler.SslOptions.CertificateChainPolicy = chainPolicy;

        return new HttpClient(handler)
        {
            BaseAddress = new UriBuilder(Uri.UriSchemeHttps, "localhost", host.GetMappedPublicPort(8443)).Uri
        };
    }

    private static X509Certificate2 CreateProbeCertificateAuthority(
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=GameGuild rate-limit probe root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            critical: true));

        var now = DateTimeOffset.UtcNow;
        return request.CreateSelfSigned(notBefore ?? now.AddMinutes(-1), notAfter ?? now.AddHours(1));
    }

    [Fact]
    public void ProbeServerCertificate_UsesIssuerValidityWindow()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Short-lived probe root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, critical: true));
        var now = DateTimeOffset.UtcNow;
        using var certificateAuthority = request.CreateSelfSigned(now.AddMinutes(-1), now.AddMinutes(5));

        using var certificate = CreateProbeServerCertificate(certificateAuthority);

        certificate.NotBefore.Should().Be(certificateAuthority.NotBefore);
        certificate.NotAfter.Should().Be(certificateAuthority.NotAfter);
        certificate.HasPrivateKey.Should().BeTrue();
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        chain.Build(certificate).Should().BeTrue();
    }

    private static X509Certificate2 CreateProbeServerCertificate(X509Certificate2 certificateAuthority)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));
        var serverAuthentication = new OidCollection { new("1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(serverAuthentication, critical: false));

        using var publicCertificate = request.Create(
            certificateAuthority,
            new DateTimeOffset(certificateAuthority.NotBefore.ToUniversalTime()),
            new DateTimeOffset(certificateAuthority.NotAfter.ToUniversalTime()),
            RandomNumberGenerator.GetBytes(16));
        return publicCertificate.CopyWithPrivateKey(key);
    }

    private static async Task WaitForProbeHostAsync(HttpClient client)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync("/healthz", timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Kestrel may have opened its port before the health endpoint is ready.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }

        throw new TimeoutException("The HTTPS rate-limiting probe did not become ready within 20 seconds.");
    }

    private static TimeSpan Percentile(IEnumerable<TimeSpan> latencies, double percentile)
    {
        var ordered = latencies.OrderBy(latency => latency).ToArray();
        var index = Math.Clamp((int)Math.Ceiling(ordered.Length * percentile) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    [Fact]
    public async Task EndpointMiddlewareSharesTheGlobalLimitAcrossIndependentRedisConnections()
    {
        const int requestLimit = 25;
        var userId = Guid.NewGuid().ToString("N");
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = requestLimit;
        options.Period = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];

        var next = new RequestDelegate(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var middlewares = fixture.Connections
            .Select(_ => new RedisEndpointRateLimitingMiddleware(next))
            .ToArray();
        var limiterLog = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        var responses = await Task.WhenAll(Enumerable.Range(0, 200).Select(index =>
            SendThroughMiddlewareAsync(
                middlewares[index % middlewares.Length],
                fixture.Connections[index % fixture.Connections.Count],
                limiterLog,
                options,
                requestServices,
                userId,
                endpoint: null)));

        limiterLog.Errors.Should().BeEmpty();
        responses.Count(response => response.StatusCode == StatusCodes.Status204NoContent).Should().Be(requestLimit);
        responses.Count(response => response.StatusCode == StatusCodes.Status429TooManyRequests).Should().Be(200 - requestLimit);

        var rejected = responses.First(response => response.StatusCode == StatusCodes.Status429TooManyRequests);
        rejected.Response.ContentType.Should().StartWith("application/problem+json");
        rejected.Response.Headers.RetryAfter.Should().NotBeEmpty();
        rejected.Response.Headers["RateLimit-Limit"].ToString().Should().Be(requestLimit.ToString());
        rejected.Response.Headers["RateLimit-Remaining"].ToString().Should().Be("0");
    }

    [Fact]
    public async Task EndpointMiddlewareSharesNamedPerIpPolicyAcrossIndependentRedisConnections()
    {
        const int policyLimit = 4;
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 100;
        options.Period = TimeSpan.FromMinutes(1);
        options.IpRequestsPerMinute = policyLimit;
        options.IpWindow = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];

        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.PerIp)),
            "per-ip-rate-limited");
        var next = new RequestDelegate(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var middlewares = fixture.Connections
            .Select(_ => new RedisEndpointRateLimitingMiddleware(next))
            .ToArray();
        var limiterLog = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        var responses = await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            SendThroughMiddlewareAsync(
                middlewares[index % middlewares.Length],
                fixture.Connections[index % fixture.Connections.Count],
                limiterLog,
                options,
                requestServices,
                Guid.NewGuid().ToString("N"),
                endpoint)));

        limiterLog.Errors.Should().BeEmpty();
        responses.Count(response => response.StatusCode == StatusCodes.Status204NoContent).Should().Be(policyLimit);
        responses.Count(response => response.StatusCode == StatusCodes.Status429TooManyRequests).Should().Be(40 - policyLimit);
    }

    [Fact]
    public async Task EndpointMiddlewareSharesFixedAuthorizationLimitForResolvedTenantAcrossConnections()
    {
        const int policyLimit = 2;
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 100;
        options.Period = TimeSpan.FromMinutes(1);
        options.AuthorizationRequestsPerMinute = policyLimit;
        options.AuthorizationWindow = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.Authorization)),
            "tenant-authorization-rate-limited");
        var next = new RequestDelegate(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var middlewares = fixture.Connections
            .Select(_ => new RedisEndpointRateLimitingMiddleware(next))
            .ToArray();
        var limiterLog = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var userId = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            SendThroughMiddlewareAsync(
                middlewares[index % middlewares.Length],
                fixture.Connections[index % fixture.Connections.Count],
                limiterLog,
                options,
                requestServices,
                userId,
                endpoint,
                resolvedTenantId: tenantId)));

        limiterLog.Errors.Should().BeEmpty();
        responses.Count(response => response.StatusCode == StatusCodes.Status204NoContent).Should().Be(policyLimit);
        responses.Count(response => response.StatusCode == StatusCodes.Status429TooManyRequests).Should().Be(20 - policyLimit);
    }

    [Fact]
    public async Task EndpointMiddlewareRecordsLowCardinalityRedisRejectionMetrics()
    {
        var measurements = new ConcurrentQueue<(long Value, string? Policy, string? Enforcement, string[] TagNames)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "GameGuild.API.RateLimiting" &&
                instrument.Name == "gameguild.api.rate_limit.rejections")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? policy = null;
            string? enforcement = null;
            var tagNames = new List<string>();
            foreach (var tag in tags)
            {
                tagNames.Add(tag.Key);
                if (tag.Key == "policy")
                {
                    policy = tag.Value?.ToString();
                }
                else if (tag.Key == "enforcement")
                {
                    enforcement = tag.Value?.ToString();
                }
            }

            measurements.Enqueue((value, policy, enforcement, tagNames.ToArray()));
        });
        listener.Start();

        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 10;
        options.Period = TimeSpan.FromMinutes(1);
        options.IpRequestsPerMinute = 1;
        options.IpWindow = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.PerIp)),
            "per-ip-metrics");
        var middleware = new RedisEndpointRateLimitingMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var logger = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var userId = Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(index =>
            SendThroughMiddlewareAsync(
                middleware,
                fixture.Connections[index],
                logger,
                options,
                requestServices,
                userId,
                endpoint,
                remoteIpAddress: "192.0.2.21")));

        responses.Select(response => response.StatusCode).Should().Contain(StatusCodes.Status204NoContent);
        responses.Select(response => response.StatusCode).Should().Contain(StatusCodes.Status429TooManyRequests);
        measurements.Should().Contain(measurement =>
            measurement.Value == 1 &&
            measurement.Policy == RateLimitPolicies.PerIp &&
            measurement.Enforcement == "redis");
        measurements.Should().OnlyContain(measurement =>
            (measurement.Policy == "global" || measurement.Policy == RateLimitPolicies.PerIp) &&
            measurement.Enforcement == "redis" &&
            measurement.TagNames.Length == 2 &&
            measurement.TagNames.Contains("policy") &&
            measurement.TagNames.Contains("enforcement"));
    }

    [Fact]
    public async Task EndpointMiddlewareSkipsGlobalAndNamedPoliciesForAllowlistedUsers()
    {
        var userId = Guid.NewGuid().ToString("N");
        var accessOptions = new RateLimitAccessOptions { AllowlistedUserIds = [userId] };
        accessOptions.Validate();
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 1;
        options.Period = TimeSpan.FromMinutes(1);
        options.AuthenticationRequestsPerMinute = 1;
        options.AuthenticationWindow = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.Authentication)),
            "authentication-rate-limited");
        var next = new RequestDelegate(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var middlewares = fixture.Connections
            .Select(_ => new RedisEndpointRateLimitingMiddleware(next))
            .ToArray();
        var limiterLog = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            SendThroughMiddlewareAsync(
                middlewares[index % middlewares.Length],
                fixture.Connections[index % fixture.Connections.Count],
                limiterLog,
                options,
                requestServices,
                userId,
                endpoint,
                accessOptions: accessOptions)));

        limiterLog.Errors.Should().BeEmpty();
        responses.Should().OnlyContain(response => response.StatusCode == StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task TokenBucketAdmissionIsAtomicAcrossConnectionsAndReplenishes()
    {
        const int capacity = 6;
        const int refillTokens = 2;
        var period = TimeSpan.FromMinutes(1);
        var log = new CapturingLogger<RedisDistributedRateLimiter>();
        var limiters = fixture.Connections
            .Select(connection => new RedisDistributedRateLimiter(connection, log))
            .ToArray();

        var initial = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ =>
            limiters[Random.Shared.Next(limiters.Length)].TryAcquireTokenBucketAsync(
                "integration:shared-token-bucket",
                capacity,
                refillTokens,
                period)));

        initial.Count(decision => decision.IsAllowed).Should().Be(capacity);
        initial.Where(decision => !decision.IsAllowed).Should().OnlyContain(decision => decision.RetryAfter > TimeSpan.Zero);

        var database = fixture.Connections[0].GetDatabase();
        const string bucketKey = "ratelimit:token-bucket:integration:shared-token-bucket";
        (await database.HashGetAsync(bucketKey, "tokens")).ToString().Should().Be("0");

        // Position the fixture one period behind Redis's own clock. A short
        // wall-clock delay can cross multiple periods on a busy CI runner.
        var positioned = await database.ScriptEvaluateAsync(
            """
            local now = redis.call('TIME')
            local nowMs = tonumber(now[1]) * 1000 + math.floor(tonumber(now[2]) / 1000)
            if redis.call('HEXISTS', KEYS[1], 'lastRefill') ~= 1 then
                return 0
            end
            redis.call('HSET', KEYS[1], 'lastRefill', nowMs - tonumber(ARGV[1]))
            return 1
            """,
            [bucketKey],
            [(long)period.TotalMilliseconds]);
        ((int)positioned).Should().Be(1);
        var refilled = await Task.WhenAll(Enumerable.Range(0, 3).Select(index =>
            limiters[index % limiters.Length].TryAcquireTokenBucketAsync(
                "integration:shared-token-bucket",
                capacity,
                refillTokens,
                period)));

        refilled.Count(decision => decision.IsAllowed).Should().Be(refillTokens);
        refilled.Single(decision => !decision.IsAllowed).RetryAfter.Should().BeGreaterThan(TimeSpan.Zero);
        log.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task EndpointMiddlewareSharesBurstyTokenBucketAcrossConnections()
    {
        const int tokenLimit = 5;
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 100;
        options.Period = TimeSpan.FromMinutes(1);
        options.TokenBucketLimit = tokenLimit;
        options.TokensPerPeriod = 1;
        options.TokenReplenishmentPeriod = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.Bursty)),
            "bursty-rate-limited");
        var next = new RequestDelegate(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var middlewares = fixture.Connections
            .Select(_ => new RedisEndpointRateLimitingMiddleware(next))
            .ToArray();
        var limiterLog = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var userId = Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(index =>
            SendThroughMiddlewareAsync(
                middlewares[index % middlewares.Length],
                fixture.Connections[index % fixture.Connections.Count],
                limiterLog,
                options,
                requestServices,
                userId,
                endpoint)));

        limiterLog.Errors.Should().BeEmpty();
        responses.Count(response => response.StatusCode == StatusCodes.Status204NoContent).Should().Be(tokenLimit);
        responses.Count(response => response.StatusCode == StatusCodes.Status429TooManyRequests).Should().Be(30 - tokenLimit);
    }

    [Fact]
    public async Task EndpointMiddlewareUsesPremiumApiKeyTokenBucketLimit()
    {
        const int premiumLimit = 3;
        var apiKeyId = Guid.NewGuid().ToString("N");
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 100;
        options.Period = TimeSpan.FromMinutes(1);
        options.StandardApiKeyRequestsPerMinute = 1;
        options.PremiumApiKeyRequestsPerMinute = premiumLimit;
        options.ApiKeyWindow = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.ApiKey)),
            "api-key-rate-limited");
        var next = new RequestDelegate(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var middlewares = fixture.Connections
            .Select(_ => new RedisEndpointRateLimitingMiddleware(next))
            .ToArray();
        var limiterLog = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(index =>
            SendThroughMiddlewareAsync(
                middlewares[index % middlewares.Length],
                fixture.Connections[index % fixture.Connections.Count],
                limiterLog,
                options,
                requestServices,
                Guid.NewGuid().ToString("N"),
                endpoint,
                apiKeyId: apiKeyId,
                apiKeyTier: "premium")));

        limiterLog.Errors.Should().BeEmpty();
        responses.Count(response => response.StatusCode == StatusCodes.Status204NoContent).Should().Be(premiumLimit);
        responses.Count(response => response.StatusCode == StatusCodes.Status429TooManyRequests).Should().Be(30 - premiumLimit);
    }

    [Fact]
    public async Task ConcurrentInstancesNeverAdmitMoreThanTheConfiguredSlidingWindowLimit()
    {
        const int requestLimit = 25;
        const int concurrentRequests = 200;
        var logger = new CapturingLogger<RedisDistributedRateLimiter>();
        var limiters = fixture.Connections
            .Select(connection => new RedisDistributedRateLimiter(
                connection,
                logger))
            .ToArray();

        var decisions = await Task.WhenAll(Enumerable.Range(0, concurrentRequests)
            .Select(index => limiters[index % limiters.Length]
                .IsAllowedAsync("integration:shared-actor", requestLimit, TimeSpan.FromSeconds(30))));

        logger.Errors.Should().BeEmpty();
        decisions.Count(allowed => allowed).Should().Be(requestLimit);
        (await limiters[0].GetCurrentCountAsync("integration:shared-actor", TimeSpan.FromSeconds(30)))
            .Should().Be(requestLimit);
        (await limiters[1].IsAllowedAsync("integration:shared-actor", requestLimit, TimeSpan.FromSeconds(30)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SlidingWindowEntriesExpireAndAllowNewRequests()
    {
        var logger = new CapturingLogger<RedisDistributedRateLimiter>();
        var limiter = new RedisDistributedRateLimiter(
            fixture.Connections[0],
            logger);

        (await limiter.IsAllowedAsync("integration:expiring-actor", 1, TimeSpan.FromMilliseconds(250)))
            .Should().BeTrue();
        (await limiter.IsAllowedAsync("integration:expiring-actor", 1, TimeSpan.FromMilliseconds(250)))
            .Should().BeFalse();

        await Task.Delay(TimeSpan.FromMilliseconds(350));

        (await limiter.IsAllowedAsync("integration:expiring-actor", 1, TimeSpan.FromMilliseconds(250)))
            .Should().BeTrue();
        logger.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpensiveOperationConcurrencyIsSharedRenewedAndReleasedAcrossConnections()
    {
        var userId = Guid.NewGuid().ToString("N");
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 100;
        options.Period = TimeSpan.FromMinutes(1);
        options.MaxConcurrentRequests = 1;
        options.ConcurrencyLeaseDuration = TimeSpan.FromMilliseconds(180);
        options.ExemptPaths = [];
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(RateLimitPolicies.ExpensiveOperations)),
            "expensive-operation");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executed = 0;
        var next = new RequestDelegate(async context =>
        {
            Interlocked.Increment(ref executed);
            entered.TrySetResult();
            await release.Task;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });
        var middleware = new RedisEndpointRateLimitingMiddleware(next);
        var logger = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        var firstRequest = SendThroughMiddlewareAsync(
            middleware,
            fixture.Connections[0],
            logger,
            options,
            requestServices,
            userId,
            endpoint);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task<MiddlewareResponse>? secondRequest = null;
        MiddlewareResponse? secondResponse = null;
        try
        {
            // The first operation outlives the initial lease TTL. Its renewal must
            // keep the shared slot occupied while it is still running.
            await Task.Delay(TimeSpan.FromMilliseconds(450));
            secondRequest = SendThroughMiddlewareAsync(
                middleware,
                fixture.Connections[1],
                logger,
                options,
                requestServices,
                userId,
                endpoint);
            secondResponse = await secondRequest.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            release.TrySetResult();
        }

        var firstResponse = await firstRequest.WaitAsync(TimeSpan.FromSeconds(5));
        if (secondRequest is not null)
        {
            secondResponse ??= await secondRequest.WaitAsync(TimeSpan.FromSeconds(5));
        }

        firstResponse.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        secondResponse!.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        secondResponse.Response.Headers.RetryAfter.Should().NotBeEmpty();

        var afterRelease = await SendThroughMiddlewareAsync(
            middleware,
            fixture.Connections[2],
            logger,
            options,
            requestServices,
            userId,
            endpoint);
        afterRelease.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        executed.Should().Be(2);
        logger.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpensiveOperationConcurrencyPrunesExpiredLeasesBeforeAdmission()
    {
        var userId = Guid.NewGuid().ToString("N");
        var redisKey = $"ratelimit:concurrency:api:concurrency:{RateLimitPolicies.ExpensiveOperations}:user:{userId}";
        var database = fixture.Connections[0].GetDatabase();
        await database.SortedSetAddAsync(
            redisKey,
            "abandoned-process",
            DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeMilliseconds());

        var limiter = new RedisDistributedRateLimiter(
            fixture.Connections[1],
            new CapturingLogger<RedisDistributedRateLimiter>());
        var decision = await limiter.TryAcquireConcurrencyLeaseAsync(
            $"api:concurrency:{RateLimitPolicies.ExpensiveOperations}:user:{userId}",
            maxConcurrent: 1,
            leaseDuration: TimeSpan.FromSeconds(2));

        decision.IsAllowed.Should().BeTrue();
        decision.Lease.Should().NotBeNull();
        await decision.Lease!.DisposeAsync();
        (await database.SortedSetLengthAsync(redisKey)).Should().Be(0);
    }

    [Fact]
    public async Task ProgressivePenaltiesEscalateAcrossRedisConnectionsAndDecay()
    {
        var log = new CapturingLogger<RedisDistributedRateLimiter>();
        var firstConnectionLimiter = new RedisDistributedRateLimiter(fixture.Connections[0], log);
        var secondConnectionLimiter = new RedisDistributedRateLimiter(fixture.Connections[1], log);
        var key = $"integration:penalty:{Guid.NewGuid():N}";

        var firstViolation = await firstConnectionLimiter.RecordRateLimitViolationAsync(
            key,
            violationThreshold: 2,
            decayWindow: TimeSpan.FromSeconds(5),
            basePenalty: TimeSpan.FromMilliseconds(120),
            maxPenalty: TimeSpan.FromMilliseconds(480));
        var secondViolation = await secondConnectionLimiter.RecordRateLimitViolationAsync(
            key,
            violationThreshold: 2,
            decayWindow: TimeSpan.FromSeconds(5),
            basePenalty: TimeSpan.FromMilliseconds(120),
            maxPenalty: TimeSpan.FromMilliseconds(480));

        firstViolation.Should().BeNull();
        secondViolation.Should().Be(TimeSpan.FromMilliseconds(120));
        (await firstConnectionLimiter.GetActivePenaltyAsync(key)).Should().BeGreaterThan(TimeSpan.Zero);

        await Task.Delay(TimeSpan.FromMilliseconds(160));
        (await secondConnectionLimiter.GetActivePenaltyAsync(key)).Should().BeNull();
        var escalatedViolation = await firstConnectionLimiter.RecordRateLimitViolationAsync(
            key,
            violationThreshold: 2,
            decayWindow: TimeSpan.FromSeconds(5),
            basePenalty: TimeSpan.FromMilliseconds(120),
            maxPenalty: TimeSpan.FromMilliseconds(480));

        escalatedViolation.Should().Be(TimeSpan.FromMilliseconds(240));
        log.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task EndpointMiddlewareEnforcesTemporaryPenaltyBeforeRateLimitPolicies()
    {
        var userId = Guid.NewGuid().ToString("N");
        var options = RateLimitingOptions.CreateDefault();
        options.EnableProgressivePenalties = true;
        options.PenaltyViolationThreshold = 1;
        options.PenaltyDecayWindow = TimeSpan.FromMinutes(1);
        options.PenaltyBaseDuration = TimeSpan.FromSeconds(2);
        options.PenaltyMaxDuration = TimeSpan.FromSeconds(2);
        options.Limit = 1;
        options.Period = TimeSpan.FromMinutes(1);
        options.ExemptPaths = [];
        var next = new RequestDelegate(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var middleware = new RedisEndpointRateLimitingMiddleware(next);
        var logger = new CapturingLogger<RedisDistributedRateLimiter>();
        using var requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        var accepted = await SendThroughMiddlewareAsync(
            middleware, fixture.Connections[0], logger, options, requestServices, userId, endpoint: null);
        var firstRejection = await SendThroughMiddlewareAsync(
            middleware, fixture.Connections[1], logger, options, requestServices, userId, endpoint: null);
        var penaltyRejection = await SendThroughMiddlewareAsync(
            middleware, fixture.Connections[2], logger, options, requestServices, userId, endpoint: null);

        accepted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        firstRejection.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        penaltyRejection.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        penaltyRejection.Response.ContentType.Should().StartWith("application/problem+json");
        penaltyRejection.Response.Headers.RetryAfter.Should().NotBeEmpty();
        logger.Errors.Should().BeEmpty();
    }

    private static async Task<IHost> StartRateLimitedTestHostAsync(
        IConnectionMultiplexer connection,
        RateLimitingOptions options)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddRouting();
                    var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager
                    {
                        ["Redis:Enabled"] = "true"
                    };
                    services.SetupRateLimiting(configuration, options);
                    services.AddSingleton<IDistributedRateLimiter>(provider => new RedisDistributedRateLimiter(
                        connection,
                        provider.GetRequiredService<ILogger<RedisDistributedRateLimiter>>()));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Headers.TryGetValue("X-Test-User", out var userId) &&
                            !string.IsNullOrWhiteSpace(userId))
                        {
                            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                                "rate-limit-integration-test"));
                        }

                        await next(context).ConfigureAwait(false);
                    });
                    app.UseRateLimiter();
                    app.UseMiddleware<RedisEndpointRateLimitingMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapGet("/limited", () => Results.NoContent()));
                }))
            .Build();

        await host.StartAsync().ConfigureAwait(false);
        return host;
    }

    private static async Task<MiddlewareResponse> SendThroughMiddlewareAsync(
        RedisEndpointRateLimitingMiddleware middleware,
        IConnectionMultiplexer connection,
        CapturingLogger<RedisDistributedRateLimiter> logger,
        RateLimitingOptions options,
        IServiceProvider requestServices,
        string userId,
        Endpoint? endpoint,
        string remoteIpAddress = "192.0.2.20",
        Guid? resolvedTenantId = null,
        RateLimitAccessOptions? accessOptions = null,
        string? apiKeyId = null,
        string? apiKeyTier = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = requestServices,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "integration-test"))
        };
        if (apiKeyId is not null)
        {
            context.User.AddIdentity(new ClaimsIdentity(
            [
                new Claim("auth_method", "api_key"),
                new Claim("api_key_id", apiKeyId),
                new Claim("api_key_tier", apiKeyTier ?? "standard")
            ], "api-key"));
        }
        context.Request.Path = "/limited";
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
        if (resolvedTenantId.HasValue)
        {
            context.Items[HttpContextKeys.AuthorizationTenantId] = resolvedTenantId.Value;
        }
        context.SetEndpoint(endpoint);

        await middleware.InvokeAsync(
            context,
            new RedisDistributedRateLimiter(connection, logger),
            options,
            accessOptions ?? new RateLimitAccessOptions());

        return new MiddlewareResponse(context.Response.StatusCode, context.Response);
    }

    private sealed record MiddlewareResponse(int StatusCode, HttpResponse Response);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<Exception> Errors { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            _ = state;
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            _ = logLevel;
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _ = logLevel;
            _ = eventId;
            _ = state;
            _ = formatter;
            if (exception is not null)
            {
                Errors.Enqueue(exception);
            }
        }
    }
}
