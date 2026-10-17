namespace GameGuild.Identity.Authentication;

/// <summary>
///     Parses a raw user-agent string into structured device metadata. The default
///     implementation is server-side and dependency-free (no external API calls); unknown
///     agents must degrade to an "Unknown" device rather than throwing.
/// </summary>
public interface IUserAgentParser
{
    /// <summary>
    ///     Parses the user-agent string into structured device information. Never throws.
    /// </summary>
    /// <param name="userAgent">Raw User-Agent header value (may be null or empty).</param>
    /// <returns>Structured device metadata for the parsed agent.</returns>
    DeviceInfo Parse(string? userAgent);
}
