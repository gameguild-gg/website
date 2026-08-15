using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using GameGuild.Configuration.PresentationLayer.FeatureFlags;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Configuration.PresentationLayer.HealthChecks;
using GameGuild.Configuration.PresentationLayer.Localization;
using GameGuild.Configuration.PresentationLayer.ModelValidation;
using GameGuild.Configuration.PresentationLayer.RequestContext;
using GameGuild.Configuration.PresentationLayer.ResponseCompression;
using GameGuild.Configuration.PresentationLayer.SignalR;
using ApiHttpLoggingOptions = GameGuild.Configuration.PresentationLayer.HttpLogging.HttpLoggingOptions;
using FrameworkHttpLoggingOptions = Microsoft.AspNetCore.HttpLogging.HttpLoggingOptions;
using FrameworkResponseCompressionOptions = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionOptions;

namespace GameGuild.API.UnitTests.Core;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void SetupInfrastructure_WhenBoundFromConfiguration_ShouldMaterializeConfiguredOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpLogging:LogRequestHeaders"] = "true",
                ["HttpLogging:LogResponseHeaders"] = "true",
                ["HttpLogging:LogRequestBody"] = "true",
                ["HttpLogging:LogResponseBody"] = "true",
                ["ProblemDetails:IncludeExceptionDetails"] = "true",
                ["Localization:DefaultCulture"] = "pt-BR",
                ["Localization:SupportedCultures:0"] = "pt-BR",
                ["Localization:SupportedCultures:1"] = "en-US",
                ["ResponseCompression:MimeTypes:0"] = "application/json",
                ["ModelValidation:SuppressModelStateInvalidFilter"] = "true"
            })
            .Build();
        var services = new ServiceCollection();

        services.SetupHttpLogging(configuration, null);
        services.SetupProblemDetails(configuration, null);
        services.SetupLocalization(configuration, null);
        services.SetupResponseCompression(configuration, null);
        services.SetupRequestContext(configuration, null);
        services.SetupModelValidation(configuration, null);
        services.SetupHealthChecks(configuration, null);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<FrameworkHttpLoggingOptions>>().Value.LoggingFields
            .Should().Be(HttpLoggingFields.All);

        var problemDetails = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.ProblemDetailsOptions>>().Value;
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-configured" };
        httpContext.Request.Path = "/configured";
        var problemContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails(),
            Exception = new InvalidOperationException("ordinary failure")
        };
        problemDetails.CustomizeProblemDetails!(problemContext);
        problemContext.ProblemDetails.Instance.Should().Be("/configured");
        problemContext.ProblemDetails.Extensions["traceId"].Should().Be("trace-configured");
        problemContext.ProblemDetails.Extensions.Should().ContainKey("exception");

        var noExceptionContext = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails()
        };
        problemDetails.CustomizeProblemDetails(noExceptionContext);
        noExceptionContext.ProblemDetails.Extensions.Should().NotContainKey("exception");

        var localization = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
        localization.DefaultRequestCulture.Culture.Name.Should().Be("pt-BR");
        localization.SupportedCultures!.Select(culture => culture.Name)
            .Should().BeEquivalentTo("pt-BR", "en-US").And.HaveCount(2);
        localization.SupportedUICultures!.Select(culture => culture.Name)
            .Should().BeEquivalentTo("pt-BR", "en-US").And.HaveCount(2);
        provider.GetRequiredService<IOptions<Microsoft.Extensions.Localization.LocalizationOptions>>()
            .Value.ResourcesPath.Should().Be("Resources");

        var compression = provider.GetRequiredService<IOptions<FrameworkResponseCompressionOptions>>().Value;
        compression.EnableForHttps.Should().BeTrue();
        compression.MimeTypes.Should().BeEquivalentTo("text/plain", "application/json").And.HaveCount(2);
        provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.SuppressModelStateInvalidFilter
            .Should().BeTrue();

        var health = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var database = health.Registrations.Should().ContainSingle(registration => registration.Name == "database").Subject;
        database.Tags.Should().BeEquivalentTo("ready", "dependency");
    }

    [Fact]
    public void SetupInfrastructure_WhenOptionsAreSupplied_ShouldUseSuppliedValues()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.SetupHttpLogging(configuration, new ApiHttpLoggingOptions
        {
            LogRequestHeaders = false,
            LogResponseHeaders = false,
            LogRequestBody = false,
            LogResponseBody = false
        });
        services.SetupLocalization(configuration, new LocalizationOptions
        {
            DefaultCulture = "en-US",
            SupportedCultures = ["en-US"]
        });
        services.SetupResponseCompression(configuration, new ResponseCompressionOptions
        {
            MimeTypes = ["text/plain"]
        });
        services.SetupRequestContext(configuration, new RequestContextOptions());
        services.SetupFeatureFlags(configuration, new FeatureFlagsOptions());
        services.SetupModelValidation(configuration, new ModelValidationOptions
        {
            SuppressModelStateInvalidFilter = false
        });
        services.SetupHealthChecks(configuration, new HealthChecksOptions());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<FrameworkHttpLoggingOptions>>().Value.LoggingFields
            .Should().Be(HttpLoggingFields.All);
        provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value
            .DefaultRequestCulture.Culture.Name.Should().Be("en-US");
        provider.GetRequiredService<IOptions<FrameworkResponseCompressionOptions>>().Value.MimeTypes
            .Should().Equal("text/plain");
        provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.SuppressModelStateInvalidFilter
            .Should().BeFalse();
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Should().ContainSingle(registration => registration.Name == "database");
    }

    [Fact]
    public void SetupSignalR_WhenOptionsAreConfigured_BindsAndRegistersHubOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SignalR:EnableDetailedErrors"] = "true",
                ["SignalR:KeepAliveInterval"] = "00:00:09",
                ["SignalR:ClientTimeoutInterval"] = "00:00:45",
                ["SignalR:MaximumReceiveMessageSize"] = "4096"
            })
            .Build();
        var services = new ServiceCollection();

        var result = services.SetupSignalR(configuration, options: null);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HubOptions>>().Value;

        result.Should().BeSameAs(services);
        options.EnableDetailedErrors.Should().BeTrue();
        options.KeepAliveInterval.Should().Be(TimeSpan.FromSeconds(9));
        options.ClientTimeoutInterval.Should().Be(TimeSpan.FromSeconds(45));
        options.MaximumReceiveMessageSize.Should().Be(4096);
    }

    [Fact]
    public void SetupSignalR_WhenOptionsAreSupplied_UsesTheSuppliedValues()
    {
        var services = new ServiceCollection();
        var supplied = new SignalROptions
        {
            EnableDetailedErrors = false,
            KeepAliveInterval = TimeSpan.FromSeconds(11),
            ClientTimeoutInterval = TimeSpan.FromSeconds(50),
            MaximumReceiveMessageSize = null
        };

        services.SetupSignalR(new ConfigurationBuilder().Build(), supplied);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HubOptions>>().Value;

        options.EnableDetailedErrors.Should().BeFalse();
        options.KeepAliveInterval.Should().Be(TimeSpan.FromSeconds(11));
        options.ClientTimeoutInterval.Should().Be(TimeSpan.FromSeconds(50));
        options.MaximumReceiveMessageSize.Should().BeNull();
    }

    [Fact]
    public void SetupGraphQL_WhenOptionsAreConfigured_BindsAndValidatesConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GraphQL:EnableGraphQL"] = "true",
                ["GraphQL:Endpoint"] = "/v1/graphql"
            })
            .Build();
        var services = new ServiceCollection();

        var result = services.SetupGraphQL(configuration, options: null);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void SetupGraphQL_WhenOptionsAreSupplied_ValidatesAndReturnsServices()
    {
        var services = new ServiceCollection();
        var options = new GraphQLOptions { Endpoint = "/graphql" };

        var result = services.SetupGraphQL(new ConfigurationBuilder().Build(), options);

        result.Should().BeSameAs(services);
    }
}
