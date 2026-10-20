using Microsoft.Extensions.Configuration;

namespace GameGuild.Configuration.PresentationLayer.ResponseCompression;

/// <summary>
///     Builder for response compression options.
/// </summary>
public static class ResponseCompressionOptionsBuilder
{
    /// <summary>
    ///     Creates response compression options with default values.
    /// </summary>
    /// <returns>Default response compression options</returns>
    public static ResponseCompressionOptions Create()
    {
        return new ResponseCompressionOptions
            { MimeTypes = ["application/json", "text/plain", "application/xml"], EnableCompression = true };
    }

    /// <summary>
    ///     Creates response compression options from a specific configuration section.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Configured response compression options</returns>
    public static ResponseCompressionOptions Create(IConfiguration configuration,
        string sectionName = "ResponseCompression")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = Create();
        var section = configuration.GetSection(sectionName);

        if (section.Exists())
        {
            section.Bind(options);
        }

        return options;
    }

    /// <summary>
    ///     Builds and validates response compression options.
    /// </summary>
    /// <param name="options">The options to validate and return</param>
    /// <returns>Validated response compression options</returns>
    public static ResponseCompressionOptions Build(this ResponseCompressionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        return options;
    }
}
