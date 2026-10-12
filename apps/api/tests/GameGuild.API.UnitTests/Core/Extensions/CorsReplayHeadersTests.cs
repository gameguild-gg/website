using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlatformCorsOptions = GameGuild.Configuration.PresentationLayer.CORS.CorsOptions;
using FrameworkCorsOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace GameGuild.API.UnitTests.Core.Extensions;

public sealed class CorsReplayHeadersTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ExposesReplayDigestWithoutChangingConfiguredRequestPolicy(int scenario)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(scenario == 2 ? new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://audit.example",
                ["Cors:AllowedMethods:0"] = "POST",
                ["Cors:AllowedHeaders:0"] = "Authorization"
            } : new Dictionary<string, string?>()).Build();
        var configured = scenario == 1 ? new PlatformCorsOptions
        {
            AllowedOrigins = ["https://audit.example"],
            AllowedMethods = ["POST"],
            AllowedHeaders = ["Authorization"]
        } : null;
        services.AddLogging();
        services.SetupCors(configuration, configured);
        using var provider = services.BuildServiceProvider();
        var cors = provider.GetRequiredService<IOptions<FrameworkCorsOptions>>().Value;
        var policy = cors.GetPolicy(cors.DefaultPolicyName);
        Assert.NotNull(policy);
        Assert.Equal(["X-Audit-Snapshot-SHA256"], policy.ExposedHeaders);
        Assert.False(policy.SupportsCredentials);
        if (scenario == 0)
        {
            Assert.True(policy.AllowAnyOrigin);
            Assert.True(policy.AllowAnyMethod);
            Assert.True(policy.AllowAnyHeader);
        }
        else
        {
            Assert.Equal(["https://audit.example"], policy.Origins);
            Assert.Equal(["POST"], policy.Methods);
            Assert.Equal(["Authorization"], policy.Headers);
            Assert.False(policy.AllowAnyOrigin);
            Assert.False(policy.AllowAnyMethod);
            Assert.False(policy.AllowAnyHeader);
        }
    }
}
