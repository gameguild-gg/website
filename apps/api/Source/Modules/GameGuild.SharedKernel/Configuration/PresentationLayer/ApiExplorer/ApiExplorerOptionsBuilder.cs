using Microsoft.Extensions.Configuration;

namespace GameGuild.Configuration.PresentationLayer.ApiExplorer;

/// <summary>
///     Builder for API explorer options.
/// </summary>
public static class ApiExplorerOptionsBuilder
{
    /// <summary>
    ///     Creates API explorer options with default values.
    /// </summary>
    /// <returns>Default API explorer options</returns>
    public static ApiExplorerOptions Create() { return new ApiExplorerOptions { GroupNameFormat = "v{version}", DefaultGroupName = "v1" }; }

    /// <summary>
    ///     Creates API explorer options from a specific configuration section.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Configured API explorer options</returns>
    public static ApiExplorerOptions Create(IConfiguration configuration, string sectionName = "ApiExplorer")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = Create();
        var section = configuration.GetSection(sectionName);

        if (section.Exists()) { section.Bind(options); }

        return options;
    }

    /// <summary>
    ///     Validates the provided API explorer options.
    /// </summary>
    /// <param name="options">The options to validate</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public static void Validate(ApiExplorerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.DefaultGroupName))
        {
            throw new InvalidOperationException("Default group name cannot be null or empty.");
        }
    }

    /// <summary>
    ///     Creates and validates API explorer options with default values.
    /// </summary>
    /// <returns>Validated API explorer options with default configuration</returns>
    public static ApiExplorerOptions Build()
    {
        var options = Create();
        Validate(options);

        return options;
    }

    /// <summary>
    ///     Creates and validates API explorer options from configuration.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Validated API explorer options</returns>
    public static ApiExplorerOptions Build(IConfiguration configuration, string sectionName = "ApiExplorer")
    {
        var options = Create(configuration, sectionName);
        Validate(options);

        return options;
    }
}
