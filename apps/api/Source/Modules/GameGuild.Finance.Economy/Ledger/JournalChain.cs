using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Posting;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class JournalChain
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly object _gate = new();
    private readonly List<JournalEntry> _entries = [];

    public JournalChain()
    {
    }

    internal JournalChain(IEnumerable<JournalEntry> entries) => _entries.AddRange(entries);

    public IReadOnlyList<JournalEntry> Entries
    {
        get
        {
            lock (_gate) return _entries.ToArray();
        }
    }

    public JournalEntry? Head
    {
        get
        {
            lock (_gate) return _entries.Count == 0 ? null : _entries[^1];
        }
    }

    public JournalAppendResult Append(PostingRequest request, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        PostingMatrix.EnsureValid(request);

        lock (_gate)
        {
            if (_entries.Any(entry => entry.PostingId == request.Id))
                throw new DuplicatePostingException(request.Id);
            var sequence = checked((long)_entries.Count + 1);
            var previousHash = _entries.Count == 0 ? GenesisHash : _entries[^1].Hash;
            var requestHash = CanonicalPostingSerializer.ComputeHash(request);
            var hash = ComputeVerificationHash(sequence, previousHash, requestHash, recordedAt);
            var lines = request.Lines
                .OrderBy(line => line.Sequence)
                .Select(line => new JournalEntryLine(
                    line.Sequence,
                    DeterministicLineId(hash, line.Sequence),
                    line.Side,
                    line.Account,
                    line.Amount,
                    line.WalletId,
                    line.LotId,
                    line.Provenance))
                .ToArray();
            var entry = new JournalEntry(
                sequence,
                request.Id,
                previousHash,
                requestHash,
                hash,
                recordedAt,
                lines);
            _entries.Add(entry);

            var result = new PostingResult(
                request.Id,
                PostingStatus.Accepted,
                hash,
                recordedAt,
                lines.Select(line => new PostedLineResult(line.Sequence, line.Id)).ToArray());
            return new JournalAppendResult(entry, result);
        }
    }

    public bool Verify()
    {
        lock (_gate)
        {
            return VerifyEntries(_entries.Select(entry => new JournalVerificationEntry(
                entry.Sequence,
                entry.PreviousHash,
                entry.RequestHash,
                entry.Hash,
                entry.RecordedAt)));
        }
    }

    public static bool VerifyEntries(IEnumerable<JournalVerificationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var previousHash = GenesisHash;
        var expectedSequence = 1L;
        foreach (var entry in entries)
        {
            if (entry.Sequence != expectedSequence || entry.PreviousHash != previousHash)
                return false;
            if (entry.Hash != ComputeVerificationHash(entry.Sequence, entry.PreviousHash, entry.RequestHash, entry.RecordedAt))
                return false;
            previousHash = entry.Hash;
            expectedSequence = checked(expectedSequence + 1);
        }

        return true;
    }

    public static string ComputeVerificationHash(
        long sequence,
        string previousHash,
        string requestHash,
        DateTimeOffset recordedAt)
    {
        var canonical = string.Join(
            '|',
            sequence.ToString(CultureInfo.InvariantCulture),
            previousHash,
            requestHash,
            recordedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static Guid DeterministicLineId(string entryHash, int sequence)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{entryHash}:{sequence.ToString(CultureInfo.InvariantCulture)}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

public sealed class JournalEntry
{
    internal JournalEntry(
        long sequence,
        PostingId postingId,
        string previousHash,
        string requestHash,
        string hash,
        DateTimeOffset recordedAt,
        IReadOnlyList<JournalEntryLine> lines)
    {
        Sequence = sequence;
        PostingId = postingId;
        PreviousHash = previousHash;
        RequestHash = requestHash;
        Hash = hash;
        RecordedAt = recordedAt;
        Lines = Array.AsReadOnly(lines.ToArray());
    }

    public long Sequence { get; }
    public PostingId PostingId { get; }
    public string PreviousHash { get; }
    public string RequestHash { get; }
    public string Hash { get; }
    public DateTimeOffset RecordedAt { get; }
    public IReadOnlyList<JournalEntryLine> Lines { get; }
}

public sealed class JournalEntryLine
{
    public JournalEntryLine(
        int sequence,
        Guid id,
        EntrySide side,
        EconomyAccountCode account,
        CoinAmount amount,
        WalletId? walletId,
        CreditLotId? lotId,
        ProvenanceKind? provenance)
    {
        Sequence = sequence;
        Id = id;
        Side = side;
        Account = account;
        Amount = amount;
        WalletId = walletId;
        LotId = lotId;
        Provenance = provenance;
    }

    public int Sequence { get; }
    public Guid Id { get; }
    public EntrySide Side { get; }
    public EconomyAccountCode Account { get; }
    public CoinAmount Amount { get; }
    public WalletId? WalletId { get; }
    public CreditLotId? LotId { get; }
    public ProvenanceKind? Provenance { get; }
}

public sealed record JournalAppendResult(JournalEntry Entry, PostingResult Result);

public sealed record JournalVerificationEntry(
    long Sequence,
    string PreviousHash,
    string RequestHash,
    string Hash,
    DateTimeOffset RecordedAt);

public sealed class DuplicatePostingException : InvalidOperationException
{
    public DuplicatePostingException(PostingId postingId)
        : base($"Posting {postingId.Value:N} already exists in the journal.")
    {
    }
}
