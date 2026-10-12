using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using GameGuild.API.Core.OpenApi;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiVersionDocumentCatalogTests
{
    [Fact]
    public void Single_version_group_alias_keeps_its_name_and_is_scoped_to_its_controller_group()
    {
        var selector = new VersionedOpenApiDocumentSelector(
        [
            new(new ApiVersion(1, 0), "Administration", false),
            new(new ApiVersion(1, 0), "Reporting", false)
        ]);
        Assert.Equal(new[] { "Administration", "Reporting", "v1" },
            selector.Descriptions.Select(description => description.GroupName));
        var action = Description("Reporting", new ApiVersion(1, 0));
        Assert.True(selector.Includes("Reporting", action));
        Assert.True(selector.Includes("v1", action));
        Assert.False(selector.Includes("Administration", action));
    }

    [Fact]
    public void Multi_version_groups_have_unique_version_qualified_aliases()
    {
        var selector = new VersionedOpenApiDocumentSelector(
        [
            new(new ApiVersion(1, 0), "Administration", true),
            new(new ApiVersion(1, 1), "Administration", false)
        ]);
        Assert.Equal(new[] { "Administration.v1", "Administration.v1.1", "v1", "v1.1" },
            selector.Descriptions.Select(description => description.GroupName));
        var action = Description("Administration", new ApiVersion(1, 1));
        Assert.True(selector.Includes("Administration.v1.1", action));
        Assert.True(selector.Includes("v1.1", action));
        Assert.False(selector.Includes("Administration.v1", action));
        Assert.False(selector.Includes("v1", action));
    }

    [Fact]
    public void Version_documents_are_not_deprecated_while_any_group_is_current()
    {
        var catalog = new VersionedOpenApiDocumentCatalog(
        [
            new(new ApiVersion(1, 0), "Administration", true),
            new(new ApiVersion(1, 0), "Reporting", false)
        ]);
        Assert.True(catalog.TryGetDescription("v1", out var version));
        Assert.False(version!.IsDeprecated);
        Assert.True(catalog.TryGetDescription("Administration", out var administration));
        Assert.True(administration!.IsDeprecated);
    }

    [Fact]
    public void Existing_aliases_are_reserved_before_allocating_new_qualified_names()
    {
        ApiVersionDescription[] descriptions =
        [
            new(new ApiVersion(1, 0), "Administration", false),
            new(new ApiVersion(2, 0), "Administration", false),
            new(new ApiVersion(3, 0), "Administration.v1", false)
        ];
        var catalog = new VersionedOpenApiDocumentCatalog(descriptions);
        Assert.True(catalog.TryGetDescription("Administration.v1", out var existing));
        Assert.Equal(new ApiVersion(3, 0), existing!.ApiVersion);
        Assert.True(catalog.TryGetDescription("Administration.v1.2", out var qualified));
        Assert.Equal(new ApiVersion(1, 0), qualified!.ApiVersion);
        Assert.Equal(catalog.Descriptions.Select(description => description.GroupName),
            new VersionedOpenApiDocumentCatalog(descriptions.Reverse()).Descriptions.Select(description => description.GroupName));
    }

    [Fact]
    public void A_group_matching_another_version_name_does_not_replace_the_version_document()
    {
        var catalog = new VersionedOpenApiDocumentCatalog(
        [
            new(new ApiVersion(1, 0), "v2", false),
            new(new ApiVersion(2, 0), "v2", false)
        ]);
        Assert.True(catalog.TryGetDescription("v2", out var version));
        Assert.Equal(new ApiVersion(2, 0), version!.ApiVersion);
        Assert.True(catalog.TryGetDescription("v2.v1", out var alias));
        Assert.Equal(new ApiVersion(1, 0), alias!.ApiVersion);
    }

    [Theory]
    [InlineData("'v'V")]
    [InlineData("'all'")]
    public void Formats_collapsing_distinct_versions_fail_with_a_configuration_error(string format)
    {
        var error = Assert.Throws<ArgumentException>(() => new VersionedOpenApiDocumentCatalog(
        [
            new(new ApiVersion(1, 0), "Administration", false),
            new(new ApiVersion(1, 1), "Administration", false)
        ], format));
        Assert.Equal("groupNameFormat", error.ParamName);
        Assert.Contains("same document name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Date_versions_use_the_configured_group_date_format_without_collisions()
    {
        var first = new ApiVersion(new DateOnly(2026, 10, 3));
        var second = new ApiVersion(new DateOnly(2026, 10, 4));
        ApiVersionDescription[] descriptions = [new(first, "Daily", false), new(second, "Daily", false)];
        var selector = new VersionedOpenApiDocumentSelector(descriptions, "'v'GGGGVVV");
        Assert.True(selector.Includes("v2026-10-04", Description("Daily", second)));
        Assert.False(selector.Includes("v2026-10-03", Description("Daily", second)));
        Assert.Throws<ArgumentException>(() => new VersionedOpenApiDocumentCatalog(descriptions));
    }

    [Theory]
    [InlineData("'v'V")]
    [InlineData("'v'VV")]
    [InlineData("'all'")]
    public void Formats_collapsing_semantic_patch_versions_are_rejected(string format)
    {
        var error = Assert.Throws<ArgumentException>(() => new VersionedOpenApiDocumentCatalog(
        [
            new(new SemanticApiVersion(1, 2, 3), "Administration", false),
            new(new SemanticApiVersion(1, 2, 4), "Administration", false)
        ], format));
        Assert.Equal("groupNameFormat", error.ParamName);
        Assert.Contains("same document name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Quoted_version_literals_and_date_tokens_have_independently_expected_document_names()
    {
        var catalog = new VersionedOpenApiDocumentCatalog(
        [
            new(new SemanticApiVersion(1, 2, 3), "Administration", false),
            new(new ApiVersion(new DateOnly(2026, 10, 4)), "Daily", false)
        ], "'Version-'GVVV");

        Assert.True(catalog.TryGetDescription("Version-1.2.3", out var semantic));
        Assert.Equal(new SemanticApiVersion(1, 2, 3), semantic!.ApiVersion);
        Assert.True(catalog.TryGetDescription("Version-2026-10-04", out var date));
        Assert.Equal(new ApiVersion(new DateOnly(2026, 10, 4)), date!.ApiVersion);
    }

    private static ApiDescription Description(string group, ApiVersion version)
    {
        var description = new ApiDescription { ActionDescriptor = new ActionDescriptor(), GroupName = group };
        description.SetApiVersion(version);
        return description;
    }
}
