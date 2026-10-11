using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameGuild.API.IntegrationTests.Infrastructure;

/// <summary>
/// Independently owned test host with a caller-provided configuration callback.
/// The caller disposes this factory after each test.
/// </summary>
internal sealed class ConfiguredApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Action<IWebHostBuilder> _configuration;

    public ConfiguredApiWebApplicationFactory(Action<IWebHostBuilder> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => _configuration(builder);
}
