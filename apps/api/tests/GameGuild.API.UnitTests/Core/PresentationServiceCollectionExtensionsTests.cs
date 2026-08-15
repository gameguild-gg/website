using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using GameGuild.API.Setup;
using GameGuild.Configuration.PresentationLayer.Controllers;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using Moq;

namespace GameGuild.API.UnitTests.Core;

public class PresentationServiceCollectionExtensionsTests
{
    [Fact]
    public void LogControllersFromAssembly_WhenSomeTypesFailToLoad_UsesAvailableTypes()
    {
        var loadException = new ReflectionTypeLoadException(
            [typeof(AvailableController), null],
            [new TypeLoadException("Unavailable dependency"), null]);
        var assembly = new Mock<Assembly>();
        assembly.Setup(value => value.GetTypes()).Throws(loadException);
        assembly.Setup(value => value.GetName()).Returns(new AssemblyName("PartiallyLoaded"));
        var logger = new Mock<ILogger>();
        var method = typeof(PresentationServiceCollectionExtensions)
            .GetMethod("LogControllersFromAssembly", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var act = () => method!.Invoke(null, [assembly.Object, logger.Object, Stopwatch.StartNew()]);

        act.Should().NotThrow();
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void SetupControllers_ShouldUseOneDeterministicApplicationPartCatalog()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Modules:EnabledModules:0"] = "AI"
            })
            .Build();
        var services = new ServiceCollection();

        services.SetupControllers(configuration, ControllersOptions.CreateDefault());

        using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<ApplicationPartManager>();
        var names = manager.ApplicationParts.Select(part => part.Name).ToArray();
        names.Should().BeEquivalentTo("GameGuild.API", "GameGuild.AI");
        names.Should().OnlyHaveUniqueItems();
        var mvc = provider.GetRequiredService<IOptions<MvcOptions>>().Value;
        mvc.Conventions.Should().Contain(convention => convention is MinimumOrderRouteApplicationModelConvention);
        mvc.Conventions.Should().HaveCount(2);
        mvc.Filters.OfType<TypeFilterAttribute>().Should().NotContain(typeFilter =>
            typeFilter.ImplementationType == typeof(ResourcePermissionAuthorizationFilter));
    }

    [Fact]
    public void SetupControllers_WhenOptionsComeFromConfiguration_ShouldApplyMvcOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Controllers:UseKebabCaseRoutes"] = "false",
                ["Controllers:EnablePermissionAuthorizationFilter"] = "true",
                ["Controllers:WriteIndentedJson"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.SetupControllers(configuration, null);

        using var provider = services.BuildServiceProvider();
        var mvc = provider.GetRequiredService<IOptions<MvcOptions>>().Value;
        mvc.Conventions.Should().ContainSingle(convention =>
            convention is MinimumOrderRouteApplicationModelConvention);
        mvc.Filters.OfType<TypeFilterAttribute>().Should().Contain(typeFilter =>
            typeFilter.ImplementationType == typeof(ResourcePermissionAuthorizationFilter));
        provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions.WriteIndented
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("CamelCase", JsonKnownNamingPolicy.CamelCase)]
    [InlineData("SnakeCaseLower", JsonKnownNamingPolicy.SnakeCaseLower)]
    [InlineData("SnakeCaseUpper", JsonKnownNamingPolicy.SnakeCaseUpper)]
    [InlineData("KebabCaseLower", JsonKnownNamingPolicy.KebabCaseLower)]
    [InlineData("KebabCaseUpper", JsonKnownNamingPolicy.KebabCaseUpper)]
    [InlineData("Unknown", JsonKnownNamingPolicy.CamelCase)]
    public void SetupControllers_ShouldApplyConfiguredJsonNamingPolicy(
        string configuredPolicy,
        JsonKnownNamingPolicy expectedPolicy)
    {
        var services = new ServiceCollection();
        var options = ControllersOptions.CreateDefault();
        options.JsonPropertyNamingPolicy = configuredPolicy;

        services.SetupControllers(new ConfigurationBuilder().Build(), options);

        using var provider = services.BuildServiceProvider();
        var json = provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        json.PropertyNamingPolicy.Should().Be(GetNamingPolicy(expectedPolicy));
        json.Converters.Should().ContainSingle(converter => converter is JsonStringEnumConverter);
    }

    [Fact]
    public void SetupEndpointsAndMiddlewares_WhenOptionsComeFromConfiguration_ShouldRegisterSuccessfully()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.SetupEndpoints(configuration, null);
        services.SetupMiddlewares(configuration);

        services.Should().NotBeEmpty();
    }

    private static JsonNamingPolicy GetNamingPolicy(JsonKnownNamingPolicy policy) => policy switch
    {
        JsonKnownNamingPolicy.SnakeCaseLower => JsonNamingPolicy.SnakeCaseLower,
        JsonKnownNamingPolicy.SnakeCaseUpper => JsonNamingPolicy.SnakeCaseUpper,
        JsonKnownNamingPolicy.KebabCaseLower => JsonNamingPolicy.KebabCaseLower,
        JsonKnownNamingPolicy.KebabCaseUpper => JsonNamingPolicy.KebabCaseUpper,
        _ => JsonNamingPolicy.CamelCase
    };

    private sealed class AvailableController : ControllerBase;
}
