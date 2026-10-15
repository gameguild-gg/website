namespace GameGuild.Assets.Openinary;

/// <summary>
/// Successful single-file upload result.
/// </summary>
/// <param name="Path">Storage path of the uploaded file.</param>
/// <param name="Url">Public CDN URL of the uploaded file.</param>
/// <param name="SizeBytes">Stored size in bytes.</param>
/// <param name="PrewarmedUrls">URLs of prewarmed derived variants, when requested.</param>
public sealed record OpeninaryUploadResult(
    string Path,
    string Url,
    long SizeBytes,
    IReadOnlyList<string> PrewarmedUrls);

/// <summary>
/// Storage metadata for a stored file.
/// </summary>
/// <param name="Path">Storage path.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="UpdatedAt">Last-update timestamp.</param>
public sealed record OpeninaryFileMetadata(
    string Path,
    long Size,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
