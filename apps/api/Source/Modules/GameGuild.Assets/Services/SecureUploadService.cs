using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets;

/// <summary>
/// Secure upload service with virus scanning and moderation.
/// Mitigates: Malware Upload (#7), Moderation Bypass (#8), Storage Quota Exhaustion (#9)
/// </summary>
public interface ISecureUploadService
{
    /// <summary>
    /// Uploads an asset with full security checks.
    /// </summary>
    Task<SecureUploadResult> UploadWithSecurityChecksAsync(
        Stream content,
        string fileName,
        string mimeType,
        Guid userId,
        Guid tenantId,
        UploadAssetOptions options,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a secure upload operation.
/// </summary>
public sealed record SecureUploadResult(
    bool Success,
    Guid? AssetReferenceId,
    Guid? AssetContentId,
    string? Error = null,
    SecureUploadStatus Status = SecureUploadStatus.Completed,
    bool RequiresModerationReview = false);

/// <summary>
/// Status of a secure upload operation.
/// </summary>
public enum SecureUploadStatus
{
    Completed,
    PendingVirusScan,
    PendingModeration,
    Quarantined,
    Rejected,
    QuotaExceeded
}

/// <summary>
/// Implementation of secure upload with threat mitigations.
/// </summary>
public class SecureUploadService : ISecureUploadService
{
    private readonly IAssetUploadService _uploadService;
    private readonly IVirusScanService _virusScanService;
    private readonly IAssetModerationService _moderationService;
    private readonly IAssetContentRepository _contentRepository;
    private readonly IAssetStorageService _storageService;
    private readonly VirusScanOptions _virusScanOptions;
    private readonly ILogger<SecureUploadService> _logger;

    public SecureUploadService(
        IAssetUploadService uploadService,
        IVirusScanService virusScanService,
        IAssetModerationService moderationService,
        IAssetContentRepository contentRepository,
        IAssetStorageService storageService,
        IOptions<VirusScanOptions> virusScanOptions,
        ILogger<SecureUploadService> logger)
    {
        _uploadService = uploadService;
        _virusScanService = virusScanService;
        _moderationService = moderationService;
        _contentRepository = contentRepository;
        _storageService = storageService;
        _virusScanOptions = virusScanOptions.Value;
        _logger = logger;
    }

    public async Task<SecureUploadResult> UploadWithSecurityChecksAsync(
        Stream content,
        string fileName,
        string mimeType,
        Guid userId,
        Guid tenantId,
        UploadAssetOptions options,
        CancellationToken ct = default)
    {
        // Threat #7 & #8: Determine scan mode based on MIME type risk
        var requiresSyncScan = RequiresSyncVirusScan(mimeType);
        var requiresSyncModeration = RequiresSyncModeration(mimeType);

        _logger.LogInformation(
            "Starting secure upload: {FileName}, MIME: {MimeType}, SyncScan: {SyncScan}, SyncMod: {SyncMod}",
            fileName, mimeType, requiresSyncScan, requiresSyncModeration);

        // Threat #7: Synchronous virus scan for high-risk types
        if (_virusScanOptions.Enabled && requiresSyncScan)
        {
            var scanResult = await _virusScanService.ScanAsync(content, fileName, ct).ConfigureAwait(false);
            content.Position = 0;

            if (!scanResult.IsClean)
            {
                _logger.LogWarning(
                    "Virus detected in upload {FileName}: {Threat}",
                    fileName, scanResult.ThreatName);

                if (_virusScanOptions.QuarantineInfected)
                {
                    // Move to quarantine bucket
                    await QuarantineContentAsync(content, fileName, mimeType, userId, scanResult, ct).ConfigureAwait(false);
                }

                return new SecureUploadResult(
                    false, null, null,
                    $"Malware detected: {scanResult.ThreatName}",
                    SecureUploadStatus.Quarantined);
            }
        }

        // Perform the actual upload
        var uploadResult = await _uploadService.UploadAsync(
            content, fileName, mimeType, userId, options, ct).ConfigureAwait(false);

        if (!uploadResult.Success)
        {
            return new SecureUploadResult(
                false, null, null,
                uploadResult.Error,
                SecureUploadStatus.Rejected);
        }

        var assetContentId = uploadResult.AssetContentId!.Value;
        var assetReferenceId = uploadResult.AssetReferenceId!.Value;

        // Update content with scan status
        var assetContent = await _contentRepository.GetByIdAsync(assetContentId, ct).ConfigureAwait(false);
        ModerationResult? moderationResult = null;
        if (assetContent != null)
        {
            // Mark scan status
            if (_virusScanOptions.Enabled)
            {
                if (requiresSyncScan)
                {
                    // Already scanned synchronously
                    assetContent.SetVirusScanStatus(VirusScanStatus.Clean, "Sync scan passed");
                }
                else
                {
                    // Queue for async scan
                    assetContent.SetVirusScanStatus(VirusScanStatus.Pending);
                }
            }
            else
            {
                // Scanning disabled
                assetContent.SetVirusScanStatus(VirusScanStatus.Clean, "Scanning disabled");
            }

            // Persist the scan decision before moderation loads the content again.
            await _contentRepository.UpdateAsync(assetContent, ct).ConfigureAwait(false);

            // Threat #8: media that requires synchronous moderation must actually be
            // inspected here. Merely marking it NeedsReview leaves every image/video
            // permanently stuck in Processing when no human review was requested.
            if (requiresSyncModeration)
            {
                if (content.CanSeek)
                    content.Position = 0;

                moderationResult = await _moderationService
                    .ModerateAsync(assetContentId, content, mimeType, ct)
                    .ConfigureAwait(false);

                if (content.CanSeek)
                    content.Position = 0;
            }
            else
            {
                // Queue for async moderation
                assetContent.SetModerationStatus(ModerationStatus.Pending);
                await _contentRepository.UpdateAsync(assetContent, ct).ConfigureAwait(false);
            }
        }

        // Determine final status
        var status = SecureUploadStatus.Completed;
        var requiresReview = false;

        if (!requiresSyncScan && _virusScanOptions.Enabled)
        {
            status = SecureUploadStatus.PendingVirusScan;
        }

        if (requiresSyncModeration && moderationResult is null)
        {
            status = SecureUploadStatus.PendingModeration;
            requiresReview = true;
        }
        else if (moderationResult is { IsApproved: false })
        {
            if (moderationResult.Status is ModerationStatus.Blocked or ModerationStatus.Rejected)
            {
                return new SecureUploadResult(
                    false,
                    assetReferenceId,
                    assetContentId,
                    moderationResult.DetectedIssue ?? moderationResult.Error ?? "Content moderation rejected the upload.",
                    SecureUploadStatus.Rejected);
            }

            status = SecureUploadStatus.PendingModeration;
            requiresReview = true;
        }

        return new SecureUploadResult(
            true,
            assetReferenceId,
            assetContentId,
            null,
            status,
            requiresReview);
    }

    private bool RequiresSyncVirusScan(string mimeType)
    {
        if (!_virusScanOptions.Enabled)
            return false;

        if (_virusScanOptions.Mode == VirusScanMode.Sync)
            return true;

        if (_virusScanOptions.Mode == VirusScanMode.Async)
            return false;

        // Hybrid mode: check high-risk MIME types
        return _virusScanOptions.SyncScanMimeTypes.Contains(
            mimeType, StringComparer.OrdinalIgnoreCase);
    }

    private static bool RequiresSyncModeration(string mimeType)
    {
        // High-risk content types that should be reviewed before serving
        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
               mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task QuarantineContentAsync(
        Stream content,
        string fileName,
        string mimeType,
        Guid userId,
        VirusScanResult scanResult,
        CancellationToken ct)
    {
        try
        {
            var quarantineKey = $"quarantine/{userId}/{SystemClock.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}/{fileName}";
            var metadata = new Dictionary<string, string>
            {
                ["OriginalFileName"] = fileName,
                ["MimeType"] = mimeType,
                ["UserId"] = userId.ToString(),
                ["ThreatName"] = scanResult.ThreatName ?? "Unknown",
                ["ThreatType"] = scanResult.ThreatType ?? "Unknown",
                ["QuarantinedAt"] = SystemClock.UtcNow.ToString("O")
            };

            content.Position = 0;
            await _storageService.UploadToQuarantineAsync(content, quarantineKey, metadata, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Quarantined infected file: {FileName} -> {QuarantineKey}",
                fileName, quarantineKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to quarantine infected file: {FileName}", fileName);
            throw;
        }
    }
}
