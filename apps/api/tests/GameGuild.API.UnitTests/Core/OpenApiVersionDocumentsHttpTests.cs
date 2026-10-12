using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using GameGuild.API.Core.OpenApi;
using GameGuild.API.Setup;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.SwaggerUI;
using Xunit.Abstractions;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiVersionDocumentsHttpTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, "'v'VVV")]
    [InlineData(false, "'release-'VVV")]
    [InlineData(true, "'v'VVV")]
    [InlineData(true, "'release-'VVV")]
    [InlineData(false, "'v'GGGGVVV")]
    [InlineData(true, "'v'GGGGVVV")]
    [InlineData(false, "'v'GVVV")]
    [InlineData(true, "'v'GVVV")]
    [InlineData(false, "'Version-'GVVV")]
    [InlineData(true, "'Version-'GVVV")]
    public async Task Serialized_version_documents_and_locales_contain_only_matching_actions(
        bool customControllerGroup, string groupFormat)
    {
        using var host = await CreateHostAsync(customControllerGroup, groupFormat);
        using var client = host.GetTestClient();
        output.WriteLine("Discovered groups: " + string.Join(", ", host.Services
            .GetRequiredService<IApiVersionDescriptionProvider>().ApiVersionDescriptions
            .Select(description => description.GroupName + "=" + description.ApiVersion)));
        var versions = new Dictionary<string, (string Payload, string NumericDocumentVersion)>
        {
            ["1.0"] = ("first", "1"),
            ["1.1"] = ("minor", "1.1"),
            ["2.0"] = ("second", "2"),
            ["1.2.0-1"] = ("numeric", "1.2.0-1"),
            ["1.2.3"] = ("patch", "1.2.3"),
            ["1.2.3-rc-1"] = ("hyphen", "1.2.3-rc-1"),
            ["1.2.4-beta.1"] = ("beta", "1.2.4-beta.1"),
            ["2026-10-04"] = ("date", "")
        };
        foreach (var (version, expected) in versions)
        {
            // Independent wire-name expectations must not repeat the production formatter.
            var prefix = groupFormat.StartsWith("'Version-'", StringComparison.Ordinal) ? "Version-"
                : groupFormat.StartsWith("'release-'", StringComparison.Ordinal) ? "release-" : "v";
            var documentVersion = version == "2026-10-04" && groupFormat.Contains('G')
                ? "2026-10-04" : expected.NumericDocumentVersion;
            var group = prefix + documentVersion;
            foreach (var locale in new[] { "", ".pt-BR", ".en" })
            {
                using var response = await client.GetAsync($"/swagger/{group}{locale}/swagger.json");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = document.RootElement;
                Assert.StartsWith("3.0.", root.GetProperty("openapi").GetString());
                var paths = root.GetProperty("paths");
                var samplePaths = paths.EnumerateObject()
                    .Where(path => path.Name.EndsWith("/version-doc-sample", StringComparison.Ordinal)).ToArray();
                if (version.Count(character => character == '.') >= 2)
                {
                    Assert.Equal($"/api/v{version}/version-doc-sample", Assert.Single(samplePaths).Name);
                }
                var operation = Assert.Single(samplePaths).Value.GetProperty("get");
                Assert.True(operation.GetProperty("x-gameguild-allow-anonymous").GetBoolean());
                Assert.True(paths.TryGetProperty("/api/openapi-neutral-sample", out _));
                Assert.Equal(version == "1.0", paths.TryGetProperty("/api/v1/reporting-doc-sample", out _));
                var schemas = root.GetProperty("components").GetProperty("schemas");
                var schema = Assert.Single(schemas.EnumerateObject(),
                    item => item.Name.EndsWith("_OpenApiVersionSampleDto", StringComparison.Ordinal)).Value;
                Assert.False(string.IsNullOrWhiteSpace(schema.GetProperty("description").GetString()));
                Assert.Contains(schema.GetProperty("required").EnumerateArray(),
                    item => item.GetString() == "name");
                var nameSchema = schema.GetProperty("properties").GetProperty("name");
                Assert.Equal(1, nameSchema.GetProperty("minLength").GetInt32());
                Assert.Equal(64, nameSchema.GetProperty("maxLength").GetInt32());
                var exampleName = schema.GetProperty("example").GetProperty("name").GetString();
                Assert.Matches(nameSchema.GetProperty("pattern").GetString()!, exampleName!);
                if (locale == ".pt-BR")
                {
                    Assert.Equal("API de teste", root.GetProperty("info").GetProperty("title").GetString());
                }
                else if (locale == ".en")
                {
                    Assert.Equal("Test API", root.GetProperty("info").GetProperty("title").GetString());
                }
            }
            using var actualResponse = await client.GetAsync($"/api/v{version}/version-doc-sample");
            Assert.Equal(HttpStatusCode.OK, actualResponse.StatusCode);
            using var actualBody = JsonDocument.Parse(await actualResponse.Content.ReadAsStringAsync());
            Assert.Equal(expected.Payload, actualBody.RootElement.GetProperty("name").GetString());
        }
        using var unknown = await client.GetAsync("/swagger/unregistered/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var unknownVersion = await client.GetAsync("/api/v1.2.5/version-doc-sample");
        Assert.NotEqual(HttpStatusCode.OK, unknownVersion.StatusCode);
        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        var uiHtml = await ui.Content.ReadAsStringAsync();
        Assert.Contains("Swagger UI", uiHtml, StringComparison.Ordinal);
        Assert.Contains("src=\"index.js\"", uiHtml, StringComparison.Ordinal);
        using var uiInitializer = await client.GetAsync("/swagger/index.js");
        Assert.Equal(HttpStatusCode.OK, uiInitializer.StatusCode);
        var uiConfiguration = await uiInitializer.Content.ReadAsStringAsync();
        var uiOptions = new SwaggerUIOptions();
        PipelineExtensions.ConfigureOpenApiDocuments(uiOptions,
            host.Services.GetRequiredService<IApiVersionDescriptionProvider>(), "v1",
            host.Services.GetRequiredService<OpenApiLocalizationOptions>(), groupFormat);
        foreach (var endpoint in uiOptions.ConfigObject.Urls)
        {
            Assert.Contains(endpoint.Url, uiConfiguration, StringComparison.Ordinal);
            using var response = await client.GetAsync(endpoint.Url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = document.RootElement.GetProperty("paths");
            if (endpoint.Url.Contains("/Reporting", StringComparison.Ordinal))
            {
                Assert.True(paths.TryGetProperty("/api/v1/reporting-doc-sample", out _));
                Assert.DoesNotContain(paths.EnumerateObject(),
                    path => path.Name.EndsWith("/version-doc-sample", StringComparison.Ordinal));
                var inherited = Assert.Single(document.RootElement.GetProperty("components")
                    .GetProperty("schemas").EnumerateObject(),
                    item => item.Name.EndsWith("_OpenApiInheritedVersionSampleDto", StringComparison.Ordinal)).Value;
                Assert.True(inherited.GetProperty("properties").TryGetProperty("name", out _));
                Assert.True(inherited.GetProperty("properties").TryGetProperty("category", out _));
                Assert.False(string.IsNullOrWhiteSpace(inherited.GetProperty("description").GetString()));
                Assert.True(inherited.GetProperty("example").TryGetProperty("name", out _));
            }
            if (endpoint.Url.Contains("/Administration.", StringComparison.Ordinal))
            {
                Assert.False(paths.TryGetProperty("/api/v1/reporting-doc-sample", out _));
            }
            Assert.True(paths.TryGetProperty("/api/openapi-neutral-sample", out _));
        }
    }

    private static async Task<IHost> CreateHostAsync(bool customControllerGroup, string groupFormat)
    {
        var options = GameGuild.Configuration.PresentationLayer.ApiVersioning.ApiVersioningOptions.CreateDefault();
        options.VersionFormat = ApiVersionFormatKind.SemanticVersion;
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.GroupNameFormat = groupFormat;
        var localization = new OpenApiLocalizationOptions
        {
            Locales = new Dictionary<string, OpenApiLocalizedDocumentOptions>
            {
                ["pt-BR"] = new() { Title = "API de teste" },
                ["en"] = new() { Title = "Test API" }
            }
        };
        return await new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(services =>
            {
                var configuration = new ConfigurationBuilder().Build();
                services.AddRouting();
                services.AddControllers(mvc =>
                {
                    if (customControllerGroup)
                    {
                        mvc.Conventions.Add(new SampleGroupConvention());
                    }
                }).AddApplicationPart(typeof(OpenApiVersionSampleController).Assembly)
                    .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new SampleControllerFilter()));
                services.SetupApiVersioning(configuration, options);
                services.SetupApiExplorer(configuration, options);
                services.SetupOpenApi(configuration, OpenApiOptions.CreateDefault(), localization);
            });
            web.Configure(app =>
            {
                app.UseSwagger();
                app.UseSwaggerUI(ui => PipelineExtensions.ConfigureOpenApiDocuments(ui,
                    app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>(), "v1", localization, groupFormat));
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        }).StartAsync();
    }

    private sealed class SampleGroupConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            if (controller.ControllerType.AsType() == typeof(OpenApiVersionSampleController))
            {
                controller.ApiExplorer.GroupName = "Administration";
            }
        }
    }

    private sealed class SampleControllerFilter : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            ArgumentNullException.ThrowIfNull(parts);
            foreach (var controller in feature.Controllers.Where(type =>
                type.AsType() != typeof(OpenApiVersionSampleController)
                && type.AsType() != typeof(OpenApiReportingSampleController)
                && type.AsType() != typeof(OpenApiNeutralSampleController)).ToArray())
            {
                feature.Controllers.Remove(controller);
            }
        }
    }
}

[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "Reporting")]
[Route("api/v{version:apiVersion}/reporting-doc-sample")]
public sealed class OpenApiReportingSampleController : ControllerBase
{
    [HttpGet]
    public OpenApiInheritedVersionSampleDto Read() => new("report", "summary");
}

public record OpenApiVersionSampleDto(
    [property: Required, MinLength(1), MaxLength(64), RegularExpression("^[a-z]+$")] string Name);

public sealed record OpenApiInheritedVersionSampleDto(string Name, string Category) : OpenApiVersionSampleDto(Name);

[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[ApiVersion("1.1")]
[ApiVersion("2.0")]
[ApiVersion("2026-10-04")]
[SemanticApiVersion("1.2.0-1")]
[SemanticApiVersion("1.2.3")]
[SemanticApiVersion("1.2.3-rc-1")]
[SemanticApiVersion("1.2.4-beta.1")]
[Route("api/v{version:apiVersion}/version-doc-sample")]
public sealed class OpenApiVersionSampleController : ControllerBase
{
    [HttpGet, MapToApiVersion("1.0")]
    public OpenApiVersionSampleDto First() => new("first");

    [HttpGet, MapToApiVersion("1.1")]
    public OpenApiVersionSampleDto Minor() => new("minor");

    [HttpGet, MapToApiVersion("2.0")]
    public OpenApiVersionSampleDto Second() => new("second");

    [HttpGet, MapToSemanticApiVersion("1.2.0-1")]
    public OpenApiVersionSampleDto NumericPrerelease() => new("numeric");

    [HttpGet, MapToSemanticApiVersion("1.2.3")]
    public OpenApiVersionSampleDto Patch() => new("patch");

    [HttpGet, MapToSemanticApiVersion("1.2.3-rc-1")]
    public OpenApiVersionSampleDto HyphenatedPrerelease() => new("hyphen");

    [HttpGet, MapToSemanticApiVersion("1.2.4-beta.1")]
    public OpenApiVersionSampleDto Beta() => new("beta");

    [HttpGet, MapToApiVersion("2026-10-04")]
    public OpenApiVersionSampleDto Date() => new("date");
}

[ApiController]
[AllowAnonymous]
[ApiVersionNeutral]
[Route("api/openapi-neutral-sample")]
public sealed class OpenApiNeutralSampleController : ControllerBase
{
    [HttpGet]
    public OpenApiVersionSampleDto Read() => new("neutral");
}
