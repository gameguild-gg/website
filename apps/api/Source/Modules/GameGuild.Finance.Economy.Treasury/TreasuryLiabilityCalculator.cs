using System.Numerics;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public static class TreasuryLiabilityCalculator
{
    public static TreasuryLiabilityCalculation Calculate(
        InMemoryLedgerKernelStore ledger,
        IReadOnlySet<WalletId> companyOwnedWallets,
        IReadOnlyCollection<TreasuryServiceCostSnapshot> serviceCosts,
        IReadOnlyCollection<TreasuryOpenServiceAuthorization> openAuthorizations,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(companyOwnedWallets);
        ArgumentNullException.ThrowIfNull(serviceCosts);
        ArgumentNullException.ThrowIfNull(openAuthorizations);

        var lots = ledger.CreditLots
            .Where(lot => !companyOwnedWallets.Contains(lot.WalletId) &&
                          lot.State is CreditLotState.Active or CreditLotState.Held)
            .Select(lot => ToLiability(lot, ledger.FragmentConsumptions))
            .Where(lot => lot.OutstandingUnits > 0)
            .OrderBy(lot => lot.LotId.Value)
            .ToArray();
        var hard = Sum(lots.Where(lot => lot.Currency == CurrencyCode.HardCoin).Select(lot => lot.OutstandingUnits));
        var soft = Sum(lots.Where(lot => lot.Currency == CurrencyCode.SoftCoin).Select(lot => lot.OutstandingUnits));

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var reservedByService = new Dictionary<string, long>(StringComparer.Ordinal);
        BigInteger irreversible = 0;
        foreach (var authorization in openAuthorizations)
        {
            if (authorization is null || string.IsNullOrWhiteSpace(authorization.AuthorizationKey) ||
                string.IsNullOrWhiteSpace(authorization.ServiceCode) || authorization.ReservedSoftUnits < 0 ||
                authorization.IrreversibleProviderCostUsdNanos < 0 ||
                !keys.Add(authorization.AuthorizationKey.Trim()))
                throw new ReserveInputUnknownException("Open service authorization evidence is invalid or duplicated.");
            var serviceCode = authorization.ServiceCode.Trim();
            reservedByService[serviceCode] = checked(
                reservedByService.GetValueOrDefault(serviceCode) + authorization.ReservedSoftUnits);
            irreversible += authorization.IrreversibleProviderCostUsdNanos;
        }

        var reserved = Sum(reservedByService.Values);
        if (reserved > soft)
            throw new ReserveInputUnknownException("Open service authorizations exceed outstanding soft liability.");

        var services = serviceCosts.Select(cost =>
        {
            if (cost is null) throw new ReserveInputUnknownException("Service cost evidence is missing.");
            var code = cost.ServiceCode?.Trim() ?? string.Empty;
            return new ReserveServiceObservation(
                code,
                cost.CurrentServicePriceSoftUnits,
                cost.CurrentProviderCostUsdNanos,
                cost.TrailingHighPercentileCostUsdNanos,
                cost.ProviderFxStressCostUsdNanos,
                reservedByService.GetValueOrDefault(code),
                cost.Enabled,
                cost.ObservedAt,
                cost.ExpiresAt);
        }).OrderBy(service => service.ServiceCode, StringComparer.Ordinal).ToArray();
        if (reservedByService.Keys.Except(services.Select(service => service.ServiceCode), StringComparer.Ordinal).Any())
            throw new ReserveInputUnknownException("An open authorization has no service cost observation.");

        return new TreasuryLiabilityCalculation(
            new ReserveLiabilityPosition(hard, soft, soft - reserved, ToLong(irreversible)),
            services,
            lots);
    }

    private static TreasuryLotLiability ToLiability(
        CreditLot lot,
        IReadOnlyCollection<FragmentConsumption> consumptions)
    {
        BigInteger remainingTraceUnits = 0;
        foreach (var range in lot.Ranges)
        {
            var overlaps = consumptions
                .Where(consumption => consumption.ParentLotId == lot.Id)
                .SelectMany(consumption => consumption.Ranges)
                .Where(consumed => consumed.Root == range.Root && consumed.Epoch == range.Epoch &&
                                   consumed.EndExclusive > range.Start && consumed.Start < range.EndExclusive)
                .Select(consumed => (Start: Math.Max(range.Start, consumed.Start), End: Math.Min(range.EndExclusive, consumed.EndExclusive)))
                .OrderBy(interval => interval.Start)
                .ToArray();
            long cursor = range.Start;
            long consumedLength = 0;
            foreach (var overlap in overlaps)
            {
                if (overlap.End <= cursor) continue;
                var start = Math.Max(cursor, overlap.Start);
                consumedLength = checked(consumedLength + overlap.End - start);
                cursor = overlap.End;
            }
            remainingTraceUnits += range.Length - consumedLength;
        }

        if (remainingTraceUnits % lot.TraceUnitsPerCoinUnit != 0)
            throw new ReserveInputUnknownException("Outstanding liability does not resolve to whole coin units.");
        return new TreasuryLotLiability(
            lot.Id,
            lot.WalletId,
            lot.Amount.Currency,
            ToLong(remainingTraceUnits / lot.TraceUnitsPerCoinUnit),
            lot.State);
    }

    private static long Sum(IEnumerable<long> values)
    {
        var total = values.Aggregate(BigInteger.Zero, (current, value) => current + value);
        return ToLong(total);
    }

    private static long ToLong(BigInteger value)
    {
        if (value > long.MaxValue || value < long.MinValue)
            throw new OverflowException("Treasury liability arithmetic exceeded the supported range.");
        return (long)value;
    }
}
