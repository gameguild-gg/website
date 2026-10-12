using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;
using GameGuild.Assets;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Core.Compliance;

internal sealed class AuditScheduledExportStorageAdapter(
    IAmazonS3 s3Client,
    IOptions<AssetStorageOptions> storageOptions) : IAuditScheduledExportStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AssetStorageOptions _storageOptions = storageOptions.Value;

    public async Task<string> StoreAsync(
        Guid tenantId,
        Stream content,
        string contentHash,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        var objectKey = $"compliance/audit-exports/{tenantId:D}/{contentHash}/{fileName}";
        var request = new PutObjectRequest
        {
            BucketName = _storageOptions.BucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        request.Metadata.Add("content-hash", contentHash);
        request.Metadata.Add("tenant-id", tenantId.ToString("D"));
        await s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);

        var reference = new StoredAuditExportFileReference(_storageOptions.BucketName, objectKey, fileName, contentType);
        return JsonSerializer.Serialize(reference, JsonOptions);
    }

    public async Task<Stream> OpenReadAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken)
    {
        var reference = ReadAndValidateReference(tenantId, storageReference);
        var response = await s3Client.GetObjectAsync(reference.BucketName, reference.ObjectKey, cancellationToken).ConfigureAwait(false);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken)
    {
        var reference = ReadAndValidateReference(tenantId, storageReference);
        await s3Client.DeleteObjectAsync(reference.BucketName, reference.ObjectKey, cancellationToken).ConfigureAwait(false);
    }

    private StoredAuditExportFileReference ReadAndValidateReference(Guid tenantId, string storageReference)
    {
        var reference = JsonSerializer.Deserialize<StoredAuditExportFileReference>(storageReference, JsonOptions)
            ?? throw new InvalidDataException("The audit export storage reference is invalid.");
        var tenantPrefix = $"compliance/audit-exports/{tenantId:D}/";
        if (!string.Equals(reference.BucketName, _storageOptions.BucketName, StringComparison.Ordinal)
            || !reference.ObjectKey.StartsWith(tenantPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The audit export storage reference does not belong to this tenant.");
        }

        return reference;
    }
}
