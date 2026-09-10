using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards;

public enum AdRewardIssuanceMode
{
    ImmediateProviderProof = 1,
    DeferredReport = 2,
    Disabled = 3
}

public enum AdNetworkYieldState
{
    ColdStart = 1,
    Trailing = 2
}

public sealed record AdNetworkPolicy
{
    public AdNetworkPolicy(
        string network,
        PolicyVersion version,
        DateTimeOffset effectiveAt,
        DateTimeOffset expiresAt,
        AdRewardIssuanceMode issuanceMode,
        AdNetworkYieldState yieldState,
        long estimatedNetEcpmUsdNanos,
        int contractedRevenueSharePpm,
        int safetyBufferPpm,
        int minimumVisiblePpm,
        TimeSpan maximumFocusLoss,
        long maximumRewardSoftUnits,
        DateTimeOffset reportsCurrentThrough,
        TimeSpan reportStaleAfter,
        int ranking)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        if (expiresAt <= effectiveAt) throw new ArgumentException("Policy expiry must follow its effective time.", nameof(expiresAt));
        if (!Enum.IsDefined(issuanceMode)) throw new ArgumentOutOfRangeException(nameof(issuanceMode));
        if (!Enum.IsDefined(yieldState)) throw new ArgumentOutOfRangeException(nameof(yieldState));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedNetEcpmUsdNanos);
        ValidatePpm(contractedRevenueSharePpm, nameof(contractedRevenueSharePpm), allowOneMillion: true);
        ValidatePpm(safetyBufferPpm, nameof(safetyBufferPpm), allowOneMillion: false);
        ValidatePpm(minimumVisiblePpm, nameof(minimumVisiblePpm), allowOneMillion: true);
        if (maximumFocusLoss < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumFocusLoss));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRewardSoftUnits);
        if (reportStaleAfter <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(reportStaleAfter));
        ArgumentOutOfRangeException.ThrowIfNegative(ranking);

        Network = network.Trim();
        Version = version;
        EffectiveAt = effectiveAt;
        ExpiresAt = expiresAt;
        IssuanceMode = issuanceMode;
        YieldState = yieldState;
        EstimatedNetEcpmUsdNanos = estimatedNetEcpmUsdNanos;
        ContractedRevenueSharePpm = contractedRevenueSharePpm;
        SafetyBufferPpm = safetyBufferPpm;
        MinimumVisiblePpm = minimumVisiblePpm;
        MaximumFocusLoss = maximumFocusLoss;
        MaximumRewardSoftUnits = maximumRewardSoftUnits;
        ReportsCurrentThrough = reportsCurrentThrough;
        ReportStaleAfter = reportStaleAfter;
        Ranking = ranking;
    }

    public string Network { get; }
    public PolicyVersion Version { get; }
    public DateTimeOffset EffectiveAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public AdRewardIssuanceMode IssuanceMode { get; }
    public AdNetworkYieldState YieldState { get; }
    public long EstimatedNetEcpmUsdNanos { get; }
    public int ContractedRevenueSharePpm { get; }
    public int SafetyBufferPpm { get; }
    public int MinimumVisiblePpm { get; }
    public TimeSpan MaximumFocusLoss { get; }
    public long MaximumRewardSoftUnits { get; }
    public DateTimeOffset ReportsCurrentThrough { get; }
    public TimeSpan ReportStaleAfter { get; }
    public int Ranking { get; }

    public bool IsEffective(DateTimeOffset now) => EffectiveAt <= now && now < ExpiresAt;
    public bool IsReportCurrent(DateTimeOffset now) => now <= ReportsCurrentThrough + ReportStaleAfter;

    private static void ValidatePpm(int value, string parameterName, bool allowOneMillion)
    {
        var maximum = allowOneMillion ? 1_000_000 : 999_999;
        if (value is < 0 || value > maximum) throw new ArgumentOutOfRangeException(parameterName);
    }
}

public sealed class AdNetworkPolicyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<AdNetworkPolicy>> _policies = new(StringComparer.OrdinalIgnoreCase);

    public void Publish(AdNetworkPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_gate)
        {
            if (!_policies.TryGetValue(policy.Network, out var versions))
            {
                versions = [];
                _policies.Add(policy.Network, versions);
            }

            var latest = versions.OrderByDescending(item => item.Version.Value).FirstOrDefault();
            if (latest is not null &&
                (policy.Version.Value <= latest.Version.Value || policy.EffectiveAt < latest.ExpiresAt))
                throw new AdNetworkPolicyConflictException("Ad network policy versions must increase without overlapping.");
            versions.Add(policy);
        }
    }

    public AdNetworkPolicy Current(string network, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        lock (_gate)
        {
            var current = _policies.GetValueOrDefault(network.Trim())?
                .Where(policy => policy.IsEffective(now))
                .OrderByDescending(policy => policy.Version.Value)
                .FirstOrDefault();
            return current ?? throw new AdNetworkPolicyUnavailableException("No effective ad network policy is available.");
        }
    }

    public AdNetworkPolicy Get(string network, PolicyVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        lock (_gate)
        {
            return _policies.GetValueOrDefault(network.Trim())?
                       .SingleOrDefault(policy => policy.Version == version)
                   ?? throw new AdNetworkPolicyUnavailableException("Ad network policy version was not found.");
        }
    }

    public AdNetworkPolicy Latest(string network)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        lock (_gate)
        {
            return _policies.GetValueOrDefault(network.Trim())?
                       .OrderByDescending(policy => policy.Version.Value)
                       .FirstOrDefault()
                   ?? throw new AdNetworkPolicyUnavailableException("Ad network policy was not found.");
        }
    }
}

public sealed class AdRewardControlState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _disabledNetworks = new(StringComparer.OrdinalIgnoreCase);
    private long _globalEpoch;
    private bool _globalEnabled = true;

    public void DisableNetwork(string network, long epoch, string reason)
    {
        ValidateChange(network, epoch, reason);
        lock (_gate)
        {
            EnsureNetworkEpoch(network, epoch);
            _disabledNetworks[network.Trim()] = epoch;
        }
    }

    public void EnableNetwork(string network, long epoch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        lock (_gate)
        {
            EnsureNetworkEpoch(network, epoch);
            _disabledNetworks.Remove(network.Trim());
        }
    }

    public void DisableGlobally(long epoch, string reason)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            if (epoch <= _globalEpoch) throw new AdRewardControlConflictException("Global control epochs must increase.");
            _globalEpoch = epoch;
            _globalEnabled = false;
        }
    }

    public void EnableGlobally(long epoch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        lock (_gate)
        {
            if (epoch <= _globalEpoch) throw new AdRewardControlConflictException("Global control epochs must increase.");
            _globalEpoch = epoch;
            _globalEnabled = true;
        }
    }

    public void EnsureIssuanceEnabled(string network)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        lock (_gate)
        {
            if (!_globalEnabled || _disabledNetworks.ContainsKey(network.Trim()))
                throw new AdRewardIssuanceDisabledException("Ad reward issuance is disabled by an active control.");
        }
    }

    private static void ValidateChange(string network, long epoch, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
    }

    private void EnsureNetworkEpoch(string network, long epoch)
    {
        var previous = _disabledNetworks.GetValueOrDefault(network.Trim());
        if (epoch <= previous) throw new AdRewardControlConflictException("Network control epochs must increase.");
    }
}

public sealed class AdNetworkPolicyConflictException(string message) : InvalidOperationException(message);
public sealed class AdNetworkPolicyUnavailableException(string message) : InvalidOperationException(message);
public sealed class AdRewardControlConflictException(string message) : InvalidOperationException(message);
public sealed class AdRewardIssuanceDisabledException(string message) : InvalidOperationException(message);
