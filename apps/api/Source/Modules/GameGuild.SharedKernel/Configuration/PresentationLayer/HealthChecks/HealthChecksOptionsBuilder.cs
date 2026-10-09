using Microsoft.Extensions.Configuration;

namespace GameGuild.Configuration.PresentationLayer.HealthChecks;

/// <summary>
///     Builder for health checks options.
/// </summary>
public static class HealthChecksOptionsBuilder
{
    /// <summary>
    ///     Creates health checks options with default values.
    /// </summary>
    /// <returns>Default health checks options</returns>
    public static HealthChecksOptions Create() { return new HealthChecksOptions { HealthCheckPath = "/health", TimeoutSeconds = 30 }; }

    /// <summary>
    ///     Creates health checks options from a specific configuration section.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Configured health checks options</returns>
    public static HealthChecksOptions Create(IConfiguration configuration, string sectionName = "HealthChecks")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = Create();
        var section = configuration.GetSection(sectionName);

        if (section.Exists()) { section.Bind(options); }

        return options;
    }

    /// <summary>
    ///     Validates the provided health checks options.
    /// </summary>
    /// <param name="options">The options to validate</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public static void Validate(HealthChecksOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.HealthCheckPath))
        {
            throw new InvalidOperationException("Health check path cannot be null or empty.");
        }

        if (!options.HealthCheckPath.StartsWith('/'))
        {
            throw new InvalidOperationException("Health check path must start with '/'.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Timeout seconds must be greater than zero.");
        }
    }

    /// <summary>
    ///     Creates and validates health checks options with default values.
    /// </summary>
    /// <returns>Validated health checks options with default configuration</returns>
    public static HealthChecksOptions Build()
    {
        var options = Create();
        Validate(options);

        return options;
    }

    /// <summary>
    ///     Creates and validates health checks options from configuration.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Validated health checks options</returns>
    public static HealthChecksOptions Build(IConfiguration configuration, string sectionName = "HealthChecks")
    {
        var options = Create(configuration, sectionName);
        Validate(options);

        return options;
    }
}
