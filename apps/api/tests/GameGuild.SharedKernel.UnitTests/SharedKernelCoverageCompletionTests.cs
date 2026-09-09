using System.Text;
using System.Text.Json;
using System.Reflection;
using FluentValidation;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Configuration.InfrastructureLayer.MemoryCaching;
using GameGuild.Configuration.PresentationLayer;
using GameGuild.Configuration.PresentationLayer.ApiExplorer;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;
using GameGuild.Configuration.PresentationLayer.Authentication;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Configuration.PresentationLayer.CORS;
using GameGuild.Configuration.PresentationLayer.Controllers;
using GameGuild.Configuration.PresentationLayer.Endpoints;
using GameGuild.Configuration.PresentationLayer.FeatureFlags;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Configuration.PresentationLayer.HealthChecks;
using GameGuild.Configuration.PresentationLayer.HttpLogging;
using GameGuild.Configuration.PresentationLayer.Localization;
using GameGuild.Configuration.PresentationLayer.ModelValidation;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using GameGuild.Configuration.PresentationLayer.ProblemDetails;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Configuration.PresentationLayer.RequestContext;
using GameGuild.Configuration.PresentationLayer.ResponseCaching;
using GameGuild.Configuration.PresentationLayer.ResponseCompression;
using GameGuild.Configuration.PresentationLayer.SignalR;
using GameGuild.CQRS;
using GameGuild.CQRS.Implementation;
using GameGuild.CQRS.Publishers;
using GameGuild.Email;
using Moq;

namespace GameGuild.SharedKernel.UnitTests;

public class JsonValueDictionaryCoverageTests
{
    [Fact]
    public void ToJsonElements_WithNullAndEmptySources_ReturnsEmptyOrdinalDictionaries()
    {
        JsonValueDictionary.ToJsonElements(null).Should().BeEmpty();
        JsonValueDictionary.ToJsonElements(new Dictionary<string, object?>()).Should().BeEmpty();
    }

    [Fact]
    public void ToJsonElements_ClonesJsonElementsAndSerializesRuntimeValues()
    {
        using var document = JsonDocument.Parse("""{"nested":{"value":5}}""");
        var source = new Dictionary<string, object?>
        {
            ["element"] = document.RootElement.GetProperty("nested"),
            ["text"] = "hello",
            ["null"] = null,
            ["number"] = 42
        };

        var result = JsonValueDictionary.ToJsonElements(source);

        result["element"].GetProperty("value").GetInt32().Should().Be(5);
        result["text"].GetString().Should().Be("hello");
        result["null"].ValueKind.Should().Be(JsonValueKind.Null);
        result["number"].GetInt32().Should().Be(42);
    }

    [Fact]
    public void ToObjects_ConvertsEveryJsonValueKindAndNumberShape()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "string":"value",
              "int":12,
              "long":9223372036854775807,
              "decimal":79228162514264337593543950335,
              "double":1e100,
              "trueValue":true,
              "falseValue":false,
              "nullValue":null,
              "object":{"child":"x"},
              "array":[1,"two",false]
            }
            """);

        var source = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        source["undefined"] = default;

        var result = JsonValueDictionary.ToObjects(source);

        result["string"].Should().Be("value");
        result["int"].Should().Be(12);
        result["long"].Should().Be(long.MaxValue);
        result["decimal"].Should().Be(decimal.MaxValue);
        result["double"].Should().BeOfType<double>();
        result["trueValue"].Should().Be(true);
        result["falseValue"].Should().Be(false);
        result["nullValue"].Should().BeNull();
        result["undefined"].Should().BeNull();
        result["object"].Should().BeAssignableTo<Dictionary<string, object?>>();
        result["array"].Should().BeAssignableTo<List<object?>>();
    }

    [Fact]
    public void ToObjects_WithNullAndEmptySources_ReturnsEmptyOrdinalDictionaries()
    {
        JsonValueDictionary.ToObjects(null).Should().BeEmpty();
        JsonValueDictionary.ToObjects(new Dictionary<string, JsonElement>()).Should().BeEmpty();
    }

    [Fact]
    public void GetObjectMap_ReturnsEmptyForMissingOrNonObjectAndClonesObjectProperties()
    {
        using var document = JsonDocument.Parse("""{"payload":{"one":1},"other":true}""");
        var source = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

        JsonValueDictionary.GetObjectMap(null, "payload").Should().BeEmpty();
        JsonValueDictionary.GetObjectMap(source, "missing").Should().BeEmpty();
        JsonValueDictionary.GetObjectMap(source, "other").Should().BeEmpty();

        var result = JsonValueDictionary.GetObjectMap(source, "PAYLOAD");
        result.Should().ContainKey("one");
        result["one"].GetInt32().Should().Be(1);
    }

    [Fact]
    public void GetString_HandlesDefaultsCaseInsensitiveKeysAndNonStringValues()
    {
        using var document = JsonDocument.Parse("""{"name":"Ada","count":7,"nil":null}""");
        var source = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        source["undefined"] = default;

        JsonValueDictionary.GetString(null, "name", "fallback").Should().Be("fallback");
        JsonValueDictionary.GetString(source, "missing", "fallback").Should().Be("fallback");
        JsonValueDictionary.GetString(source, "nil", "fallback").Should().Be("fallback");
        JsonValueDictionary.GetString(source, "undefined", "fallback").Should().Be("fallback");
        JsonValueDictionary.GetString(source, "NAME").Should().Be("Ada");
        JsonValueDictionary.GetString(source, "count").Should().Be("7");
    }
}

public class MemoryCacheServicePatternCompletionTests
{
    [Fact]
    public async Task RemoveByPatternAsync_Covers_Exact_All_And_Backtracking_Wildcard_Branches()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new MemoryCacheService(memoryCache);

        await service.SetAsync("alpha:one", 1, TimeSpan.FromMinutes(5));
        await service.SetAsync("alpha:two", 2, TimeSpan.FromMinutes(5));
        await service.SetAsync("beta:one", 3, TimeSpan.FromMinutes(5));
        await service.SetAsync("alphabet", 4, TimeSpan.FromMinutes(5));
        await service.SetAsync("literal", 5, TimeSpan.FromMinutes(5));

        (await service.RemoveByPatternAsync("missing")).Should().Be(0);
        (await service.RemoveByPatternAsync("literal")).Should().Be(1);
        (await service.RemoveByPatternAsync("alpha:t?o")).Should().Be(1);
        (await service.RemoveByPatternAsync("a*one")).Should().Be(1);
        (await service.RemoveByPatternAsync("*one")).Should().Be(1);
        (await service.RemoveByPatternAsync("*")).Should().Be(1);
    }

    [Theory]
    [InlineData("", "*", true)]
    [InlineData("", "?", false)]
    [InlineData("abc", "a?c", true)]
    [InlineData("abc", "a?d", false)]
    [InlineData("abc", "a*c", true)]
    [InlineData("abc", "a*d", false)]
    [InlineData("abc", "*c", true)]
    [InlineData("abc", "*d", false)]
    [InlineData("abc", "abc*", true)]
    [InlineData("abc", "????", false)]
    public void MatchesPattern_PrivateHelper_Covers_Backtracking_And_TrailingWildcard_Branches(
        string key,
        string pattern,
        bool expected)
    {
        typeof(MemoryCacheService)
            .GetMethod("MatchesPattern", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [key, pattern])
            .Should()
            .Be(expected);
    }
}

public class JwtOptionsResolverCoverageTests
{
    private const string ValidSecret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void CreateValidated_WithNullConfiguration_Throws()
    {
        Action act = () => JwtOptionsResolver.CreateValidated(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateValidated_UsesPrimaryKeysAndParsesValues()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:Secret"] = ValidSecret,
            ["Jwt:AccessTokenExpirationMinutes"] = "15",
            ["Jwt:RefreshTokenExpirationDays"] = "7",
            ["Jwt:ClockSkewSeconds"] = "3",
            ["Jwt:ValidateIssuer"] = "false",
            ["Jwt:ValidateAudience"] = "false",
            ["Jwt:ValidateLifetime"] = "false",
            ["Jwt:ValidateIssuerSigningKey"] = "false"
        });

        var options = JwtOptionsResolver.CreateValidated(configuration);

        options.Issuer.Should().Be("issuer");
        options.Audience.Should().Be("audience");
        options.SecretKey.Should().Be(ValidSecret);
        options.AccessTokenExpirationMinutes.Should().Be(15);
        options.RefreshTokenExpirationDays.Should().Be(7);
        options.ClockSkewSeconds.Should().Be(3);
        options.ValidateIssuer.Should().BeFalse();
        options.ValidateAudience.Should().BeFalse();
        options.ValidateLifetime.Should().BeFalse();
        options.ValidateIssuerSigningKey.Should().BeFalse();
    }

    [Fact]
    public void CreateValidated_UsesFallbackKeysAndDefaultsForInvalidPrimitiveValues()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = " ",
            ["JwtSettings:Issuer"] = "fallback-issuer",
            ["JwtSettings:Audience"] = "fallback-audience",
            ["Authentication:JwtSecretKey"] = ValidSecret,
            ["Jwt:AccessTokenExpirationMinutes"] = "not-int",
            ["Jwt:ValidateIssuer"] = "not-bool"
        });

        var options = JwtOptionsResolver.CreateValidated(configuration);

        options.Issuer.Should().Be("fallback-issuer");
        options.Audience.Should().Be("fallback-audience");
        options.AccessTokenExpirationMinutes.Should().Be(60);
        options.ValidateIssuer.Should().BeTrue();
    }

    [Fact]
    public void CreateValidated_WithInvalidOptions_ThrowsConfigurationError()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:Secret"] = "too-short"
        });

        Action act = () => JwtOptionsResolver.CreateValidated(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("JWT configuration is invalid:*");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}

public class RepositoryBaseCoverageTests
{
    [Fact]
    public async Task RepositoryBase_CoversReadWriteDeleteAndPagingPaths()
    {
        await using var context = new TestRepositoryDbContext(NewDbOptions());
        var repository = new TestRepository(context);
        var first = new RepositoryEntity { Name = "first", Version = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-2) };
        var second = new RepositoryEntity { Name = "second", Version = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-1) };

        await repository.AddAsync(first);
        await repository.AddRangeAsync([second]);
        await repository.SaveChangesAsync();

        (await repository.GetByIdAsync(first.Id)).Should().BeSameAs(first);
        (await repository.GetAllAsync()).Should().ContainInOrder(second, first);
        (await repository.GetPagedAsync(1, 1)).Items.Should().ContainSingle().Which.Should().Be(second);
        (await repository.FindAsync(entity => entity.Name.Contains("ir"))).Should().ContainSingle().Which.Should().Be(first);
        (await repository.FirstOrDefaultAsync(entity => entity.Name == "second")).Should().Be(second);
        (await repository.AnyAsync(entity => entity.Name == "first")).Should().BeTrue();
        (await repository.CountAsync()).Should().Be(2);
        (await repository.CountAsync(entity => entity.Name == "first")).Should().Be(1);

        first.Name = "updated";
        (await repository.UpdateAsync(first)).Should().BeSameAs(first);
        await repository.UpdateRangeAsync([first, second]);

        await repository.SoftDeleteAsync(first.Id);
        first.IsDeleted.Should().BeTrue();
        await repository.RestoreAsync(first.Id);
        first.IsDeleted.Should().BeFalse();

        var third = new RepositoryEntity { Name = "third", Version = 1 };
        await repository.AddAsync(third);
        await repository.SaveChangesAsync();
        await repository.RemoveAsync(third);

        await repository.RemoveAsync(second.Id);
        await repository.RemoveRangeAsync([first]);
        await repository.SaveChangesAsync();
        (await repository.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RepositoryBase_RemoveAndSoftDeleteMissingEntities_Throw()
    {
        await using var context = new TestRepositoryDbContext(NewDbOptions());
        var repository = new TestRepository(context);

        await repository.Invoking(r => r.RemoveAsync(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found.");
        await repository.Invoking(r => r.SoftDeleteAsync(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found for soft-delete.");
        await repository.Invoking(r => r.RestoreAsync(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found for restore.");
    }

    [Fact]
    public async Task RepositoryBase_GuidConvenienceBase_UsesGuidKey()
    {
        await using var context = new TestRepositoryDbContext(NewDbOptions());
        var repository = new GuidTestRepository(context);

        var entity = await repository.AddAsync(new RepositoryEntity { Name = "guid", Version = 1 });

        entity.Id.Should().NotBeEmpty();
    }

    private static DbContextOptions<TestRepositoryDbContext> NewDbOptions()
        => new DbContextOptionsBuilder<TestRepositoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private sealed class TestRepositoryDbContext(DbContextOptions<TestRepositoryDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<RepositoryEntity> RepositoryEntities => Set<RepositoryEntity>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestRepository(IApplicationDbContext context) : RepositoryBase<RepositoryEntity, Guid>(context);

    private sealed class GuidTestRepository(IApplicationDbContext context) : RepositoryBase<RepositoryEntity>(context);

    private sealed class RepositoryEntity : EntityBase
    {
        public string Name { get; set; } = string.Empty;
    }
}

public class CacheEmailAndBuilderCoverageTests
{
    [Fact]
    public async Task DistributedCacheService_GetsSetsAndRemovesValues()
    {
        var cache = new Mock<IDistributedCache>();
        var stored = Encoding.UTF8.GetBytes("""{"name":"Ada"}""");
        byte[]? capturedSetBytes = null;
        DistributedCacheEntryOptions? capturedOptions = null;
        cache.Setup(c => c.GetAsync("hit", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        cache.Setup(c => c.GetAsync("miss", It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        cache.Setup(c => c.SetAsync("set", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, bytes, options, _) =>
            {
                capturedSetBytes = bytes;
                capturedOptions = options;
            })
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveAsync("remove", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new DistributedCacheService(cache.Object);

        var hit = await service.GetAsync<CachedPayload>("hit");
        var miss = await service.GetAsync<CachedPayload>("miss");
        await service.SetAsync("set", new CachedPayload("Grace"), TimeSpan.FromMinutes(5));
        await service.RemoveAsync("remove");

        hit.Should().BeEquivalentTo(new CachedPayload("Ada"));
        miss.Should().BeNull();
        Encoding.UTF8.GetString(capturedSetBytes!).Should().Contain("Grace");
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(5));
        cache.Verify(c => c.RemoveAsync("remove", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DistributedCacheService_RejectsBlankKeys(string key)
    {
        var service = new DistributedCacheService(Mock.Of<IDistributedCache>());

        await service.Invoking(s => s.GetAsync<CachedPayload>(key)).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(s => s.SetAsync(key, new CachedPayload("x"), TimeSpan.FromSeconds(1))).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(s => s.RemoveAsync(key)).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(s => s.RemoveByPatternAsync(key)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DistributedCacheService_RemoveByPatternAsync_ExactKeyRemovesValue()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.RemoveAsync("tenant:1", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new DistributedCacheService(cache.Object);

        var removed = await service.RemoveByPatternAsync("tenant:1");

        removed.Should().Be(1);
        cache.Verify(c => c.RemoveAsync("tenant:1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("tenant:*")]
    [InlineData("tenant:?")]
    public async Task DistributedCacheService_RemoveByPatternAsync_WildcardThrowsNotSupported(string pattern)
    {
        var service = new DistributedCacheService(Mock.Of<IDistributedCache>());

        await service.Invoking(s => s.RemoveByPatternAsync(pattern))
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*key enumeration*");
    }

    [Fact]
    public void EmailDeliveryOptionsAndMessage_ExposeConfiguredValues()
    {
        var options = new EmailDeliveryOptions
        {
            Enabled = true,
            FromEmail = "from@example.com",
            FromName = "GameGuild",
            Ses = { Region = "us-east-1" },
            Events = { TopicArn = "arn:aws:sns:us-east-1:000000000000:email-events" }
        };
        var attachment = new EmailAttachment("statement.pdf", "application/pdf", [1, 2, 3]);
        var message = new EmailMessage(
            "to@example.com",
            "Subject",
            "plain",
            "<b>html</b>",
            "Ada",
            [attachment]);

        options.Enabled.Should().BeTrue();
        options.FromEmail.Should().Be("from@example.com");
        options.FromName.Should().Be("GameGuild");
        options.Ses.Region.Should().Be("us-east-1");
        options.Events.TopicArn.Should().Be("arn:aws:sns:us-east-1:000000000000:email-events");
        message.ToEmail.Should().Be("to@example.com");
        message.Subject.Should().Be("Subject");
        message.PlainTextContent.Should().Be("plain");
        message.HtmlContent.Should().Be("<b>html</b>");
        message.ToName.Should().Be("Ada");
        message.Attachments.Should().ContainSingle().Which.Should().Be(attachment);
    }

    [Fact]
    public void SmallOptionBuilders_CreateBindBuildAndRejectNulls()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://example.com",
                ["RateLimiting:RequestsPerMinute"] = "120"
            })
            .Build();

        CorsOptionsBuilder.Create().AllowedOrigins.Should().BeEmpty();
        CorsOptionsBuilder.Create(configuration).AllowedOrigins.Should().Contain("https://example.com");
        new CorsOptions { AllowedOrigins = ["*"], AllowedMethods = ["GET"], AllowedHeaders = ["*"] }.Build().Should().NotBeNull();
        FluentActions.Invoking(() => CorsOptionsBuilder.Build(null!)).Should().Throw<ArgumentNullException>();

        RateLimitingOptionsBuilder.Create().RequestsPerMinute.Should().Be(60);
        RateLimitingOptionsBuilder.Create(configuration).RequestsPerMinute.Should().Be(120);
        new RateLimitingOptions { RequestsPerMinute = 1, BurstSize = 1 }.Build().Should().NotBeNull();
        FluentActions.Invoking(() => RateLimitingOptionsBuilder.Build(null!)).Should().Throw<ArgumentNullException>();

        ControllersOptionsBuilder.CreateWithValidation(new ConfigurationBuilder().Build()).Should().NotBeNull();
        EndpointsOptionsBuilder.CreateWithValidation(new ConfigurationBuilder().Build()).Should().NotBeNull();
    }

    [Fact]
    public void ModuleExtensions_AddAndMapConcreteModules()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var endpoints = new Mock<IEndpointRouteBuilder>();

        services.AddModule<TestModuleForExtensions>(configuration);
        endpoints.Object.UseModule<TestModuleForExtensions>().Should().BeSameAs(endpoints.Object);

        TestModuleForExtensions.ServicesConfigured.Should().BeTrue();
        TestModuleForExtensions.EndpointsMapped.Should().BeTrue();
    }

    [Fact]
    public void EndpointAndPaginationExtensions_RegisterComponents()
    {
        var services = new ServiceCollection();

        services.AddEndpoints(typeof(TestEndpoint).Assembly);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEndpoint) &&
            descriptor.ImplementationType == typeof(TestEndpoint));
        FluentActions.Invoking(() => EndpointExtensions.AddEndpoints(services, null!))
            .Should().Throw<ArgumentNullException>();

        services.AddControllers().AddPaginationHeaders();
        var provider = services.BuildServiceProvider();
        var filters = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MvcOptions>>()
            .Value.Filters;
        filters.Any(filter => filter is TypeFilterAttribute typeFilter &&
                              typeFilter.ImplementationType == typeof(PaginationHeadersFilter)).Should().BeTrue();
    }

    [Fact]
    public async Task PaginationHeadersFilter_AddsHeadersForPagedResults()
    {
        var page = PagedResult<string>.FromPage(["b"], totalCount: 3, pageNumber: 2, pageSize: 1);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        httpContext.Request.Path = "/items";
        httpContext.Request.QueryString = new QueryString("?skip=1&take=1&filter=active&page=2&pageSize=1");
        var actionContext = new ActionContext(httpContext, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var executingContext = new ResultExecutingContext(actionContext, [], new ObjectResult(page), controller: new object());
        var executedContext = new ResultExecutedContext(actionContext, [], new ObjectResult(page), controller: new object());
        var filter = new PaginationHeadersFilter();

        await filter.OnResultExecutionAsync(executingContext, () => Task.FromResult(executedContext));

        httpContext.Response.Headers["X-Pagination"].ToString().Should().Contain("\"totalCount\":3");
        httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("3");
        httpContext.Response.Headers["Link"].ToString().Should().Contain("filter=active");
        httpContext.Response.Headers["Link"].ToString().Should().Contain("rel=\"prev\"");
        httpContext.Response.Headers["Link"].ToString().Should().Contain("rel=\"next\"");
    }

    private sealed record CachedPayload(string Name);

    public sealed class TestEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) { }
    }

    public sealed class TestModuleForExtensions : IModule
    {
        public static bool ServicesConfigured { get; private set; }
        public static bool EndpointsMapped { get; private set; }
        public string Name => nameof(TestModuleForExtensions);
        public IReadOnlyList<Type> Dependencies => [];
        public bool EnabledByDefault => true;

        public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            ServicesConfigured = true;
            return services;
        }

        public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            EndpointsMapped = true;
            return endpoints;
        }
    }
}

public class CqrsMediatorCoverageCompletionTests
{
    [Fact]
    public async Task MediatorSender_WithPipelineBehaviors_ExecutesNonNullBehaviorsInOrder()
    {
        var handler = new PipelineRequestHandler();
        var behaviors = new IPipelineBehavior<PipelineRequest, string>?[]
        {
            null,
            new WrappingBehavior("outer"),
            new WrappingBehavior("inner")
        };
        var sender = new MediatorSender(type =>
        {
            if (type == typeof(IRequestHandler<PipelineRequest, string>)) return handler;
            if (type == typeof(IEnumerable<IPipelineBehavior<PipelineRequest, string>>)) return behaviors;
            return null;
        });

        var result = await sender.Send(new PipelineRequest("value"));

        result.Should().Be("outer(inner(value))");
    }

    [Fact]
    public async Task MediatorSender_WithNullBehaviorEnumerable_UsesTerminalHandler()
    {
        var sender = new MediatorSender(type =>
            type == typeof(IRequestHandler<PipelineRequest, string>)
                ? new PipelineRequestHandler()
                : null);

        var result = await sender.Send(new PipelineRequest("direct"));

        result.Should().Be("direct");
    }

    [Fact]
    public async Task MediatorSender_DynamicUnitRequest_ReturnsUnit()
    {
        var handler = new UnitCommandHandler();
        var sender = new MediatorSender(type =>
            type == typeof(IRequestHandler<UnitCommand, Unit>)
                ? handler
                : null);

        var result = await sender.Send((object)new UnitCommand());

        result.Should().Be(Unit.Value);
        handler.Called.Should().BeTrue();
    }

    [Fact]
    public async Task MediatorSender_InvalidDynamicRequestAndMissingHandler_Throw()
    {
        var sender = new MediatorSender(_ => null);

        await sender.Invoking(s => s.Send(new PipelineRequest("missing")))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler not found*");

        await sender.Invoking(s => s.Send(new object()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No handler found*");
    }

    [Fact]
    public async Task MediatorSender_WithInvalidPipelineBehavior_ThrowsHelpfulError()
    {
        var sender = new MediatorSender(type =>
        {
            if (type == typeof(IEnumerable<IPipelineBehavior<PipelineRequest, string>>)) return new object[] { new InvalidBehaviorWithoutHandle() };
            if (type == typeof(IRequestHandler<PipelineRequest, string>)) return new PipelineRequestHandler();
            return null;
        });

        await sender.Invoking(s => s.Send(new PipelineRequest("x")))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not have a Handle method*");
    }

    [Fact]
    public async Task MediatorFacade_DelegatesSendAndPublishPaths()
    {
        var handler = new PipelineRequestHandler();
        var notificationHandler = new TestNotificationHandler();
        var publisher = new CapturingNotificationPublisher();
        var mediator = new Mediator(type =>
        {
            if (type == typeof(IRequestHandler<PipelineRequest, string>)) return handler;
            if (type == typeof(IEnumerable<INotificationHandler<TestNotification>>)) return new[] { notificationHandler };
            return null;
        }, publisher);

        (await mediator.Send(new PipelineRequest("facade"))).Should().Be("facade");
        (await mediator.Send((object)new PipelineRequest("dynamic"))).Should().Be("dynamic");
        await mediator.Publish(new TestNotification());
        await mediator.Publish((object)new TestNotification());

        publisher.PublishCount.Should().Be(2);
        notificationHandler.Calls.Should().Be(2);
    }

    [Fact]
    public void MediatorSender_CreateCachedInvoker_ReturnsNullForUnsupportedMethods()
    {
        var create = typeof(MediatorSender).GetMethod("CreateCachedInvoker", BindingFlags.NonPublic | BindingFlags.Static)!;

        var voidResult = create.Invoke(null, [typeof(UnsupportedInvokerMethods).GetMethod(nameof(UnsupportedInvokerMethods.VoidMethod))!, typeof(Unit)]);

        voidResult.Should().BeNull();
    }

    [Fact]
    public async Task MediatorFacade_DefaultPublisherAndUnitSendAreCovered()
    {
        var handler = new UnitCommandHandler();
        var mediator = new Mediator(type =>
            type == typeof(IRequestHandler<UnitCommand, Unit>)
                ? handler
                : null);

        await mediator.Send(new UnitCommand());

        handler.Called.Should().BeTrue();
    }

    [Fact]
    public async Task ValidationAndObservabilityBehaviors_CoverSuccessFailureSlowAndExceptionPaths()
    {
        var validBehavior = new ValidationBehavior<PipelineRequest, string>([]);
        (await validBehavior.Handle(new PipelineRequest("ok"), () => Task.FromResult("next"), CancellationToken.None))
            .Should().Be("next");
        await validBehavior.Invoking(b => b.Handle(new PipelineRequest("ok"), null!, CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();

        var invalidBehavior = new ValidationBehavior<PipelineRequest, string>([new PipelineRequestValidator()]);
        await invalidBehavior.Invoking(b => b.Handle(new PipelineRequest(""), () => Task.FromResult("next"), CancellationToken.None))
            .Should().ThrowAsync<RequestValidationException>();

        var logger = NullLogger<ObservabilityBehavior<PipelineRequest, string>>.Instance;
        var fast = new ObservabilityBehavior<PipelineRequest, string>(logger, Options.Create(new ObservabilityOptions { WarningThresholdMs = 1000 }));
        (await fast.Handle(new PipelineRequest("fast"), () => Task.FromResult("fast"), CancellationToken.None)).Should().Be("fast");

        var slow = new ObservabilityBehavior<PipelineRequest, string>(logger, Options.Create(new ObservabilityOptions { WarningThresholdMs = 0 }));
        (await slow.Handle(new PipelineRequest("slow"), async () => { await Task.Delay(2); return "slow"; }, CancellationToken.None)).Should().Be("slow");

        await slow.Invoking(b => b.Handle(new PipelineRequest("fail"), () => throw new InvalidOperationException("boom"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ValidationResultAndRequestValidationException_CoverFactoriesAndConstructors()
    {
        ValidationResult.Success().IsValid.Should().BeTrue();
        var error = new GameGuild.CQRS.ValidationError("Name", "Required", null);
        var failure = ValidationResult.Failure(error);
        failure.IsValid.Should().BeFalse();
        failure.Errors.Should().ContainSingle().Which.Should().Be(error);

        new RequestValidationException().Errors.Should().BeEmpty();
        new RequestValidationException("custom").Message.Should().Be("custom");
        new RequestValidationException("custom", new InvalidOperationException()).InnerException.Should().BeOfType<InvalidOperationException>();
        new RequestValidationException([error]).Errors.Should().ContainSingle();
    }

    private sealed record PipelineRequest(string Value) : IRequest<string>;

    private sealed class PipelineRequestHandler : IRequestHandler<PipelineRequest, string>
    {
        public Task<string> Handle(PipelineRequest request, CancellationToken cancellationToken) => Task.FromResult(request.Value);
    }

    private sealed class WrappingBehavior(string label) : IPipelineBehavior<PipelineRequest, string>
    {
        public async Task<string> Handle(PipelineRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
            => $"{label}({await next()})";
    }

    private sealed class InvalidBehaviorWithoutHandle;

    private sealed record UnitCommand : IRequest;

    private sealed class UnitCommandHandler : IRequestHandler<UnitCommand>
    {
        public bool Called { get; private set; }

        public Task<Unit> Handle(UnitCommand request, CancellationToken cancellationToken)
        {
            Called = true;
            return Unit.Task;
        }
    }

    private sealed class PipelineRequestValidator : AbstractValidator<PipelineRequest>
    {
        public PipelineRequestValidator()
        {
            RuleFor(request => request.Value).NotEmpty();
        }
    }

    private sealed class TestNotification : INotification;

    private sealed class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public int Calls { get; private set; }

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingNotificationPublisher : INotificationPublisher
    {
        public int PublishCount { get; private set; }

        public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        {
            PublishCount++;
            foreach (var executor in handlerExecutors)
            {
                await executor.ExecuteHandler(notification, cancellationToken);
            }
        }
    }

    private static class UnsupportedInvokerMethods
    {
        public static void VoidMethod() { }
    }
}

public class OptionsBranchCoverageCompletionTests
{
    [Fact]
    public void EncryptionOptions_CoversHexBase64FormatAndRotationBranches()
    {
        new EncryptionOptions { EncryptionKey = new string('A', 64), Algorithm = "AES256" }
            .Validate().IsValid.Should().BeTrue();
        new EncryptionOptions { EncryptionKey = new string('A', 32), Algorithm = "AES256" }
            .Validate().Errors.Should().Contain(e => e.Contains("hexadecimal"));
        new EncryptionOptions { EncryptionKey = new string('?', 32), Algorithm = "AES256" }
            .Validate().Errors.Should().Contain(e => e.Contains("valid base64"));
        new EncryptionOptions { EncryptionKey = new string('A', 64), Algorithm = "AES256", EnableKeyRotation = true, KeyRotationIntervalDays = 0 }
            .Validate().Errors.Should().Contain(e => e.Contains("KeyRotationIntervalDays"));
        new EncryptionOptions { EncryptionKey = new string('A', 64), Algorithm = "AES256", EnableKeyRotation = true, KeyRotationIntervalDays = 366 }
            .Validate().Errors.Should().Contain(e => e.Contains("KeyRotationIntervalDays"));
    }

    [Fact]
    public void ApplicationSecurityOptions_CoverLowerAndUpperValidationBranches()
    {
        AuthenticationSecurityOptions invalid = new()
        {
            MaxFailedAttemptsPerHour = 0,
            MaxFailedAttemptsPerDay = 0,
            MaxAttemptsPerIpPerHour = 0,
            AccountLockoutDurationMinutes = 0,
            EmailVerificationTokenValidityHours = 0,
            PasswordResetTokenValidityHours = 0,
            SuspiciousThreshold = 0
        };
        invalid.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(7);

        invalid = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 101,
            MaxFailedAttemptsPerDay = 501,
            MaxAttemptsPerIpPerHour = 1001,
            AccountLockoutDurationMinutes = 1441,
            EmailVerificationTokenValidityHours = 169,
            PasswordResetTokenValidityHours = 25,
            SuspiciousThreshold = 11
        };
        invalid.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(7);

        new AuthenticationSecurityOptions { MaxFailedAttemptsPerHour = 50, MaxFailedAttemptsPerDay = 20 }
            .Validate().Errors.Should().Contain(e => e.Contains("cannot exceed"));
    }

    [Fact]
    public void SessionMfaAndAnomalyOptions_CoverRangeBranches()
    {
        new GameGuild.Configuration.ApplicationLayer.SessionOptions
        {
            IdleTimeoutMinutes = 0,
            AbsoluteTimeoutMinutes = 0,
            MaxConcurrentSessions = 0,
            TrustedDeviceDurationDays = 0,
            MaxTrustedDevices = 0
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(5);
        new GameGuild.Configuration.ApplicationLayer.SessionOptions
        {
            IdleTimeoutMinutes = 10081,
            AbsoluteTimeoutMinutes = 43201,
            MaxConcurrentSessions = 101,
            TrustedDeviceDurationDays = 366,
            MaxTrustedDevices = 51
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(5);
        new GameGuild.Configuration.ApplicationLayer.SessionOptions { IdleTimeoutMinutes = 60, AbsoluteTimeoutMinutes = 30 }
            .Validate().Errors.Should().Contain(e => e.Contains("cannot be greater"));

        new MfaOptions
        {
            MaxFailedAttempts = 0,
            LockoutDurationMinutes = 0,
            BackupCodesCount = 0,
            BackupCodeLength = 5,
            TotpTimeStepSeconds = 14,
            TotpClockSkew = -1,
            SetupSessionDurationMinutes = 0,
            TotpIssuer = ""
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(8);
        new MfaOptions
        {
            MaxFailedAttempts = 101,
            LockoutDurationMinutes = 1441,
            BackupCodesCount = 21,
            BackupCodeLength = 17,
            TotpTimeStepSeconds = 61,
            TotpClockSkew = 6,
            SetupSessionDurationMinutes = 61
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(7);

        new AuthenticationAnomalyOptions
        {
            MaxFailedAttemptsPerHour = 0,
            MaxFailedAttemptsPerDay = 0,
            SuspiciousThreshold = 0,
            ThrottleDurationMinutes = 0,
            MaxAttemptsPerIpPerHour = 9,
            MinTimeBetweenAttemptsSeconds = 0
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(6);
        new AuthenticationAnomalyOptions
        {
            MaxFailedAttemptsPerHour = 51,
            MaxFailedAttemptsPerDay = 201,
            SuspiciousThreshold = 11,
            ThrottleDurationMinutes = 1441,
            MaxAttemptsPerIpPerHour = 1001,
            MinTimeBetweenAttemptsSeconds = 301
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(6);
        new AuthenticationAnomalyOptions { EnableVelocityChecks = false, MinTimeBetweenAttemptsSeconds = 0 }
            .Validate().Errors.Should().BeEmpty();
    }

    [Fact]
    public void UserEnumerationAndJwtOptions_CoverRemainingValidationBranches()
    {
        new UserEnumerationProtectionOptions
        {
            MinProcessingTimeMs = 49,
            MaxProcessingTimeMs = 99,
            TargetProcessingTimeMs = 99,
            ConsistentErrorMessage = "",
            MaxJitterMs = -1
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(5);
        new UserEnumerationProtectionOptions
        {
            MinProcessingTimeMs = 2001,
            MaxProcessingTimeMs = 5001,
            TargetProcessingTimeMs = 3001,
            MaxJitterMs = 501
        }.Validate().Errors.Should().HaveCountGreaterThanOrEqualTo(4);
        new UserEnumerationProtectionOptions { MinProcessingTimeMs = 500, TargetProcessingTimeMs = 400 }
            .Validate().Errors.Should().Contain(e => e.Contains("MinProcessingTimeMs cannot"));
        new UserEnumerationProtectionOptions { TargetProcessingTimeMs = 900, MaxProcessingTimeMs = 800 }
            .Validate().Errors.Should().Contain(e => e.Contains("TargetProcessingTimeMs cannot"));
        new UserEnumerationProtectionOptions { EnableRandomJitter = false, MaxJitterMs = -1 }
            .Validate().Errors.Should().BeEmpty();

        new JwtOptions { Issuer = "", Audience = "", SecretKey = "", AccessTokenExpirationMinutes = 0, RefreshTokenExpirationDays = 0, ClockSkewSeconds = -1 }
            .Validate().Should().HaveCountGreaterThanOrEqualTo(6);
        new JwtOptions { SecretKey = "short" }.Validate().Should().Contain(e => e.Contains("at least 32"));
    }

    [Fact]
    public void AuthorizationOptions_CoverRemainingValidationBranches()
    {
        Assert.Throws<InvalidOperationException>(() => new AuthorizationCacheOptions { PermissionTtlSeconds = -1 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new AuthorizationCacheOptions { AccessControlListTtlSeconds = -1 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new AuthorizationCacheOptions { MaxL1CacheSize = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new AuthorizationCacheOptions { UseDistributedCache = true, RedisConnectionString = " " }.Validate());
        Assert.Throws<InvalidOperationException>(() => new AuthorizationCacheOptions { PolicyTtlSeconds = 500, DistributedCacheTtlSeconds = 100 }.Validate());

        Assert.Throws<InvalidOperationException>(() => new AuthorizationTokenOptions { TenantClaimType = "" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new AuthorizationTokenOptions { PermissionClaimType = "" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new TenancyOptions { DefaultTenantId = "" }.Validate());
    }

    [Fact]
    public void PresentationLayerOptions_Validate_CascadesEveryNestedOptionWhenPresent()
    {
        var options = PresentationLayerOptions.CreateDefault();
        options.Authentication = new AuthenticationOptions
        {
            JwtSecretKey = "0123456789abcdef0123456789abcdef",
            JwtIssuer = "issuer",
            JwtAudience = "audience"
        };

        options.Validate();

        options.Cors.Should().NotBeNull();
        options.Endpoints.Should().NotBeNull();
    }

    [Fact]
    public void BuilderUtilities_CoverNullMissingSectionAndInvalidBranches()
    {
        var empty = new ConfigurationBuilder().Build();
        var populated = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Localization:DefaultCulture"] = "pt-BR",
                ["Localization:SupportedCultures:0"] = "pt-BR",
                ["HealthChecks:HealthCheckPath"] = "/ready",
                ["OpenApi:Title"] = "API",
                ["OpenApi:Version"] = "v9",
                ["SignalR:MaximumReceiveMessageSize"] = "512",
                ["ResponseCaching:MaximumBodySize"] = "99",
                ["ApiExplorer:DefaultGroupName"] = "v2"
            })
            .Build();

        LocalizationOptionsBuilder.Build(populated).DefaultCulture.Should().Be("pt-BR");
        HealthChecksOptionsBuilder.Build(populated).HealthCheckPath.Should().Be("/ready");
        OpenApiOptionsBuilder.Build(populated).Title.Should().Be("API");
        SignalROptionsBuilder.Build(populated).MaximumReceiveMessageSize.Should().Be(512);
        ResponseCachingOptionsBuilder.Build(populated).MaximumBodySize.Should().Be(99);
        ApiExplorerOptionsBuilder.Build(populated).DefaultGroupName.Should().Be("v2");

        Assert.Throws<ArgumentNullException>(() => LocalizationOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => LocalizationOptionsBuilder.Validate(null!));
        Assert.Throws<InvalidOperationException>(() => LocalizationOptionsBuilder.Validate(new LocalizationOptions { DefaultCulture = "", SupportedCultures = ["en-US"] }));
        Assert.Throws<InvalidOperationException>(() => LocalizationOptionsBuilder.Validate(new LocalizationOptions { DefaultCulture = "en-US", SupportedCultures = [] }));
        Assert.Throws<InvalidOperationException>(() => LocalizationOptionsBuilder.Validate(new LocalizationOptions { DefaultCulture = "pt-BR", SupportedCultures = ["en-US"] }));

        Assert.Throws<ArgumentNullException>(() => HealthChecksOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => HealthChecksOptionsBuilder.Validate(null!));
        Assert.Throws<InvalidOperationException>(() => HealthChecksOptionsBuilder.Validate(new HealthChecksOptions { HealthCheckPath = "" }));
        Assert.Throws<InvalidOperationException>(() => HealthChecksOptionsBuilder.Validate(new HealthChecksOptions { HealthCheckPath = "health" }));
        Assert.Throws<InvalidOperationException>(() => HealthChecksOptionsBuilder.Validate(new HealthChecksOptions { HealthCheckPath = "/health", TimeoutSeconds = 0 }));

        Assert.Throws<ArgumentNullException>(() => OpenApiOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => OpenApiOptionsBuilder.Validate(null!));
        Assert.Throws<InvalidOperationException>(() => OpenApiOptionsBuilder.Validate(new OpenApiOptions { Title = "" }));
        Assert.Throws<InvalidOperationException>(() => OpenApiOptionsBuilder.Validate(new OpenApiOptions { Title = "API", Version = "" }));

        Assert.Throws<ArgumentNullException>(() => SignalROptionsBuilder.Validate(null!));
        Assert.Throws<InvalidOperationException>(() => SignalROptionsBuilder.Validate(new SignalROptions { KeepAliveInterval = TimeSpan.Zero, ClientTimeoutInterval = TimeSpan.FromSeconds(1), MaximumReceiveMessageSize = 1 }));
        Assert.Throws<InvalidOperationException>(() => SignalROptionsBuilder.Validate(new SignalROptions { KeepAliveInterval = TimeSpan.FromSeconds(1), ClientTimeoutInterval = TimeSpan.Zero, MaximumReceiveMessageSize = 1 }));
        Assert.Throws<InvalidOperationException>(() => SignalROptionsBuilder.Validate(new SignalROptions { KeepAliveInterval = TimeSpan.FromSeconds(1), ClientTimeoutInterval = TimeSpan.FromSeconds(1), MaximumReceiveMessageSize = 0 }));

        ResponseCachingOptionsBuilder.Create(empty).Should().NotBeNull();
        Assert.Throws<ArgumentNullException>(() => ResponseCachingOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ResponseCachingOptionsBuilder.Validate(null!));
        Assert.Throws<InvalidOperationException>(() => ResponseCachingOptionsBuilder.Validate(new ResponseCachingOptions { MaximumBodySize = -1 }));

        ApiExplorerOptionsBuilder.Create(empty).Should().NotBeNull();
        Assert.Throws<ArgumentNullException>(() => ApiExplorerOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ApiExplorerOptionsBuilder.Validate(null!));
        Assert.Throws<InvalidOperationException>(() => ApiExplorerOptionsBuilder.Validate(new ApiExplorerOptions { DefaultGroupName = "" }));

        GraphQLOptionsBuilder.Create(empty).Should().NotBeNull();
        Assert.Throws<ArgumentNullException>(() => GraphQLOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => GraphQLOptionsBuilder.Build(null!));

        FeatureFlagsOptionsBuilder.Create(empty).Should().NotBeNull();
        Assert.Throws<ArgumentNullException>(() => FeatureFlagsOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => FeatureFlagsOptionsBuilder.Build(null!));
        FeatureFlagsOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FeatureFlags:EnableOpenFeature"] = "true" })
            .Build()).EnableOpenFeature.Should().BeTrue();

        ProblemDetailsOptionsBuilder.Build().Should().NotBeNull();
        ProblemDetailsOptionsBuilder.Create(empty).Should().NotBeNull();
        ProblemDetailsOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ProblemDetails:IncludeExceptionDetails"] = "true" })
            .Build()).IncludeExceptionDetails.Should().BeTrue();
        Assert.Throws<ArgumentNullException>(() => ProblemDetailsOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ProblemDetailsOptionsBuilder.Validate(null!));
        RequestContextOptionsBuilder.Build().Should().NotBeNull();
        RequestContextOptionsBuilder.Create(empty).Should().NotBeNull();
        RequestContextOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RequestContext:EnableTenant"] = "false" })
            .Build()).EnableTenant.Should().BeFalse();
        Assert.Throws<ArgumentNullException>(() => RequestContextOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => RequestContextOptionsBuilder.Validate(null!));
        ModelValidationOptionsBuilder.Build().Should().NotBeNull();
        ModelValidationOptionsBuilder.Create(empty).Should().NotBeNull();
        ModelValidationOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ModelValidation:ReturnBadRequestOnFailure"] = "false" })
            .Build()).ReturnBadRequestOnFailure.Should().BeFalse();
        Assert.Throws<ArgumentNullException>(() => ModelValidationOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ModelValidationOptionsBuilder.Validate(null!));
        AuthorizationOptionsBuilder.Build().Should().NotBeNull();
        AuthorizationOptionsBuilder.Create(empty).Should().NotBeNull();
        AuthorizationOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Authorization:DefaultPolicy"] = "Custom" })
            .Build()).DefaultPolicy.Should().Be("Custom");
        Assert.Throws<InvalidOperationException>(() => AuthorizationOptionsBuilder.Validate(new AuthorizationOptions { DefaultPolicy = "" }));
        Assert.Throws<ArgumentNullException>(() => AuthorizationOptionsBuilder.Validate(null!));

        HttpLoggingOptionsBuilder.CreateDefault().LogRequestHeaders.Should().BeTrue();
        HttpLoggingOptionsBuilder.Create(empty).Should().NotBeNull();
        HttpLoggingOptionsBuilder.CreateWithValidation(empty).Should().NotBeNull();

        ResponseCompressionOptionsBuilder.Create(empty).Should().NotBeNull();
        ResponseCompressionOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ResponseCompression:EnableCompression"] = "false" })
            .Build()).EnableCompression.Should().BeFalse();
        new ResponseCompressionOptions { MimeTypes = ["text/plain"], EnableCompression = true }.Build().Should().NotBeNull();
        Assert.Throws<ArgumentNullException>(() => ResponseCompressionOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ResponseCompressionOptionsBuilder.Build(null!));

        ApiVersioningOptionsBuilder.Create(empty).Should().NotBeNull();
        ApiVersioningOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiVersioning:HeaderName"] = "X-Api-Version" })
            .Build()).HeaderName.Should().Be("X-Api-Version");
        ApiVersioningOptionsBuilder.Build(empty).Should().NotBeNull();
        Assert.Throws<ArgumentNullException>(() => ApiVersioningOptionsBuilder.Create(null!));
        Assert.Throws<ArgumentNullException>(() => ApiVersioningOptionsBuilder.Validate(null!));
        Assert.Throws<ArgumentException>(() => ApiVersioningOptionsBuilder.Validate(new ApiVersioningOptions { HeaderName = "" }));
        Assert.Throws<ArgumentException>(() => ApiVersioningOptionsBuilder.Validate(new ApiVersioningOptions { GroupNameFormat = "" }));
        foreach (var strategy in Enum.GetValues<ApiVersionReadingStrategy>())
        {
            ApiVersioningOptionsBuilder.CreateReader(strategy, new ApiVersioningOptions()).Should().NotBeNull();
        }
        ApiVersioningOptionsBuilder.CreateReader((ApiVersionReadingStrategy)999, new ApiVersioningOptions()).Should().NotBeNull();

        GraphQLOptionsBuilder.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GraphQL:Endpoint"] = "/gql" })
            .Build()).Endpoint.Should().Be("/gql");
        Assert.Throws<InvalidOperationException>(() => new GraphQLOptions { Endpoint = "" }.Build());

        Assert.Throws<InvalidOperationException>(() => new LocalizationOptions { SupportedCultures = null! }.Validate());
        Assert.Throws<InvalidOperationException>(() => new LocalizationOptions { SupportedCultures = [] }.Validate());
        Assert.Throws<InvalidOperationException>(() => new LocalizationOptions { DefaultCulture = "" }.Validate());
        new LocalizationOptions().Validate();
        Assert.Throws<InvalidOperationException>(() => LocalizationOptionsBuilder.Validate(new LocalizationOptions { DefaultCulture = "en-US", SupportedCultures = null! }));
    }
}

public class ExtensionsAndModelBranchCoverageCompletionTests
{
    [Fact]
    public async Task EndpointExtensions_MapEndpointsAndPermissionMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IEndpoint>(new RecordingEndpoint());
        await using var app = builder.Build();

        app.MapEndpoints(null).Should().BeSameAs(app);
        var group = app.MapGroup("/group");
        app.MapEndpoints(group).Should().BeSameAs(app);
        app.MapGet("/secure", () => "ok").HasPermission("permission").Should().NotBeNull();

        RecordingEndpoint.MapCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CqrsAndModuleDiscovery_CoverReflectionTypeLoadFallbacks()
    {
        var services = new ServiceCollection();
        var throwingAssembly = new ThrowingTypeAssembly(
            typeof(ScanRequestHandler),
            typeof(ScanNotificationHandler),
            typeof(ScanRequestValidator));

        services.AddCqrs(throwingAssembly);

        services.Should().Contain(descriptor => descriptor.ImplementationType == typeof(ScanRequestHandler));
        services.Should().Contain(descriptor => descriptor.ImplementationType == typeof(ScanNotificationHandler));
        services.Should().Contain(descriptor => descriptor.ImplementationType == typeof(ScanRequestValidator));

        var registry = new ModuleRegistry();
        registry.DiscoverModules([new ThrowingTypeAssembly(typeof(DiscoverableModule))], new ConfigurationBuilder().Build());
        registry.Modules.Should().Contain(descriptor => descriptor.ModuleType == typeof(DiscoverableModule));
    }

    [Fact]
    public void SecurityAndIdempotencyExtensions_RegisterMiddleware()
    {
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

        app.UseSecurityHeaders().Should().BeSameAs(app);
        app.UseSecurityHeaders(options => options.EnableXFrameOptions = false).Should().BeSameAs(app);
        app.UseIdempotency().Should().BeSameAs(app);
        app.UseIdempotency(options => options.CacheDuration = TimeSpan.FromMinutes(1)).Should().BeSameAs(app);
        app.UseCorrelationId().Should().BeSameAs(app);
    }

    [Fact]
    public async Task CorrelationAndExceptionMiddleware_CoverRemainingBranches()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "abc\r\n" + new string('x', 80);
        context.Response.Body = new MemoryStream();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        context.GetCorrelationId().Should().StartWith("abc");
        context.GetCorrelationId().Should().NotContain("\r");
        new DefaultHttpContext().GetCorrelationId().Should().NotBeNull();
        var nullItemContext = new DefaultHttpContext();
        nullItemContext.Items[CorrelationIdMiddleware.CorrelationIdHeader] = null;
        nullItemContext.GetCorrelationId().Should().BeNull();

        var exceptionContext = new DefaultHttpContext();
        exceptionContext.Response.Body = new MemoryStream();
        var exceptionMiddleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await exceptionMiddleware.InvokeAsync(exceptionContext);

        exceptionContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var oddSecurityContext = new DefaultHttpContext();
        oddSecurityContext.Response.Body = new MemoryStream();
        var oddSecurityMiddleware = new ExceptionHandlingMiddleware(
            _ => throw new OddSecurityException(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await oddSecurityMiddleware.InvokeAsync(oddSecurityContext);

        oddSecurityContext.Response.StatusCode.Should().Be(402);
    }

    [Fact]
    public async Task IdempotencyMiddleware_CoversAnonymousNullUserAndNonCacheableSuccessBranch()
    {
        var store = new Mock<IIdempotencyStore>();
        store.Setup(s => s.TryGetResponseAsync(It.IsAny<string>())).ReturnsAsync((IdempotentResponse?)null);
        store.Setup(s => s.TryMarkInFlightAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
        store.Setup(s => s.RemoveInFlightAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        var middleware = new IdempotencyMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status302Found;
                return Task.CompletedTask;
            },
            NullLogger<IdempotencyMiddleware>.Instance,
            store.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = null!;
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/redirect";
        httpContext.Request.Headers[IdempotencyMiddleware.IdempotencyKeyHeader] = "key";
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        store.Verify(s => s.TryGetResponseAsync(It.Is<string>(key => key.Contains(":anonymous:"))), Times.Once);
        store.Verify(s => s.SetResponseAsync(It.IsAny<string>(), It.IsAny<IdempotentResponse>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public void ModuleRegistry_CoversDiscoveryDuplicateInvalidDependencyAndCyclePaths()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Modules:DiscoverableModule:Enabled"] = "false" })
            .Build();
        var registry = new ModuleRegistry();

        registry.DiscoverModules([new FixedTypeAssembly(typeof(DiscoverableModule))], configuration);
        registry.RegisterModule<DiscoverableModule>(configuration);
        registry.Modules.Should().Contain(descriptor => descriptor.ModuleType == typeof(DiscoverableModule));
        registry.Modules.First(descriptor => descriptor.ModuleType == typeof(DiscoverableModule)).IsEnabled.Should().BeFalse();
        registry.Invoking(r => r.RegisterModule(typeof(string), configuration)).Should().Throw<ArgumentException>();

        var cycle = new ModuleRegistry();
        cycle.RegisterModule(new CycleModuleA());
        cycle.RegisterModule(new CycleModuleB());
        cycle.Invoking(r => r.ResolveDependencies()).Should().Throw<InvalidOperationException>().WithMessage("*Circular dependency*");
    }

    [Fact]
    public void EntityBase_CoversNewSoftDeleteRaiseToDictionaryPartialAndDeletedToString()
    {
        var entity = new ExposedEntity();
        entity.Invoking(e => e.SoftDelete()).Should().Throw<InvalidOperationException>();

        entity.ExposedRaise(new TestDomainEvent("raised"));
        entity.DomainEvents.Should().ContainSingle();
        entity.ExposedDictionary().Should().ContainKey(nameof(ExposedEntity.Name));

        entity.Version = 1;
        entity.SoftDelete();
        entity.ToString().Should().Contain("DELETED");
        entity.ExposedApplyPartial(new { Name = "partial" });
        entity.Name.Should().Be("partial");
    }

    [Fact]
    public void StatefulEntity_TransitionsAndDefaultModuleMembers_AreCovered()
    {
        var entity = new TestStatefulEntity();

        entity.CanTransitionTo(TestStatus.Active).Should().BeTrue();
        entity.CanTransitionTo(TestStatus.Closed).Should().BeFalse();
        entity.Activate();
        entity.Status.Should().Be(TestStatus.Active);
        entity.Invoking(e => e.Close()).Should().Throw<InvalidStateTransitionException>();
        entity.ForceStatus(TestStatus.Closed);
        entity.CanTransitionTo(TestStatus.Draft).Should().BeFalse();

        IModule module = new DefaultInterfaceModule();
        module.Order.Should().Be(100);
        module.EnabledByDefault.Should().BeTrue();
        module.Dependencies.Should().BeEmpty();
        module.MapEndpoints(Mock.Of<IEndpointRouteBuilder>()).Should().NotBeNull();
    }

    [Fact]
    public async Task IntegrationEventBus_CoversDefaultConstructorExtensionAndHandlerRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationEventBus();
        services.AddIntegrationEventHandler<SampleIntegrationEvent, SampleIntegrationEventHandler>();
        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IIntegrationEventBus>();

        await bus.PublishAsync(new SampleIntegrationEvent());

        SampleIntegrationEventHandler.Calls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ModelsAndValueObjects_CoverRemainingSwitchBranches()
    {
        CustomResults.GetStatusCode(ErrorType.Validation).Should().Be(StatusCodes.Status400BadRequest);
        CustomResults.GetStatusCode(ErrorType.Problem).Should().Be(StatusCodes.Status400BadRequest);
        CustomResults.GetStatusCode(ErrorType.NotFound).Should().Be(StatusCodes.Status404NotFound);
        CustomResults.GetStatusCode(ErrorType.Conflict).Should().Be(StatusCodes.Status409Conflict);
        CustomResults.GetStatusCode(ErrorType.Unauthorized).Should().Be(StatusCodes.Status401Unauthorized);
        CustomResults.GetStatusCode(ErrorType.Forbidden).Should().Be(StatusCodes.Status403Forbidden);
        Assert.Throws<InvalidOperationException>(() => CustomResults.GetStatusCode(ErrorType.None));
        CustomResults.GetStatusCode((ErrorType)999).Should().Be(StatusCodes.Status500InternalServerError);

        RfcUrls.ForErrorType(ErrorType.Validation).Should().Be(RfcUrls.BadRequest);
        RfcUrls.ForErrorType(ErrorType.Problem).Should().Be(RfcUrls.BadRequest);
        RfcUrls.ForErrorType(ErrorType.NotFound).Should().Be(RfcUrls.NotFound);
        RfcUrls.ForErrorType(ErrorType.Conflict).Should().Be(RfcUrls.Conflict);
        RfcUrls.ForErrorType(ErrorType.Unauthorized).Should().Be(RfcUrls.Unauthorized);
        RfcUrls.ForErrorType(ErrorType.Forbidden).Should().Be(RfcUrls.Forbidden);
        RfcUrls.ForErrorType(ErrorType.None).Should().BeEmpty();
        RfcUrls.ForErrorType((ErrorType)999).Should().Be(RfcUrls.InternalServerError);

        ProblemDetailsMapper.ToProblemDetails(Error.Validation("code", "description")).Title.Should().Be("code");
        ProblemDetailsMapper.ToProblemDetails(new Error("unknown", "unknown", (ErrorType)999)).Title.Should().Be("Server failure");
        CustomResults.Problem(Result.Failure(Error.Conflict("conflict", "details"))).Should().NotBeNull();
        CustomResults.Problem(Result.ValidationFailure([Error.Validation("name", "bad")])).Should().NotBeNull();
        Assert.Throws<InvalidOperationException>(() => CustomResults.Problem(Result.Success()));

        new PhoneNumber("+1 (555) 123-4567").GetDisplayFormat().Should().Be("(555) 123-4567");
        new PhoneNumber("+999123456789").GetDisplayFormat().Should().Be("+99 9123456789");
        (new Money(10, "usd") > new Money(9, "USD")).Should().BeTrue();
        (new Money(9, "usd") < new Money(10, "USD")).Should().BeTrue();
        (new Money(10, "usd") > new Money(10, "USD")).Should().BeFalse();
        (new Money(10, "usd") < new Money(10, "USD")).Should().BeFalse();
        Assert.Throws<InvalidOperationException>(() => _ = new Money(10, "USD") > new Money(9, "EUR"));
        Assert.Throws<InvalidOperationException>(() => _ = new Money(10, "USD") < new Money(9, "EUR"));
        Assert.Throws<BusinessRuleViolationException>(() => _ = new Money(1, "USD") - new Money(2, "USD"));

        new PagedResult<int>(new List<int> { 1 }, 1, 0, 1).Items.Should().ContainSingle();
        new PagedResult<int>(Enumerable.Range(1, 1).Where(value => value == 1), 1, 0, 1).Items.Should().ContainSingle();
        new PagedResult<int>([], 0, 0, 0).TotalPages.Should().Be(0);
        new MemoryCachingOptions { SizeLimit = 1, CompactionPercentage = 0.5, ExpirationScanFrequency = TimeSpan.FromSeconds(1) }.Validate();
        Assert.Throws<InvalidOperationException>(() => new MemoryCachingOptions { SizeLimit = 1, CompactionPercentage = 0, ExpirationScanFrequency = TimeSpan.FromSeconds(1) }.Validate());
        Assert.Throws<InvalidOperationException>(() => new MemoryCachingOptions { SizeLimit = 1, CompactionPercentage = 1, ExpirationScanFrequency = TimeSpan.FromSeconds(1) }.Validate());

        new OpenApiServerOptions { Url = "https://api.example.com", Variables = { ["env"] = new OpenApiServerVariableOptions { Default = "prod" } } }.Validate();
        Assert.Throws<ArgumentException>(() => new OpenApiServerOptions { Url = "https://api.example.com", Variables = { ["env"] = new OpenApiServerVariableOptions() } }.Validate());
        OpenApiServerVariableOptions.CreateDefault().Default.Should().BeEmpty();
        OpenApiServerOptions.CreateDefault().Variables.Should().BeEmpty();

        new MfaOptions().IsValid.Should().BeTrue();
        new AuthenticationOptions { EnableAuthentication = false }.Validate();
        Assert.Throws<InvalidOperationException>(() => new AuthenticationOptions { JwtSecretKey = "secret" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new AuthenticationOptions { JwtSecretKey = "secret", JwtIssuer = "issuer" }.Validate());

        var crossTenant = new CrossTenantAccessException(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        crossTenant.UserTenantId.Should().NotBeNull();
        crossTenant.PublicMessage.Should().Contain("permission");

        Assert.Throws<ArgumentException>(() => new InvalidResult(true, Error.Conflict("bad", "bad")));
        Assert.Throws<ArgumentException>(() => new InvalidResult(false, Error.None));
        Result<string> valueResult = "value";
        valueResult.Value.Should().Be("value");
        Result<string> nullResult = (string?)null;
        nullResult.IsFailure.Should().BeTrue();
        Result.Success("value").ValueOrDefault("fallback").Should().Be("value");
        Result.Failure<string>(Error.Conflict("x", "x")).ValueOrDefault("fallback").Should().Be("fallback");
        Result.Success(5).Ensure(value => value > 0, Error.Conflict("bad", "bad")).IsSuccess.Should().BeTrue();
        Result.Success(5).Ensure(value => value < 0, Error.Conflict("bad", "bad")).IsFailure.Should().BeTrue();
        Result.Failure<int>(Error.Conflict("bad", "bad")).Ensure(value => true, Error.Conflict("other", "other")).Error.Code.Should().Be("bad");

        new CorsOptions { AllowedOrigins = ["*"], AllowedMethods = ["*"], AllowedHeaders = ["x"] }.Validate();
        new CorsOptions { AllowedOrigins = ["*"], AllowedMethods = ["GET"], AllowedHeaders = ["*"] }.Validate();
        new EncryptionOptions { EncryptionKey = new string('A', 65), Algorithm = "AES256" }.Validate().Errors.Should().NotBeEmpty();
        JwtOptionsResolver.CreateValidated(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SecretKey"] = "0123456789abcdef0123456789abcdef" })
            .Build()).Issuer.Should().Be("GameGuild");
        JwtOptionsResolver.CreateValidated(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:SecretKey"] = "0123456789abcdef0123456789abcdef" })
            .Build()).Audience.Should().Be("GameGuild.Users");
        Assert.Throws<InvalidOperationException>(() => JwtOptionsResolver.CreateValidated(new ConfigurationBuilder().Build()));
    }

    private sealed class RecordingEndpoint : IEndpoint
    {
        public static int MapCount { get; private set; }

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            MapCount++;
        }
    }

    private sealed class ThrowingTypeAssembly(params Type[] loadableTypes) : Assembly
    {
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => (object[])Array.CreateInstance(attributeType, 0);

        public override Type[] GetTypes()
            => throw new ReflectionTypeLoadException(loadableTypes, loadableTypes.Select(_ => new Exception("load")).ToArray());
    }

    private sealed record ScanRequest(string Value) : IRequest<string>;

    private sealed class ScanRequestHandler : IRequestHandler<ScanRequest, string>
    {
        public Task<string> Handle(ScanRequest request, CancellationToken cancellationToken) => Task.FromResult(request.Value);
    }

    private sealed class ScanNotification : INotification;

    private sealed class ScanNotificationHandler : INotificationHandler<ScanNotification>
    {
        public Task Handle(ScanNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ScanRequestValidator : AbstractValidator<ScanRequest>;

    public sealed class DiscoverableModule : IModule
    {
        public string Name => nameof(DiscoverableModule);
        public IReadOnlyList<Type> Dependencies => [];
        public bool EnabledByDefault => true;
        public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration) => services;
        public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
    }

    private sealed class FixedTypeAssembly(params Type[] types) : System.Reflection.Assembly
    {
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => (object[])Array.CreateInstance(attributeType, 0);

        public override Type[] GetTypes() => types;
    }

    private enum TestStatus
    {
        Draft,
        Active,
        Closed
    }

    private sealed class TestStatefulEntity : StatefulEntity<TestStatus>
    {
        protected override IReadOnlyDictionary<TestStatus, IReadOnlySet<TestStatus>> ValidTransitions { get; } =
            new Dictionary<TestStatus, IReadOnlySet<TestStatus>>
            {
                [TestStatus.Draft] = new HashSet<TestStatus> { TestStatus.Active },
                [TestStatus.Active] = new HashSet<TestStatus> { TestStatus.Closed }
            };

        public override TestStatus Status { get; protected set; } = TestStatus.Draft;

        public void Activate() => TransitionTo(TestStatus.Active);

        public void Close() => TransitionTo(TestStatus.Draft);

        public void ForceStatus(TestStatus status) => Status = status;
    }

    private sealed class DefaultInterfaceModule : IModule
    {
        public string Name => nameof(DefaultInterfaceModule);

        public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration) => services;

        public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
    }

    private sealed class InvalidResult(bool isSuccess, Error error) : Result(isSuccess, error);

    private sealed class OddSecurityException : SecurityException
    {
        public override System.Net.HttpStatusCode StatusCode => System.Net.HttpStatusCode.PaymentRequired;

        public override string PublicMessage => "Payment required.";

        public OddSecurityException() : base("odd security")
        {
        }
    }

    private sealed class CycleModuleA : IModule
    {
        public string Name => nameof(CycleModuleA);
        public IReadOnlyList<Type> Dependencies => [typeof(CycleModuleB)];
        public bool EnabledByDefault => true;
        public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration) => services;
        public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
    }

    private sealed class CycleModuleB : IModule
    {
        public string Name => nameof(CycleModuleB);
        public IReadOnlyList<Type> Dependencies => [typeof(CycleModuleA)];
        public bool EnabledByDefault => true;
        public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration) => services;
        public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
    }

    private sealed class ExposedEntity : EntityBase
    {
        public string Name { get; set; } = "initial";
        public void ExposedRaise(IDomainEvent domainEvent) => Raise(domainEvent);
        public Dictionary<string, object?> ExposedDictionary() => ToDictionary();
        public void ExposedApplyPartial(object partial) => ApplyPartial(partial);
    }

    private sealed class TestDomainEvent(string message) : DomainEvent
    {
        public string Message { get; } = message;
    }

    private sealed record SampleIntegrationEvent : IntegrationEventBase
    {
        public override string SourceModule => "SharedKernelTests";
    }

    private sealed class SampleIntegrationEventHandler : IIntegrationEventHandler<SampleIntegrationEvent>
    {
        public static int Calls { get; private set; }

        public Task HandleAsync(SampleIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
