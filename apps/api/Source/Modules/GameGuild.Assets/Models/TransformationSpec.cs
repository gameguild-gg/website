namespace GameGuild.Assets;

/// <summary>
/// Strongly-typed transformation specification for asset transformations.
/// Immutable record for cache keying and URL generation.
/// </summary>
public sealed record TransformationSpec
{
    /// <summary>Target width in pixels</summary>
    public int? Width { get; init; }
    
    /// <summary>Target height in pixels</summary>
    public int? Height { get; init; }
    
    /// <summary>How to fit the image within target dimensions</summary>
    public ImageFit? Fit { get; init; }
    
    /// <summary>Quality level (1-100, for lossy formats)</summary>
    public int? Quality { get; init; }
    
    /// <summary>Output format</summary>
    public ImageFormat? Format { get; init; }
    
    /// <summary>Whether to apply blur effect</summary>
    public bool? Blur { get; init; }
    
    /// <summary>Blur radius (if blur is enabled)</summary>
    public int? BlurRadius { get; init; }
    
    /// <summary>Whether to convert to grayscale</summary>
    public bool? Grayscale { get; init; }
    
    /// <summary>
    /// Returns a canonical, sorted string representation for cache keying.
    /// This ensures the same transformation always produces the same key.
    /// </summary>
    public string ToCanonicalString()
    {
        var parts = new List<string>();
        
        if (Width.HasValue)
        {
            parts.Add($"w={Width}");
        }

        if (Height.HasValue)
        {
            parts.Add($"h={Height}");
        }

        if (Fit.HasValue)
        {
            parts.Add($"fit={Fit.ToString()!.ToLowerInvariant()}");
        }

        if (Quality.HasValue)
        {
            parts.Add($"q={Quality}");
        }

        if (Format.HasValue)
        {
            parts.Add($"f={Format.ToString()!.ToLowerInvariant()}");
        }

        if (Blur == true)
        {
            parts.Add($"blur={BlurRadius ?? 10}");
        }

        if (Grayscale == true)
        {
            parts.Add("gray=1");
        }

        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", parts);
    }

    /// <summary>
    /// Parses a canonical string into a TransformationSpec.
    /// </summary>
    /// <param name="spec">String in format "w=100,h=200,fit=cover,q=80"</param>
    /// <returns>Parsed TransformationSpec</returns>
    public static TransformationSpec Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return new TransformationSpec();
        }

        int? width = null, height = null, quality = null, blurRadius = null;
        ImageFit? fit = null;
        ImageFormat? format = null;
        bool? blur = null, grayscale = null;

        var parts = spec.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length != 2)
            {
                continue;
            }

            var key = keyValue[0].Trim().ToLowerInvariant();
            var value = keyValue[1].Trim();

            switch (key)
            {
                case "w":
                    if (int.TryParse(value, out var w))
                    {
                        width = w;
                    }

                    break;
                case "h":
                    if (int.TryParse(value, out var h))
                    {
                        height = h;
                    }

                    break;
                case "fit":
                    if (Enum.TryParse<ImageFit>(value, ignoreCase: true, out var f))
                    {
                        fit = f;
                    }

                    break;
                case "q":
                    if (int.TryParse(value, out var q))
                    {
                        quality = Math.Clamp(q, 1, 100);
                    }

                    break;
                case "f":
                    if (Enum.TryParse<ImageFormat>(value, ignoreCase: true, out var fmt))
                    {
                        format = fmt;
                    }

                    break;
                case "blur":
                    blur = true;
                    if (int.TryParse(value, out var br))
                    {
                        blurRadius = br;
                    }

                    break;
                case "gray":
                    grayscale = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        return new TransformationSpec
        {
            Width = width,
            Height = height,
            Fit = fit,
            Quality = quality,
            Format = format,
            Blur = blur,
            BlurRadius = blurRadius,
            Grayscale = grayscale
        };
    }

    /// <summary>
    /// Returns true if this spec represents no transformation (identity).
    /// </summary>
    public bool IsIdentity =>
        !Width.HasValue &&
        !Height.HasValue &&
        !Fit.HasValue &&
        !Quality.HasValue &&
        !Format.HasValue &&
        Blur != true &&
        Grayscale != true;

    /// <summary>
    /// Validates the transformation spec against maximum allowed dimensions.
    /// </summary>
    public bool IsWithinLimits(int maxDimension)
    {
        if (Width.HasValue && Width.Value > maxDimension)
        {
            return false;
        }

        if (Height.HasValue && Height.Value > maxDimension)
        {
            return false;
        }

        return true;
    }
}
