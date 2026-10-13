using FluentAssertions;
using GameGuild.Identity.Provisioning.Scim;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests;

/// <summary>
///     Protocol contract units: pagination parsing, ServiceProviderConfig advertisement,
/// and discovery document shapes.
/// </summary>
public sealed class ScimProtocolContractTests
{
    private static readonly ScimProvisioningOptions Options = new()
    {
        Enabled = true,
        DefaultPageSize = 100,
        MaxPageSize = 200
    };

    [Fact]
    public void PageRequest_DefaultsToOneBasedStartAndDefaultCount()
    {
        var page = ScimPageRequest.Parse(null, null, Options);

        page.StartIndex.Should().Be(1);
        page.Count.Should().Be(100);
    }

    [Theory]
    [InlineData("2", "25", 2, 25)]
    [InlineData("1", "0", 1, 0)]
    public void PageRequest_ParsesExplicitValues(string startIndex, string count, int expectedStart, int expectedCount)
    {
        var page = ScimPageRequest.Parse(startIndex, count, Options);

        page.StartIndex.Should().Be(expectedStart);
        page.Count.Should().Be(expectedCount);
    }

    [Fact]
    public void PageRequest_ClampsCountToTheConfiguredMaximum()
    {
        var page = ScimPageRequest.Parse("1", "5000", Options);

        page.Count.Should().Be(200);
    }

    [Theory]
    [InlineData("0", null)]
    [InlineData("-1", null)]
    [InlineData("abc", null)]
    [InlineData(null, "-3")]
    [InlineData(null, "nope")]
    public void PageRequest_RejectsInvalidValues(string? startIndex, string? count)
    {
        var act = () => ScimPageRequest.Parse(startIndex, count, Options);

        act.Should().Throw<ScimException>().Which.Status.Should().Be(400);
    }

    [Fact]
    public void ServiceProviderConfig_AdvertisesExactlyWhatIsImplemented()
    {
        // The discovery document nests anonymous types, which are internal to the
        // producing assembly and cannot be reached through dynamic binding from the
        // test assembly — assert on the serialized JSON shape instead.
        var serialized = System.Text.Json.JsonSerializer.Serialize(
            ScimDiscoveryDocuments.BuildServiceProviderConfig(Options));

        using var document = System.Text.Json.JsonDocument.Parse(serialized);
        var config = document.RootElement;

        config.GetProperty("patch").GetProperty("supported").GetBoolean().Should().BeTrue();

        var filter = config.GetProperty("filter");
        filter.GetProperty("supported").GetBoolean().Should().BeTrue();
        filter.GetProperty("maxResults").GetInt32().Should().Be(200);

        var bulk = config.GetProperty("bulk");
        bulk.GetProperty("supported").GetBoolean().Should().BeTrue();
        bulk.GetProperty("maxOperations").GetInt32().Should().BeGreaterThan(0);

        config.GetProperty("sort").GetProperty("supported").GetBoolean().Should().BeFalse("sorting is not implemented");
        config.GetProperty("etag").GetProperty("supported").GetBoolean().Should().BeFalse("ETags are not implemented");
        config.GetProperty("changeLog").GetProperty("supported").GetBoolean().Should().BeFalse("change log is not implemented");
    }

    [Fact]
    public void ResourceTypes_AdvertiseUsersAndGroups()
    {
        var document = ScimDiscoveryDocuments.BuildResourceTypes() as Dictionary<string, object?>;

        document!["Resources"].Should().BeAssignableTo<IEnumerable<object>>().Which.Should().HaveCount(2);
    }

    [Fact]
    public void Schemas_ResolveByUrn_AndUnknownUrnsReturnNull()
    {
        ScimDiscoveryDocuments.BuildSchema(ScimConstants.UserSchemaUrn).Should().NotBeNull();
        ScimDiscoveryDocuments.BuildSchema(ScimConstants.GroupSchemaUrn).Should().NotBeNull();
        ScimDiscoveryDocuments.BuildSchema("urn:unknown").Should().BeNull();
    }

    [Fact]
    public void ScimErrorBody_CarriesSchemaStatusTypeAndDetail()
    {
        var body = ScimErrorBody.From(ScimException.InvalidFilter("nope"));

        body.Schemas.Should().BeEquivalentTo([ScimConstants.ErrorSchema]);
        body.Status.Should().Be(400);
        body.ScimType.Should().Be("invalidFilter");
        body.Detail.Should().Be("nope");
    }

    [Fact]
    public void ListResponse_SerializesResourcesMemberWithRfcCapitalR()
    {
        // RFC 7644 §3.4.2.3 spells this member "Resources" while every other member is
        // camelCase; the host's camelCase naming policy must not rename it.
        var payload = new ScimListResponse<ScimUserResource>
        {
            TotalResults = 1,
            StartIndex = 1,
            ItemsPerPage = 1,
            Resources = Array.Empty<ScimUserResource>()
        };

        var serialized = System.Text.Json.JsonSerializer.Serialize(
            payload,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        using var document = System.Text.Json.JsonDocument.Parse(serialized);
        document.RootElement.TryGetProperty("Resources", out var resources).Should().BeTrue(
            "RFC 7644 §3.4.2.3 requires the member spelled 'Resources' regardless of the host naming policy");
        resources.GetArrayLength().Should().Be(0);
        document.RootElement.TryGetProperty("totalResults", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("itemsPerPage", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("startIndex", out _).Should().BeTrue();
    }

    [Fact]
    public void ScimProvisioningOptions_Validation_GuardsPageSizes()
    {
        var act = () => new ScimProvisioningOptions { DefaultPageSize = 500, MaxPageSize = 100 }.Validate();

        act.Should().Throw<InvalidOperationException>();
    }
}
