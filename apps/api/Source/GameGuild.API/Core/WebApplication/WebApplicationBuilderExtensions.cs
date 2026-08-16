namespace GameGuild.API;

using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

/// <summary>
///     Extension methods for WebApplicationBuilder following SOLID principles.
///     Provides fluent configuration with clean separation of concerns.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    ///     Configures environment variables and configuration sources.
    ///     Adds JSON configuration files with proper precedence and reload-on-change support.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance</param>
    /// <returns>The WebApplicationBuilder for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null</exception>
    public static WebApplicationBuilder AddAppSettings(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var environmentSettings = $"appsettings.{builder.Environment.EnvironmentName}.json";
        var hasApplicationSettings = builder.Configuration.Sources
            .OfType<JsonConfigurationSource>()
            .Any(source => string.Equals(source.Path, "appsettings.json", StringComparison.OrdinalIgnoreCase));
        var hasEnvironmentSettings = builder.Configuration.Sources
            .OfType<JsonConfigurationSource>()
            .Any(source => string.Equals(source.Path, environmentSettings, StringComparison.OrdinalIgnoreCase));

        if (!hasApplicationSettings || !hasEnvironmentSettings)
        {
            builder.Configuration.SetBasePath(AppContext.BaseDirectory);
        }

        if (!hasApplicationSettings)
        {
            builder.Configuration.AddJsonFile("appsettings.json", true, true);
        }

        if (!hasEnvironmentSettings)
        {
            builder.Configuration.AddJsonFile(environmentSettings, true, true);
        }

        return builder;
    }

    /// <summary>
    ///     Adds environment variables to the configuration pipeline.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance</param>
    /// <returns>The WebApplicationBuilder for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null</exception>
    public static WebApplicationBuilder AddEnvironmentVariables(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var hasUnprefixedEnvironmentVariables = builder.Configuration.Sources
            .OfType<EnvironmentVariablesConfigurationSource>()
            .Any(source => string.IsNullOrEmpty(source.Prefix));

        if (!hasUnprefixedEnvironmentVariables)
        {
            builder.Configuration.AddEnvironmentVariables();
        }

        return builder;
    }
}
