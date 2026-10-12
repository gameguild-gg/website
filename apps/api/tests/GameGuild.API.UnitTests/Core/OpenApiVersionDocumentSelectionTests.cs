using System.Globalization;
using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using GameGuild.API.Core.OpenApi;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiVersionDocumentSelectionTests
{
    [Theory]
    [InlineData("1.1", "'v'VVV")]
    [InlineData("2.0", "'v'VVV")]
    [InlineData("1.2.3", "'v'VVV")]
    [InlineData("1.2.3-beta.1", "'preview-'VVV")]
    [InlineData("2026-10-04", "VVV")]
    [InlineData("1.1", "'release-v'VVV")]
    public void Explorer_selected_version_is_included_only_in_its_base_and_localized_documents(string version, string groupFormat)
    {
        var parsed = new SemanticApiVersionParser().Parse(version.AsSpan());
        var group = parsed.ToString(groupFormat, CultureInfo.InvariantCulture);
        var originalGroup = new ApiVersion(1, 0).ToString(groupFormat, CultureInfo.InvariantCulture);
        var predicate = CreatePredicate(groupFormat, new ApiVersionDescription(new ApiVersion(1, 0), originalGroup, false),
            new ApiVersionDescription(parsed, group, false));
        var description = ControllerDescription(typeof(MultipleVersionActions), nameof(MultipleVersionActions.Read));
        description.GroupName = "Administration";
        description.SetApiVersion(parsed);

        Assert.True(predicate(group, description));
        Assert.True(predicate(group + ".pt-BR", description));
        Assert.True(predicate(group + ".en", description));
        Assert.False(predicate(originalGroup, description));
        Assert.False(predicate(originalGroup + ".pt-BR", description));
    }

    [Fact]
    public void Explicit_action_mapping_does_not_leak_into_the_other_controller_version()
    {
        var predicate = CreatePredicate(new ApiVersionDescription(new ApiVersion(1, 0), "v1", false),
            new ApiVersionDescription(new ApiVersion(2, 0), "v2", false));
        var description = ControllerDescription(typeof(MultipleVersionActions), nameof(MultipleVersionActions.ReadV2));

        Assert.True(predicate("v2", description));
        Assert.False(predicate("v1", description));
        Assert.True(predicate("v2.pt-BR", description));
        Assert.False(predicate("v1.pt-BR", description));
    }

    [Fact]
    public void Non_controller_explorer_metadata_also_uses_the_selected_version()
    {
        var predicate = CreatePredicate("'release-'VVV", new ApiVersionDescription(new ApiVersion(1, 1), "release-1.1", false));
        var description = new ApiDescription { ActionDescriptor = new ActionDescriptor(), GroupName = "Operations" };
        description.SetApiVersion(new ApiVersion(1, 1));

        Assert.True(predicate("release-1.1", description));
        Assert.True(predicate("release-1.1.pt-BR", description));
    }

    [Fact]
    public void Neutral_actions_are_available_in_all_registered_documents()
    {
        var predicate = CreatePredicate(new ApiVersionDescription(new ApiVersion(1, 0), "v1", false),
            new ApiVersionDescription(new ApiVersion(2, 0), "v2", false));
        var description = ControllerDescription(typeof(NeutralActions), nameof(NeutralActions.Read));

        Assert.True(predicate("v1", description));
        Assert.True(predicate("v2", description));
        Assert.True(predicate("v2.pt-BR", description));
        Assert.False(predicate("unknown", description));
    }

    [Fact]
    public void Undiscovered_document_names_do_not_match_controller_attributes()
    {
        var predicate = CreatePredicate(new ApiVersionDescription(new ApiVersion(2, 0), "v2", false));
        var description = ControllerDescription(typeof(MultipleVersionActions), nameof(MultipleVersionActions.Read));

        Assert.False(predicate("v1", description));
        Assert.False(predicate("v1.pt-BR", description));
    }

    private static Func<string, ApiDescription, bool> CreatePredicate(params ApiVersionDescription[] descriptions) =>
        CreatePredicate(VersionedOpenApiDocumentCatalog.DefaultGroupNameFormat, descriptions);

    private static Func<string, ApiDescription, bool> CreatePredicate(string groupFormat, params ApiVersionDescription[] descriptions)
    {
        var services = new ServiceCollection();
        var versionOptions = GameGuild.Configuration.PresentationLayer.ApiVersioning.ApiVersioningOptions.CreateDefault();
        versionOptions.GroupNameFormat = groupFormat;
        services.AddSingleton(versionOptions);
        var versions = new Mock<IApiVersionDescriptionProvider>();
        versions.SetupGet(value => value.ApiVersionDescriptions).Returns(descriptions);
        services.AddSingleton(versions.Object);
        services.SetupOpenApi(new ConfigurationBuilder().Build(), OpenApiOptions.CreateDefault(),
            new OpenApiLocalizationOptions
            {
                Locales = new Dictionary<string, OpenApiLocalizedDocumentOptions>
                {
                    ["pt-BR"] = new(),
                    ["en"] = new()
                }
            });
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value.SwaggerGeneratorOptions.DocInclusionPredicate;
    }

    private static ApiDescription ControllerDescription(Type controller, string method) => new()
    {
        ActionDescriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = controller.GetTypeInfo(),
            MethodInfo = controller.GetMethod(method)!,
            ControllerName = controller.Name
        }
    };

    [ApiVersion("1.0")]
    [ApiVersion("1.1")]
    [ApiVersion("2.0")]
    [ApiVersion("2026-10-04")]
    [SemanticApiVersion("1.2.3")]
    [SemanticApiVersion("1.2.3-beta.1")]
    private sealed class MultipleVersionActions
    {
        public string Read() => "Shared versioned action";

        [MapToApiVersion("2.0")]
        public string ReadV2() => "Version two action";
    }

    [ApiVersionNeutral]
    private sealed class NeutralActions
    {
        public string Read() => "Neutral action";
    }
}
