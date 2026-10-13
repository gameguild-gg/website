namespace GameGuild.Assets.Openinary;

/// <summary>
/// Typed HTTP client for the Openinary asset CDN service.
/// </summary>
public interface IOpeninaryClient
{
    /// <summary>
    /// Uploads a single file, optionally prewarming derived variants.
    /// </summary>
    Task<OpeninaryUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string folder,
        IReadOnlyCollection<string> prewarmTransforms,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the stored file and its cached variants.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates cached variants for the stored file.
    /// </summary>
    Task InvalidateAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Gets storage metadata for the stored file.
    /// </summary>
    Task<OpeninaryFileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken);
}
