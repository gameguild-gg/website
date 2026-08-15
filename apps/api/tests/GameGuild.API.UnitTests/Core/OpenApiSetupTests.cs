using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using GameGuild.CQRS;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using GameGuild.Identity.Tenants;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiSetupTests
{
    private sealed class UnversionedController
    {
        public void Execute() { }
    }

    [ApiVersion("1.0")]
    private sealed class VersionedController
    {
        public void Execute() { }
    }

    [Fact]
    public void SetupMethods_HandleConfiguredAndSuppliedOptions()
    {
        var configuration = new ConfigurationBuilder().Build();
        var configuredServices = new ServiceCollection();
        var suppliedServices = new ServiceCollection();
        var versioningOptions = GameGuild.Configuration.PresentationLayer.ApiVersioning.ApiVersioningOptions.CreateDefault();

        configuredServices.SetupOpenApi(configuration, options: null);
        configuredServices.SetupApiVersioning(configuration, options: null);
        configuredServices.SetupApiExplorer(configuration, options: null);
        suppliedServices.SetupOpenApi(configuration, OpenApiOptions.CreateDefault());
        suppliedServices.SetupApiVersioning(configuration, versioningOptions);
        suppliedServices.SetupApiExplorer(configuration, versioningOptions);

        configuredServices.Should().NotBeEmpty();
        suppliedServices.Should().NotBeEmpty();
    }

    [Fact]
    public void SetupOpenApi_WithoutVersionExplorer_ShouldCreateConfiguredFallbackDocument()
    {
        var services = new ServiceCollection();
        var options = OpenApiOptions.CreateDefault();
        options.ContactName = "Platform Operations";
        options.ContactEmail = "operations@example.com";
        options.ContactUrl = "https://example.com/support";

        services.SetupOpenApi(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var swagger = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        var document = swagger.SwaggerGeneratorOptions.SwaggerDocs[options.Version];
        document.Contact.Name.Should().Be(options.ContactName);
        document.Contact.Email.Should().Be(options.ContactEmail);
        document.Contact.Url.Should().Be(options.ContactUrl);
    }

    [Fact]
    public void SetupOpenApi_ShouldPreserveNamedRoutesAsOperationIds()
    {
        var services = new ServiceCollection();
        services.SetupOpenApi(new ConfigurationBuilder().Build(), OpenApiOptions.CreateDefault());

        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<IOptions<SwaggerGenOptions>>()
            .Value.SwaggerGeneratorOptions.OperationIdSelector;
        var named = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor
            {
                AttributeRouteInfo = new AttributeRouteInfo { Name = "GetPlatformStatus" }
            }
        };
        var unnamed = new ApiDescription { ActionDescriptor = new ActionDescriptor() };

        selector(named).Should().Be("GetPlatformStatus");
        selector(unnamed).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://example.com/support")]
    public void SetupOpenApi_WithVersionExplorer_ConfiguresDocumentsAndInclusionPolicy(string? contactUrl)
    {
        var services = new ServiceCollection();
        var versionProvider = new Mock<IApiVersionDescriptionProvider>();
        versionProvider.SetupGet(value => value.ApiVersionDescriptions).Returns(
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", false)
        ]);
        services.AddSingleton(versionProvider.Object);
        var options = OpenApiOptions.CreateDefault();
        options.ContactUrl = contactUrl ?? string.Empty;
        services.SetupOpenApi(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var swagger = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        var document = swagger.SwaggerGeneratorOptions.SwaggerDocs["v1"];
        var predicate = swagger.SwaggerGeneratorOptions.DocInclusionPredicate;

        document.Version.Should().Be("1.0");
        document.Contact.Url?.ToString().Should().Be(contactUrl);
        predicate("v1", CreateDescription(new ActionDescriptor(), "v1")).Should().BeTrue();
        predicate("v2", CreateDescription(new ActionDescriptor(), "v1")).Should().BeFalse();
        predicate("v1", CreateDescription(CreateControllerDescriptor(typeof(UnversionedController)), "v1")).Should().BeTrue();
        predicate("v2", CreateDescription(CreateControllerDescriptor(typeof(UnversionedController)), "v1")).Should().BeFalse();
        predicate("v1", CreateDescription(CreateControllerDescriptor(typeof(VersionedController)), null)).Should().BeTrue();
        predicate("v2", CreateDescription(CreateControllerDescriptor(typeof(VersionedController)), null)).Should().BeFalse();
    }

    [Fact]
    public void SetupOpenApi_ConfiguresNativeTransformerAndStableSchemaIds()
    {
        var services = new ServiceCollection();
        services.SetupOpenApi(new ConfigurationBuilder().Build(), OpenApiOptions.CreateDefault());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptionsMonitor<Microsoft.AspNetCore.OpenApi.OpenApiOptions>>()
            .Get("v1").Should().NotBeNull();
        var schemaId = provider.GetRequiredService<IOptions<SwaggerGenOptions>>()
            .Value.SchemaGeneratorOptions.SchemaIdSelector;
        var genericParameter = typeof(Dictionary<,>).GetGenericArguments()[0];

        schemaId(typeof(IRequest<string>)).Should().Be("CQRS_IRequestString");
        schemaId(typeof(List<string>)).Should().Be("ListString");
        schemaId(typeof(TenantSettingsDto)).Should().Be("Identity_Tenants_TenantSettingsDto");
        schemaId(typeof(Unit)).Should().Be("CQRS_Unit");
        schemaId(typeof(string)).Should().Be("System_String");
        schemaId(genericParameter).Should().Be("TKey");
    }

    [Theory]
    [InlineData("2.3", 2, 3)]
    [InlineData("invalid.invalid", 1, 0)]
    [InlineData("4", 4, 0)]
    public void SetupApiVersioning_ParsesConfiguredDefaultVersion(string configured, int expectedMajor, int expectedMinor)
    {
        var services = new ServiceCollection();
        var options = GameGuild.Configuration.PresentationLayer.ApiVersioning.ApiVersioningOptions.CreateDefault();
        options.DefaultVersion = configured;

        services.SetupApiVersioning(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var configuredOptions = provider.GetRequiredService<IOptions<Asp.Versioning.ApiVersioningOptions>>().Value;
        var explorerOptions = provider.GetRequiredService<IOptions<Asp.Versioning.ApiExplorer.ApiExplorerOptions>>().Value;
        configuredOptions.DefaultApiVersion.Should().Be(new ApiVersion(expectedMajor, expectedMinor));
        configuredOptions.AssumeDefaultVersionWhenUnspecified.Should().Be(options.AssumeDefaultVersionWhenUnspecified);
        configuredOptions.ApiVersionReader.Should().NotBeNull();
        explorerOptions.GroupNameFormat.Should().Be(options.GroupNameFormat);
        explorerOptions.SubstituteApiVersionInUrl.Should().Be(options.SubstituteApiVersionInUrl);
    }

    private static ApiDescription CreateDescription(ActionDescriptor descriptor, string? groupName)
        => new() { ActionDescriptor = descriptor, GroupName = groupName };

    private static ControllerActionDescriptor CreateControllerDescriptor(Type controllerType)
        => new()
        {
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            MethodInfo = controllerType.GetMethod("Execute")!,
            ControllerName = controllerType.Name
        };
}
