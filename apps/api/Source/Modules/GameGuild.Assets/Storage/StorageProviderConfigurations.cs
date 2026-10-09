namespace GameGuild.Assets.Storage;

/// <summary>
/// Base configuration for all storage providers.
/// </summary>
public abstract class StorageProviderConfiguration
{
    /// <summary>
    /// Provider type identifier.
    /// </summary>
    public abstract StorageProviderType ProviderType { get; }

    /// <summary>
    /// Validates the configuration has all required fields.
    /// </summary>
    public abstract ValidationResult Validate();
}

/// <summary>
/// Configuration for S3-compatible storage providers.
/// Supports: AWS S3, MinIO, DigitalOcean Spaces, Wasabi, Linode Object Storage, etc.
/// </summary>
public class S3CompatibleConfiguration : StorageProviderConfiguration
{
    public override StorageProviderType ProviderType => StorageProviderType.S3Compatible;

    /// <summary>
    /// S3 service endpoint URL.
    /// Examples:
    /// - AWS S3: https://s3.us-east-1.amazonaws.com (or use empty for SDK default)
    /// - MinIO: http://localhost:9000
    /// - DigitalOcean: https://nyc3.digitaloceanspaces.com
    /// - Wasabi: https://s3.wasabisys.com
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Access key ID.
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Secret access key.
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// AWS region (e.g., us-east-1, eu-west-1).
    /// Required for AWS S3, optional for other providers.
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Use path-style addressing (bucket.endpoint vs endpoint/bucket).
    /// Required for MinIO and some S3-compatible services.
    /// </summary>
    public bool ForcePathStyle { get; set; } = false;

    /// <summary>
    /// Use HTTP instead of HTTPS (development only).
    /// </summary>
    public bool UseHttp { get; set; } = false;

    /// <summary>
    /// Session token for temporary credentials (optional).
    /// </summary>
    public string? SessionToken { get; set; }

    public override ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(AccessKeyId))
        {
            errors.Add("AccessKeyId is required");
        }

        if (string.IsNullOrWhiteSpace(SecretAccessKey))
        {
            errors.Add("SecretAccessKey is required");
        }

        if (string.IsNullOrWhiteSpace(Region))
        {
            errors.Add("Region is required");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Configuration for Google Cloud Storage.
/// </summary>
public class GoogleCloudStorageConfiguration : StorageProviderConfiguration
{
    public override StorageProviderType ProviderType => StorageProviderType.GoogleCloudStorage;

    /// <summary>
    /// GCP Project ID.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Service account credentials JSON.
    /// Can be the full JSON or a path to the credentials file.
    /// </summary>
    public string CredentialsJson { get; set; } = string.Empty;

    /// <summary>
    /// Use Application Default Credentials instead of explicit credentials.
    /// Useful in GCP-hosted environments (Cloud Run, GKE, etc.)
    /// </summary>
    public bool UseApplicationDefaultCredentials { get; set; } = false;

    /// <summary>
    /// Location for the bucket (e.g., US, EU, ASIA).
    /// </summary>
    public string? Location { get; set; }

    public override ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            errors.Add("ProjectId is required");
        }

        if (!UseApplicationDefaultCredentials && string.IsNullOrWhiteSpace(CredentialsJson))
        {
            errors.Add("CredentialsJson is required when not using Application Default Credentials");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Configuration for Azure Blob Storage.
/// </summary>
public class AzureBlobStorageConfiguration : StorageProviderConfiguration
{
    public override StorageProviderType ProviderType => StorageProviderType.AzureBlobStorage;

    /// <summary>
    /// Full connection string for Azure Blob Storage.
    /// Can be used instead of individual properties.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Storage account name.
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Storage account access key.
    /// </summary>
    public string? AccountKey { get; set; }

    /// <summary>
    /// Blob service endpoint URL.
    /// Default: https://{accountName}.blob.core.windows.net
    /// </summary>
    public string? BlobServiceUri { get; set; }

    /// <summary>
    /// Use Azure Managed Identity instead of keys.
    /// Useful in Azure-hosted environments.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = false;

    /// <summary>
    /// Client ID for user-assigned managed identity (optional).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }

    public override ValidationResult Validate()
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            // Connection string mode - no other validation needed
            return new ValidationResult(true, errors);
        }

        if (UseManagedIdentity)
        {
            if (string.IsNullOrWhiteSpace(AccountName) && string.IsNullOrWhiteSpace(BlobServiceUri))
            {
                errors.Add("AccountName or BlobServiceUri is required when using Managed Identity");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(AccountName))
            {
                errors.Add("AccountName is required");
            }

            if (string.IsNullOrWhiteSpace(AccountKey))
            {
                errors.Add("AccountKey is required");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Configuration for Cloudflare R2.
/// </summary>
public class CloudflareR2Configuration : StorageProviderConfiguration
{
    public override StorageProviderType ProviderType => StorageProviderType.CloudflareR2;

    /// <summary>
    /// Cloudflare Account ID.
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// R2 Access Key ID.
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// R2 Secret Access Key.
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Public bucket URL (if bucket is public).
    /// </summary>
    public string? PublicBucketUrl { get; set; }

    /// <summary>
    /// Use jurisdiction-specific endpoint (EU data residency).
    /// </summary>
    public string? Jurisdiction { get; set; }

    /// <summary>
    /// Computed S3-compatible endpoint URL.
    /// </summary>
    public string GetEndpointUrl()
    {
        var jurisdiction = string.IsNullOrEmpty(Jurisdiction) ? "" : $"{Jurisdiction}.";
        return $"https://{AccountId}.{jurisdiction}r2.cloudflarestorage.com";
    }

    public override ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(AccountId))
        {
            errors.Add("AccountId is required");
        }

        if (string.IsNullOrWhiteSpace(AccessKeyId))
        {
            errors.Add("AccessKeyId is required");
        }

        if (string.IsNullOrWhiteSpace(SecretAccessKey))
        {
            errors.Add("SecretAccessKey is required");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Configuration for Backblaze B2.
/// </summary>
public class BackblazeB2Configuration : StorageProviderConfiguration
{
    public override StorageProviderType ProviderType => StorageProviderType.BackblazeB2;

    /// <summary>
    /// Application Key ID.
    /// </summary>
    public string ApplicationKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Application Key.
    /// </summary>
    public string ApplicationKey { get; set; } = string.Empty;

    /// <summary>
    /// B2 endpoint (e.g., s3.us-west-004.backblazeb2.com).
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Region derived from endpoint (e.g., us-west-004).
    /// </summary>
    public string Region { get; set; } = string.Empty;

    public override ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ApplicationKeyId))
        {
            errors.Add("ApplicationKeyId is required");
        }

        if (string.IsNullOrWhiteSpace(ApplicationKey))
        {
            errors.Add("ApplicationKey is required");
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            errors.Add("Endpoint is required");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Configuration for local filesystem storage (development only).
/// </summary>
public class LocalFileSystemConfiguration : StorageProviderConfiguration
{
    public override StorageProviderType ProviderType => StorageProviderType.LocalFileSystem;

    /// <summary>
    /// Base path for storing files.
    /// </summary>
    public string BasePath { get; set; } = "./storage";

    /// <summary>
    /// URL prefix for serving files (if using static file server).
    /// </summary>
    public string? ServeUrlPrefix { get; set; }

    public override ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(BasePath))
        {
            errors.Add("BasePath is required");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Validation result for provider configuration.
/// </summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success() => new(true, Array.Empty<string>());
    public static ValidationResult Failure(params string[] errors) => new(false, errors);
}
