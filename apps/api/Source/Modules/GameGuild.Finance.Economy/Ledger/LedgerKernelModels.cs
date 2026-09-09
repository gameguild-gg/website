using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record TransferFragmentsCommand(
    PostingId PostingId,
    IdempotencyKey IdempotencyKey,
    WalletId SourceWalletId,
    WalletId DestinationWalletId,
    CoinAmount Amount,
    ProvenanceKind Provenance,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    DateTimeOffset RequestedAt);

public sealed class FragmentConsumption
{
    public FragmentConsumption(
        PostingId postingId,
        CreditLotId parentLotId,
        CoinAmount amount,
        IReadOnlyList<RootTraceRange> ranges)
    {
        PostingId = postingId;
        ParentLotId = parentLotId;
        Amount = amount;
        Ranges = Array.AsReadOnly(ranges.ToArray());
    }

    public PostingId PostingId { get; }
    public CreditLotId ParentLotId { get; }
    public CoinAmount Amount { get; }
    public IReadOnlyList<RootTraceRange> Ranges { get; }
}

public sealed class WalletProjectionUpdate
{
    public WalletProjectionUpdate(
        PostingId postingId,
        WalletId walletId,
        CurrencyCode currency,
        long deltaUnits,
        long journalSequence)
    {
        PostingId = postingId;
        WalletId = walletId;
        Currency = currency;
        DeltaUnits = deltaUnits;
        JournalSequence = journalSequence;
    }

    public PostingId PostingId { get; }
    public WalletId WalletId { get; }
    public CurrencyCode Currency { get; }
    public long DeltaUnits { get; }
    public long JournalSequence { get; }
}

public sealed class IdempotencyRecord
{
    public IdempotencyRecord(IdempotencyKey key, string requestHash, PostingResult result)
    {
        Key = key;
        RequestHash = requestHash;
        Result = result;
    }

    public IdempotencyKey Key { get; }
    public string RequestHash { get; }
    public PostingResult Result { get; }
}

public sealed class ImmutableOutboxMessage
{
    public ImmutableOutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Outbox message ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        Id = id;
        Type = type.Trim();
        Payload = payload;
        PayloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        OccurredAt = occurredAt;
    }

    public Guid Id { get; }
    public string Type { get; }
    public string Payload { get; }
    public string PayloadHash { get; }
    public DateTimeOffset OccurredAt { get; }
}

public interface IEconomyOutboxFactory
{
    ImmutableOutboxMessage PostingAccepted(PostingResult result);
}

public sealed class EconomyOutboxFactory : IEconomyOutboxFactory
{
    public ImmutableOutboxMessage PostingAccepted(PostingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var payload = JsonSerializer.Serialize(new PostingAcceptedPayload(
            result.PostingId.Value,
            result.Hash,
            result.RecordedAt,
            result.Lines.Select(line => line.JournalLineId).ToArray()));
        return new ImmutableOutboxMessage(Guid.NewGuid(), "economy.posting.accepted.v1", payload, result.RecordedAt);
    }

    private sealed record PostingAcceptedPayload(
        Guid PostingId,
        string Hash,
        DateTimeOffset RecordedAt,
        IReadOnlyList<Guid> JournalLineIds);
}

public sealed class IdempotencyConflictException : InvalidOperationException
{
    public IdempotencyConflictException(IdempotencyKey key)
        : base($"Idempotency key '{key.Value}' was already used for a different command.")
    {
    }
}

public readonly record struct LedgerKernelCounts(
    int Sources,
    int JournalEntries,
    int CreditLots,
    int FragmentConsumptions,
    int Lineages,
    int ProjectionUpdates,
    int IdempotencyRecords,
    int OutboxMessages);
