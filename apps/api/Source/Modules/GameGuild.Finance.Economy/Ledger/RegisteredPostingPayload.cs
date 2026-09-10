using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameGuild.Finance.Economy.Ledger;

internal sealed record RegisteredPostingPayload(
    string Lines,
    string Allocations,
    string RootRanges,
    string ExpectedReversalEpochs);

internal static class RegisteredPostingPayloadFactory
{
    public static RegisteredPostingPayload Create(
        RegisteredPostingRequest request,
        IReadOnlyDictionary<int, Guid> accountIds,
        Func<Guid>? nextId = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accountIds);
        nextId ??= CreateDeterministicIdFactory(request.Posting.Id.Value);

        var lines = request.Posting.Lines
            .OrderBy(line => line.Sequence)
            .Select(line => new RegisteredLine(line, GetAccountId(accountIds, line.Sequence), nextId()))
            .ToArray();
        var linesBySequence = lines.ToDictionary(line => line.Sequence);
        var allocations = request.Allocations
            .OrderBy(allocation => allocation.LineSequence)
            .Select(allocation => new RegisteredAllocation(
                nextId(),
                linesBySequence[allocation.LineSequence].Id,
                allocation.ParentLotId.Value,
                allocation.AmountUnits,
                allocation.RootRanges))
            .ToArray();
        var rootRanges = allocations
            .SelectMany(allocation => allocation.RootRanges.Select(range => new RegisteredRootRange(
                nextId(),
                range.Root.Value,
                allocation.Id,
                range.Start,
                range.EndExclusive,
                range.Epoch)))
            .OrderBy(range => range.RootSourceStampId)
            .ThenBy(range => range.StartInclusive)
            .ToArray();
        var expectedEpochs = rootRanges
            .GroupBy(range => range.RootSourceStampId)
            .Select(group => new { RootSourceStampId = group.Key, Epochs = group.Select(range => range.ReversalEpoch).Distinct().ToArray() })
            .Select(group => group.Epochs.Length == 1
                ? new RegisteredReversalEpoch(group.RootSourceStampId, group.Epochs[0])
                : throw new RegisteredPostingRejectedException(
                    "A root source stamp cannot be submitted with conflicting reversal epochs."))
            .OrderBy(item => item.RootSourceStampId)
            .ToArray();

        return new RegisteredPostingPayload(
            JsonSerializer.Serialize(lines.Select(line => new
            {
                id = line.Id,
                account_id = line.AccountId,
                account_code = (int)line.AccountCode,
                wallet_id = line.WalletId,
                credit_lot_id = line.CreditLotId,
                side = (int)line.Side,
                currency = (int)line.Currency,
                amount_units = line.AmountUnits,
                provenance = line.Provenance is null ? null : (int?)line.Provenance.Value
            })),
            JsonSerializer.Serialize(allocations.Select(allocation => new
            {
                id = allocation.Id,
                journal_line_id = allocation.JournalLineId,
                parent_lot_id = allocation.ParentLotId,
                amount_units = allocation.AmountUnits
            })),
            JsonSerializer.Serialize(rootRanges.Select(range => new
            {
                id = range.Id,
                root_source_stamp_id = range.RootSourceStampId,
                credit_lot_id = (Guid?)null,
                entry_allocation_id = range.EntryAllocationId,
                start_inclusive = range.StartInclusive,
                end_exclusive = range.EndExclusive,
                reversal_epoch = range.ReversalEpoch
            })),
            JsonSerializer.Serialize(expectedEpochs.Select(epoch => new
            {
                root_source_stamp_id = epoch.RootSourceStampId,
                expected_epoch = epoch.ExpectedEpoch
            })));
    }

    private static Func<Guid> CreateDeterministicIdFactory(Guid postingId)
    {
        var index = 0;
        return () =>
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{postingId:N}:registered-posting:{index++}"));
            return new Guid(bytes.AsSpan(0, 16));
        };
    }
    private static Guid GetAccountId(IReadOnlyDictionary<int, Guid> accountIds, int sequence) =>
        accountIds.TryGetValue(sequence, out var accountId) && accountId != Guid.Empty
            ? accountId
            : throw new RegisteredPostingRejectedException(
                "A registered posting line has no matching economy account.");

    private sealed record RegisteredLine(Contracts.PostingLine Line, Guid AccountId, Guid Id)
    {
        public int Sequence => Line.Sequence;
        public Contracts.EconomyAccountCode AccountCode => Line.Account;
        public Guid? WalletId => Line.WalletId?.Value;
        public Guid? CreditLotId => Line.LotId?.Value;
        public Contracts.EntrySide Side => Line.Side;
        public Contracts.CurrencyCode Currency => Line.Amount.Currency;
        public long AmountUnits => Line.Amount.Units;
        public Contracts.ProvenanceKind? Provenance => Line.Provenance;
    }

    private sealed record RegisteredAllocation(
        Guid Id,
        Guid JournalLineId,
        Guid ParentLotId,
        long AmountUnits,
        IReadOnlyList<RootTraceRange> RootRanges);

    private sealed record RegisteredRootRange(
        Guid Id,
        Guid RootSourceStampId,
        Guid EntryAllocationId,
        long StartInclusive,
        long EndExclusive,
        long ReversalEpoch);

    private sealed record RegisteredReversalEpoch(Guid RootSourceStampId, long ExpectedEpoch);
}
