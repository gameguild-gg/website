using Microsoft.Extensions.Configuration;

namespace GameGuild.Configuration.PresentationLayer.ResponseCaching;

/// <summary>
///     Builder for response caching options.
/// </summary>
public static class ResponseCachingOptionsBuilder
{
    /// <summary>
    ///     Creates response caching options with default values.
    /// </summary>
    /// <returns>Default response caching options</returns>
    public static ResponseCachingOptions Create()
    {
        return new ResponseCachingOptions
        {
            EnableResponseCaching = true,
            MaximumBodySize = 64 * 1024 * 1024, // 64MB
            UseCaseSensitivePaths = false,
            DurationSeconds = 60
        };
    }

    /// <summary>
    ///     Creates response caching options from a specific configuration section.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Configured response caching options</returns>
    public static ResponseCachingOptions Create(IConfiguration configuration, string sectionName = "ResponseCaching")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = Create();
        var section = configuration.GetSection(sectionName);

        if (section.Exists()) { section.Bind(options); }

        return options;
    }

    /// <summary>
    ///     Validates the provided response caching options.
    /// </summary>
    /// <param name="options">The options to validate</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public static void Validate(ResponseCachingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumBodySize < 0)
        {
            throw new InvalidOperationException("Maximum body size cannot be negative.");
        }
    }

    /// <summary>
    ///     Creates and validates response caching options with default values.
    /// </summary>
    /// <returns>Validated response caching options with default configuration</returns>
    public static ResponseCachingOptions Build()
    {
        var options = Create();
        Validate(options);

        return options;
    }

    /// <summary>
    ///     Creates and validates response caching options from configuration.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Validated response caching options</returns>
    public static ResponseCachingOptions Build(IConfiguration configuration, string sectionName = "ResponseCaching")
    {
        var options = Create(configuration, sectionName);
        Validate(options);

        return options;
    }
}
