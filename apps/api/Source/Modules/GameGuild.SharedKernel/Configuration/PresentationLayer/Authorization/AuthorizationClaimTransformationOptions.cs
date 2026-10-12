namespace GameGuild.Configuration.PresentationLayer.Authorization;

/// <summary>
///     An allowlisted mapping from a trusted source claim value to a target claim value.
/// </summary>
public sealed class AuthorizationClaimTransformationOptions
{
    /// <summary>The claim type whose values are inspected.</summary>
    public string SourceClaimType { get; set; } = string.Empty;

    /// <summary>The claim type added to the authenticated identity.</summary>
    public string TargetClaimType { get; set; } = string.Empty;

    /// <summary>
    ///     Explicit source-to-target value mappings. Values absent from this dictionary are ignored.
    /// </summary>
    public Dictionary<string, string> ValueMappings { get; set; } = new(StringComparer.Ordinal);
}
