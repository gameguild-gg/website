using System.Text.Json;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Identity.Authorization;
using GameGuild.Resources;
using Moq;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GameGuild.API.UnitTests.Core;

public sealed class RateLimitingServiceCollectionExtensionsTests
{
    [Fact]
    public void GetClientIpAddress_IgnoresUntrustedForwardingHeaders()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.25";
        context.Request.Headers["X-Real-IP"] = "203.0.113.26";

        var clientIp = RateLimitingServiceCollectionExtensions.GetClientIpAddress(context);

        clientIp.Should().Be("192.0.2.10");
    }

    [Fact]
    public void SetupRateLimiting_EnablesForwardedHeadersOnlyFromConfiguredProxyAddresses()
    {
        var services = new ServiceCollection();
        services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.All;
            forwardedHeadersOptions.KnownProxies.Clear();
            forwardedHeadersOptions.KnownIPNetworks.Clear();
        });
        var options = RateLimitingOptions.CreateDefault();
        options.TrustedProxyAddresses = ["192.0.2.50", "2001:db8::50"];
        options.TrustedProxyForwardLimit = 2;
        services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var forwardedHeadersOptions = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        forwardedHeadersOptions.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedFor);
        forwardedHeadersOptions.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedProto);
        forwardedHeadersOptions.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        forwardedHeadersOptions.ForwardLimit.Should().Be(2);
        forwardedHeadersOptions.KnownProxies.Should().HaveCount(2);
        forwardedHeadersOptions.KnownProxies.Should().Contain(IPAddress.Parse("192.0.2.50"));
        forwardedHeadersOptions.KnownProxies.Should().Contain(IPAddress.Parse("2001:db8::50"));
        forwardedHeadersOptions.KnownIPNetworks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("192.0.2.50", true)]
    [InlineData("192.0.2.51", false)]
    public async Task ForwardedHttpsScheme_IsAcceptedOnlyFromConfiguredTrustedProxies(
        string remoteAddress,
        bool expectedHttps)
    {
        var options = RateLimitingOptions.CreateDefault();
        options.TrustedProxyAddresses = ["192.0.2.50"];
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
                    {
                        forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.All;
                        forwardedHeadersOptions.KnownProxies.Clear();
                        forwardedHeadersOptions.KnownIPNetworks.Clear();
                    });
                    services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);
                })
                .Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.Run(_ => Task.CompletedTask);
                }))
            .StartAsync();

        var context = await host.GetTestServer().SendAsync(httpContext =>
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
            httpContext.Request.Headers["X-Forwarded-Proto"] = "https";
        });

        context.Request.IsHttps.Should().Be(expectedHttps);
    }

    [Fact]
    public async Task ForwardedHttpsScheme_IsIgnoredWhenNoTrustedProxyIsConfigured()
    {
        var options = RateLimitingOptions.CreateDefault();
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
                    {
                        forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.All;
                        forwardedHeadersOptions.KnownProxies.Clear();
                        forwardedHeadersOptions.KnownIPNetworks.Clear();
                    });
                    services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);
                })
                .Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.Run(_ => Task.CompletedTask);
                }))
            .StartAsync();

        var context = await host.GetTestServer().SendAsync(httpContext =>
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.50");
            httpContext.Request.Headers["X-Forwarded-Proto"] = "https";
        });

        context.Request.IsHttps.Should().BeFalse();
    }

    [Fact]
    public void SetupRateLimiting_RejectsInvalidTrustedProxyAddressOrForwardLimit()
    {
        var configuration = new ConfigurationBuilder().Build();
        var invalidAddressOptions = RateLimitingOptions.CreateDefault();
        invalidAddressOptions.TrustedProxyAddresses = ["forwarded-by-anyone"];
        var invalidAddressAct = () => new ServiceCollection().SetupRateLimiting(configuration, invalidAddressOptions);
        invalidAddressAct.Should().Throw<InvalidOperationException>().WithMessage("*trusted proxy addresses*");

        var invalidLimitOptions = RateLimitingOptions.CreateDefault();
        invalidLimitOptions.TrustedProxyAddresses = ["192.0.2.50"];
        invalidLimitOptions.TrustedProxyForwardLimit = 0;
        var invalidLimitAct = () => new ServiceCollection().SetupRateLimiting(configuration, invalidLimitOptions);
        invalidLimitAct.Should().Throw<InvalidOperationException>().WithMessage("*forward limit*");
    }

    [Fact]
    public async Task SetupRateLimiting_BindsRedisFailureModeAndInjectsItIntoLimiter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "true",
                ["RateLimiting:RedisFailureMode"] = "FailClosed"
            })
            .Build();
        var database = new Mock<IDatabase>();
        database.Setup(redisDatabase => redisDatabase.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis is unavailable."));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(multiplexer => multiplexer.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(database.Object);

        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupRateLimiting(configuration, options: null);
        services.AddSingleton(redis.Object);
        services.AddSingleton<IDistributedRateLimiter, RedisDistributedRateLimiter>();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<RateLimitingOptions>();
        options.RedisFailureMode.Should().Be(RedisRateLimitFailureMode.FailClosed);

        var limiter = provider.GetRequiredService<IDistributedRateLimiter>();
        var act = async () => await limiter.IsAllowedFixedWindowAsync(
            "global:test",
            1,
            TimeSpan.FromMinutes(1));

        await act.Should().ThrowAsync<RateLimitBackendUnavailableException>();
    }

    [Fact]
    public void SetupRateLimiting_RejectsReservedCustomPolicyNames()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Policies["API"] = new RateLimitPolicyOptions();

        var act = () => new ServiceCollection().SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*reserved for a built-in policy*");
    }

    [Fact]
    public void SetupRateLimiting_RejectsCustomRedisWindowsBelowMillisecondPrecision()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Policies["sub-millisecond"] = new RateLimitPolicyOptions { Window = TimeSpan.FromTicks(1) };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Enabled"] = "true" })
            .Build();

        var act = () => new ServiceCollection().SetupRateLimiting(configuration, options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*sub-millisecond*at least one millisecond*");
    }

    [Fact]
    public void AuthorizationPartition_UsesResolvedTenantInsteadOfRequestHeader()
    {
        var context = CreateAuthenticatedContext();
        var resolvedTenantId = Guid.Parse("2bd62f12-7f5a-4579-87f0-b75cbba7423c");
        context.Items[HttpContextKeys.AuthorizationTenantId] = resolvedTenantId;
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

        var partition = RateLimitingServiceCollectionExtensions.GetUserTenantPartitionKey(context);

        partition.Should().Be($"user:user-1:tenant:{resolvedTenantId:D}");
    }

    [Fact]
    public void AuthorizationPartition_UsesClientIpWhenTenantWasNotResolved()
    {
        var context = CreateAuthenticatedContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.11");
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

        var partition = RateLimitingServiceCollectionExtensions.GetUserTenantPartitionKey(context);

        partition.Should().Be("user:user-1:no-tenant:192.0.2.11");
    }

    [Fact]
    public void ApiKeyPartition_UsesAuthenticatedKeyIdAndNeverTheRawCredential()
    {
        var context = CreateAuthenticatedContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.12");
        context.User.AddIdentity(new ClaimsIdentity(
        [
            new Claim("auth_method", "api_key"),
            new Claim("api_key_id", "key-123")
        ], "api-key"));
        context.Request.Headers["X-API-Key"] = "pk_attacker-chosen-value";

        var partition = RateLimitingServiceCollectionExtensions.GetApiKeyPartitionKey(context);

        partition.Should().Be("standard:key-123");
        partition.Should().NotContain("attacker-chosen-value");
    }

    [Fact]
    public void ApiKeyPartition_IsIsolatedFromAuthenticatedUserPartition()
    {
        var apiKeyContext = CreateAuthenticatedContext();
        apiKeyContext.User.AddIdentity(new ClaimsIdentity(
        [
            new Claim("auth_method", "api_key"),
            new Claim("api_key_id", "credential-42"),
            new Claim("api_key_tier", "premium")
        ], "api-key"));
        var userContext = CreateAuthenticatedContext();
        userContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("user-1");

        var apiKeyPartition = RateLimitingServiceCollectionExtensions.GetApiKeyPartitionKey(apiKeyContext);
        var userPartition = RateLimitingServiceCollectionExtensions.GetUserPartitionKey(userContext);

        apiKeyPartition.Should().Be("premium:credential-42");
        apiKeyPartition.Should().NotBe(userPartition);
    }

    [Fact]
    public async Task SetupRateLimiting_SeparatesApiKeyAndUserGlobalBucketsForTheSameOwner()
    {
        var services = new ServiceCollection();
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 1;
        options.Period = TimeSpan.FromHours(1);
        options.QueueLimit = 0;
        services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        var userContext = CreateAuthenticatedContext();
        var apiKeyContext = CreateAuthenticatedContext();
        apiKeyContext.User.AddIdentity(new ClaimsIdentity(
        [
            new Claim("auth_method", "api_key"),
            new Claim("api_key_id", "key-123")
        ], "api-key"));

        using var userPermit = await limiter.AcquireAsync(userContext, 1);
        using var apiKeyPermit = await limiter.AcquireAsync(apiKeyContext, 1);

        userPermit.IsAcquired.Should().BeTrue();
        apiKeyPermit.IsAcquired.Should().BeTrue();
        RateLimitingServiceCollectionExtensions.GetUserOrIpPartitionKey(userContext).Should().Be("user:user-1");
        RateLimitingServiceCollectionExtensions.GetUserOrIpPartitionKey(apiKeyContext).Should().Be("api-key:key-123");
    }

    [Fact]
    public void AuthenticationPartition_UsesClientIpForAnonymousAndUserIdForAuthenticatedRequests()
    {
        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.41");
        var authenticated = CreateAuthenticatedContext();
        authenticated.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.42");

        RateLimitingServiceCollectionExtensions.GetAuthenticationPartitionKey(anonymous).Should().Be("ip:192.0.2.41");
        RateLimitingServiceCollectionExtensions.GetAuthenticationPartitionKey(authenticated).Should().Be("user:user-1");
    }

    [Fact]
    public void ApiKeyPartition_DoesNotTrustAnUnvalidatedRequestHeader()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.13");
        context.Request.Headers["X-API-Key"] = "pk_forged";

        var partition = RateLimitingServiceCollectionExtensions.GetApiKeyPartitionKey(context);

        partition.Should().Be("ip:192.0.2.13");
    }

    [Fact]
    public async Task SetupRateLimiting_EnforcesConfiguredGlobalLimitPerTrustedIp()
    {
        var services = new ServiceCollection();
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 1;
        options.Period = TimeSpan.FromHours(1);
        options.QueueLimit = 0;
        services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter;
        limiter.Should().NotBeNull();

        using var first = await limiter!.AcquireAsync(CreateRequest("192.0.2.20"), 1);
        using var blocked = await limiter.AcquireAsync(CreateRequest("192.0.2.20"), 1);
        using var separateClient = await limiter.AcquireAsync(CreateRequest("192.0.2.21"), 1);

        first.IsAcquired.Should().BeTrue();
        blocked.IsAcquired.Should().BeFalse();
        separateClient.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task SetupRateLimiting_LeavesGlobalEnforcementToRedisWhenRedisIsEnabled()
    {
        var services = new ServiceCollection();
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 1;
        options.Period = TimeSpan.FromHours(1);
        options.QueueLimit = 0;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Enabled"] = "true" })
            .Build();
        services.SetupRateLimiting(configuration, options);

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter;
        limiter.Should().NotBeNull();

        using var first = await limiter!.AcquireAsync(CreateRequest("192.0.2.24"), 1);
        using var second = await limiter.AcquireAsync(CreateRequest("192.0.2.24"), 1);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task SetupRateLimiting_RegistersConfiguredCustomPolicyPartitionedByEndpoint()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 100;
        options.Policies["reports"] = new RateLimitPolicyOptions
        {
            Algorithm = RateLimitingAlgorithm.SlidingWindow,
            PartitionBy = RateLimitPartitionStrategy.Endpoint,
            PermitLimit = 1,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            SlidingWindowSegments = 1
        };
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapGet("/reports/{reportId}", () => Results.Ok()).RequireRateLimiting("reports"));
                }))
            .StartAsync();

        using var client = host.GetTestClient();
        var first = await client.GetAsync("/reports/1");
        var second = await client.GetAsync("/reports/2");

        first.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        second.StatusCode.Should().Be(System.Net.HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RedisEndpointRateLimitingMiddleware_EnforcesConfiguredCustomEndpointPolicy()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Policies["reports"] = new RateLimitPolicyOptions
        {
            Algorithm = RateLimitingAlgorithm.SlidingWindow,
            PartitionBy = RateLimitPartitionStrategy.Endpoint,
            PermitLimit = 7,
            Window = TimeSpan.FromMinutes(3),
            SlidingWindowSegments = 3
        };
        var context = CreateRequest("192.0.2.90", "/reports/123");
        context.TraceIdentifier = "trace-rate-limit";
        context.Response.Body = new MemoryStream();
        context.Request.Method = "GET";
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/reports/{reportId}"),
            0,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute("reports")),
            "GET reports"));

        var limiter = new Mock<IDistributedRateLimiter>();
        limiter.Setup(service => service.IsAllowedFixedWindowAsync(
                "api:global:ip:192.0.2.90",
                options.Limit,
                options.Period,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        limiter.Setup(service => service.IsAllowedAsync(
                "api:policy:reports:endpoint:GET:/reports/{reportId}",
                7,
                TimeSpan.FromMinutes(3),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        limiter.Setup(service => service.GetTimeUntilResetAsync(
                "api:policy:reports:endpoint:GET:/reports/{reportId}",
                TimeSpan.FromMinutes(3),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TimeSpan.FromSeconds(20));

        var nextCalled = false;
        var middleware = new GameGuild.API.Core.Middleware.RedisEndpointRateLimitingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, limiter.Object, options, new RateLimitAccessOptions());

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Headers["RateLimit-Limit"].ToString().Should().Be("7");
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status429TooManyRequests);
        problem.RootElement.GetProperty("errorCode").GetString().Should().Be("rate_limit_exceeded");
        problem.RootElement.GetProperty("instance").GetString().Should().Be("/reports/123");
        problem.RootElement.GetProperty("traceId").GetString().Should().Be("trace-rate-limit");
        problem.RootElement.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
        nextCalled.Should().BeFalse();
        limiter.VerifyAll();
    }

    [Fact]
    public async Task SetupRateLimiting_AllowsConfiguredUserToBypassLocalGlobalQuota()
    {
        var services = new ServiceCollection();
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 1;
        options.Period = TimeSpan.FromHours(1);
        options.QueueLimit = 0;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AccessControl:AllowlistedUserIds:0"] = "user-1"
            })
            .Build();
        services.SetupRateLimiting(configuration, options);

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        var context = CreateAuthenticatedContext();

        using var first = await limiter.AcquireAsync(context, 1);
        using var second = await limiter.AcquireAsync(context, 1);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task SetupRateLimiting_RegistersTheWebhookCallbackPolicyPartitionedByIp()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 100;
        options.WebhookRequestsPerMinute = 2;
        options.WebhookWindow = TimeSpan.FromHours(1);
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints => endpoints
                        .MapGet("/api/v1/billing/webhooks/stripe", () => Results.Ok())
                        .RequireRateLimiting(RateLimitPolicies.Webhook));
                }))
            .StartAsync();

        using var client = host.GetTestClient();
        using var first = await client.GetAsync("/api/v1/billing/webhooks/stripe");
        using var second = await client.GetAsync("/api/v1/billing/webhooks/stripe");
        using var third = await client.GetAsync("/api/v1/billing/webhooks/stripe");

        first.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        second.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        third.StatusCode.Should().Be(System.Net.HttpStatusCode.TooManyRequests,
            "the webhook policy must throttle floods on provider callbacks before signature verification");
    }

    [Fact]
    public void RateLimitingOptions_RejectInvalidWebhookPolicySettings()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.WebhookRequestsPerMinute = 0;

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*WebhookRequestsPerMinute*");
    }

    [Fact]
    public void RateLimitingOptions_ReserveTheWebhookPolicyNameForTheBuiltInPolicy()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Policies[RateLimitPolicies.Webhook] = new RateLimitPolicyOptions();

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*reserved*");
    }

    [Fact]
    public void SetupRateLimiting_RejectsInvalidAccessControlIpAddress()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AccessControl:DenylistedIpAddresses:0"] = "not-an-ip"
            })
            .Build();
        var services = new ServiceCollection();

        var act = () => services.SetupRateLimiting(configuration, RateLimitingOptions.CreateDefault());

        act.Should().Throw<InvalidOperationException>().WithMessage("*DenylistedIpAddresses*invalid IP address*");
    }

    [Fact]
    public async Task SetupRateLimiting_ExemptPathsBypassGlobalLimiterForPathAndChildren()
    {
        var services = new ServiceCollection();
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 1;
        options.Period = TimeSpan.FromHours(1);
        options.QueueLimit = 0;
        options.ExemptPaths = ["/health"];
        services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;

        using var normalFirst = await limiter.AcquireAsync(CreateRequest("192.0.2.22", "/api/items"), 1);
        using var normalBlocked = await limiter.AcquireAsync(CreateRequest("192.0.2.22", "/api/items"), 1);
        using var healthOne = await limiter.AcquireAsync(CreateRequest("192.0.2.23", "/health"), 1);
        using var healthTwo = await limiter.AcquireAsync(CreateRequest("192.0.2.23", "/health/ready"), 1);

        normalFirst.IsAcquired.Should().BeTrue();
        normalBlocked.IsAcquired.Should().BeFalse();
        healthOne.IsAcquired.Should().BeTrue();
        healthTwo.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void RejectionHeaders_ReportConfiguredLimitAndRetryWindow()
    {
        var context = new DefaultHttpContext();

        RateLimitingServiceCollectionExtensions.SetRateLimitHeaders(context.Response, 17, 4.2);

        context.Response.Headers.RetryAfter.ToString().Should().Be("5");
        context.Response.Headers["RateLimit-Limit"].ToString().Should().Be("17");
        context.Response.Headers["RateLimit-Remaining"].ToString().Should().Be("0");
        context.Response.Headers["RateLimit-Reset"].ToString().Should().Be("5");
    }

    [Fact]
    public void SetupRateLimiting_RejectsNonPositiveGlobalLimit()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Limit = 0;
        var services = new ServiceCollection();

        var act = () => services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*global rate limit*");
    }

    [Fact]
    public void SetupRateLimiting_RejectsNonPositiveNamedPolicyLimit()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.UserRequestsPerMinute = 0;
        var services = new ServiceCollection();

        var act = () => services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*UserRequestsPerMinute*");
    }

    [Fact]
    public void SetupRateLimiting_RejectsInvalidWindowAndSlidingSegments()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.ApiWindow = TimeSpan.Zero;
        options.SlidingWindowSegments = 0;
        var services = new ServiceCollection();

        var act = () => services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiWindow*");
    }

    [Fact]
    public void SetupRateLimiting_RejectsRedisWindowBelowMillisecondPrecision()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.ApiWindow = TimeSpan.FromTicks(1);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Enabled"] = "true" })
            .Build();
        var services = new ServiceCollection();

        var act = () => services.SetupRateLimiting(configuration, options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiWindow*at least one millisecond*");
    }

    [Fact]
    public void SetupRateLimiting_RejectsRedisTokenBucketPeriodBelowMillisecondPrecision()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.TokenReplenishmentPeriod = TimeSpan.FromTicks(1);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Enabled"] = "true" })
            .Build();
        var services = new ServiceCollection();

        var act = () => services.SetupRateLimiting(configuration, options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TokenReplenishmentPeriod*at least one millisecond*");
    }

    [Fact]
    public void SetupRateLimiting_RejectsMalformedExemptPath()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.ExemptPaths = ["health"];
        var services = new ServiceCollection();

        var act = () => services.SetupRateLimiting(new ConfigurationBuilder().Build(), options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*exempt paths*");
    }

    [Fact]
    public void RateLimitingOptionsBuilder_BindsConfiguredPolicyWindowsAndLimits()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:IpRequestsPerMinute"] = "45",
                ["RateLimiting:IpWindow"] = "00:00:30",
                ["RateLimiting:SlidingWindowSegments"] = "3",
                ["RateLimiting:InternalRequestsPerMinute"] = "250",
                ["RateLimiting:InternalWindow"] = "01:00:00",
                ["RateLimiting:TrustedProxyAddresses:0"] = "192.0.2.50",
                ["RateLimiting:TrustedProxyForwardLimit"] = "2",
                ["RateLimiting:Policies:reports:Algorithm"] = "TokenBucket",
                ["RateLimiting:Policies:reports:PartitionBy"] = "Endpoint",
                ["RateLimiting:Policies:reports:PermitLimit"] = "12",
                ["RateLimiting:Policies:reports:Window"] = "00:00:30",
                ["RateLimiting:Policies:reports:TokensPerPeriod"] = "3"
            })
            .Build();

        var options = RateLimitingOptionsBuilder.Create(configuration).Build();

        options.IpRequestsPerMinute.Should().Be(45);
        options.IpWindow.Should().Be(TimeSpan.FromSeconds(30));
        options.SlidingWindowSegments.Should().Be(3);
        options.InternalRequestsPerMinute.Should().Be(250);
        options.InternalWindow.Should().Be(TimeSpan.FromHours(1));
        options.TrustedProxyAddresses.Should().ContainSingle().Which.Should().Be("192.0.2.50");
        options.TrustedProxyForwardLimit.Should().Be(2);
        options.Policies.Should().ContainKey("reports");
        options.Policies["reports"].Algorithm.Should().Be(RateLimitingAlgorithm.TokenBucket);
        options.Policies["reports"].PartitionBy.Should().Be(RateLimitPartitionStrategy.Endpoint);
        options.Policies["reports"].PermitLimit.Should().Be(12);
        options.Policies["reports"].Window.Should().Be(TimeSpan.FromSeconds(30));
        options.Policies["reports"].TokensPerPeriod.Should().Be(3);
    }

    private static DefaultHttpContext CreateAuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            authenticationType: "test"));

        return context;
    }

    private static DefaultHttpContext CreateRequest(string remoteIpAddress, string path = "/")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
        context.Request.Path = path;
        return context;
    }
}
