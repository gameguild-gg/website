using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Threat-intelligence provider backed by an operator-supplied local JSON feed.
///     The feed (format documented in docs/authentication-configuration.md) is a CIDR
///     IP blocklist plus a breached-password SHA-256 full-hash set. The file is read
///     from local disk only — this provider never performs external calls.
///     <para>
///         Failure policy is fail-open: a missing, unreadable, or unparseable feed never
///         throws and never blocks authentication. When no previously good feed exists,
///         checks report <c>ProviderAvailable = false</c> with no match, and one security
///         event per outage is emitted through <see cref="IAuthenticationAuditEventSink" />
///         (which bridges to the central <c>SecurityEventLogger</c> pipeline). A previously
///         good feed keeps being served while reload attempts fail.
///     </para>
/// </summary>
public sealed class LocalFileThreatIntelligenceProvider : IThreatIntelligenceProvider
{
    private const string FeedUnavailableActionType = "Authentication.ThreatIntelligenceFeedUnavailable";
    private const int MaxLoggedStringLength = 256;

    private static readonly JsonSerializerOptions FeedJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LocalFileThreatIntelligenceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LocalFileThreatIntelligenceProvider> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    private FeedState _state = FeedState.Initial;
    private DateTimeOffset _lastLoadAttemptUtc;
    private DateTimeOffset? _lastLoadWriteTimeUtc;
    private bool _outageAnnounced;

    public LocalFileThreatIntelligenceProvider(
        ThreatIntelligenceOptions options,
        TimeProvider timeProvider,
        ILogger<LocalFileThreatIntelligenceProvider> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.LocalFile;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public string ProviderName => ThreatIntelligenceOptions.LocalFileProvider;

    public async Task<ThreatIntelligenceIpResult> CheckIpAddressAsync(string? ipAddress, CancellationToken cancellationToken = default)
    {
        var state = await GetFreshStateAsync(cancellationToken).ConfigureAwait(false);

        if (!TryNormalizeIpAddress(ipAddress, out var ipBytes))
        {
            return UnavailableOrNoIpMatch(state);
        }

        foreach (var cidr in state.Data?.Cidrs ?? [])
        {
            if (MatchesCidr(cidr, ipBytes))
            {
                return new ThreatIntelligenceIpResult(
                    IsMatch: true,
                    MatchedCidr: cidr.Original,
                    ProviderAvailable: state.HasUsableData,
                    ProviderError: state.LastLoadError);
            }
        }

        return UnavailableOrNoIpMatch(state);
    }

    public async Task<ThreatIntelligencePasswordResult> CheckPasswordHashAsync(string? passwordSha256Hex, CancellationToken cancellationToken = default)
    {
        var state = await GetFreshStateAsync(cancellationToken).ConfigureAwait(false);

        // The digest is only ever compared; it is never echoed in results or logs.
        var normalizedHash = passwordSha256Hex?.Trim();
        if (string.IsNullOrEmpty(normalizedHash) || !IsWellFormedSha256Hex(normalizedHash))
        {
            return UnavailableOrNoPasswordMatch(state);
        }

        var matched = state.Data?.BreachedPasswordHashes.Contains(normalizedHash) == true;
        return matched
            ? new ThreatIntelligencePasswordResult(IsMatch: true, ProviderAvailable: state.HasUsableData, ProviderError: state.LastLoadError)
            : UnavailableOrNoPasswordMatch(state);
    }

    // ── Feed state handling ─────────────────────────────────────────────

    private async Task<FeedState> GetFreshStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureFeedCurrentAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Serve whatever state we have; feed refresh must never abort an authentication check.
        }
        catch (Exception exception)
        {
            // Belt-and-braces fail-open: the reload path already catches its own errors.
            _logger.LogWarning(exception, "Threat intelligence feed refresh skipped after an unexpected error");
        }

        return Volatile.Read(ref _state);
    }

    private async Task EnsureFeedCurrentAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (now - _lastLoadAttemptUtc < _options.ReloadInterval && !FeedFileWriteTimeChanged())
        {
            return;
        }

        // Reload work is serialized; cancellation is not propagated into the gate so a
        // completed load is always published to the shared state.
        await _reloadGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (now - _lastLoadAttemptUtc < _options.ReloadInterval && !FeedFileWriteTimeChanged())
            {
                return; // another caller just refreshed the feed
            }

            await LoadFeedAsync(now, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private async Task LoadFeedAsync(DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken)
    {
        _lastLoadAttemptUtc = attemptedAtUtc;

        FeedData? data = null;
        DateTimeOffset? writeTimeUtc = null;
        string? loadError = null;

        try
        {
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(_options.FilePath);
            if (lastWriteTimeUtc == DateTime.MinValue)
            {
                // Normalized to null so a persistently missing file is not treated as
                // "changed" on every check; the reload interval gates retry attempts.
                writeTimeUtc = null;
                throw new FileNotFoundException("Threat intelligence feed file was not found.", _options.FilePath);
            }

            writeTimeUtc = lastWriteTimeUtc;

            await using var stream = new FileStream(
                _options.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var document = await JsonSerializer.DeserializeAsync<FeedDocument>(stream, FeedJsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new JsonException("Threat intelligence feed document was empty.");

            data = BuildFeedData(document, out var invalidEntries);
            if (invalidEntries > 0)
            {
                _logger.LogWarning(
                    "Threat intelligence feed {FilePath} contained {InvalidEntryCount} invalid entries that were skipped",
                    _options.FilePath,
                    invalidEntries);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loadError = exception.Message;
        }

        _lastLoadWriteTimeUtc = writeTimeUtc;

        var previous = Volatile.Read(ref _state);
        var newState = new FeedState(
            Data: data ?? previous.Data,
            LoadedAtUtc: data is null ? previous.LoadedAtUtc : attemptedAtUtc,
            HasUsableData: data is not null || previous.HasUsableData,
            LastLoadError: loadError,
            MaliciousCidrCount: data?.Cidrs.Count ?? previous.MaliciousCidrCount,
            BreachedHashCount: data?.BreachedPasswordHashes.Count ?? previous.BreachedHashCount);
        Volatile.Write(ref _state, newState);

        if (loadError is null)
        {
            _logger.LogInformation(
                "Threat intelligence feed {FilePath} loaded: {CidrCount} IP blocklist CIDRs, {HashCount} breached-password hashes",
                _options.FilePath,
                newState.MaliciousCidrCount,
                newState.BreachedHashCount);
            _outageAnnounced = false;
            return;
        }

        _logger.LogWarning(
            "Threat intelligence feed {FilePath} could not be loaded ({LoadError}); serving last-good data: {ServingLastGood}. Authentication fails open for this signal.",
            _options.FilePath,
            Truncate(loadError),
            newState.HasUsableData);

        if (!_outageAnnounced)
        {
            _outageAnnounced = true;
            await AnnounceFeedOutageAsync(loadError).ConfigureAwait(false);
        }
    }

    private async Task AnnounceFeedOutageAsync(string loadError)
    {
        if (_scopeFactory is null)
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sink = scope.ServiceProvider.GetService<IAuthenticationAuditEventSink>();
            if (sink is null)
            {
                return;
            }

            // Routed through the central security event pipeline by the audit module's sink
            // implementation (CentralAuthenticationAuditEventSink -> SecurityEventLogger).
            await sink.RecordAsync(
                new AuthenticationAuditEvent(
                    FeedUnavailableActionType,
                    UserId: null,
                    Success: false,
                    Method: "ThreatIntelligence",
                    ErrorMessage: Truncate(loadError),
                    AssessedRiskLevel: RiskLevel.Medium,
                    Metadata: new
                    {
                        Provider = ProviderName,
                        FeedPath = _options.FilePath,
                        ServingLastGoodData = Volatile.Read(ref _state).HasUsableData
                    }),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not emit the threat intelligence feed outage security event");
        }
    }

    private bool FeedFileWriteTimeChanged()
    {
        if (!_options.ReloadOnFileChange)
        {
            return false;
        }

        try
        {
            var writeTime = File.GetLastWriteTimeUtc(_options.FilePath);
            var observed = writeTime == DateTime.MinValue ? null : (DateTimeOffset?)writeTime;
            return !observed.Equals(_lastLoadWriteTimeUtc);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private FeedData BuildFeedData(FeedDocument document, out int invalidEntryCount)
    {
        invalidEntryCount = 0;
        var cidrs = new List<MaliciousCidr>();
        foreach (var entry in document.MaliciousIpCidrs ?? [])
        {
            if (entry is null || !TryParseCidr(entry.Trim(), out var cidr))
            {
                invalidEntryCount++;
                continue;
            }

            cidrs.Add(cidr);
        }

        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.BreachedPasswordSha256 ?? [])
        {
            if (entry is null || !IsWellFormedSha256Hex(entry))
            {
                invalidEntryCount++;
                continue;
            }

            hashes.Add(entry.Trim());
        }

        return new FeedData(cidrs, hashes);
    }

    private ThreatIntelligenceIpResult UnavailableOrNoIpMatch(FeedState state)
        => new(IsMatch: false, MatchedCidr: null, ProviderAvailable: state.HasUsableData, ProviderError: state.LastLoadError);

    private ThreatIntelligencePasswordResult UnavailableOrNoPasswordMatch(FeedState state)
        => new(IsMatch: false, ProviderAvailable: state.HasUsableData, ProviderError: state.LastLoadError);

    // ── Parsing helpers ─────────────────────────────────────────────────

    private static bool TryNormalizeIpAddress(string? ipAddress, out byte[] addressBytes)
    {
        addressBytes = [];
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress.Trim(), out var parsed))
        {
            return false;
        }

        if (parsed.IsIPv4MappedToIPv6)
        {
            parsed = parsed.MapToIPv4();
        }

        addressBytes = parsed.GetAddressBytes();
        return true;
    }

    private static bool TryParseCidr(string cidrText, out MaliciousCidr cidr)
    {
        cidr = default;
        var separator = cidrText.IndexOf('/');
        if (separator <= 0 || separator == cidrText.Length - 1)
        {
            return false;
        }

        if (!IPAddress.TryParse(cidrText[..separator], out var network))
        {
            return false;
        }

        if (!int.TryParse(cidrText[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength))
        {
            return false;
        }

        var networkBytes = network.GetAddressBytes();
        if (prefixLength < 0 || prefixLength > networkBytes.Length * 8)
        {
            return false;
        }

        var mask = new byte[networkBytes.Length];
        var remainingBits = prefixLength;
        for (var i = 0; i < mask.Length; i++)
        {
            if (remainingBits >= 8)
            {
                mask[i] = 0xFF;
                remainingBits -= 8;
            }
            else if (remainingBits > 0)
            {
                mask[i] = (byte)(0xFF << (8 - remainingBits));
                remainingBits = 0;
            }
        }

        // Host bits present in the feed entry are masked off so entries such as
        // "203.0.113.37/24" behave identically to "203.0.113.0/24".
        var normalizedNetwork = new byte[networkBytes.Length];
        for (var i = 0; i < networkBytes.Length; i++)
        {
            normalizedNetwork[i] = (byte)(networkBytes[i] & mask[i]);
        }

        cidr = new MaliciousCidr(normalizedNetwork, mask, cidrText);
        return true;
    }

    private static bool MatchesCidr(MaliciousCidr cidr, byte[] addressBytes)
    {
        if (addressBytes.Length != cidr.Network.Length)
        {
            return false;
        }

        for (var i = 0; i < addressBytes.Length; i++)
        {
            if ((addressBytes[i] & cidr.Mask[i]) != cidr.Network[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWellFormedSha256Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.AsSpan().Trim();
        return trimmed.Length == 64 && IsHex(trimmed);
    }

    private static bool IsHex(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (character is (< '0' or > '9') and (< 'a' or > 'f') and (< 'A' or > 'F'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Truncate(string value)
        => value.Length <= MaxLoggedStringLength ? value : value[..MaxLoggedStringLength];

    // ── Internal state shapes ────────────────────────────────────────────

    private readonly record struct MaliciousCidr(byte[] Network, byte[] Mask, string Original);

    private sealed record FeedData(
        IReadOnlyList<MaliciousCidr> Cidrs,
        HashSet<string> BreachedPasswordHashes);

    private sealed record FeedState(
        FeedData? Data,
        DateTimeOffset? LoadedAtUtc,
        bool HasUsableData,
        string? LastLoadError,
        int MaliciousCidrCount,
        int BreachedHashCount)
    {
        public static readonly FeedState Initial = new(
            Data: null,
            LoadedAtUtc: null,
            HasUsableData: false,
            LastLoadError: null,
            MaliciousCidrCount: 0,
            BreachedHashCount: 0);
    }

    /// <summary>Local feed JSON document. The format is documented in docs/authentication-configuration.md.</summary>
    private sealed class FeedDocument
    {
        public List<string>? MaliciousIpCidrs { get; set; }

        public List<string>? BreachedPasswordSha256 { get; set; }
    }
}
