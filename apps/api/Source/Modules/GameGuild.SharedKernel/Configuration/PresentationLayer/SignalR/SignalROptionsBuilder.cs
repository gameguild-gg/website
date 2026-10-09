using Microsoft.Extensions.Configuration;

namespace GameGuild.Configuration.PresentationLayer.SignalR;

/// <summary>
///     Builder for SignalR options.
/// </summary>
public static class SignalROptionsBuilder
{
    /// <summary>
    ///     Creates SignalR options with default values.
    /// </summary>
    /// <returns>Default SignalR options</returns>
    public static SignalROptions Create()
    {
        return new SignalROptions
        {
            EnableDetailedErrors = false, KeepAliveInterval = TimeSpan.FromSeconds(15), ClientTimeoutInterval = TimeSpan.FromSeconds(30), MaximumReceiveMessageSize = 32 * 1024 // 32KB
        };
    }

    /// <summary>
    ///     Creates SignalR options from a specific configuration section.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Configured SignalR options</returns>
    public static SignalROptions Create(IConfiguration configuration, string sectionName = "SignalR") { return OptionBuilderUtilities.CreateAndBind(configuration, sectionName, Create); }

    /// <summary>
    ///     Validates the provided SignalR options.
    /// </summary>
    /// <param name="options">The options to validate</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public static void Validate(SignalROptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.KeepAliveInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Keep alive interval must be greater than zero.");
        }

        if (options.ClientTimeoutInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Client timeout interval must be greater than zero.");
        }

        if (options.MaximumReceiveMessageSize <= 0)
        {
            throw new InvalidOperationException("Maximum receive message size must be greater than zero.");
        }
    }

    /// <summary>
    ///     Creates and validates SignalR options with default values.
    /// </summary>
    /// <returns>Validated SignalR options with default configuration</returns>
    public static SignalROptions Build()
    {
        var options = Create();
        Validate(options);

        return options;
    }

    /// <summary>
    ///     Creates and validates SignalR options from configuration.
    /// </summary>
    /// <param name="configuration">The configuration to bind from</param>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>Validated SignalR options</returns>
    public static SignalROptions Build(IConfiguration configuration, string sectionName = "SignalR")
    {
        var options = Create(configuration, sectionName);
        Validate(options);

        return options;
    }
}
