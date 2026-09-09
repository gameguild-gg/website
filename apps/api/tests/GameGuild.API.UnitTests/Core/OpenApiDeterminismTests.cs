using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiDeterminismTests
{
    [Fact]
    public void UsesReleaseVersionWithoutChangingTheDocumentKey()
    {
        using var provider = CreateProvider();
        var options = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        var expected = typeof(OpenApiExtensions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];
        Assert.Equal(expected, options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Version);
    }

    [Fact]
    public void SortsPathsAndSchemaMembersWithoutRecursingForeverOnSharedReferences()
    {
        using var provider = CreateProvider();
        var options = provider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        var descriptor = Assert.Single(options.DocumentFilterDescriptors,
            value => value.Type.Name == "DeterministicOpenApiDocumentFilter");
        var filter = (IDocumentFilter)Activator.CreateInstance(descriptor.Type)!;
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, OpenApiSchema> { ["z"] = new(), ["a"] = new() },
            Required = new HashSet<string> { "z", "a" }
        };
        schema.Items = schema;
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths { ["/z"] = new(), ["/a"] = new() },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema> { ["Z"] = schema, ["A"] = schema }
            }
        };

        filter.Apply(document, null!);
        filter.Apply(document, null!);

        Assert.Equal(new[] { "/a", "/z" }, document.Paths.Keys);
        Assert.Equal(new[] { "A", "Z" }, document.Components.Schemas.Keys);
        Assert.Equal(new[] { "a", "z" }, schema.Properties.Keys);
        Assert.Equal(new[] { "a", "z" }, schema.Required);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.SetupOpenApi(new ConfigurationBuilder().Build(), OpenApiOptions.CreateDefault());
        return services.BuildServiceProvider();
    }
}
