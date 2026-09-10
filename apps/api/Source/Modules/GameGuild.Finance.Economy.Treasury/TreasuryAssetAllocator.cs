using System.Numerics;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public static class TreasuryAssetAllocator
{
    private const int PpmScale = 1_000_000;

    public static IReadOnlyList<ExternalReserveAsset> Allocate(
        IReadOnlyCollection<ExternalAssetObservation> observations,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<ExternalReserveAsset>();
        foreach (var observation in observations)
        {
            Validate(observation, now);
            if (!seen.Add(observation.AssetKey))
                throw new DuplicateReserveAssetException(
                    $"External reserve asset {observation.AssetKey} cannot back more than one reserve pool.");
            if (observation.Finality != TreasurySettlementFinality.Final) continue;

            var eligible = observation.Kind == TreasuryAssetKind.SettledCash
                ? observation.GrossUsdNanos
                : (long)((BigInteger)observation.GrossUsdNanos * (PpmScale - observation.HaircutPpm) / PpmScale);
            if (eligible <= 0)
                throw new ReserveInputUnknownException("A final reserve asset has no eligible value after haircut.");
            result.Add(new ExternalReserveAsset(observation.AssetKey, observation.Purpose, eligible));
        }

        return result.OrderBy(asset => asset.AssetKey, StringComparer.Ordinal).ToArray();
    }

    private static void Validate(ExternalAssetObservation observation, DateTimeOffset now)
    {
        if (observation is null ||
            string.IsNullOrWhiteSpace(observation.Provider) ||
            string.IsNullOrWhiteSpace(observation.AccountOrNetwork) ||
            string.IsNullOrWhiteSpace(observation.ProviderObjectId) ||
            string.IsNullOrWhiteSpace(observation.EvidenceHash) ||
            !Enum.IsDefined(observation.Kind) ||
            !Enum.IsDefined(observation.Purpose) ||
            !Enum.IsDefined(observation.Finality) ||
            observation.GrossUsdNanos <= 0 ||
            observation.HaircutPpm is < 0 or >= PpmScale ||
            observation.ObservedAt > now || observation.ExpiresAt <= now ||
            observation.Kind == TreasuryAssetKind.SettledCash && observation.HaircutPpm != 0)
            throw new ReserveInputUnknownException("External reserve asset evidence is missing, stale, or invalid.");
    }

}
