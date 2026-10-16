namespace GameGuild.Assets;

/// <summary>
/// Dual-storage backend discrimination for asset content rows.
/// </summary>
public static class AssetStorageBackend
{
    /// <summary>
    /// Returns true when the content row's binary lives in Openinary.
    /// </summary>
    public static bool IsOpeninary(AssetContent content) =>
        content.BucketName == Openinary.OpeninaryOptions.StorageBackendName;
}
