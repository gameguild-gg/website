using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiSetupTests
{
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
}
