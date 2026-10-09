namespace GameGuild.Assets.Storage;

/// <summary>
/// Tenant-specific storage configuration.
/// Allows tenants to use their own cloud storage instead of the platform default.
/// </summary>
public class TenantStorageConfiguration : EntityBase
{
    /// <summary>
    /// User who created this configuration.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>
    /// Storage provider type (S3, GCS, Azure, etc.)
    /// </summary>
    public StorageProviderType ProviderType { get; private set; }

    /// <summary>
    /// Whether this tenant storage is enabled.
    /// If disabled, falls back to platform default.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Display name for this storage configuration.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// JSON-serialized provider-specific configuration.
    /// Encrypted at rest.
    /// </summary>
    public string EncryptedConfiguration { get; private set; } = string.Empty;

    /// <summary>
    /// Bucket/container name for assets.
    /// </summary>
    public string BucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Bucket/container name for transformed assets (thumbnails, resized).
    /// </summary>
    public string TransformedBucketName { get; private set; } = string.Empty;

    /// <summary>
    /// Region where storage is located (for latency optimization).
    /// </summary>
    public string? Region { get; private set; }

    /// <summary>
    /// CDN URL prefix for serving assets (optional).
    /// If set, presigned URLs use this as base.
    /// </summary>
    public string? CdnUrlPrefix { get; private set; }

    /// <summary>
    /// Last time credentials were validated.
    /// </summary>
    public DateTime? LastValidated { get; private set; }

    /// <summary>
    /// Whether last validation was successful.
    /// </summary>
    public bool? LastValidationSuccess { get; private set; }

    /// <summary>
    /// Error message from last failed validation.
    /// </summary>
    public string? LastValidationError { get; private set; }

    /// <summary>
    /// User who last updated this configuration.
    /// </summary>
    public Guid? UpdatedBy { get; private set; }

    private TenantStorageConfiguration() { } // EF Core

    public static TenantStorageConfiguration Create(
        Guid tenantId,
        StorageProviderType providerType,
        string name,
        string encryptedConfiguration,
        string bucketName,
        string transformedBucketName,
        string? region,
        string? cdnUrlPrefix,
        Guid createdBy)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(bucketName))
        {
            throw new ArgumentException("BucketName is required", nameof(bucketName));
        }

        return new TenantStorageConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderType = providerType,
            Name = name,
            EncryptedConfiguration = encryptedConfiguration,
            BucketName = bucketName,
            TransformedBucketName = transformedBucketName,
            Region = region,
            CdnUrlPrefix = cdnUrlPrefix,
            IsEnabled = false, // Must be validated before enabling
            CreatedBy = createdBy
        };
    }

    public void Enable()
    {
        if (LastValidationSuccess != true)
        {
            throw new InvalidOperationException("Cannot enable storage configuration that hasn't been validated");
        }

        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    public void UpdateConfiguration(
        string encryptedConfiguration,
        string bucketName,
        string transformedBucketName,
        string? region,
        string? cdnUrlPrefix,
        Guid updatedBy)
    {
        EncryptedConfiguration = encryptedConfiguration;
        BucketName = bucketName;
        TransformedBucketName = transformedBucketName;
        Region = region;
        CdnUrlPrefix = cdnUrlPrefix;
        UpdatedBy = updatedBy;

        // Require re-validation after configuration change
        IsEnabled = false;
        LastValidated = null;
        LastValidationSuccess = null;
        LastValidationError = null;
    }

    public void RecordValidation(bool success, string? error = null)
    {
        LastValidated = SystemClock.UtcNow;
        LastValidationSuccess = success;
        LastValidationError = error;
    }
}
