namespace GameGuild.Assets.SocialMedia;

public enum SocialMediaProcessingState
{
    Processing,
    Ready,
    Rejected
}

public sealed record SocialMediaValidationResult(bool IsValid, string? Error = null);

public sealed record SocialMediaAssetDescriptor(
    Guid AssetReferenceId,
    string DeliveryUrl,
    string MimeType,
    long SizeBytes,
    SocialMediaProcessingState State);

public static class SocialMediaAssetPolicy
{
    public const long ImageLimitBytes = 10 * 1024 * 1024;
    public const long VideoLimitBytes = 100 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, long> Limits =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ImageLimitBytes,
            ["image/png"] = ImageLimitBytes,
            ["image/webp"] = ImageLimitBytes,
            ["image/gif"] = ImageLimitBytes,
            ["video/mp4"] = VideoLimitBytes
        };

    public static async Task<SocialMediaValidationResult> ValidateAsync(
        Stream content,
        string mimeType,
        long declaredSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var normalizedMimeType = NormalizeMimeType(mimeType);
        if (!Limits.TryGetValue(normalizedMimeType, out var maximumSize))
            return new SocialMediaValidationResult(false, "Only JPEG, PNG, WebP, GIF, and MP4 files are supported.");

        if (declaredSize <= 0 || declaredSize > maximumSize)
            return new SocialMediaValidationResult(false, $"The file exceeds the {maximumSize} byte limit for {normalizedMimeType}.");

        if (!content.CanRead || !content.CanSeek)
            return new SocialMediaValidationResult(false, "The uploaded file cannot be inspected safely.");

        var originalPosition = content.Position;
        content.Position = 0;
        var header = new byte[12];
        var bytesRead = await content.ReadAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
        content.Position = originalPosition;

        return SignatureMatches(normalizedMimeType, header.AsSpan(0, bytesRead))
            ? new SocialMediaValidationResult(true)
            : new SocialMediaValidationResult(false, "The file content does not match its declared media type.");
    }

    public static string NormalizeMimeType(string? mimeType)
        => mimeType?.Split(';', 2)[0].Trim().ToLowerInvariant() ?? string.Empty;

    public static bool IsSupported(string mimeType, long sizeBytes)
    {
        var normalized = NormalizeMimeType(mimeType);
        return Limits.TryGetValue(normalized, out var maximumSize) && sizeBytes > 0 && sizeBytes <= maximumSize;
    }

    public static SocialMediaProcessingState GetProcessingState(AssetContent content)
    {
        if (content.VirusScanStatus is VirusScanStatus.Infected or VirusScanStatus.ScanFailed ||
            content.ModerationStatus is ModerationStatus.Blocked or ModerationStatus.Rejected)
            return SocialMediaProcessingState.Rejected;

        return content.IsSafeToServe
            ? SocialMediaProcessingState.Ready
            : SocialMediaProcessingState.Processing;
    }

    private static bool SignatureMatches(string mimeType, ReadOnlySpan<byte> header) => mimeType switch
    {
        "image/jpeg" => header.Length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
        "image/png" => header.Length >= 8 &&
                       header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4e && header[3] == 0x47 &&
                       header[4] == 0x0d && header[5] == 0x0a && header[6] == 0x1a && header[7] == 0x0a,
        "image/gif" => header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8),
        "image/webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        "video/mp4" => header.Length >= 8 && header[4..8].SequenceEqual("ftyp"u8),
        _ => false
    };
}

public interface ISocialMediaAssetService
{
    Task<SocialMediaAssetDescriptor?> InspectOwnedAsync(
        Guid assetReferenceId,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task<SocialMediaAssetDescriptor?> ResolveReadyOwnedAsync(
        Guid assetReferenceId,
        Guid ownerId,
        CancellationToken cancellationToken = default);
}

public sealed class SocialMediaAssetService(IAssetReferenceRepository references) : ISocialMediaAssetService
{
    public async Task<SocialMediaAssetDescriptor?> InspectOwnedAsync(
        Guid assetReferenceId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (assetReferenceId == Guid.Empty || ownerId == Guid.Empty)
            return null;

        var reference = await references.GetByIdWithContentAsync(assetReferenceId, cancellationToken).ConfigureAwait(false);
        if (reference?.Content is null || reference.DeletedAt.HasValue || reference.CreatedByUserId != ownerId ||
            !SocialMediaAssetPolicy.IsSupported(reference.Content.MimeType, reference.Content.SizeBytes))
            return null;

        return new SocialMediaAssetDescriptor(
            reference.Id,
            $"/api/assets/{reference.Id}/content",
            SocialMediaAssetPolicy.NormalizeMimeType(reference.Content.MimeType),
            reference.Content.SizeBytes,
            SocialMediaAssetPolicy.GetProcessingState(reference.Content));
    }

    public async Task<SocialMediaAssetDescriptor?> ResolveReadyOwnedAsync(
        Guid assetReferenceId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var descriptor = await InspectOwnedAsync(assetReferenceId, ownerId, cancellationToken).ConfigureAwait(false);
        return descriptor?.State == SocialMediaProcessingState.Ready ? descriptor : null;
    }
}
