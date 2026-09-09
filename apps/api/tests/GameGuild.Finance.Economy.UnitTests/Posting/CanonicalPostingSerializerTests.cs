using System.Text;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Posting;

namespace GameGuild.Finance.Economy.UnitTests.Posting;

public sealed class CanonicalPostingSerializerTests
{
    [Fact]
    public void SamePostingWithReorderedInputLines_HasSameCanonicalBytesAndHash()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        var reordered = request with { Lines = request.Lines.Reverse().ToArray() };

        CanonicalPostingSerializer.Serialize(request).Should().Equal(CanonicalPostingSerializer.Serialize(reordered));
        CanonicalPostingSerializer.ComputeHash(request).Should().Be(CanonicalPostingSerializer.ComputeHash(reordered));
    }

    [Fact]
    public void MaterialMutation_ChangesHashAndTextIsCultureIndependent()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        var changed = request with { IdempotencyKey = new IdempotencyKey("changed") };

        CanonicalPostingSerializer.ComputeHash(request).Should().NotBe(CanonicalPostingSerializer.ComputeHash(changed));
        Encoding.UTF8.GetString(CanonicalPostingSerializer.Serialize(request)).Should().Contain("2026-07-18T12:00:00.0000000+00:00");
        FluentActions.Invoking(() => CanonicalPostingSerializer.Serialize(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SourceEvidenceAndOptionalFields_AreIncludedDeterministically()
    {
        var confirmed = PostingFixture.Valid(PostingTemplateKind.ConfirmedTopUpMint);
        var reversed = PostingFixture.Valid(PostingTemplateKind.ProviderReversalFull) with
        {
            Source = new SourceStampContract(
                SourceStampId.New(), "reverse-hash", SourceConfirmationState.Reversed,
                DateTimeOffset.Parse("2026-07-18T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-18T12:01:00Z"),
                null),
            Lines =
            [
                PostingFixture.Valid(PostingTemplateKind.ProviderReversalFull).Lines[0] with { LotId = CreditLotId.New() },
                PostingFixture.Valid(PostingTemplateKind.ProviderReversalFull).Lines[1]
            ]
        };

        Encoding.UTF8.GetString(CanonicalPostingSerializer.Serialize(confirmed)).Should().Contain("sha256-source").And.Contain("pi_test");
        Encoding.UTF8.GetString(CanonicalPostingSerializer.Serialize(reversed)).Should().Contain("reverse-hash");
    }

    [Fact]
    public void ObservedSource_SerializesWithoutAConfirmationTimestamp()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend) with
        {
            Source = new SourceStampContract(
                SourceStampId.New(), "observed-hash", SourceConfirmationState.Observed,
                DateTimeOffset.Parse("2026-07-18T12:00:00Z"), null, null)
        };

        Encoding.UTF8.GetString(CanonicalPostingSerializer.Serialize(request))
            .Should().Contain("observed-hash");
    }

    [Fact]
    public void PostingResult_IsAnImmutableTypedReceipt()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        var result = new PostingResult(
            request.Id,
            PostingStatus.Accepted,
            CanonicalPostingSerializer.ComputeHash(request),
            request.RequestedAt,
            request.Lines.Select(line => new PostedLineResult(line.Sequence, Guid.NewGuid())).ToArray());

        result.Lines.Should().HaveCount(2);
        result.Hash.Should().HaveLength(64);
        result.Status.Should().Be(PostingStatus.Accepted);
    }
}
