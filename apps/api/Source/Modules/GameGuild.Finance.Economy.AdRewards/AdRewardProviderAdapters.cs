namespace GameGuild.Finance.Economy.AdRewards;

public sealed class AdRewardProviderAdapterResolver : IAdRewardProviderAdapterResolver
{
    private readonly IReadOnlyDictionary<string, IAdRewardProviderAdapter> _adapters;

    public AdRewardProviderAdapterResolver(IEnumerable<IAdRewardProviderAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = adapters.ToDictionary(adapter => adapter.Network, StringComparer.OrdinalIgnoreCase);
    }

    public IAdRewardProviderAdapter Resolve(string network)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        return _adapters.TryGetValue(network.Trim(), out var adapter)
            ? adapter
            : throw new AdRewardProviderUnavailableException(
                "No certified ad reward provider adapter is configured for the network.");
    }
}
