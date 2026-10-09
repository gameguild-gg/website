using Microsoft.Extensions.Configuration;

namespace GameGuild.Configuration.PresentationLayer.Localization;

/// <summary>
///     Builder for localization options.
/// </summary>
public static class LocalizationOptionsBuilder
{
    /// <summary>
    ///     Creates localization options with default values.
    /// </summary>
    /// <returns>Default localization options</returns>
    public static LocalizationOptions Create() { return new LocalizationOptions { DefaultCulture = "en-US", SupportedCultures =
        ["en-US"]
    }; }

    /// <summary>
    ///     Creates localization options from a specific configuration section.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Configured localization options</returns>
    public static LocalizationOptions Create(IConfiguration configuration, string sectionName = "Localization")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = Create();
        var section = configuration.GetSection(sectionName);

        if (section.Exists()) { section.Bind(options); }

        return options;
    }

    /// <summary>
    ///     Validates the provided localization options.
    /// </summary>
    /// <param name="options">The options to validate</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public static void Validate(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DefaultCulture))
        {
            throw new InvalidOperationException("Default culture cannot be null or empty.");
        }

        if (options.SupportedCultures == null || options.SupportedCultures.Length == 0)
        {
            throw new InvalidOperationException("At least one supported culture must be specified.");
        }

        if (!options.SupportedCultures.Contains(options.DefaultCulture))
        {
            throw new InvalidOperationException("Default culture must be included in supported cultures.");
        }
    }

    /// <summary>
    ///     Creates and validates localization options with default values.
    /// </summary>
    /// <returns>Validated localization options with default configuration</returns>
    public static LocalizationOptions Build()
    {
        var options = Create();
        Validate(options);

        return options;
    }

    /// <summary>
    ///     Creates and validates localization options from configuration.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Validated localization options</returns>
    public static LocalizationOptions Build(IConfiguration configuration, string sectionName = "Localization")
    {
        var options = Create(configuration, sectionName);
        Validate(options);

        return options;
    }
}
