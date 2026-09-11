using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Authoring;

public sealed class ProgramContentDraftTests
{
    [Fact]
    public void Update_WithCurrentRevision_IncrementsRevisionAndChangesEtag()
    {
        var author = Guid.NewGuid();
        var draft = ProgramContentDraft.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            author,
            7,
            "{\"body\":\"before\"}",
            DateTimeOffset.Parse("2026-09-10T10:00:00Z"));
        var originalEtag = draft.ETag;

        draft.Update(1, "{\"body\":\"after\"}", author, DateTimeOffset.Parse("2026-09-10T10:01:00Z"));

        draft.Revision.Should().Be(2);
        draft.PayloadJson.Should().Contain("after");
        draft.ETag.Should().NotBe(originalEtag);
    }

    [Fact]
    public void Update_WithStaleRevision_RejectsWithoutOverwritingCurrentWork()
    {
        var author = Guid.NewGuid();
        var draft = ProgramContentDraft.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            author,
            3,
            "{\"body\":\"current\"}",
            DateTimeOffset.UtcNow);
        draft.Update(1, "{\"body\":\"newer\"}", author, DateTimeOffset.UtcNow);

        var act = () => draft.Update(1, "{\"body\":\"stale\"}", Guid.NewGuid(), DateTimeOffset.UtcNow);

        act.Should().Throw<AuthoringRevisionConflictException>()
            .Which.CurrentRevision.Should().Be(2);
        draft.PayloadJson.Should().Contain("newer");
    }

    [Fact]
    public void Proposal_CanOnlyBeAppliedToItsBaseDraftRevision()
    {
        var proposal = AiAuthoringProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4,
            AiProposalKind.ReplaceDocument,
            "before",
            "after",
            DateTimeOffset.UtcNow);

        proposal.EnsureApplicableTo(4);
        var act = () => proposal.EnsureApplicableTo(5);

        act.Should().Throw<AuthoringRevisionConflictException>()
            .Which.CurrentRevision.Should().Be(5);
    }

    [Fact]
    public void Proposal_CannotBeAppliedOrDiscardedAfterItWasResolved()
    {
        var proposal = AiAuthoringProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            AiProposalKind.ReplaceDocument,
            "before",
            "after",
            DateTimeOffset.UtcNow);
        proposal.MarkApplied(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var apply = () => proposal.EnsureApplicableTo(1);
        var discard = () => proposal.Discard(Guid.NewGuid(), DateTimeOffset.UtcNow);

        apply.Should().Throw<AiProposalStateConflictException>()
            .Which.CurrentStatus.Should().Be(AiProposalStatus.Applied);
        discard.Should().Throw<AiProposalStateConflictException>()
            .Which.CurrentStatus.Should().Be(AiProposalStatus.Applied);
    }
}
