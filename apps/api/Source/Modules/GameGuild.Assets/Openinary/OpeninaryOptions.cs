namespace GameGuild.Assets.Openinary;

/// <summary>
/// Options for the Openinary asset CDN integration.
/// An empty <see cref="ApiKey"/> disables the integration.
/// </summary>
public class OpeninaryOptions
{
    public const string SectionName = "Assets:Openinary";

    public const string DefaultBaseUrl = "https://openinary.gameguild.gg";

    /// <summary>
    /// Base URL of the Openinary service.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// Bearer API key; empty means the integration is off.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Folder prefix applied by callers when composing upload folders
    /// (configuration value — never hardcoded in client code).
    /// </summary>
    public string FolderPrefix { get; set; } = "gg/";

    /// <summary>
    /// Maximum accepted file size in megabytes.
    /// </summary>
    public int MaxFileMb { get; set; } = 50;

    /// <summary>
    /// HTTP timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}
