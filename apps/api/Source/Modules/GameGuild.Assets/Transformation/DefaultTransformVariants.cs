namespace GameGuild.Assets.Transformation;

/// <summary>
/// Default transformation variants prewarmed for newly approved image assets.
/// Stored as Openinary segments; the cache row's TransformationSpec is the
/// corresponding canonical spec string.
/// </summary>
public static class DefaultTransformVariants
{
    /// <summary>Square thumbnail variant.</summary>
    public const string Thumbnail = "w_400,h_400,c_fill,f_webp";

    /// <summary>Full-width delivery variant with automatic format negotiation.</summary>
    public const string FullWidth = "w_1200,f_auto";

    /// <summary>Openinary segments of all default variants, in prewarm order.</summary>
    public static readonly IReadOnlyList<string> Segments = [Thumbnail, FullWidth];

    /// <summary>
    /// Canonical spec strings matching <see cref="Segments"/> — used as the
    /// TransformedAsset cache key for each prewarmed variant.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalSpecs =
    [
        "f=webp,fit=fill,h=400,w=400",
        "w=1200"
    ];
}
