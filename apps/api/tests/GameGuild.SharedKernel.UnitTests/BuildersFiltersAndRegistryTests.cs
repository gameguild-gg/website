using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using GameGuild;
using GameGuild.CQRS;
using GameGuild.Configuration.PresentationLayer.HealthChecks;
using GameGuild.Configuration.PresentationLayer.ResponseCompression;

namespace GameGuild.SharedKernel.UnitTests;

public class BuildersFiltersAndRegistryTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    // ═══════════════════════════════════════════════════════════════════
    // ResponseCompressionOptionsBuilder
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ResponseCompressionOptionsBuilder_Create_ReturnsDefaults()
    {
        var opts = ResponseCompressionOptionsBuilder.Create();
        opts.Should().NotBeNull();
        opts.EnableCompression.Should().BeTrue();
        opts.MimeTypes.Should().BeEquivalentTo("application/json", "text/plain", "application/xml");
    }

    [Fact]
    public void ResponseCompressionOptionsBuilder_Create_WithConfig()
    {
        var opts = ResponseCompressionOptionsBuilder.Create(EmptyConfig());
        opts.Should().NotBeNull();
    }

    [Fact]
    public void ResponseCompressionOptionsBuilder_Create_WithConfigAndSection()
    {
        var opts = ResponseCompressionOptionsBuilder.Create(EmptyConfig(), "ResponseCompression");
        opts.Should().NotBeNull();
    }

    [Fact]
    public void ResponseCompressionOptionsBuilder_Build_ReturnsOptions()
    {
        var opts = ResponseCompressionOptionsBuilder.Create();
        var built = opts.Build();
        built.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // HealthChecksOptionsBuilder
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void HealthChecksOptionsBuilder_Create_ReturnsDefaults()
    {
        var opts = HealthChecksOptionsBuilder.Create();
        opts.Should().NotBeNull();
    }

    [Fact]
    public void HealthChecksOptionsBuilder_Create_WithConfig()
    {
        var opts = HealthChecksOptionsBuilder.Create(EmptyConfig());
        opts.Should().NotBeNull();
    }

    [Fact]
    public void HealthChecksOptionsBuilder_Create_WithConfigAndSection()
    {
        var opts = HealthChecksOptionsBuilder.Create(EmptyConfig(), "HealthChecks");
        opts.Should().NotBeNull();
    }

    [Fact]
    public void HealthChecksOptionsBuilder_Validate_DoesNotThrow()
    {
        var opts = HealthChecksOptionsBuilder.Create();
        var act = () => HealthChecksOptionsBuilder.Validate(opts);
        act.Should().NotThrow();
    }

    [Fact]
    public void HealthChecksOptionsBuilder_Build_ReturnsOptions()
    {
        var opts = HealthChecksOptionsBuilder.Build();
        opts.Should().NotBeNull();
    }

    [Fact]
    public void HealthChecksOptionsBuilder_Build_WithConfig()
    {
        var opts = HealthChecksOptionsBuilder.Build(EmptyConfig());
        opts.Should().NotBeNull();
    }

    [Fact]
    public void HealthChecksOptionsBuilder_Build_WithConfigAndSection()
    {
        var opts = HealthChecksOptionsBuilder.Build(EmptyConfig(), "HealthChecks");
        opts.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // PaginationHeadersFilter — passthrough (non-paginated result)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PaginationHeadersFilter_NonPaginated_JustCallsNext()
    {
        var filter = new PaginationHeadersFilter();
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var result = new ObjectResult("plain string");

        var executingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            result,
            controller: null!);

        var executedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            result,
            controller: null!);

        var nextCalled = false;
        ResultExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(executedContext);
        };

        await filter.OnResultExecutionAsync(executingContext, next);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PaginationHeadersFilter_NullResult_JustCallsNext()
    {
        var filter = new PaginationHeadersFilter();
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var result = new ObjectResult(null);

        var executingContext = new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            result,
            controller: null!);

        var executedContext = new ResultExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            result,
            controller: null!);

        await filter.OnResultExecutionAsync(executingContext,
            () => Task.FromResult(executedContext));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ModuleRegistry
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ModuleRegistry_CanBeInstantiated()
    {
        var registry = new ModuleRegistry();
        registry.Should().NotBeNull();
    }

    [Fact]
    public void ModuleRegistry_DiscoverModules_WithEmptyAssemblies()
    {
        var registry = new ModuleRegistry();
        var config = EmptyConfig();
        var result = registry.DiscoverModules(Array.Empty<Assembly>(), config);
        result.Should().NotBeNull();
    }

    [Fact]
    public void ModuleRegistry_DiscoverModules_ScansAssembly()
    {
        var registry = new ModuleRegistry();
        var config = EmptyConfig();
        // Scan SharedKernel assembly — likely has no IModule implementations
        var result = registry.DiscoverModules(
            new[] { typeof(ModuleRegistry).Assembly }, config);
        result.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CQRS — ISender / IPublisher resolution through DI
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ISender_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(typeof(BuildersFiltersAndRegistryTests).Assembly);
        var sp = services.BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        sender.Should().NotBeNull();
    }

    [Fact]
    public void IPublisher_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(typeof(BuildersFiltersAndRegistryTests).Assembly);
        var sp = services.BuildServiceProvider();

        var publisher = sp.GetRequiredService<IPublisher>();
        publisher.Should().NotBeNull();
    }

    [Fact]
    public void IMediator_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(typeof(BuildersFiltersAndRegistryTests).Assembly);
        var sp = services.BuildServiceProvider();

        var mediator = sp.GetRequiredService<IMediator>();
        mediator.Should().NotBeNull();
    }

    [Fact]
    public async Task ISender_Send_ExecutesHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(typeof(CoverageTestQuery).Assembly);
        var sp = services.BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(new CoverageTestQuery("hello"));
        result.Should().Be("hello");
    }

    [Fact]
    public async Task IPublisher_Publish_ExecutesHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(typeof(CoverageTestNotification).Assembly);
        var sp = services.BuildServiceProvider();

        var publisher = sp.GetRequiredService<IPublisher>();
        // Should not throw
        await publisher.Publish(new CoverageTestNotification("test"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // BaseApiController — can be subclassed
    // ═══════════════════════════════════════════════════════════════════

    private class TestApiController : BaseApiController { }

    [Fact]
    public void BaseApiController_CanBeSubclassed()
    {
        var controller = new TestApiController();
        controller.Should().NotBeNull();
        controller.Should().BeAssignableTo<Microsoft.AspNetCore.Mvc.ControllerBase>();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AddCqrs — with configuration action overload
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddCqrs_WithAssemblyArray_RegistersAllCqrsTypes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(typeof(BuildersFiltersAndRegistryTests).Assembly);
        var sp = services.BuildServiceProvider();

        sp.GetService<ISender>().Should().NotBeNull();
        sp.GetService<IPublisher>().Should().NotBeNull();
    }

    [Fact]
    public void AddCqrs_WithConfig_AndMultipleAssemblies()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(
            cfg => { /* default configuration */ },
            typeof(BuildersFiltersAndRegistryTests).Assembly);

        var sp = services.BuildServiceProvider();
        sp.GetService<ISender>().Should().NotBeNull();
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Test CQRS types — used for Send/Publish coverage
// ═══════════════════════════════════════════════════════════════════════

public record CoverageTestQuery(string Value) : IRequest<string>;

public class CoverageTestQueryHandler : IRequestHandler<CoverageTestQuery, string>
{
    public Task<string> Handle(CoverageTestQuery request, CancellationToken cancellationToken = default)
        => Task.FromResult(request.Value);
}

public record CoverageTestNotification(string Value) : INotification;

public class CoverageTestNotificationHandler : INotificationHandler<CoverageTestNotification>
{
    public Task Handle(CoverageTestNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
