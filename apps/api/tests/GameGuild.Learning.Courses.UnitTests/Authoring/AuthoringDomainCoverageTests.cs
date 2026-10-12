using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Authoring;

public sealed class AuthoringDomainCoverageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AiAuthoringConversation_CreateRejectsEveryMissingIdentity(int emptyIndex)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[emptyIndex] = Guid.Empty;

        var act = () => AiAuthoringConversation.Create(ids[0], ids[1], ids[2], ids[3], Now);

        if (emptyIndex is 0 or 3)
        {
            act.Should().Throw<UnauthorizedAccessException>();
        }
        else
        {
            act.Should().Throw<ArgumentException>()
            .WithMessage("*Program and content IDs*");
        }
    }

    [Fact]
    public void AiAuthoringMessage_CreateRejectsMissingConversationAndUnsupportedRole()
    {
        var missingConversation = () => AiAuthoringMessage.Create(
            Guid.Empty,
            null,
            "user",
            "Question",
            Now);
        var unsupportedRole = () => AiAuthoringMessage.Create(
            Guid.NewGuid(),
            null,
            "system",
            "Instruction",
            Now);

        missingConversation.Should().Throw<ArgumentException>()
            .WithParameterName("conversationId");
        unsupportedRole.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("role");
    }

    [Fact]
    public void AiAuthoringStreamEvent_CreateRejectsMissingRun()
    {
        var act = () => AiAuthoringStreamEvent.Create(
            Guid.Empty,
            1,
            "delta",
            "running",
            null,
            null,
            Now);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("runId");
    }

    [Fact]
    public void PublishedVersionConflict_ExposesExpectedAndCurrentVersions()
    {
        var exception = new AuthoringPublishedVersionConflictException(3, 5);

        exception.ExpectedVersion.Should().Be(3);
        exception.CurrentVersion.Should().Be(5);
        exception.Message.Should().Contain("3").And.Contain("5");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ProgramContentDraft_CreateRejectsEveryMissingIdentity(int emptyIndex)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[emptyIndex] = Guid.Empty;

        var act = () => ProgramContentDraft.Create(ids[0], ids[1], ids[2], ids[3], 1, "{}", Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Draft, program, content, and author IDs*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    public void ProgramContentDraft_CreateRejectsInvalidPayload(string payload)
    {
        var act = () => CreateDraft(payload);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProgramContentDraft_UpdateRejectsMissingAuthor()
    {
        var draft = CreateDraft();

        var act = () => draft.Update(1, "{}", Guid.Empty, Now.AddMinutes(1));

        act.Should().Throw<ArgumentException>()
            .WithParameterName("authorId");
    }

    [Fact]
    public void ProgramContentDraft_UpdateRejectsStaleRevision()
    {
        var draft = CreateDraft();

        var act = () => draft.Update(9, "{}", Guid.NewGuid(), Now.AddMinutes(1));

        var exception = act.Should().Throw<AuthoringRevisionConflictException>().Which;
        exception.ExpectedRevision.Should().Be(9);
        exception.CurrentRevision.Should().Be(1);
    }

    [Fact]
    public void ProgramContentDraft_RebaseAdvancesPublishedBaseAndDraftRevision()
    {
        var draft = CreateDraft();
        var editor = Guid.NewGuid();

        draft.Rebase(4, editor, "{\"title\":\"rebased\"}", Now.AddMinutes(1));

        draft.BasePublishedVersion.Should().Be(4);
        draft.Revision.Should().Be(2);
        draft.LastEditedBy.Should().Be(editor);
        draft.PayloadJson.Should().Contain("rebased");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    public void ProgramContentDraft_RebaseRejectsInvalidPayload(string payload)
    {
        var draft = CreateDraft();

        var act = () => draft.Rebase(2, Guid.NewGuid(), payload, Now.AddMinutes(1));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AiAuthoringProposal_CreateRejectsMissingRunOrContent(bool emptyRun, bool emptyContent)
    {
        var act = () => AiAuthoringProposal.Create(
            emptyRun ? Guid.Empty : Guid.NewGuid(),
            emptyContent ? Guid.Empty : Guid.NewGuid(),
            1,
            AiProposalKind.ReplaceDocument,
            "original",
            "proposed",
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Run and content IDs*");
    }

    [Fact]
    public void AiAuthoringProposal_CreateRejectsBlankProposedContent()
    {
        var act = () => AiAuthoringProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            AiProposalKind.ReplaceDocument,
            "original",
            "   ",
            Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AiAuthoringProposal_NormalizesNullOriginalAndRejectsStaleDraft()
    {
        var proposal = AiAuthoringProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            AiProposalKind.ReplaceDocument,
            null!,
            "proposed",
            Now);

        proposal.OriginalContent.Should().BeEmpty();
        var act = () => proposal.EnsureApplicableTo(2);
        var exception = act.Should().Throw<AuthoringRevisionConflictException>().Which;
        exception.ExpectedRevision.Should().Be(1);
        exception.CurrentRevision.Should().Be(2);
    }

    [Fact]
    public void AiAuthoringProposal_RejectsMissingResolverAndSecondResolution()
    {
        var proposal = CreateProposal();

        var missingActor = () => proposal.MarkApplied(Guid.Empty, Now.AddMinutes(1));
        missingActor.Should().Throw<ArgumentException>()
            .WithParameterName("actorId");

        proposal.Discard(Guid.NewGuid(), Now.AddMinutes(2));
        proposal.Status.Should().Be(AiProposalStatus.Discarded);
        var secondResolution = () => proposal.MarkApplied(Guid.NewGuid(), Now.AddMinutes(3));
        secondResolution.Should().Throw<AiProposalStateConflictException>()
            .Which.CurrentStatus.Should().Be(AiProposalStatus.Discarded);
        var applicability = () => proposal.EnsureApplicableTo(1);
        applicability.Should().Throw<AiProposalStateConflictException>();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AiAuthoringRun_CreateRejectsMissingTenantOrActor(bool emptyTenant, bool emptyActor)
    {
        var act = () => AiAuthoringRun.Create(
            emptyTenant ? Guid.Empty : Guid.NewGuid(),
            emptyActor ? Guid.Empty : Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            AiProposalKind.ReplaceDocument,
            "Improve the lesson",
            null,
            "run-key",
            Now);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AiAuthoringRun_CreateRejectsEveryMissingContentIdentity(int emptyIndex)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[emptyIndex] = Guid.Empty;

        var act = () => AiAuthoringRun.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ids[0],
            ids[1],
            ids[2],
            ids[3],
            1,
            AiProposalKind.ReplaceDocument,
            "Improve the lesson",
            null,
            "run-key",
            Now);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Program, content, draft, and conversation IDs*");
    }

    [Fact]
    public void AiAuthoringRun_RequestCancellationAndCancelAreSafeToRepeat()
    {
        var run = CreateRun();
        run.Reserve("provider", "model", 100, 200, 500, Now.AddMinutes(1));
        run.Start(Now.AddMinutes(2));

        run.RequestCancellation(Now.AddMinutes(3));

        run.ErrorCode.Should().Be("AI_CANCEL_REQUESTED");
        run.ErrorMessage.Should().Be("Cancellation requested by the author.");
        run.UpdatedAt.Should().Be(Now.AddMinutes(3).UtcDateTime);

        run.Cancel(400, Now.AddMinutes(4));
        run.Cancel(999, Now.AddMinutes(5));
        run.RequestCancellation(Now.AddMinutes(6));

        run.Status.Should().Be(AiAuthoringRunStatus.Cancelled);
        run.ReleasedAmount.Should().Be(400);
        run.CompletedAt.Should().Be(Now.AddMinutes(4));
    }

    [Fact]
    public void AiAuthoringRun_RejectsInvalidTransitions()
    {
        var run = CreateRun();

        var start = () => run.Start(Now.AddMinutes(1));
        var cancel = () => run.Cancel(0, Now.AddMinutes(1));

        start.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be Reserved*");
        cancel.Should().Throw<InvalidOperationException>()
            .WithMessage("*reserved or running*");
    }

    [Fact]
    public void AiAuthoringRun_FailRejectsCompletedAndCancelledRuns()
    {
        var completed = CreateRun();
        completed.Reserve("provider", "model", 100, 200, 500, Now.AddMinutes(1));
        completed.Start(Now.AddMinutes(2));
        completed.Complete("Done", 50, 75, 250, 250, Now.AddMinutes(3));

        var cancelled = CreateRun();
        cancelled.Reserve("provider", "model", 100, 200, 500, Now.AddMinutes(1));
        cancelled.Cancel(500, Now.AddMinutes(2));

        var failCompleted = () => completed.Fail("FAILED", "late", 0, Now.AddMinutes(4));
        var failCancelled = () => cancelled.Fail("FAILED", "late", 0, Now.AddMinutes(4));

        failCompleted.Should().Throw<InvalidOperationException>()
            .WithMessage("*terminal AI run*");
        failCancelled.Should().Throw<InvalidOperationException>()
            .WithMessage("*terminal AI run*");
    }

    private static ProgramContentDraft CreateDraft(string payload = "{}") =>
        ProgramContentDraft.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            payload,
            Now);

    private static AiAuthoringProposal CreateProposal() =>
        AiAuthoringProposal.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            AiProposalKind.ReplaceDocument,
            "original",
            "proposed",
            Now);

    private static AiAuthoringRun CreateRun() =>
        AiAuthoringRun.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            AiProposalKind.ReplaceDocument,
            "Improve the lesson",
            null,
            "run-key",
            Now);
}
