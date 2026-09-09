using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Policy;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed class MarketplaceSettlementCoordinator
{
    private readonly object _gate = new();
    private readonly MarketplaceRiskGate _risk;
    private readonly RootReversalFenceRegistry _fences;
    private readonly IMarketplaceEntitlementGateway _entitlements;
    private readonly Dictionary<MarketplaceSettlementId, SettlementState> _settlements = [];
    private readonly Dictionary<string, MarketplaceSettlementResult> _settlementIdempotency =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, RefundRecord> _refundIdempotency =
        new(StringComparer.Ordinal);

    public MarketplaceSettlementCoordinator(
        MarketplaceRiskGate risk,
        RootReversalFenceRegistry fences,
        IMarketplaceEntitlementGateway entitlements)
    {
        _risk = risk ?? throw new ArgumentNullException(nameof(risk));
        _fences = fences ?? throw new ArgumentNullException(nameof(fences));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
    }

    public int Count
    {
        get
        {
            lock (_gate) return _settlements.Count;
        }
    }

    public MarketplaceSettlementResult Settle(SettleMarketplaceOrderCommand command)
    {
        ValidateSettlement(command);

        lock (_gate)
        {
            if (_settlementIdempotency.TryGetValue(command.IdempotencyKey.Value, out var duplicate))
            {
                if (duplicate.Id == command.Id) return duplicate;
                throw new MarketplaceIdempotencyConflictException(
                    "A settlement idempotency key cannot identify another settlement.");
            }

            if (_settlements.ContainsKey(command.Id))
                throw new MarketplaceIdempotencyConflictException("The settlement ID already exists.");
            if (command.AvailableBuyerLots.GroupBy(lot => lot.Id).Any(group => group.Count() > 1))
                throw new ArgumentException("Available buyer lots must have unique identities.", nameof(command));

            var buyerLots = command.AvailableBuyerLots
                .Where(lot =>
                    lot.WalletId == command.BuyerWalletId &&
                    lot.ConfirmedAt <= command.SettledAt)
                .ToArray();
            var parentLots = buyerLots.ToDictionary(lot => lot.Id);
            var funding = new List<MarketplaceFundingFragment>();

            foreach (var leg in command.Quote.Legs)
            {
                var selection = FifoFragmentSelector.Select(buyerLots, leg.Amount);
                funding.AddRange(selection.Selections.Select(item =>
                    new MarketplaceFundingFragment(parentLots[item.ParentLotId], item)));
            }

            var roots = funding.SelectMany(fragment => fragment.SelectedRanges)
                .Select(range => range.Root).Distinct().ToArray();
            var fence = _fences.Capture(roots);
            return _fences.WithAllocationFence(fence, roots, () =>
            {
                _risk.Authorize(command, funding);
                var nextSequence = command.FirstJournalSequence;
                var credits = CreateCredits(command, funding, ref nextSequence);
                var entitlement = _entitlements.Grant(new MarketplaceEntitlementGrantRequest(
                    command.Id,
                    command.OrderId,
                    command.ProductId,
                    command.BuyerId,
                    command.SettledAt));
                if (entitlement.Id == Guid.Empty ||
                    entitlement.SettlementId != command.Id ||
                    entitlement.OrderId != command.OrderId ||
                    entitlement.ProductId != command.ProductId ||
                    entitlement.BuyerId != command.BuyerId)
                    throw new MarketplaceEntitlementException(
                        "Entitlement receipt is not bound to the marketplace settlement.");

                var result = new MarketplaceSettlementResult(
                    command.Id,
                    command.OrderId,
                    command.ProductId,
                    command.BuyerId,
                    command.SellerId,
                    command.Quote,
                    funding,
                    credits,
                    entitlement,
                    command.SettledAt);
                _settlements.Add(command.Id, new SettlementState(result, funding));
                _settlementIdempotency.Add(command.IdempotencyKey.Value, result);
                return result;
            });
        }
    }

    public MarketplaceRefundResult Refund(RefundMarketplaceOrderCommand command)
    {
        ValidateRefund(command);

        lock (_gate)
        {
            if (_refundIdempotency.TryGetValue(command.IdempotencyKey.Value, out var duplicate))
            {
                if (duplicate.SettlementId == command.SettlementId) return duplicate.Result;
                throw new MarketplaceIdempotencyConflictException(
                    "A refund idempotency key cannot identify another settlement.");
            }

            if (!_settlements.TryGetValue(command.SettlementId, out var state))
                throw new KeyNotFoundException("Marketplace settlement was not found.");
            if (state.Result.Status == MarketplaceSettlementStatus.Refunded)
                throw new MarketplaceAlreadyRefundedException(
                    "The marketplace settlement is already fully refunded.");
            if (command.BuyerId != state.Result.BuyerId ||
                command.BuyerWalletId != state.RemainingFunding[0].ParentLot.WalletId)
                throw new MarketplaceRefundException(
                    "Only the original buyer wallet can receive restored provenance.");

            var remaining = state.RemainingFunding.ToDictionary(
                fragment => fragment.ParentLot.Id,
                fragment => fragment);
            var selected = new List<MarketplaceFundingFragment>();

            foreach (var leg in command.RefundLegs)
            {
                if (state.Result.Quote.Legs.All(quoted => quoted.Currency != leg.Currency))
                    throw new MarketplaceRefundException(
                        "Refund currency was not part of the original settlement.");

                var eligible = remaining.Values
                    .Where(fragment => fragment.Amount.Currency == leg.Currency)
                    .Select(CreateVirtualLot)
                    .ToArray();
                var selection = FifoFragmentSelector.Select(eligible, leg);
                foreach (var item in selection.Selections)
                {
                    var current = remaining[item.ParentLotId];
                    selected.Add(new MarketplaceFundingFragment(current.ParentLot, item));
                    if (item.RemainingRanges.Count == 0)
                    {
                        remaining.Remove(item.ParentLotId);
                    }
                    else
                    {
                        var remainingUnits = current.Amount.Units - item.Amount.Units;
                        remaining[item.ParentLotId] = new MarketplaceFundingFragment(
                            current.ParentLot,
                            new FragmentSelection(
                                item.ParentLotId,
                                new CoinAmount(item.Amount.Currency, remainingUnits),
                                item.RemainingRanges,
                                [],
                                item.TraceUnitsPerCoinUnit));
                    }
                }
            }

            var isFull = remaining.Count == 0;
            var roots = selected.SelectMany(fragment => fragment.SelectedRanges)
                .Select(range => range.Root).Distinct().ToArray();
            var fence = _fences.Capture(roots);
            var nextSequence = command.FirstJournalSequence;
            var restored = selected.Select(fragment => new CreditLot(
                CreditLotId.New(),
                command.BuyerWalletId,
                fragment.Amount,
                fragment.ParentLot.Provenance,
                fragment.ParentLot.ConfirmedAt,
                fragment.ParentLot.OriginalMaturesAt,
                nextSequence++,
                CreditLotState.Active,
                fragment.SelectedRanges.ToArray(),
                fragment.Selection.TraceUnitsPerCoinUnit)).ToArray();

            return _fences.WithAllocationFence(fence, roots, () =>
            {
                if (isFull)
                    _entitlements.Revoke(state.Result.Entitlement, command.RefundedAt);

                state.ReplaceRemaining(remaining.Values);
                state.Result.ApplyRefund(command.RefundLegs, isFull);
                var result = new MarketplaceRefundResult(
                    command.SettlementId,
                    command.RefundLegs.ToArray(),
                    restored,
                    isFull,
                    isFull);
                _refundIdempotency.Add(
                    command.IdempotencyKey.Value,
                    new RefundRecord(command.SettlementId, result));
                return result;
            });
        }
    }

    public MarketplaceSettlementResult Get(MarketplaceSettlementId id)
    {
        lock (_gate)
        {
            return _settlements.TryGetValue(id, out var state)
                ? state.Result
                : throw new KeyNotFoundException("Marketplace settlement was not found.");
        }
    }

    private static void ValidateSettlement(SettleMarketplaceOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Quote);
        ArgumentNullException.ThrowIfNull(command.AvailableBuyerLots);
        ArgumentNullException.ThrowIfNull(command.Risk);
        if (command.OrderId == Guid.Empty ||
            command.ProductId == Guid.Empty ||
            command.BuyerId == Guid.Empty ||
            command.SellerId == Guid.Empty)
            throw new ArgumentException(
                "Order, product, buyer, and seller identities are required.", nameof(command));
        if (command.BuyerId == command.SellerId)
            throw new ArgumentException("Buyer and seller must be distinct.", nameof(command));
        if (command.Quote.ProductId != command.ProductId ||
            command.Quote.SellerId != command.SellerId)
            throw new ArgumentException(
                "The quote must bind the order product and seller.", nameof(command));
        if (command.BuyerWalletId == command.SellerWalletId ||
            command.BuyerWalletId == command.PlatformFeeWalletId ||
            command.SellerWalletId == command.PlatformFeeWalletId)
            throw new ArgumentException("Marketplace wallets must be distinct.", nameof(command));
        if (command.RefundHoldUntil <= command.SettledAt)
            throw new ArgumentException(
                "Refund hold expiry must follow settlement.", nameof(command));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.FirstJournalSequence);
    }

    private static void ValidateRefund(RefundMarketplaceOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.RefundLegs);
        if (command.BuyerId == Guid.Empty)
            throw new ArgumentException("Buyer ID cannot be empty.", nameof(command));
        if (command.RefundLegs.Count == 0 ||
            command.RefundLegs.Any(leg => leg.Units == 0) ||
            command.RefundLegs.Select(leg => leg.Currency).Distinct().Count() !=
            command.RefundLegs.Count)
            throw new MarketplaceRefundException(
                "Refunds require unique positive currency legs.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.FirstJournalSequence);
    }

    private static IReadOnlyList<MarketplaceSettlementCredit> CreateCredits(
        SettleMarketplaceOrderCommand command,
        IReadOnlyList<MarketplaceFundingFragment> funding,
        ref long nextSequence)
    {
        var credits = new List<MarketplaceSettlementCredit>();
        foreach (var leg in command.Quote.Legs)
        {
            var sources = funding.Where(fragment => fragment.Amount.Currency == leg.Currency)
                .Select(fragment => fragment.Selection).ToArray();
            var parentLots = funding.Where(fragment => fragment.Amount.Currency == leg.Currency)
                .ToDictionary(fragment => fragment.ParentLot.Id, fragment => fragment.ParentLot);
            var outputs = leg.PlatformFeeUnits == 0
                ? new[] { new CoinAmount(leg.Currency, leg.SellerUnits) }
                : new[]
                {
                    new CoinAmount(leg.Currency, leg.SellerUnits),
                    new CoinAmount(leg.Currency, leg.PlatformFeeUnits)
                };
            var partitions = LineagePartitioner.Partition(sources, outputs);
            credits.AddRange(CreatePartitionCredits(
                command,
                MarketplaceCreditPurpose.SellerProceeds,
                command.SellerWalletId,
                partitions[0],
                parentLots,
                ref nextSequence));
            if (leg.PlatformFeeUnits > 0)
                credits.AddRange(CreatePartitionCredits(
                    command,
                    MarketplaceCreditPurpose.PlatformFee,
                    command.PlatformFeeWalletId,
                    partitions[1],
                    parentLots,
                    ref nextSequence));
        }

        return credits;
    }

    private static IReadOnlyList<MarketplaceSettlementCredit> CreatePartitionCredits(
        SettleMarketplaceOrderCommand command,
        MarketplaceCreditPurpose purpose,
        WalletId walletId,
        LineagePartition partition,
        IReadOnlyDictionary<CreditLotId, CreditLot> parents,
        ref long nextSequence)
    {
        var lineage = partition.Selections.Select(selection =>
            new MarketplaceParentLineage(
                parents[selection.ParentLotId],
                selection.Amount,
                selection.SelectedRanges.ToArray())).ToArray();

        if (partition.Amount.Currency == CurrencyCode.HardCoin)
        {
            var sourceId = SourceStampId.New();
            var source = new SourceStampContract(
                sourceId,
                EvidenceHash(command, purpose, partition.Amount),
                SourceConfirmationState.Confirmed,
                command.SettledAt,
                command.SettledAt,
                $"{MarketplaceRiskGate.ProviderReference(command.Id)}:{purpose}");
            var scale = CurrencyTraceScale.For(partition.Amount.Currency);
            var lot = new CreditLot(
                CreditLotId.New(),
                walletId,
                partition.Amount,
                ProvenanceKind.EarnedHard,
                command.SettledAt,
                CreditLotMaturity.Assign(
                    CurrencyCode.HardCoin,
                    ProvenanceKind.EarnedHard,
                    command.SettledAt),
                nextSequence++,
                CreditLotState.Held,
                [new RootTraceRange(
                    sourceId,
                    0,
                    checked(partition.Amount.Units * scale),
                    0)],
                scale);
            return [CreateCredit(command, purpose, source, lot, lineage)];
        }

        var credits = new List<MarketplaceSettlementCredit>();
        foreach (var selection in partition.Selections)
        {
            var parent = parents[selection.ParentLotId];
            var lot = new CreditLot(
                CreditLotId.New(),
                walletId,
                selection.Amount,
                parent.Provenance,
                parent.ConfirmedAt,
                parent.OriginalMaturesAt,
                nextSequence++,
                CreditLotState.Held,
                selection.SelectedRanges.ToArray(),
                selection.TraceUnitsPerCoinUnit);
            credits.Add(CreateCredit(
                command,
                purpose,
                null,
                lot,
                [
                    new MarketplaceParentLineage(
                        parent,
                        selection.Amount,
                        selection.SelectedRanges.ToArray())
                ]));
        }

        return credits;
    }

    private static MarketplaceSettlementCredit CreateCredit(
        SettleMarketplaceOrderCommand command,
        MarketplaceCreditPurpose purpose,
        SourceStampContract? source,
        CreditLot lot,
        IReadOnlyList<MarketplaceParentLineage> lineage)
    {
        var hold = new HoldContract(
            HoldId.New(),
            lot.WalletId,
            lot.Amount,
            HoldReason.RefundWindow,
            HoldStatus.Active,
            command.SettledAt,
            null);
        return new MarketplaceSettlementCredit(
            purpose, source, lot, lineage, hold, command.RefundHoldUntil);
    }

    private static CreditLot CreateVirtualLot(MarketplaceFundingFragment fragment) =>
        new(
            fragment.ParentLot.Id,
            fragment.ParentLot.WalletId,
            fragment.Amount,
            fragment.ParentLot.Provenance,
            fragment.ParentLot.ConfirmedAt,
            fragment.ParentLot.OriginalMaturesAt,
            fragment.ParentLot.JournalSequence,
            CreditLotState.Active,
            fragment.SelectedRanges.ToArray(),
            fragment.Selection.TraceUnitsPerCoinUnit);

    private static string EvidenceHash(
        SettleMarketplaceOrderCommand command,
        MarketplaceCreditPurpose purpose,
        CoinAmount amount)
    {
        var canonical =
            $"{command.Id.Value:N}|{command.OrderId:N}|{command.ProductId:N}|{purpose}|{amount.Currency}|{amount.Units}|{command.SettledAt:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed class SettlementState
    {
        private List<MarketplaceFundingFragment> _remainingFunding;

        internal SettlementState(
            MarketplaceSettlementResult result,
            IEnumerable<MarketplaceFundingFragment> remainingFunding)
        {
            Result = result;
            _remainingFunding = [.. remainingFunding];
        }

        internal MarketplaceSettlementResult Result { get; }
        internal IReadOnlyList<MarketplaceFundingFragment> RemainingFunding => _remainingFunding;

        internal void ReplaceRemaining(IEnumerable<MarketplaceFundingFragment> remaining) =>
            _remainingFunding = [.. remaining];
    }

    private sealed record RefundRecord(
        MarketplaceSettlementId SettlementId,
        MarketplaceRefundResult Result);
}
