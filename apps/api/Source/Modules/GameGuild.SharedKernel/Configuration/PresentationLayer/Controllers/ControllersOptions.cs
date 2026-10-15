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
    ///     Whether the endpoint DAC permission filter also enforces the legacy
    ///     resource/content-type marker attributes
    ///     (<c>RequireResourcePermissionAttribute&lt;TPermission,TResource&gt;</c>,
    ///     <c>RequireContentTypePermissionAttribute&lt;TResource&gt;</c>). Default:
    ///     <b>disabled</b> — those markers predate the filter and were inert while the
    ///     filter was disabled, so their surfaces are governed by their own capability
    ///     checks; enabling this switch enforces the markers through the scoped grant
    ///     stores (content-type layer / resource context with owner override).
    /// </summary>
    public bool EnforceLegacyPermissionMarkers { get; set; }

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
