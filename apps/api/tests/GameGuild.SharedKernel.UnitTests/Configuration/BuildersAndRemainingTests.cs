using FluentAssertions;
using GameGuild.Configuration.PresentationLayer;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;
using GameGuild.Configuration.PresentationLayer.CORS;
using GameGuild.Configuration.PresentationLayer.HealthChecks;
using GameGuild.Configuration.PresentationLayer.Localization;
using GameGuild.Configuration.PresentationLayer.HttpLogging;
using GameGuild.Configuration.PresentationLayer.ModelValidation;
using GameGuild.Configuration.PresentationLayer.ProblemDetails;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.Configuration.PresentationLayer.RequestContext;
using GameGuild.Configuration.PresentationLayer.ResponseCaching;
using GameGuild.Configuration.PresentationLayer.ResponseCompression;
using GameGuild.Configuration.PresentationLayer.FeatureFlags;
using GameGuild.Configuration.PresentationLayer.SignalR;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using GameGuild.Configuration.PresentationLayer.ApiExplorer;
using Microsoft.Extensions.Configuration;

namespace GameGuild.SharedKernel.UnitTests.Configuration;

public class ApiVersioningOptionsBuilderTests
{
    [Fact]
    public void Create_ShouldReturnDefaultInstance()
    {
        var options = ApiVersioningOptionsBuilder.Create();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnValidatedInstance()
    {
        var options = ApiVersioningOptionsBuilder.Build();
        options.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ApiVersionReadingStrategy.UrlSegment)]
    [InlineData(ApiVersionReadingStrategy.QueryString)]
    [InlineData(ApiVersionReadingStrategy.Header)]
    [InlineData(ApiVersionReadingStrategy.MediaType)]
    [InlineData(ApiVersionReadingStrategy.UrlSegmentAndQueryString)]
    [InlineData(ApiVersionReadingStrategy.UrlSegmentAndHeader)]
    [InlineData(ApiVersionReadingStrategy.All)]
    public void CreateReader_EachStrategy_ShouldReturnReader(ApiVersionReadingStrategy strategy)
    {
        var options = ApiVersioningOptionsBuilder.Create();
        var reader = ApiVersioningOptionsBuilder.CreateReader(strategy, options);
        reader.Should().NotBeNull();
    }
}

public class ApiExplorerOptionsBuilderTests
{
    [Fact]
    public void Create_ShouldReturnInstance()
    {
        var options = ApiExplorerOptionsBuilder.Create();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnValidatedInstance()
    {
        var options = ApiExplorerOptionsBuilder.Build();
        options.Should().NotBeNull();
    }
}

public class PresentationLayerOptionsBuilderNestedDefaultsTests
{
    [Fact]
    public void Create_WithEmptyConfiguration_ShouldPreserveNestedDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = PresentationLayerOptionsBuilder.Create(configuration);

        options.OpenApi.Should().NotBeNull();
        options.OpenApi!.Title.Should().Be("GameGuild API");
        options.ApiVersioning.Should().NotBeNull();
        options.ApiExplorer.Should().NotBeNull();
    }
}

public class LocalizationOptionsBuilderTests
{
    [Fact]
    public void Create_ShouldReturnInstance()
    {
        var options = LocalizationOptionsBuilder.Create();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnValidatedInstance()
    {
        var options = LocalizationOptionsBuilder.Build();
        options.Should().NotBeNull();
    }
}

public class HttpLoggingOptionsBuilderTests
{
    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = HttpLoggingOptionsBuilder.CreateDefault();
        options.Should().NotBeNull();
    }
}

public class ModelValidationOptionsBuilderTests
{
    [Fact]
    public void Create_ShouldReturnInstance()
    {
        var options = ModelValidationOptionsBuilder.Create();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnValidatedInstance()
    {
        var options = ModelValidationOptionsBuilder.Build();
        options.Should().NotBeNull();
    }
}

public class ProblemDetailsOptionsBuilderTests
{
    [Fact]
    public void Create_ShouldReturnInstance()
    {
        var options = ProblemDetailsOptionsBuilder.Create();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnValidatedInstance()
    {
        var options = ProblemDetailsOptionsBuilder.Build();
        options.Should().NotBeNull();
    }
}

public class RequestContextOptionsBuilderTests
{
    [Fact]
    public void Create_ShouldReturnInstance()
    {
        var options = RequestContextOptionsBuilder.Create();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnValidatedInstance()
    {
        var options = RequestContextOptionsBuilder.Build();
        options.Should().NotBeNull();
    }
}

public class ResponseCachingOptionsBuilderTests
{
    [Fact]
    public void Create_ShouldReturnInstance()
    {
        var options = ResponseCachingOptionsBuilder.Create();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldReturnValidatedInstance()
    {
        var options = ResponseCachingOptionsBuilder.Build();
        options.Should().NotBeNull();
    }
}

public class RateLimitPoliciesTests
{
    [Fact]
    public void AllConstants_ShouldBeNonEmpty()
    {
        RateLimitPolicies.Authentication.Should().NotBeEmpty();
        RateLimitPolicies.Authorization.Should().NotBeEmpty();
        RateLimitPolicies.Internal.Should().NotBeEmpty();
        RateLimitPolicies.Api.Should().NotBeEmpty();
        RateLimitPolicies.PerTenant.Should().NotBeEmpty();
        RateLimitPolicies.PerUser.Should().NotBeEmpty();
        RateLimitPolicies.Bursty.Should().NotBeEmpty();
        RateLimitPolicies.ApiKey.Should().NotBeEmpty();
        RateLimitPolicies.ExpensiveOperations.Should().NotBeEmpty();
        RateLimitPolicies.PerIp.Should().NotBeEmpty();
        RateLimitPolicies.Webhook.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("authentication")]
    [InlineData("authorization")]
    [InlineData("internal")]
    [InlineData("api")]
    [InlineData("per-tenant")]
    [InlineData("per-user")]
    [InlineData("bursty")]
    [InlineData("api-key")]
    [InlineData("expensive-operations")]
    [InlineData("per-ip")]
    [InlineData("webhook")]
    public void AllConstants_ShouldHaveExpectedValues(string expectedValue)
    {
        var allPolicies = new[]
        {
            RateLimitPolicies.Authentication,
            RateLimitPolicies.Authorization,
            RateLimitPolicies.Internal,
            RateLimitPolicies.Api,
            RateLimitPolicies.PerTenant,
            RateLimitPolicies.PerUser,
            RateLimitPolicies.Bursty,
            RateLimitPolicies.ApiKey,
            RateLimitPolicies.ExpensiveOperations,
            RateLimitPolicies.PerIp,
            RateLimitPolicies.Webhook
        };
        allPolicies.Should().Contain(expectedValue);
    }
}

// Test Options classes' CreateDefault methods
public class AdditionalSubOptionsTests
{
    [Fact]
    public void LocalizationOptions_CreateDefault()
    {
        var options = LocalizationOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void HttpLoggingOptions_CreateDefault()
    {
        var options = HttpLoggingOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void ModelValidationOptions_CreateDefault()
    {
        var options = ModelValidationOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void ProblemDetailsOptions_CreateDefault()
    {
        var options = ProblemDetailsOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void RequestContextOptions_CreateDefault()
    {
        var options = RequestContextOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void ResponseCachingOptions_CreateDefault()
    {
        var options = ResponseCachingOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void ResponseCompressionOptions_CreateDefault()
    {
        var options = ResponseCompressionOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagsOptions_CreateDefault()
    {
        var options = FeatureFlagsOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void ApiExplorerOptions_CreateDefault()
    {
        var options = ApiExplorerOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Controllers_CreateDefault()
    {
        var options = GameGuild.Configuration.PresentationLayer.Controllers.ControllersOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Endpoints_CreateDefault()
    {
        var options = GameGuild.Configuration.PresentationLayer.Endpoints.EndpointsOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void CorsOptions_CreateDefault()
    {
        var options = CorsOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void HealthChecksOptions_CreateDefault()
    {
        var options = HealthChecksOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void GraphQLOptions_CreateDefault()
    {
        var options = GraphQLOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void OpenApiOptions_CreateDefault()
    {
        var options = OpenApiOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void SignalROptions_CreateDefault()
    {
        var options = SignalROptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void RateLimitingOptions_CreateDefault()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void ApiVersioningOptions_CreateDefault()
    {
        var options = ApiVersioningOptions.CreateDefault();
        options.Should().NotBeNull();
    }
}
