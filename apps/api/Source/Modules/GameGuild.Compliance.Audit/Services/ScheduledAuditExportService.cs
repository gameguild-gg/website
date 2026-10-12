using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Server-side configuration for scheduled audit exports. InProgress executions older than
/// <see cref="StaleClaimThreshold"/> are treated as dead claims (for example after a process
/// death mid-run) and recovered by the scheduling sweep.
/// </summary>
public sealed class AuditScheduledExportOptions
{
    public static string ConfigurationSection { get; } = "Audit:ScheduledExports";
    public static string StaleClaimThresholdMinutesKey { get; } = "StaleClaimThresholdMinutes";

    public TimeSpan StaleClaimThreshold { get; set; } = TimeSpan.FromMinutes(30);
}

public sealed record ScheduledAuditExportDownload(Stream Content, string FileName, string ContentType);

public interface IScheduledAuditExportService
{
    Task<ScheduledAuditExportResponse> CreateAsync(
        CreateScheduledAuditExportRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledAuditExportResponse>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<ScheduledAuditExportResponse?> GetAsync(Guid exportId, Guid tenantId, CancellationToken cancellationToken);

    Task<bool> DisableAsync(Guid exportId, Guid tenantId, Guid adminUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditExportHistoryResponse>> GetHistoryAsync(
        Guid exportId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<ScheduledAuditExportDownload?> OpenDownloadAsync(
        Guid historyId,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<int> ProcessDueAsync(CancellationToken cancellationToken);

    Task<int> ExpireExpiredFilesAsync(CancellationToken cancellationToken);
}

public sealed partial class ScheduledAuditExportService(
    IScheduledAuditExportRepository repository,
    IAuditService auditService,
    IAuditExportCronSchedule cronSchedule,
    IAuditScheduledExportStorage storage,
    IOptions<AuditScheduledExportOptions> optionsAccessor,
    ILogger<ScheduledAuditExportService> logger) : IScheduledAuditExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AuditScheduledExportOptions _options = optionsAccessor.Value;

    public async Task<ScheduledAuditExportResponse> CreateAsync(
        CreateScheduledAuditExportRequest request,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var nowUtc = SystemClock.UtcNow;
        var nextRunUtc = cronSchedule.GetNextRunUtc(request.CronExpression, request.Timezone, nowUtc);
        var export = ScheduledAuditExport.Create(
            request.TenantId,
            request.JobName.Trim(),
            request.CronExpression.Trim(),
            ExportDestinationType.TenantStorage,
            "tenant-storage",
            request.ExportFormat);
        export.ConfigureFilters(
            request.Timezone.Trim(),
            request.StartDate,
            request.EndDate,
            request.ActionType,
            request.RiskLevel?.ToString(),
            request.UserId?.ToString("D"),
            request.Columns,
            request.RetentionDays);
        export.UpdateNextRunTime(nextRunUtc);

        await repository.AddAsync(export, cancellationToken).ConfigureAwait(false);
        await auditService.LogTenantOperationAsync(
            "CreateScheduledAuditExport",
            request.TenantId,
            adminUserId,
            $"Created scheduled audit export '{export.JobName}'",
            new { export.Id, export.CronExpression, export.ExportFormat }).ConfigureAwait(false);

        return Map(export);
    }

    public async Task<IReadOnlyList<ScheduledAuditExportResponse>> GetForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        (await repository.GetForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
        .Select(Map)
        .ToArray();

    public async Task<ScheduledAuditExportResponse?> GetAsync(
        Guid exportId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var export = await repository.GetByIdAsync(exportId, cancellationToken).ConfigureAwait(false);
        return export?.TenantId == tenantId ? Map(export) : null;
    }

    public async Task<bool> DisableAsync(
        Guid exportId,
        Guid tenantId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var export = await repository.GetByIdAsync(exportId, cancellationToken).ConfigureAwait(false);
        if (export is null || export.TenantId != tenantId) { return false; }

        export.Disable();
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await auditService.LogTenantOperationAsync(
            "DisableScheduledAuditExport",
            tenantId,
            adminUserId,
            $"Disabled scheduled audit export '{export.JobName}'",
            new { export.Id }).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<AuditExportHistoryResponse>> GetHistoryAsync(
        Guid exportId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        (await repository.GetHistoryAsync(exportId, tenantId, limit: 100, cancellationToken).ConfigureAwait(false))
        .Select(history => new AuditExportHistoryResponse(
            history.Id,
            history.ScheduledExportId,
            history.ExecutedAt,
            history.Status,
            history.RecordCount,
            history.FileSizeBytes,
            history.FileName,
            history.ExportPath is not null,
            history.ErrorMessage,
            history.ExecutionDuration))
        .ToArray();

    public async Task<ScheduledAuditExportDownload?> OpenDownloadAsync(
        Guid historyId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var history = await repository.GetHistoryByIdAsync(historyId, tenantId, cancellationToken).ConfigureAwait(false);
        if (history?.ExportPath is null || string.IsNullOrWhiteSpace(history.FileName)) { return null; }

        var contentType = history.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? "text/csv; charset=utf-8"
            : "application/json; charset=utf-8";
        var content = await storage.OpenReadAsync(tenantId, history.ExportPath, cancellationToken).ConfigureAwait(false);
        return new ScheduledAuditExportDownload(content, history.FileName, contentType);
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        await RecoverStaleClaimsAsync(cancellationToken).ConfigureAwait(false);
        var nowUtc = SystemClock.UtcNow;
        var dueExports = await repository.GetDueAsync(nowUtc, limit: 20, cancellationToken).ConfigureAwait(false);
        var processed = 0;

        foreach (var export in dueExports)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextRunUtc = cronSchedule.GetNextRunUtc(export.CronExpression, export.Timezone, nowUtc);
            var history = AuditExportHistory.Create(export.Id, export.TenantId!.Value);
            if (!await repository.TryClaimAsync(
                    export.Id,
                    export.Version,
                    nowUtc,
                    nextRunUtc,
                    history,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            var timer = Stopwatch.StartNew();
            try
            {
                var artifact = await CreateArtifactAsync(export, nowUtc, cancellationToken).ConfigureAwait(false);
                await repository.RecordCompletedAsync(
                    export.Id,
                    history.Id,
                    artifact.RecordCount,
                    artifact.FileSizeBytes,
                    artifact.StorageReference,
                    artifact.Sha256,
                    artifact.FileName,
                    timer.Elapsed,
                    SystemClock.UtcNow,
                    cancellationToken).ConfigureAwait(false);
                await LogExecutionAsync(
                    export,
                    history.Id,
                    artifact.RecordCount,
                    succeeded: true).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await repository.RecordFailedAsync(
                    export.Id,
                    history.Id,
                    timer.Elapsed,
                    SystemClock.UtcNow,
                    CancellationToken.None).ConfigureAwait(false);
                await LogExecutionAsync(export, history.Id, recordCount: 0, succeeded: false).ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled audit export {ExportId} failed", export.Id);
                await repository.RecordFailedAsync(
                    export.Id,
                    history.Id,
                    timer.Elapsed,
                    SystemClock.UtcNow,
                    CancellationToken.None).ConfigureAwait(false);
                await LogExecutionAsync(export, history.Id, recordCount: 0, succeeded: false).ConfigureAwait(false);
            }

            processed++;
        }

        return processed;
    }

    private async Task RecoverStaleClaimsAsync(CancellationToken cancellationToken)
    {
        var recovered = await repository.RecoverStaleClaimsAsync(
            SystemClock.UtcNow,
            _options.StaleClaimThreshold,
            cancellationToken).ConfigureAwait(false);

        if (recovered > 0)
        {
            logger.LogWarning(
                "Recovered {ClaimCount} stale scheduled audit export claim(s) older than {StaleClaimThreshold}",
                recovered,
                _options.StaleClaimThreshold);
        }
    }

    private async Task LogExecutionAsync(
        ScheduledAuditExport export,
        Guid historyId,
        int recordCount,
        bool succeeded)
    {
        try
        {
            await auditService.LogTenantOperationAsync(
                "ExecuteScheduledAuditExport",
                export.TenantId!.Value,
                description: succeeded ? "Scheduled audit export completed." : "Scheduled audit export failed.",
                metadata: new { export.Id, HistoryId = historyId, RecordCount = recordCount },
                success: succeeded).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Scheduled audit export {HistoryId} was not recorded in the audit log", historyId);
        }
    }

    public async Task<int> ExpireExpiredFilesAsync(CancellationToken cancellationToken)
    {
        var expiredFiles = await repository.GetExpiredFilesAsync(SystemClock.UtcNow, limit: 100, cancellationToken)
            .ConfigureAwait(false);
        var expired = 0;

        foreach (var file in expiredFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await storage.DeleteAsync(file.TenantId, file.StorageReference, cancellationToken).ConfigureAwait(false);
                await repository.MarkFileExpiredAsync(file.HistoryId, cancellationToken).ConfigureAwait(false);
                expired++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Expired audit export file for history {HistoryId} could not be removed", file.HistoryId);
            }
        }

        return expired;
    }

    private async Task<ScheduledExportArtifact> CreateArtifactAsync(
        ScheduledAuditExport export,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var tenantId = export.TenantId!.Value;
        var userId = Guid.TryParse(export.UserIdFilter, out var parsedUserId) ? parsedUserId : (Guid?)null;
        var riskLevel = Enum.TryParse<AuditRiskLevel>(export.RiskLevelFilter, ignoreCase: true, out var parsedRiskLevel)
            ? parsedRiskLevel
            : (AuditRiskLevel?)null;
        var query = new AuditLogQuery
        {
            TenantId = tenantId,
            UserId = userId,
            ActionType = export.IncludeEventTypes.SingleOrDefault(),
            RiskLevel = riskLevel,
            StartDate = export.StartDate ?? export.LastRunAt ?? export.CreatedAt,
            EndDate = export.EndDate ?? nowUtc,
            Skip = 0,
            Take = 0
        };
        var recordCount = await auditService.GetAuditLogCountAsync(query).ConfigureAwait(false);
        var extension = export.ExportFormat == ExportFormat.Csv ? "csv" : "json";
        var contentType = export.ExportFormat == ExportFormat.Csv ? "text/csv; charset=utf-8" : "application/json; charset=utf-8";
        var safeJobName = InvalidFileNameCharacters().Replace(export.JobName, "-").Trim('-', '.');
        if (safeJobName.Length == 0) { safeJobName = "audit-export"; }
        var fileName = $"{safeJobName}-{nowUtc:yyyyMMdd'T'HHmmss'Z'}.{extension}";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "gameguild-audit-exports");
        Directory.CreateDirectory(tempDirectory);
        var tempFilePath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.{extension}");

        try
        {
            await using (var file = new FileStream(
                             tempFilePath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             bufferSize: 64 * 1024,
                             options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var records = auditService.StreamAuditLogsAsync(query, cancellationToken);
                if (export.ExportFormat == ExportFormat.Csv)
                {
                    if (!AuditCsvExporter.TryResolveColumns(export.CsvColumns, out var columns, out var error))
                    {
                        throw new InvalidOperationException(error);
                    }

                    await AuditCsvExporter.WriteAsync(file, records, columns, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var document = new AuditJsonExportDocument(
                        "1.0",
                        new AuditJsonExportPagination(1, Math.Max(recordCount, 1), recordCount, recordCount == 0 ? 0 : 1),
                        MapJsonRecordsAsync(records, cancellationToken));
                    await JsonSerializer.SerializeAsync(file, document, JsonOptions, cancellationToken).ConfigureAwait(false);
                }

                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                file.Position = 0;
                var hash = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);
                var sha256 = Convert.ToHexString(hash).ToLowerInvariant();
                file.Position = 0;
                var storageReference = await storage.StoreAsync(
                    tenantId,
                    file,
                    sha256,
                    contentType,
                    fileName,
                    cancellationToken).ConfigureAwait(false);
                return new ScheduledExportArtifact(recordCount, file.Length, storageReference, sha256, fileName);
            }
        }
        finally
        {
            try
            {
                File.Delete(tempFilePath);
            }
            catch (IOException exception)
            {
                logger.LogWarning(exception, "Temporary audit export {TemporaryFile} could not be removed", tempFilePath);
            }
        }
    }

    private static async IAsyncEnumerable<AuditJsonExportRecord> MapJsonRecordsAsync(
        IAsyncEnumerable<AuditLog> records,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return AuditJsonExportMapper.Map(record);
        }
    }

    private static ScheduledAuditExportResponse Map(ScheduledAuditExport export) => new(
        export.Id,
        export.TenantId!.Value,
        export.JobName,
        export.CronExpression,
        export.Timezone,
        "tenant-storage",
        export.ExportFormat,
        export.IsEnabled,
        export.NextRunAt,
        export.LastRunAt,
        export.SuccessCount,
        export.FailureCount,
        export.RetentionDays,
        export.CreatedAt,
        export.UpdatedAt);

    [GeneratedRegex("[^A-Za-z0-9._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidFileNameCharacters();

    private sealed record ScheduledExportArtifact(
        int RecordCount,
        long FileSizeBytes,
        string StorageReference,
        string Sha256,
        string FileName);
}
