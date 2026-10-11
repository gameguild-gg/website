using ImageMagick;

namespace GameGuild.Assets.Deduplication;

/// <summary>Bounded raster decoding for the existing 64-bit average image hash.</summary>
internal static class RasterPerceptualImageReader
{
    private const int MaximumEncodedBytes = 64 * 1024 * 1024;

    static RasterPerceptualImageReader()
    {
        // This module is the only ImageMagick consumer. Bound native allocations
        // and forbid the disk fallback for data supplied by an upload stream.
        ResourceLimits.Memory = 256UL * 1024 * 1024;
        ResourceLimits.MaxMemoryRequest = 256UL * 1024 * 1024;
        ResourceLimits.Disk = 0;
        ResourceLimits.Width = 32768;
        ResourceLimits.Height = 32768;
        ResourceLimits.MaxProfileSize = 2UL * 1024 * 1024;
        ResourceLimits.Thread = 2;
        ResourceLimits.Time = 120;
    }

    internal static async Task<byte[]> ReadAsync(Stream content, CancellationToken ct)
    {
        using var encoded = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(buffer, ct).ConfigureAwait(false)) != 0)
        {
            if (encoded.Length + read > MaximumEncodedBytes)
            {
                throw new InvalidDataException("The image exceeds the perceptual decoder input limit.");
            }
            encoded.Write(buffer, 0, read);
        }

        var format = DetectRasterFormat(encoded.GetBuffer().AsSpan(0, (int)Math.Min(encoded.Length, 18)));
        encoded.Position = 0;
        // Explicitly select only raster coders. SVG/PDF/MVG, URL readers and
        // external delegates never receive untrusted input through auto-detection.
        var settings = new MagickReadSettings { Format = format, FrameIndex = 0, FrameCount = 1 };
        using var image = new MagickImage();
        await image.ReadAsync(encoded, settings, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        // PixelMapping.RGB exports channels without converting CMYK data.
        // Normalize decoded colors before applying the existing RGB luminance.
        image.ColorSpace = ColorSpace.sRGB;
        image.Resize(new MagickGeometry(8, 8) { IgnoreAspectRatio = true }, FilterType.Catrom);
        using var pixels = image.GetPixels();
        var rgb = pixels.ToByteArray(PixelMapping.RGB) ?? throw new InvalidDataException("The raster has no pixels.");
        if (rgb.Length != 8 * 8 * 3)
        {
            throw new InvalidDataException("The raster decoder returned unexpected dimensions.");
        }

        var grayscale = new byte[64];
        for (var index = 0; index < grayscale.Length; index++)
        {
            var offset = index * 3;
            var luminance = (0.2126f * rgb[offset]) + (0.7152f * rgb[offset + 1]) + (0.0722f * rgb[offset + 2]);
            grayscale[index] = (byte)Math.Clamp((int)(luminance + 0.5f), 0, 255);
        }
        return grayscale;
    }

    private static MagickFormat DetectRasterFormat(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return MagickFormat.Png;
        }
        if (header.StartsWith(new byte[] { 255, 216, 255 }))
        {
            return MagickFormat.Jpeg;
        }
        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return MagickFormat.Gif;
        }
        if (header.StartsWith("BM"u8))
        {
            return MagickFormat.Bmp;
        }
        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return MagickFormat.WebP;
        }
        if (header.StartsWith(new byte[] { 73, 73, 42, 0 }) || header.StartsWith(new byte[] { 77, 77, 0, 42 }) ||
            header.StartsWith(new byte[] { 73, 73, 43, 0 }) || header.StartsWith(new byte[] { 77, 77, 0, 43 }))
        {
            return MagickFormat.Tiff;
        }
        if (header.StartsWith(new byte[] { 0, 0, 1, 0 }))
        {
            return MagickFormat.Ico;
        }
        if (header.StartsWith("qoif"u8))
        {
            return MagickFormat.Qoi;
        }
        if (header.Length >= 3 && header[0] == (byte)'P' && header[1] is >= (byte)'1' and <= (byte)'6' &&
            header[2] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            return MagickFormat.Pbm;
        }
        if (header.Length >= 18 && header[1] <= 1 && header[2] is 1 or 2 or 3 or 9 or 10 or 11 &&
            header[16] is 8 or 16 or 24 or 32)
        {
            return MagickFormat.Tga;
        }
        throw new InvalidDataException("The perceptual decoder accepts recognized raster images only.");
    }
}
