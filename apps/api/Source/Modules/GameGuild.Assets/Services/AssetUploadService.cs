using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace GameGuild.Assets;

/// <summary>
/// Configuration options for asset upload.
/// </summary>
public class AssetUploadConfiguration
{
    public const string SectionName = "Assets:Upload";

    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB
    public long ChunkedUploadThreshold { get; set; } = 5 * 1024 * 1024; // 5 MB
    public int ChunkSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB
    public string[] AllowedMimeTypes { get; set; } = Array.Empty<string>();
    public int ChunkedUploadExpiryMinutes { get; set; } = 60;
}

/// <summary>
/// Implementation of asset upload with deduplication.
/// </summary>
public class AssetUploadService : IAssetUploadService
{
    private readonly IAssetContentRepository _contentRepository;
    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly IAssetStorageService _storageService;
    private readonly Microsoft.Extensions.Options.IOptions<AssetUploadConfiguration> _options;
    private readonly ILogger<AssetUploadService> _logger;
    private readonly IUseCaseOperationContextAccessor? _operationContextAccessor;
    
    // In-memory store for chunked uploads (should be replaced with distributed cache in production)
    private static readonly Dictionary<string, ChunkedUploadSession> _chunkedSessions = new();
    private static readonly Dictionary<string, SortedDictionary<int, string>> _chunkedSessionPartETags = new();

    public AssetUploadService(
        IAssetContentRepository contentRepository,
        IAssetReferenceRepository referenceRepository,
        IAssetStorageService storageService,
        Microsoft.Extensions.Options.IOptions<AssetUploadConfiguration> options,
        ILogger<AssetUploadService> logger,
        IUseCaseOperationContextAccessor? operationContextAccessor = null)
    {
        _contentRepository = contentRepository;
        _referenceRepository = referenceRepository;
        _storageService = storageService;
        _options = options;
        _logger = logger;
        _operationContextAccessor = operationContextAccessor;
    }

    public async Task<AssetUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string mimeType,
        Guid userId,
        UploadAssetOptions options,
        CancellationToken ct = default)
    {
        var uploadOptions = _options.Value;

        // Validate file size
        if (content.Length > uploadOptions.MaxFileSizeBytes)
        {
            return new AssetUploadResult(
                false, null, null,
                $"File size exceeds maximum allowed ({uploadOptions.MaxFileSizeBytes} bytes)");
        }

        // Validate MIME type
        if (uploadOptions.AllowedMimeTypes.Length > 0 && 
            !uploadOptions.AllowedMimeTypes.Contains(mimeType))
        {
            return new AssetUploadResult(
                false, null, null,
                $"MIME type '{mimeType}' is not allowed");
        }

        // Compute content hash
        content.Position = 0;
        var contentHash = await ComputeHashAsync(content, ct).ConfigureAwait(false);
        content.Position = 0;

        // Check for existing content (deduplication)
        var existingContent = await _contentRepository.GetByContentHashAsync(contentHash, ct).ConfigureAwait(false);
        AssetContent assetContent;

        if (existingContent != null)
        {
            _logger.LogInformation("Content already exists with hash {ContentHash}, reusing", contentHash);
            assetContent = existingContent;
            await _contentRepository.IncrementReferenceCountAsync(existingContent.Id, ct).ConfigureAwait(false);
        }
        else
        {
            // Get image dimensions if applicable
            int? width = null, height = null;
            if (mimeType.StartsWith("image/"))
            {
                (width, height) = await ExtractImageDimensionsAsync(content, ct).ConfigureAwait(false);
                content.Position = 0;
            }

            // Upload to storage
            var storageResult = await _storageService.UploadAsync(
                content, contentHash, mimeType, false, ct).ConfigureAwait(false);

            // Create content record
            assetContent = new AssetContent(
                storageResult.BucketName,
                storageResult.ObjectKey,
                contentHash,
                mimeType,
                content.Length,
                width,
                height);

            assetContent.TenantId = options.TenantId;
            assetContent.AddIntegrationEvent(CreateObjectStoredEvent(assetContent, userId, options.TenantId));

            assetContent = await _contentRepository.AddAsync(assetContent, ct).ConfigureAwait(false);
        }

        // Create reference
        var reference = new AssetReference(
            assetContent.Id,
            userId,
            options.DisplayName ?? fileName,
            options.AccessPolicy,
            options.ParentResourceType,
            options.ParentResourceId);

        reference.TenantId = options.TenantId;
        reference.MoveToFolder(options.FolderId);

        reference.CreateInitialRevision(userId);

        reference.AddIntegrationEvent(CreateReferenceCreatedEvent(reference, assetContent, userId, options.TenantId));

        reference = await _referenceRepository.AddAsync(reference, ct).ConfigureAwait(false);

        return new AssetUploadResult(true, reference.Id, assetContent.Id, null);
    }

    public async Task<ChunkedUploadSession> InitiateChunkedUploadAsync(
        string fileName,
        string mimeType,
        long totalSize,
        Guid userId,
        CancellationToken ct = default)
    {
        var uploadId = await _storageService.InitiateMultipartUploadAsync(mimeType, ct).ConfigureAwait(false);
        var chunkSize = _options.Value.ChunkSizeBytes;
        var totalChunks = (int)Math.Ceiling((double)totalSize / chunkSize);

        var session = new ChunkedUploadSession(
            uploadId,
            $"multipart/{uploadId}",
            userId,
            fileName,
            mimeType,
            totalSize,
            totalChunks,
            SystemClock.UtcNow.AddMinutes(_options.Value.ChunkedUploadExpiryMinutes));

        _chunkedSessions[uploadId] = session;
        _chunkedSessionPartETags[uploadId] = new SortedDictionary<int, string>();

        return session;
    }

    public async Task<bool> UploadChunkAsync(
        string uploadId,
        int chunkIndex,
        Stream chunkContent,
        CancellationToken ct = default)
    {
        if (!_chunkedSessions.TryGetValue(uploadId, out var session))
        {
            return false;
        }

        if (session.ExpiresAt < SystemClock.UtcNow)
        {
            _chunkedSessions.Remove(uploadId);
            _chunkedSessionPartETags.Remove(uploadId);
            await _storageService.AbortMultipartUploadAsync(uploadId, session.ObjectKey, ct).ConfigureAwait(false);
            return false;
        }

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            return false;
        }

        var partNumber = chunkIndex + 1;
        var eTag = await _storageService.UploadPartAsync(
            uploadId, session.ObjectKey, partNumber, chunkContent, ct).ConfigureAwait(false);

        if (!_chunkedSessionPartETags.TryGetValue(uploadId, out var partETags))
        {
            partETags = new SortedDictionary<int, string>();
            _chunkedSessionPartETags[uploadId] = partETags;
        }

        partETags[partNumber] = eTag;
        session = session with 
        { 
            UploadedChunks = partETags.Count
        };
        _chunkedSessions[uploadId] = session;

        return true;
    }

    public async Task<AssetUploadResult> CompleteChunkedUploadAsync(
        string uploadId,
        UploadAssetOptions options,
        CancellationToken ct = default)
    {
        if (!_chunkedSessions.TryGetValue(uploadId, out var session))
        {
            return new AssetUploadResult(false, null, null, "Upload session not found");
        }

        if (session.ExpiresAt < SystemClock.UtcNow)
        {
            _chunkedSessions.Remove(uploadId);
            _chunkedSessionPartETags.Remove(uploadId);
            await _storageService.AbortMultipartUploadAsync(uploadId, session.ObjectKey, ct).ConfigureAwait(false);
            return new AssetUploadResult(false, null, null, "Upload session expired");
        }

        if (!_chunkedSessionPartETags.TryGetValue(uploadId, out var partETags) ||
            partETags.Count < session.TotalChunks)
        {
            var receivedParts = partETags?.Keys.ToHashSet() ?? [];
            var missingParts = Enumerable.Range(1, session.TotalChunks)
                .Where(partNumber => !receivedParts.Contains(partNumber))
                .ToList();

            return new AssetUploadResult(
                false,
                null,
                null,
                $"Upload is incomplete. Missing chunks: {string.Join(", ", missingParts)}");
        }

        _chunkedSessions.Remove(uploadId);
        _chunkedSessionPartETags.Remove(uploadId);

        var eTags = partETags
            .OrderBy(part => part.Key)
            .Select(part => part.Value)
            .ToList();

        var storageResult = await _storageService.CompleteMultipartUploadAsync(
            uploadId, session.ObjectKey, eTags, ct).ConfigureAwait(false);

        // Download and compute hash
        using var stream = await _storageService.DownloadAsync(
            storageResult.BucketName, storageResult.ObjectKey, ct).ConfigureAwait(false);

        var contentHash = await ComputeHashAsync(stream, ct).ConfigureAwait(false);

        // Create content record
        var assetContent = new AssetContent(
            storageResult.BucketName,
            storageResult.ObjectKey,
            contentHash,
            session.MimeType,
            session.TotalSize,
            null, null);

        assetContent.TenantId = options.TenantId;
        assetContent.AddIntegrationEvent(CreateObjectStoredEvent(assetContent, session.UserId, options.TenantId));

        assetContent = await _contentRepository.AddAsync(assetContent, ct).ConfigureAwait(false);

        // Create reference
        var reference = new AssetReference(
            assetContent.Id,
            session.UserId,
            options.DisplayName ?? session.FileName,
            options.AccessPolicy,
            options.ParentResourceType,
            options.ParentResourceId);

        reference.TenantId = options.TenantId;
        reference.MoveToFolder(options.FolderId);

        reference.CreateInitialRevision(session.UserId);

        reference.AddIntegrationEvent(CreateReferenceCreatedEvent(reference, assetContent, session.UserId, options.TenantId));

        reference = await _referenceRepository.AddAsync(reference, ct).ConfigureAwait(false);

        return new AssetUploadResult(true, reference.Id, assetContent.Id, null);
    }

    public async Task AbortChunkedUploadAsync(string uploadId, CancellationToken ct = default)
    {
        if (_chunkedSessions.TryGetValue(uploadId, out var session))
        {
            _chunkedSessions.Remove(uploadId);
            _chunkedSessionPartETags.Remove(uploadId);
            await _storageService.AbortMultipartUploadAsync(uploadId, session.ObjectKey, ct).ConfigureAwait(false);
        }
    }

    private static async Task<string> ComputeHashAsync(Stream content, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(content, ct).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private AssetObjectStoredEvent CreateObjectStoredEvent(
        AssetContent content,
        Guid actorId,
        Guid? tenantId) => new(content.Id, content.ContentHash, content.SizeBytes, "object-storage")
    {
        TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
        ActorId = actorId,
        AggregateType = nameof(AssetContent),
        AggregateId = content.Id.ToString(),
        CorrelationId = _operationContextAccessor?.Current?.CorrelationId ?? Guid.NewGuid(),
        CausationId = _operationContextAccessor?.Current?.CausationId
    };

    private AssetReferenceCreatedEvent CreateReferenceCreatedEvent(
        AssetReference reference,
        AssetContent content,
        Guid actorId,
        Guid? tenantId) => new(reference.Id, content.Id, content.ContentHash, content.SizeBytes)
    {
        TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
        ActorId = actorId,
        AggregateType = nameof(AssetReference),
        AggregateId = reference.Id.ToString(),
        CorrelationId = _operationContextAccessor?.Current?.CorrelationId ?? Guid.NewGuid(),
        CausationId = _operationContextAccessor?.Current?.CausationId
    };

    private static async Task<(int? Width, int? Height)> ExtractImageDimensionsAsync(
        Stream content, CancellationToken ct)
    {
        // Simplified - in production, use ImageSharp or similar
        // This would parse image headers to get dimensions without loading full image
        await Task.CompletedTask;
        return (null, null);
    }
}
