using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.VirusScan;

/// <summary>
/// Configuration for virus scanning.
/// </summary>
public class VirusScanOptions
{
    public const string SectionName = "Assets:VirusScan";

    /// <summary>
    /// Whether virus scanning is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Scan mode: Sync (block upload), Async (scan after upload), or Hybrid
    /// </summary>
    public VirusScanMode Mode { get; set; } = VirusScanMode.Hybrid;

    /// <summary>
    /// MIME types that require synchronous scanning (high-risk).
    /// </summary>
    public string[] SyncScanMimeTypes { get; set; } =
    [
        "application/x-msdownload",     // .exe
        "application/x-dosexec",        // DOS/Windows executable
        "application/x-executable",     // Executable
        "application/x-msdos-program",  // DOS executable
        "application/vnd.microsoft.portable-executable", // PE
        "application/x-sharedlib",      // Shared library
        "application/x-object",         // Object file
        "application/javascript",       // JavaScript
        "text/javascript",
        "application/x-javascript",
        "application/x-sh",             // Shell script
        "application/x-bash",
        "application/x-csh",
        "text/x-script.sh",
        "application/x-bat",            // Batch file
        "application/bat",
        "application/x-msi",            // MSI installer
        "application/x-ms-installer",
        "application/x-java-archive",   // JAR
        "application/java-archive",
        "application/x-rar-compressed", // RAR
        "application/vnd.rar",
        "application/x-7z-compressed",  // 7z
        "application/zip",              // ZIP
        "application/x-zip-compressed",
        "application/octet-stream"      // Unknown binary
    ];

    /// <summary>
    /// ClamAV daemon host.
    /// </summary>
    public string ClamAvHost { get; set; } = "localhost";

    /// <summary>
    /// ClamAV daemon port.
    /// </summary>
    public int ClamAvPort { get; set; } = 3310;

    /// <summary>
    /// Timeout for scan operations in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Delay between asynchronous scan cycles.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum number of pending objects claimed in one cycle.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Maximum file size to scan (larger files are rejected).
    /// </summary>
    public long MaxScanSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Whether to stream file bytes to a ClamAV daemon.
    /// </summary>
    public bool UseClamAvDaemon { get; set; }

    /// <summary>
    /// Whether scanner connectivity failures should reject the file.
    /// </summary>
    public bool FailClosedWhenScannerUnavailable { get; set; } = true;

    /// <summary>
    /// Extensions that are rejected by local policy scanning.
    /// </summary>
    public string[] BlockedExtensions { get; set; } =
    [
        ".exe",
        ".dll",
        ".msi",
        ".bat",
        ".cmd",
        ".ps1",
        ".sh",
        ".jar"
    ];

    /// <summary>
    /// Whether to quarantine infected files instead of deleting.
    /// </summary>
    public bool QuarantineInfected { get; set; } = true;

    /// <summary>
    /// Quarantine bucket name for infected files.
    /// </summary>
    public string QuarantineBucket { get; set; } = "quarantine";
}

/// <summary>
/// Virus scan mode.
/// </summary>
public enum VirusScanMode
{
    /// <summary>Scan synchronously before upload completes (blocking)</summary>
    Sync,

    /// <summary>Scan asynchronously after upload (non-blocking)</summary>
    Async,

    /// <summary>Sync for high-risk MIME types, async for others</summary>
    Hybrid
}

/// <summary>
/// Result of a virus scan.
/// </summary>
public sealed record VirusScanResult(
    bool IsClean,
    string Status,
    string? ThreatName = null,
    string? ThreatType = null,
    string? ScanEngine = null,
    string? ScanEngineVersion = null,
    TimeSpan ScanDuration = default,
    string? Details = null);

/// <summary>
/// Interface for virus scanning service.
/// Mitigates: Malware Upload (Threat #7)
/// </summary>
public interface IVirusScanService
{
    /// <summary>
    /// Scans a stream for viruses/malware.
    /// </summary>
    Task<VirusScanResult> ScanAsync(
        Stream content,
        string fileName,
        CancellationToken ct = default);

    /// <summary>
    /// Scans content stored in object storage.
    /// </summary>
    Task<VirusScanResult> ScanStoredAsync(
        string bucketName,
        string objectKey,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the health status of the virus scan service.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);

    /// <summary>
    /// Determines if a MIME type requires synchronous scanning.
    /// </summary>
    bool RequiresSyncScan(string mimeType);
}

/// <summary>
/// Virus scanning service with local policy checks and optional ClamAV daemon integration.
/// </summary>
public class VirusScanService : IVirusScanService
{
    private const string EicarSignature = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
    private readonly VirusScanOptions _options;
    private readonly ILogger<VirusScanService> _logger;

    public VirusScanService(
        IOptions<VirusScanOptions> options,
        ILogger<VirusScanService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VirusScanResult> ScanAsync(
        Stream content,
        string fileName,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new VirusScanResult(true, "Scanning disabled");
        }

        var startTime = SystemClock.UtcNow;

        try
        {
            if (content.CanSeek && content.Length > _options.MaxScanSizeBytes)
            {
                return new VirusScanResult(
                    false,
                    "File too large for scanning",
                    ThreatName: "OVERSIZED_FILE",
                    ThreatType: "Policy",
                    Details: $"File size {content.Length} exceeds maximum {_options.MaxScanSizeBytes}");
            }

            if (_options.UseClamAvDaemon)
            {
                try
                {
                    return await ScanWithClamAvAsync(content, fileName, startTime, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is SocketException or IOException or TimeoutException)
                {
                    _logger.LogError(ex, "ClamAV scan failed for {FileName}", fileName);

                    if (_options.FailClosedWhenScannerUnavailable)
                    {
                        return new VirusScanResult(
                            false,
                            "Scanner unavailable",
                            ThreatName: "SCANNER_UNAVAILABLE",
                            ThreatType: "Configuration",
                            ScanEngine: "ClamAV",
                            ScanDuration: SystemClock.UtcNow - startTime,
                            Details: ex.Message);
                    }
                }
            }

            var localResult = await ScanWithLocalPolicyAsync(content, fileName, startTime, ct).ConfigureAwait(false);
            if (!localResult.IsClean)
            {
                return localResult;
            }

            var duration = SystemClock.UtcNow - startTime;

            _logger.LogDebug(
                "Virus scan completed for {FileName}: {Status} in {Duration}ms",
                fileName,
                localResult.Status,
                duration.TotalMilliseconds);

            return localResult with { ScanDuration = duration };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Virus scan failed for {FileName}", fileName);
            return new VirusScanResult(
                false,
                "Scan failed",
                Details: ex.Message);
        }
    }

    public Task<VirusScanResult> ScanStoredAsync(
        string bucketName,
        string objectKey,
        CancellationToken ct = default)
    {
        var startTime = SystemClock.UtcNow;

        if (!_options.Enabled)
        {
            return Task.FromResult(new VirusScanResult(true, "Scanning disabled"));
        }

        var localPolicyResult = ScanFileNamePolicy(objectKey, startTime);
        if (!localPolicyResult.IsClean)
        {
            return Task.FromResult(localPolicyResult);
        }

        return Task.FromResult(new VirusScanResult(
            false,
            "Stored object scan requires stream content",
            ThreatName: "STORED_SCAN_REQUIRES_STREAM",
            ThreatType: "Configuration",
            ScanEngine: "LocalPolicy",
            ScanEngineVersion: "1.0",
            ScanDuration: SystemClock.UtcNow - startTime,
            Details: $"Object {bucketName}/{objectKey} must be downloaded and passed to ScanAsync for byte-level scanning."));
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.UseClamAvDaemon)
        {
            return true;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

            using var client = new TcpClient();
            await client.ConnectAsync(_options.ClamAvHost, _options.ClamAvPort, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();

            var ping = Encoding.ASCII.GetBytes("zPING\0");
            await stream.WriteAsync(ping, timeout.Token).ConfigureAwait(false);

            var buffer = new byte[32];
            var read = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
            var response = Encoding.ASCII.GetString(buffer, 0, read).TrimEnd('\0', '\r', '\n');

            return string.Equals(response, "PONG", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Virus scanner health check failed");
            return false;
        }
    }

    public bool RequiresSyncScan(string mimeType)
    {
        if (_options.Mode == VirusScanMode.Sync)
            return true;

        if (_options.Mode == VirusScanMode.Async)
            return false;

        // Hybrid mode: check high-risk MIME types
        return _options.SyncScanMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<VirusScanResult> ScanWithLocalPolicyAsync(
        Stream content,
        string fileName,
        DateTime startTime,
        CancellationToken ct)
    {
        var fileNamePolicy = ScanFileNamePolicy(fileName, startTime);
        if (!fileNamePolicy.IsClean)
        {
            return fileNamePolicy;
        }

        var originalPosition = content.CanSeek ? content.Position : (long?)null;
        try
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct).ConfigureAwait(false);
            var text = Encoding.ASCII.GetString(buffer.ToArray());

            if (text.Contains(EicarSignature, StringComparison.Ordinal))
            {
                return new VirusScanResult(
                    false,
                    "Malware signature detected",
                    ThreatName: "EICAR-Test-Signature",
                    ThreatType: "Virus",
                    ScanEngine: "LocalPolicy",
                    ScanEngineVersion: "1.0",
                    ScanDuration: SystemClock.UtcNow - startTime);
            }

            return new VirusScanResult(
                true,
                "Clean",
                ScanEngine: "LocalPolicy",
                ScanEngineVersion: "1.0",
                ScanDuration: SystemClock.UtcNow - startTime);
        }
        finally
        {
            if (content.CanSeek && originalPosition.HasValue)
            {
                content.Position = originalPosition.Value;
            }
        }
    }

    private VirusScanResult ScanFileNamePolicy(string fileName, DateTime startTime)
    {
        var extension = Path.GetExtension(fileName);
        if (_options.BlockedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return new VirusScanResult(
                false,
                "Blocked file type",
                ThreatName: "BLOCKED_EXTENSION",
                ThreatType: "Policy",
                ScanEngine: "LocalPolicy",
                ScanEngineVersion: "1.0",
                ScanDuration: SystemClock.UtcNow - startTime,
                Details: $"Extension '{extension}' is blocked.");
        }

        return new VirusScanResult(
            true,
            "Clean",
            ScanEngine: "LocalPolicy",
            ScanEngineVersion: "1.0",
            ScanDuration: SystemClock.UtcNow - startTime);
    }

    private async Task<VirusScanResult> ScanWithClamAvAsync(
        Stream content,
        string fileName,
        DateTime startTime,
        CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        using var client = new TcpClient();
        await client.ConnectAsync(_options.ClamAvHost, _options.ClamAvPort, timeout.Token).ConfigureAwait(false);
        await using var networkStream = client.GetStream();

        var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
        await networkStream.WriteAsync(command, timeout.Token).ConfigureAwait(false);

        var originalPosition = content.CanSeek ? content.Position : (long?)null;
        try
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                Span<byte> lengthPrefix = stackalloc byte[4];
                BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, bytesRead);
                await networkStream.WriteAsync(lengthPrefix.ToArray(), timeout.Token).ConfigureAwait(false);
                await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead), timeout.Token).ConfigureAwait(false);
            }

            await networkStream.WriteAsync(new byte[4], timeout.Token).ConfigureAwait(false);

            var responseBuffer = new byte[4096];
            var read = await networkStream.ReadAsync(responseBuffer, timeout.Token).ConfigureAwait(false);
            var response = Encoding.UTF8.GetString(responseBuffer, 0, read).TrimEnd('\0', '\r', '\n');
            var duration = SystemClock.UtcNow - startTime;

            if (response.Contains(" FOUND", StringComparison.OrdinalIgnoreCase))
            {
                var threatName = response
                    .Replace("stream:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("FOUND", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();

                return new VirusScanResult(
                    false,
                    "Malware signature detected",
                    ThreatName: threatName,
                    ThreatType: "Virus",
                    ScanEngine: "ClamAV",
                    ScanDuration: duration,
                    Details: response);
            }

            if (response.Contains(" OK", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(response, "stream: OK", StringComparison.OrdinalIgnoreCase))
            {
                return new VirusScanResult(
                    true,
                    "Clean",
                    ScanEngine: "ClamAV",
                    ScanDuration: duration,
                    Details: response);
            }

            return new VirusScanResult(
                false,
                "Scanner returned an unknown response",
                ThreatName: "UNKNOWN_SCAN_RESPONSE",
                ThreatType: "Configuration",
                ScanEngine: "ClamAV",
                ScanDuration: duration,
                Details: response);
        }
        finally
        {
            if (content.CanSeek && originalPosition.HasValue)
            {
                content.Position = originalPosition.Value;
            }
        }
    }
}
