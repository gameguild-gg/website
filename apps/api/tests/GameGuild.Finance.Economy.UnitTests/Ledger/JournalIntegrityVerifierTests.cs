using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class JournalIntegrityVerifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<JournalIntegrityFailureCode> FailureCodes => new()
    {
        JournalIntegrityFailureCode.SequenceGap,
        JournalIntegrityFailureCode.PreviousHashMismatch,
        JournalIntegrityFailureCode.CanonicalPayloadMissing,
        JournalIntegrityFailureCode.EntryHashMismatch,
        JournalIntegrityFailureCode.PostingContractInvalid,
        JournalIntegrityFailureCode.InvalidLineAmount,
        JournalIntegrityFailureCode.CurrencyParityMismatch,
        JournalIntegrityFailureCode.SourceStampInvalid,
        JournalIntegrityFailureCode.AllocationInvalid,
        JournalIntegrityFailureCode.LineageInvalid,
        JournalIntegrityFailureCode.RootRangeInvalid,
        JournalIntegrityFailureCode.CumulativeReversalInvalid
    };

    [Fact]
    public void VerifiesContiguousCanonicalBalancedEntriesIncrementally()
    {
        var entries = ValidEntries();

        var result = new JournalIntegrityVerifier().Verify(entries, 1, JournalChain.GenesisHash);

        result.IsValid.Should().BeTrue();
        result.FailureCode.Should().Be(JournalIntegrityFailureCode.None);
        result.FromSequence.Should().Be(1);
        result.ToSequence.Should().Be(2);
        result.CurrentHash.Should().Be(entries[1].Hash);
    }

    [Fact]
    public void EmptyIncrementIsValidAtPriorCheckpoint()
    {
        var result = new JournalIntegrityVerifier().Verify([], 11, "prior-hash");

        result.Should().Be(new JournalIntegrityVerificationResult(
            true,
            JournalIntegrityFailureCode.None,
            11,
            10,
            "prior-hash"));
    }

    [Theory]
    [MemberData(nameof(FailureCodes))]
    public void ReportsFirstIntegrityFailure(JournalIntegrityFailureCode failure)
    {
        var entries = ValidEntries().ToArray();
        entries[0] = failure switch
        {
            JournalIntegrityFailureCode.SequenceGap => entries[0] with { Sequence = 2 },
            JournalIntegrityFailureCode.PreviousHashMismatch => entries[0] with { PreviousHash = "wrong" },
            JournalIntegrityFailureCode.CanonicalPayloadMissing => entries[0] with { CanonicalPayloadHash = "", HashAlgorithmVersion = 0 },
            JournalIntegrityFailureCode.EntryHashMismatch => entries[0] with { Hash = "wrong" },
            JournalIntegrityFailureCode.PostingContractInvalid => entries[0] with { PostingContractValid = false },
            JournalIntegrityFailureCode.InvalidLineAmount => entries[0] with
            {
                Lines = [
                    new JournalIntegrityLine(EntrySide.Debit, CurrencyCode.HardCoin, 0),
                    new JournalIntegrityLine(EntrySide.Credit, CurrencyCode.HardCoin, 10)
                ]
            },
            JournalIntegrityFailureCode.CurrencyParityMismatch => entries[0] with
            {
                Lines = [
                    new JournalIntegrityLine(EntrySide.Debit, CurrencyCode.HardCoin, 10),
                    new JournalIntegrityLine(EntrySide.Credit, CurrencyCode.HardCoin, 9)
                ]
            },
            JournalIntegrityFailureCode.SourceStampInvalid => entries[0] with { SourceStampValid = false },
            JournalIntegrityFailureCode.AllocationInvalid => entries[0] with { AllocationsValid = false },
            JournalIntegrityFailureCode.LineageInvalid => entries[0] with { LineageValid = false },
            JournalIntegrityFailureCode.RootRangeInvalid => entries[0] with { RootRangesValid = false },
            JournalIntegrityFailureCode.CumulativeReversalInvalid => entries[0] with { CumulativeReversalsValid = false },
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };

        var result = new JournalIntegrityVerifier().Verify(entries, 1, JournalChain.GenesisHash);

        result.IsValid.Should().BeFalse();
        result.FailureCode.Should().Be(failure);
        result.ToSequence.Should().Be(0);
        result.CurrentHash.Should().Be(JournalChain.GenesisHash);
    }

    [Fact]
    public void VerifiesSqlWriterHashAlgorithm()
    {
        var postingId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
        const string requestHash = "sql-request-hash";
        var hash = JournalIntegrityVerifier.ComputeSqlWriterVerificationHash(
            1, JournalChain.GenesisHash, postingId, requestHash);
        JournalIntegrityVerifier.ComputeSqlWriterVerificationHash(
            1, JournalChain.GenesisHash, Guid.Empty, requestHash).Should().BeEmpty();
        var entry = ValidEntries()[0] with
        {
            PostingGroupId = postingId,
            CanonicalPayloadHash = requestHash,
            HashAlgorithmVersion = 2,
            Hash = hash
        };

        new JournalIntegrityVerifier().Verify([entry], 1, JournalChain.GenesisHash).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RejectsNullEntries()
    {
        FluentActions.Invoking(() => new JournalIntegrityVerifier().Verify(null!, 1, JournalChain.GenesisHash))
            .Should().Throw<ArgumentNullException>();
    }

    private static IReadOnlyList<JournalIntegrityEntry> ValidEntries()
    {
        const string requestHash1 = "request-hash-1";
        const string requestHash2 = "request-hash-2";
        var hash1 = JournalChain.ComputeVerificationHash(1, JournalChain.GenesisHash, requestHash1, Now);
        var hash2 = JournalChain.ComputeVerificationHash(2, hash1, requestHash2, Now.AddSeconds(1));
        return
        [
            new JournalIntegrityEntry(
                1, JournalChain.GenesisHash, requestHash1, 1, hash1, Now, true,
                [
                    new JournalIntegrityLine(EntrySide.Debit, CurrencyCode.HardCoin, 10),
                    new JournalIntegrityLine(EntrySide.Credit, CurrencyCode.HardCoin, 10)
                ]),
            new JournalIntegrityEntry(
                2, hash1, requestHash2, 1, hash2, Now.AddSeconds(1), true,
                [
                    new JournalIntegrityLine(EntrySide.Debit, CurrencyCode.SoftCoin, 20),
                    new JournalIntegrityLine(EntrySide.Credit, CurrencyCode.SoftCoin, 20)
                ])
        ];
    }
}
