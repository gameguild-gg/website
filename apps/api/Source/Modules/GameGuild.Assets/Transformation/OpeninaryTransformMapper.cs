namespace GameGuild.Assets.Transformation;

/// <summary>
/// Maps a <see cref="TransformationSpec"/> to an Openinary transformation URL segment
/// (e.g. <c>w_100,h_100,c_fill,q_80,f_webp</c>).
/// Parameter order is deterministic: w, h, c (fit), q, f.
/// </summary>
public static class OpeninaryTransformMapper
{
    /// <summary>
    /// Builds the comma-separated Openinary transformation segment for the spec.
    /// Throws <see cref="NotSupportedException"/> for fit modes or formats
    /// Openinary does not cover.
    /// </summary>
    public static string ToSegment(TransformationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var parts = new List<string>(5);

        if (spec.Width.HasValue)
        {
            parts.Add($"w_{spec.Width}");
        }

        if (spec.Height.HasValue)
        {
            parts.Add($"h_{spec.Height}");
        }

        if (spec.Fit.HasValue)
        {
            parts.Add(spec.Fit.Value switch
            {
                ImageFit.Contain => "c_fit",
                ImageFit.Cover => "c_fill",
                ImageFit.Fill => "c_fill",
                ImageFit.Outside => "c_fit",
                _ => throw new NotSupportedException(
                    $"ImageFit '{spec.Fit}' has no Openinary mapping.")
            });
        }

        if (spec.Quality.HasValue)
        {
            parts.Add($"q_{spec.Quality}");
        }

        if (spec.Format.HasValue)
        {
            parts.Add(spec.Format.Value switch
            {
                ImageFormat.Jpeg => "f_jpeg",
                ImageFormat.Png => "f_png",
                ImageFormat.Webp => "f_webp",
                ImageFormat.Avif => "f_avif",
                ImageFormat.Original => string.Empty, // keep original format
                _ => throw new NotSupportedException(
                    $"ImageFormat '{spec.Format}' has no Openinary mapping.")
            });
        }

        parts.RemoveAll(string.IsNullOrEmpty);
        return string.Join(",", parts);
    }
}
