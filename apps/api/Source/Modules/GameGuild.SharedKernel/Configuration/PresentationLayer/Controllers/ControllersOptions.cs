namespace GameGuild.Configuration.PresentationLayer.Controllers;

/// <summary>
///     Configuration options for MVC Controllers
/// </summary>
public sealed class ControllersOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name for this options type.
    /// </summary>
    public const string SectionName = "Controllers";

    /// <summary>
    ///     Whether to use kebab-case for route parameters
    /// </summary>
    public bool UseKebabCaseRoutes { get; set; } = true;

    /// <summary>
    ///     Whether to enable the global DAC permission authorization filter
    ///     (<c>ResourcePermissionAuthorizationFilter</c>). Default: <b>enabled</b>
    ///     (issues #300/#320) — endpoint permission attributes are enforced at the filter
    ///     level through the canonical effective-permission engine (fail-closed); the
    ///     CQRS <c>[AuthorizeRequest]</c> behavior remains the second, independent gate.
    /// </summary>
    public bool EnablePermissionAuthorizationFilter { get; set; } = true;

    /// <summary>
    ///     The property naming policy for JSON serialization (e.g., "CamelCase")
    /// </summary>
    public string JsonPropertyNamingPolicy { get; set; } = "CamelCase";

    /// <summary>
    ///     Whether to write indented JSON output
    /// </summary>
    public bool WriteIndentedJson { get; set; } = true;

    /// <summary>
    ///     List of module controller assembly names to include
    /// </summary>
    public string[] EnabledModuleAssemblies { get; set; } = [];

    public static ControllersOptions CreateDefault()
    {
        return new ControllersOptions();
    }
}
