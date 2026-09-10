using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public enum JournalIntegrityFailureCode
{
    None = 0,
    SequenceGap = 1,
    PreviousHashMismatch = 2,
    CanonicalPayloadMissing = 3,
    EntryHashMismatch = 4,
    PostingContractInvalid = 5,
    InvalidLineAmount = 6,
    CurrencyParityMismatch = 7,
    SourceStampInvalid = 8,
    AllocationInvalid = 9,
    LineageInvalid = 10,
    RootRangeInvalid = 11,
    CumulativeReversalInvalid = 12
}

public sealed record JournalIntegrityLine(
    EntrySide Side,
    CurrencyCode Currency,
    long AmountUnits);

public sealed record JournalIntegrityEntry(
    long Sequence,
    string PreviousHash,
    string CanonicalPayloadHash,
    int HashAlgorithmVersion,
    string Hash,
    DateTimeOffset RecordedAt,
    bool PostingContractValid,
    IReadOnlyList<JournalIntegrityLine> Lines)
{
    public Guid PostingGroupId { get; init; }
    public bool SourceStampValid { get; init; } = true;
    public bool AllocationsValid { get; init; } = true;
    public bool LineageValid { get; init; } = true;
    public bool RootRangesValid { get; init; } = true;
    public bool CumulativeReversalsValid { get; init; } = true;
}

public sealed record JournalIntegrityVerificationResult(
    bool IsValid,
    JournalIntegrityFailureCode FailureCode,
    long FromSequence,
    long ToSequence,
    string CurrentHash);

public interface IJournalIntegrityVerifier
{
    JournalIntegrityVerificationResult Verify(
        IReadOnlyCollection<JournalIntegrityEntry> entries,
        long expectedSequence,
        string expectedPreviousHash);
}

public sealed class JournalIntegrityVerifier : IJournalIntegrityVerifier
{
    public JournalIntegrityVerificationResult Verify(
        IReadOnlyCollection<JournalIntegrityEntry> entries,
        long expectedSequence,
        string expectedPreviousHash)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var fromSequence = expectedSequence;
        var previousHash = expectedPreviousHash;
        var lastVerifiedSequence = expectedSequence - 1;

        foreach (var entry in entries.OrderBy(item => item.Sequence))
        {
            if (entry.Sequence != expectedSequence)
                return Failed(JournalIntegrityFailureCode.SequenceGap, fromSequence, lastVerifiedSequence, previousHash);
            if (!string.Equals(entry.PreviousHash, previousHash, StringComparison.Ordinal))
                return Failed(JournalIntegrityFailureCode.PreviousHashMismatch, fromSequence, lastVerifiedSequence, previousHash);
            if (entry.HashAlgorithmVersion is not (1 or 2) || string.IsNullOrWhiteSpace(entry.CanonicalPayloadHash))
                return Failed(JournalIntegrityFailureCode.CanonicalPayloadMissing, fromSequence, lastVerifiedSequence, previousHash);

            var expectedHash = entry.HashAlgorithmVersion == 1
                ? JournalChain.ComputeVerificationHash(
                    entry.Sequence,
                    entry.PreviousHash,
                    entry.CanonicalPayloadHash,
                    entry.RecordedAt)
                : ComputeSqlWriterVerificationHash(
                    entry.Sequence,
                    entry.PreviousHash,
                    entry.PostingGroupId,
                    entry.CanonicalPayloadHash);
            if (!string.Equals(entry.Hash, expectedHash, StringComparison.Ordinal))
                return Failed(JournalIntegrityFailureCode.EntryHashMismatch, fromSequence, lastVerifiedSequence, previousHash);
            if (!entry.PostingContractValid)
                return Failed(JournalIntegrityFailureCode.PostingContractInvalid, fromSequence, lastVerifiedSequence, previousHash);
            if (entry.Lines.Any(line => line.AmountUnits <= 0))
                return Failed(JournalIntegrityFailureCode.InvalidLineAmount, fromSequence, lastVerifiedSequence, previousHash);

            var parityValid = entry.Lines
                .GroupBy(line => line.Currency)
                .All(group => group.Sum(line => line.Side == EntrySide.Debit ? line.AmountUnits : -line.AmountUnits) == 0);
            if (!parityValid)
                return Failed(JournalIntegrityFailureCode.CurrencyParityMismatch, fromSequence, lastVerifiedSequence, previousHash);
            if (!entry.SourceStampValid)
                return Failed(JournalIntegrityFailureCode.SourceStampInvalid, fromSequence, lastVerifiedSequence, previousHash);
            if (!entry.AllocationsValid)
                return Failed(JournalIntegrityFailureCode.AllocationInvalid, fromSequence, lastVerifiedSequence, previousHash);
            if (!entry.LineageValid)
                return Failed(JournalIntegrityFailureCode.LineageInvalid, fromSequence, lastVerifiedSequence, previousHash);
            if (!entry.RootRangesValid)
                return Failed(JournalIntegrityFailureCode.RootRangeInvalid, fromSequence, lastVerifiedSequence, previousHash);
            if (!entry.CumulativeReversalsValid)
                return Failed(JournalIntegrityFailureCode.CumulativeReversalInvalid, fromSequence, lastVerifiedSequence, previousHash);

            previousHash = entry.Hash;
            lastVerifiedSequence = entry.Sequence;
            expectedSequence++;
        }

        return new JournalIntegrityVerificationResult(
            true,
            JournalIntegrityFailureCode.None,
            fromSequence,
            lastVerifiedSequence,
            previousHash);
    }

    private static JournalIntegrityVerificationResult Failed(
        JournalIntegrityFailureCode failureCode,
        long fromSequence,
        long lastVerifiedSequence,
        string previousHash) =>
        new(false, failureCode, fromSequence, lastVerifiedSequence, previousHash);

    public static string ComputeSqlWriterVerificationHash(
        long sequence,
        string previousHash,
        Guid postingGroupId,
        string requestHash)
    {
        if (postingGroupId == Guid.Empty) return string.Empty;
        var canonical = string.Join('|', previousHash, postingGroupId.ToString(),
            sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), requestHash);
        return Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
    }
}
