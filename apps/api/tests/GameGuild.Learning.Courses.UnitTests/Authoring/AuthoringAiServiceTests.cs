using System.Text.Json;
using FluentAssertions;
using GameGuild.AI;
using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Authoring;

public sealed class AuthoringAiServiceTests
{
    [Fact]
    public async Task CreateRun_ReservesCreditsForExplicitActorAndQueuesPersistedRun()
    {
        await using var fixture = CreateFixture();
        var request = Request("actor-bound-run");

        var run = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            request,
            CancellationToken.None);

        run.Status.Should().Be(AiAuthoringRunStatus.Reserved);
        run.Provider.Should().Be("OpenAi");
        run.Model.Should().Be("gpt-test");
        fixture.Credits.ReservedActors.Should().ContainSingle()
            .Which.Should().Be((fixture.TenantId, fixture.ActorId));
        fixture.Queue.Items.Should().Equal(run.Id);
        (await fixture.Db.Set<AiAuthoringMessage>().SingleAsync()).Role.Should().Be("user");
        (await fixture.Db.Set<AiAuthoringStreamEvent>().SingleAsync()).Sequence.Should().Be(1);
        fixture.Quota.Verify(service => service.TryAtomicConsumeAsync(
            fixture.TenantId,
            ResourceUsageType.AiRequests,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Quota.Verify(service => service.TryAtomicConsumeAsync(
            fixture.TenantId,
            ResourceUsageType.AiTokens,
            It.Is<long>(amount => amount > 64),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRun_IdempotentReplayDoesNotReserveOrQueueTwice()
    {
        await using var fixture = CreateFixture();
        var request = Request("same-key");

        var first = await fixture.Service.CreateRun(
            fixture.TenantId, fixture.ActorId, fixture.ProgramId, fixture.ContentId, request, CancellationToken.None);
        var replay = await fixture.Service.CreateRun(
            fixture.TenantId, fixture.ActorId, fixture.ProgramId, fixture.ContentId, request, CancellationToken.None);

        replay.Id.Should().Be(first.Id);
        fixture.Credits.ReserveCalls.Should().Be(1);
        fixture.Queue.Items.Should().ContainSingle();
        (await fixture.Db.Set<AiAuthoringRun>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateRun_IdempotencyKeyBoundToDifferentPayload_ReturnsConflictWithoutAnotherCharge()
    {
        await using var fixture = CreateFixture();
        var request = Request("bound-key");
        _ = await fixture.Service.CreateRun(
            fixture.TenantId, fixture.ActorId, fixture.ProgramId, fixture.ContentId, request, CancellationToken.None);

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            request with { Instruction = "Generate a different lesson" },
            CancellationToken.None);

        await act.Should().ThrowAsync<AiAuthoringIdempotencyConflictException>();
        fixture.Credits.ReserveCalls.Should().Be(1);
        fixture.Queue.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessRun_PersistsProviderDeltasProposalUsageAndActorHistory()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("streamed-run"),
            CancellationToken.None);

        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);

        var completed = await fixture.Service.GetRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);
        completed.Status.Should().Be(AiAuthoringRunStatus.Completed);
        completed.Usage.InputTokens.Should().Be(12);
        completed.Usage.OutputTokens.Should().Be(5);
        completed.Usage.SettledCost.Should().Be(17);
        completed.Proposal.Should().NotBeNull();
        completed.Proposal!.ProposedContent.Should().Be("# Improved lesson");
        fixture.Quota.Verify(service => service.DecrementUsageAsync(
            fixture.TenantId,
            ResourceUsageType.AiTokens,
            It.Is<long>(amount => amount > 0),
            fixture.ActorId,
            "lesson-authoring",
            It.IsAny<CancellationToken>()), Times.Once);

        var events = await fixture.Db.Set<AiAuthoringStreamEvent>()
            .OrderBy(item => item.Sequence)
            .ToListAsync();
        events.Select(item => item.Type).Should().Equal("status", "status", "delta", "delta", "completed");
        events.Where(item => item.Type == "delta").Select(item => item.Delta)
            .Should().Equal("# Improved ", "lesson");

        var conversations = await fixture.Service.GetConversations(
            fixture.TenantId, fixture.ActorId, fixture.ProgramId, fixture.ContentId, CancellationToken.None);
        conversations.Should().ContainSingle();
        conversations[0].Messages.Select(message => message.Role).Should().Equal("user", "assistant");
    }

    [Fact]
    public async Task ProcessRun_WhenProviderFails_ReleasesReservationAndPersistsTerminalError()
    {
        await using var fixture = CreateFixture(failGeneration: true);
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("failed-run"),
            CancellationToken.None);

        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);

        var failed = await fixture.Service.GetRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);
        failed.Status.Should().Be(AiAuthoringRunStatus.Failed);
        failed.ErrorCode.Should().Be("AI.ProviderFailed");
        failed.Usage.ReleasedAmount.Should().Be(100);
        fixture.Credits.ReleaseCalls.Should().Be(1);
        fixture.Quota.Verify(service => service.DecrementUsageAsync(
            fixture.TenantId,
            ResourceUsageType.AiTokens,
            It.Is<long>(amount => amount > 64),
            fixture.ActorId,
            "lesson-authoring",
            It.IsAny<CancellationToken>()), Times.Once);
        (await fixture.Db.Set<AiAuthoringStreamEvent>().OrderByDescending(item => item.Sequence).FirstAsync())
            .Type.Should().Be("error");
    }

    [Fact]
    public async Task ProcessRun_WhenRecoveredInRunningState_FailsClosedWithoutCallingProviderAgain()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("interrupted-run"),
            CancellationToken.None);
        var persisted = await fixture.Db.Set<AiAuthoringRun>().SingleAsync(item => item.Id == created.Id);
        persisted.Start(DateTimeOffset.UtcNow);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);

        var recovered = await fixture.Service.GetRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);
        recovered.Status.Should().Be(AiAuthoringRunStatus.Failed);
        recovered.ErrorCode.Should().Be("AI_RUN_INTERRUPTED");
        fixture.Credits.ReleaseCalls.Should().Be(1);
        fixture.Ai.StreamCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetRun_RejectsAnotherAuthenticatedActor()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("private-run"),
            CancellationToken.None);

        var act = () => fixture.Service.GetRun(
            fixture.TenantId,
            Guid.NewGuid(),
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CancelRun_BeforeProviderStarts_ReleasesReservationAndReservedQuota()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("cancel-before-provider"),
            CancellationToken.None);

        var cancelled = await fixture.Service.CancelRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);
        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);

        cancelled.Status.Should().Be(AiAuthoringRunStatus.Cancelled);
        cancelled.Usage.ReleasedAmount.Should().Be(100);
        fixture.Credits.ReleaseCalls.Should().Be(1);
        fixture.Ai.StreamCalls.Should().Be(0);
        fixture.Quota.Verify(service => service.DecrementUsageAsync(
            fixture.TenantId,
            ResourceUsageType.AiRequests,
            1,
            fixture.ActorId,
            "lesson-authoring",
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Quota.Verify(service => service.DecrementUsageAsync(
            fixture.TenantId,
            ResourceUsageType.AiTokens,
            It.Is<long>(amount => amount > 64),
            fixture.ActorId,
            "lesson-authoring",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRun_VideoLessonRejectsDocumentReplacementBeforeReservingCredits()
    {
        await using var fixture = CreateFixture(lessonFormat: LessonContentFormat.Video);

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("unsafe-video-rewrite"),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AiProposalKindNotAllowedException>();
        exception.Which.Kind.Should().Be(AiProposalKind.ReplaceDocument);
        fixture.Credits.ReserveCalls.Should().Be(0);
        fixture.Queue.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessRun_VideoMetadataProposalCannotReplaceMediaOrContentKind()
    {
        var maliciousProviderPayload = new AuthoringContentPayload(
            "Improved video title",
            "improved-video-title",
            "Improved description",
            ProgramContentType.Questionnaire,
            "https://attacker.example/replacement.mp4",
            JsonDocument.Parse("{\"questions\":[]}").RootElement.Clone(),
            LessonContentFormat.Lexical,
            null,
            false,
            12,
            EstimatedMinutesSource.Manual,
            Visibility.Public);
        var providerOutput = JsonSerializer.Serialize(
            maliciousProviderPayload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var fixture = CreateFixture(
            lessonFormat: LessonContentFormat.Video,
            providerOutput: providerOutput);
        var request = Request("safe-video-metadata") with
        {
            ProposalKind = AiProposalKind.MetadataPatch,
        };

        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            request,
            CancellationToken.None);
        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);

        var completed = await fixture.Service.GetRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);
        var proposed = JsonSerializer.Deserialize<AuthoringContentPayload>(
            completed.Proposal!.ProposedContent,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        proposed.Should().NotBeNull();
        proposed!.Title.Should().Be("Improved video title");
        proposed.Body.Should().Be("https://cdn.example.test/original.mp4");
        proposed.JsonBody.Should().BeNull();
        proposed.Type.Should().Be(ProgramContentType.Lesson);
        proposed.LessonFormat.Should().Be(LessonContentFormat.Video);
    }

    [Fact]
    public async Task CreateRun_WhenRequestQuotaIsExceeded_DoesNotReserveCreditsOrQueueRun()
    {
        await using var fixture = CreateFixture(quotaExceeded: ResourceUsageType.AiRequests);

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("quota-exceeded"),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AiAuthoringExecutionException>();
        exception.Which.Code.Should().Be("AI_QUOTA_EXCEEDED");
        fixture.Credits.ReserveCalls.Should().Be(0);
        fixture.Queue.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntitlement_RequiresTenantScopedActorAndReturnsThatActorsWallet()
    {
        await using var fixture = CreateFixture();

        var entitlement = await fixture.Service.GetEntitlement(
            fixture.TenantId,
            fixture.ActorId,
            CancellationToken.None);

        entitlement.AvailableSoftCredits.Should().Be(1_000);
        entitlement.Currency.Should().Be("SoftCoin");
        await FluentActions.Invoking(() => fixture.Service.GetEntitlement(
                Guid.Empty,
                fixture.ActorId,
                CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Invoking(() => fixture.Service.GetEntitlement(
                fixture.TenantId,
                Guid.Empty,
                CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetConversations_WhenAuthorHasNoHistory_ReturnsEmptyCollection()
    {
        await using var fixture = CreateFixture();

        var conversations = await fixture.Service.GetConversations(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            CancellationToken.None);

        conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRun_ReusesRequestedConversationAndNormalizesBlankSelection()
    {
        await using var fixture = CreateFixture();
        var first = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("conversation-first") with { Selection = "   " },
            CancellationToken.None);

        var second = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("conversation-second") with { ConversationId = first.ConversationId },
            CancellationToken.None);

        second.ConversationId.Should().Be(first.ConversationId);
        (await fixture.Db.Set<AiAuthoringConversation>().CountAsync()).Should().Be(1);
        (await fixture.Db.Set<AiAuthoringRun>().SingleAsync(item => item.Id == first.Id))
            .Selection.Should().BeNull();
    }

    [Fact]
    public async Task CreateRun_IdempotentReplayPreservesNonBlankSelection()
    {
        await using var fixture = CreateFixture();
        var request = Request("selected-replay") with { Selection = "Keep this paragraph" };

        var first = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            request,
            CancellationToken.None);
        var replay = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            request,
            CancellationToken.None);

        replay.Id.Should().Be(first.Id);
        (await fixture.Db.Set<AiAuthoringRun>().SingleAsync()).Selection
            .Should().Be("Keep this paragraph");
    }

    [Fact]
    public async Task CreateRun_WithUnknownConversationFailsBeforeCharging()
    {
        await using var fixture = CreateFixture();

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("unknown-conversation") with { ConversationId = Guid.NewGuid() },
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        fixture.Credits.ReserveCalls.Should().Be(0);
    }

    [Fact]
    public async Task CreateRun_WithStaleDraftRevisionFailsBeforeCallingProvider()
    {
        await using var fixture = CreateFixture();

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("stale-draft") with { DraftRevision = 9 },
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AuthoringRevisionConflictException>();
        exception.Which.CurrentRevision.Should().Be(1);
        fixture.Ai.DescribeCalls.Should().Be(0);
    }

    [Fact]
    public async Task CreateRun_WhenTokenQuotaIsExceededReleasesRequestQuotaAndDoesNotCharge()
    {
        await using var fixture = CreateFixture(quotaExceeded: ResourceUsageType.AiTokens);

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("token-quota-exceeded"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AiAuthoringExecutionException>()
            .WithMessage("*token quota*");
        fixture.Credits.ReserveCalls.Should().Be(0);
        fixture.Quota.Verify(service => service.DecrementUsageAsync(
            fixture.TenantId,
            ResourceUsageType.AiRequests,
            1,
            fixture.ActorId,
            "lesson-authoring",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Markdown, AiProposalKind.ReplaceDocument, "complete replacement body")]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Markdown, AiProposalKind.InsertAtCursor, "text to insert")]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Markdown, AiProposalKind.MetadataPatch, "AuthoringContentPayload")]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Lexical, AiProposalKind.LexicalPatch, "JSON Pointer")]
    [InlineData(ProgramContentType.Questionnaire, null, AiProposalKind.QuizPatch, "quiz fields")]
    public async Task CreateRun_BuildsFormatSpecificProviderContract(
        ProgramContentType contentType,
        LessonContentFormat? lessonFormat,
        AiProposalKind kind,
        string expectedContract)
    {
        await using var fixture = CreateFixture(
            contentType: contentType,
            lessonFormat: lessonFormat,
            jsonBody: StructuredBody(contentType, lessonFormat));

        _ = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request($"contract-{kind}") with { ProposalKind = kind, Selection = "Selected text" },
            CancellationToken.None);

        fixture.Ai.LastDescribeRequest.Should().NotBeNull();
        fixture.Ai.LastDescribeRequest!.Prompt.Should().Contain(expectedContract);
        fixture.Ai.LastDescribeRequest.Prompt.Should().Contain("Selected text");
    }

    [Theory]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Video, AiProposalKind.InsertAtCursor)]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Lexical, AiProposalKind.ReplaceDocument)]
    [InlineData(ProgramContentType.Questionnaire, null, AiProposalKind.LexicalPatch)]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Markdown, AiProposalKind.QuizPatch)]
    public async Task CreateRun_RejectsProposalKindsThatCannotSafelyModifyFormat(
        ProgramContentType contentType,
        LessonContentFormat? lessonFormat,
        AiProposalKind kind)
    {
        await using var fixture = CreateFixture(
            contentType: contentType,
            lessonFormat: lessonFormat,
            jsonBody: StructuredBody(contentType, lessonFormat));

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request($"rejected-{kind}") with { ProposalKind = kind },
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AiProposalKindNotAllowedException>();
        exception.Which.Kind.Should().Be(kind);
        fixture.Credits.ReserveCalls.Should().Be(0);
    }

    [Fact]
    public async Task StreamRun_ReplaysPersistedDeltasAndTerminalPayloadAfterCursor()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("replay-stream"),
            CancellationToken.None);
        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);

        var events = await Collect(fixture.Service.StreamRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            -1,
            CancellationToken.None));

        events.Select(item => item.Type).Should().Equal("status", "status", "delta", "delta", "completed");
        events[^1].Proposal.Should().NotBeNull();
        events[^1].Usage!.SettledCost.Should().Be(17);
    }

    [Fact]
    public async Task StreamRun_WithNullTerminalPayloadRejectsCorruptPersistedEvent()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("corrupt-stream"),
            CancellationToken.None);
        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);
        fixture.Db.Set<AiAuthoringStreamEvent>().Add(AiAuthoringStreamEvent.Create(
            created.Id,
            6,
            "completed",
            AiAuthoringRunStatus.Completed.ToString(),
            null,
            "null",
            DateTimeOffset.UtcNow));
        await fixture.Db.SaveChangesAsync();

        var act = async () => await Collect(fixture.Service.StreamRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            5,
            CancellationToken.None));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*stream event is invalid*");
    }

    [Fact]
    public async Task ProcessRun_StructuredDraftWithoutJsonFailsClosedFromEmptyDocumentFallback()
    {
        await using var fixture = CreateFixture(
            lessonFormat: LessonContentFormat.Lexical,
            jsonBody: null,
            providerOutput: "{\"operations\":[]}");

        var completed = await CompleteRun(fixture, AiProposalKind.LexicalPatch, "empty-structured-body");

        completed.Status.Should().Be(AiAuthoringRunStatus.Failed);
        completed.ErrorCode.Should().Be("AI_EXECUTION_FAILED");
        completed.Proposal.Should().BeNull();
        fixture.Credits.ReleaseCalls.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAndApplyInsert_NullBodyUsesEmptyOriginalContent()
    {
        await using var fixture = CreateFixture(nullBody: true);

        var completed = await CompleteRun(fixture, AiProposalKind.InsertAtCursor, "empty-text-body");
        var draft = await fixture.Service.ApplyProposal(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            completed.Proposal!.Id,
            new ApplyAiProposalRequest(1, null),
            CancellationToken.None);

        completed.Status.Should().Be(AiAuthoringRunStatus.Completed);
        completed.Proposal.OriginalContent.Should().BeEmpty();
        draft.Payload.Body.Should().Be("# Improved lesson");
    }

    [Fact]
    public async Task ProcessRun_MetadataProviderReturningNullFailsClosed()
    {
        await using var fixture = CreateFixture(providerOutput: "null");
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("null-metadata") with { ProposalKind = AiProposalKind.MetadataPatch },
            CancellationToken.None);

        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);
        var failed = await fixture.Service.GetRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);

        failed.Status.Should().Be(AiAuthoringRunStatus.Failed);
        failed.ErrorCode.Should().Be("AI_EXECUTION_FAILED");
        fixture.Credits.ReleaseCalls.Should().Be(1);
    }

    [Fact]
    public async Task CreateRun_NullDraftPayloadFailsClosedBeforeCharging()
    {
        await using var fixture = CreateFixture(rawPayloadJson: "null");

        var act = () => fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("null-draft"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*draft payload is invalid*");
        fixture.Credits.ReserveCalls.Should().Be(0);
    }

    [Fact]
    public async Task ProcessRun_ReservedRunWithoutOutputLimitUsesProviderDefault()
    {
        await using var fixture = CreateFixture();
        var draft = await fixture.Db.Set<ProgramContentDraft>().SingleAsync();
        var conversation = AiAuthoringConversation.Create(
            fixture.TenantId,
            fixture.ProgramId,
            fixture.ContentId,
            fixture.ActorId,
            DateTimeOffset.UtcNow);
        var run = AiAuthoringRun.Create(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            draft.Id,
            conversation.Id,
            draft.Revision,
            AiProposalKind.ReplaceDocument,
            "Improve the lesson",
            null,
            "provider-default-output-limit",
            DateTimeOffset.UtcNow);
        run.Reserve("OpenAi", "gpt-test", 2_000, 0, 100, DateTimeOffset.UtcNow);
        fixture.Db.Set<AiAuthoringConversation>().Add(conversation);
        fixture.Db.Set<AiAuthoringRun>().Add(run);
        await fixture.Credits.ReserveAsync(
            run.Id,
            fixture.TenantId,
            fixture.ActorId,
            "lesson-authoring",
            new AiCreditQuote("test-v1", "OpenAi", "gpt-test", 2_000, 0, 100, 1_000_000, 1_000_000),
            "direct-reservation",
            CancellationToken.None);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ProcessRun(run.Id, CancellationToken.None);

        fixture.Ai.LastStreamRequest!.MaxTokens.Should().BeNull();
        run.Status.Should().Be(AiAuthoringRunStatus.Completed);
    }

    [Theory]
    [InlineData(null, "abcdX")]
    [InlineData(-10, "Xabcd")]
    [InlineData(2, "abXcd")]
    [InlineData(99, "abcdX")]
    public async Task ApplyProposal_InsertAtCursorClampsOffset(int? cursorOffset, string expected)
    {
        await using var fixture = CreateFixture(body: "abcd", providerOutput: "X");
        var completed = await CompleteRun(fixture, AiProposalKind.InsertAtCursor, $"insert-{cursorOffset}");

        var draft = await fixture.Service.ApplyProposal(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            completed.Proposal!.Id,
            new ApplyAiProposalRequest(1, cursorOffset),
            CancellationToken.None);

        draft.Revision.Should().Be(2);
        draft.Payload.Body.Should().Be(expected);
        completed.Proposal.Status.Should().Be(AiProposalStatus.Pending);
        (await fixture.Db.Set<AiAuthoringProposal>().SingleAsync()).Status.Should().Be(AiProposalStatus.Applied);
    }

    [Fact]
    public async Task ApplyProposal_ReplaceDocumentUpdatesDraftWithoutPublishing()
    {
        await using var fixture = CreateFixture(providerOutput: "# Replacement");
        var completed = await CompleteRun(fixture, AiProposalKind.ReplaceDocument, "replace-document");

        var draft = await fixture.Service.ApplyProposal(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            completed.Proposal!.Id,
            new ApplyAiProposalRequest(1, null),
            CancellationToken.None);

        draft.Payload.Body.Should().Be("# Replacement");
        draft.LastEditedBy.Should().Be(fixture.ActorId);
    }

    [Fact]
    public async Task ApplyProposal_UnknownPersistedKindFailsWithoutChangingDraft()
    {
        await using var fixture = CreateFixture();
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request("unknown-persisted-kind"),
            CancellationToken.None);
        var proposal = AiAuthoringProposal.Create(
            created.Id,
            fixture.ContentId,
            1,
            (AiProposalKind)999,
            "original",
            "proposed",
            DateTimeOffset.UtcNow);
        proposal.TenantId = fixture.TenantId;
        fixture.Db.Set<AiAuthoringProposal>().Add(proposal);
        await fixture.Db.SaveChangesAsync();

        var act = () => fixture.Service.ApplyProposal(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            proposal.Id,
            new ApplyAiProposalRequest(1, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await fixture.Db.Set<ProgramContentDraft>().SingleAsync()).Revision.Should().Be(1);
    }

    [Theory]
    [InlineData(ProgramContentType.Lesson, LessonContentFormat.Lexical, AiProposalKind.LexicalPatch, "/root/children/0/children/0/text", "New", "New")]
    [InlineData(ProgramContentType.Questionnaire, null, AiProposalKind.QuizPatch, "/questions/0/prompt", "New question", "New question")]
    public async Task ProcessAndApplyStructuredProposalPreservesJsonDocument(
        ProgramContentType contentType,
        LessonContentFormat? lessonFormat,
        AiProposalKind kind,
        string path,
        string replacement,
        string expected)
    {
        var patch = JsonSerializer.Serialize(new
        {
            operations = new[] { new { op = "replace", path, value = replacement } },
        });
        await using var fixture = CreateFixture(
            contentType: contentType,
            lessonFormat: lessonFormat,
            jsonBody: StructuredBody(contentType, lessonFormat),
            providerOutput: patch);
        var completed = await CompleteRun(fixture, kind, $"structured-{kind}");

        var draft = await fixture.Service.ApplyProposal(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            completed.Proposal!.Id,
            new ApplyAiProposalRequest(1, null),
            CancellationToken.None);

        draft.Payload.JsonBody.Should().NotBeNull();
        draft.Payload.JsonBody!.Value.GetRawText().Should().Contain(expected);
    }

    [Fact]
    public async Task ApplyProposal_MetadataChangesOnlyEditableMetadata()
    {
        var providerPayload = new AuthoringContentPayload(
            "Renamed lesson",
            "renamed-lesson",
            "Updated description",
            ProgramContentType.Questionnaire,
            "malicious body",
            JsonDocument.Parse("{\"questions\":[]}").RootElement.Clone(),
            LessonContentFormat.Lexical,
            null,
            false,
            42,
            EstimatedMinutesSource.Manual,
            Visibility.Public);
        await using var fixture = CreateFixture(providerOutput: JsonSerializer.Serialize(
            providerPayload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var completed = await CompleteRun(fixture, AiProposalKind.MetadataPatch, "metadata-apply");

        var draft = await fixture.Service.ApplyProposal(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            completed.Proposal!.Id,
            new ApplyAiProposalRequest(1, null),
            CancellationToken.None);

        draft.Payload.Title.Should().Be("Renamed lesson");
        draft.Payload.Body.Should().Be("# Original lesson");
        draft.Payload.Type.Should().Be(ProgramContentType.Lesson);
        draft.Payload.LessonFormat.Should().Be(LessonContentFormat.Markdown);
        draft.Payload.Visibility.Should().Be(Visibility.Public);
    }

    [Fact]
    public async Task DiscardProposal_MarksPendingProposalWithoutChangingDraft()
    {
        await using var fixture = CreateFixture();
        var completed = await CompleteRun(fixture, AiProposalKind.ReplaceDocument, "discard-proposal");

        var discarded = await fixture.Service.DiscardProposal(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            completed.Proposal!.Id,
            CancellationToken.None);

        discarded.Status.Should().Be(AiProposalStatus.Discarded);
        (await fixture.Db.Set<ProgramContentDraft>().SingleAsync()).Revision.Should().Be(1);
        await FluentActions.Invoking(() => fixture.Service.DiscardProposal(
                fixture.TenantId,
                fixture.ActorId,
                fixture.ProgramId,
                fixture.ContentId,
                completed.Proposal.Id,
                CancellationToken.None))
            .Should().ThrowAsync<AiProposalStateConflictException>();
    }

    [Fact]
    public async Task AuthoringAiRunQueue_DeliversEnqueuedRunsInOrder()
    {
        var queue = new AuthoringAiRunQueue();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await queue.Enqueue(first);
        await queue.Enqueue(second);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<Guid>();

        await foreach (var runId in queue.ReadAll(cancellation.Token))
        {
            received.Add(runId);
            if (received.Count == 2)
            {
                break;
            }
        }

        received.Should().Equal(first, second);
    }

    private static async Task<AiAuthoringRunDto> CompleteRun(
        Fixture fixture,
        AiProposalKind kind,
        string idempotencyKey)
    {
        var created = await fixture.Service.CreateRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            Request(idempotencyKey) with { ProposalKind = kind },
            CancellationToken.None);
        await fixture.Service.ProcessRun(created.Id, CancellationToken.None);
        return await fixture.Service.GetRun(
            fixture.TenantId,
            fixture.ActorId,
            fixture.ProgramId,
            fixture.ContentId,
            created.Id,
            CancellationToken.None);
    }

    private static async Task<List<AiStreamEvent>> Collect(IAsyncEnumerable<AiStreamEvent> stream)
    {
        var items = new List<AiStreamEvent>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        return items;
    }

    private static JsonElement? StructuredBody(
        ProgramContentType contentType,
        LessonContentFormat? lessonFormat)
    {
        if (contentType == ProgramContentType.Questionnaire)
        {
            return JsonDocument.Parse("{\"questions\":[{\"id\":\"q1\",\"prompt\":\"Old question\"}]}").RootElement.Clone();
        }

        if (lessonFormat == LessonContentFormat.Lexical)
        {
            return JsonDocument.Parse("{\"root\":{\"type\":\"root\",\"version\":1,\"children\":[{\"type\":\"paragraph\",\"version\":1,\"children\":[{\"type\":\"text\",\"version\":1,\"text\":\"Old\"}]}]}}").RootElement.Clone();
        }

        return null;
    }

    private static AiAuthoringRunRequest Request(string idempotencyKey) => new(
        null,
        1,
        "Improve this lesson",
        AiProposalKind.ReplaceDocument,
        null,
        idempotencyKey);

    private static Fixture CreateFixture(
        bool failGeneration = false,
        ResourceUsageType? quotaExceeded = null,
        LessonContentFormat? lessonFormat = LessonContentFormat.Markdown,
        string? providerOutput = null,
        ProgramContentType contentType = ProgramContentType.Lesson,
        JsonElement? jsonBody = null,
        string? body = null,
        bool nullBody = false,
        string? rawPayloadJson = null)
    {
        var db = new AuthoringAiTestDbContext(
            new DbContextOptionsBuilder<AuthoringAiTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        var payload = new AuthoringContentPayload(
            "Lesson",
            "lesson",
            "Description",
            contentType,
            nullBody ? null : body ?? (lessonFormat == LessonContentFormat.Video
                ? "https://cdn.example.test/original.mp4"
                : "# Original lesson"),
            jsonBody,
            lessonFormat,
            null,
            true,
            5,
            EstimatedMinutesSource.Manual,
            Visibility.Private);
        var draft = ProgramContentDraft.Create(
            Guid.NewGuid(),
            programId,
            contentId,
            actorId,
            1,
            rawPayloadJson ?? JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            DateTimeOffset.UtcNow);
        draft.TenantId = tenantId;
        db.Set<ProgramContentDraft>().Add(draft);
        db.SaveChanges();

        var quota = new Mock<IResourceQuotaEnforcer>();
        quota.Setup(service => service.TryAtomicConsumeAsync(
                tenantId,
                It.IsAny<ResourceUsageType>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, ResourceUsageType type, long amount, CancellationToken _) =>
                (type != quotaExceeded, type == quotaExceeded ? 100 : amount, type == quotaExceeded ? 100L : null));
        quota.Setup(service => service.DecrementUsageAsync(
                tenantId,
                It.IsAny<ResourceUsageType>(),
                It.IsAny<long>(),
                actorId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var credits = new RecordingCredits();
        var queue = new RecordingQueue();
        var ai = new RecordingAiOrchestrator(failGeneration, providerOutput ?? "# Improved lesson");
        var service = new AuthoringAiService(
            db,
            ai,
            credits,
            quota.Object,
            queue,
            TimeProvider.System,
            NullLogger<AuthoringAiService>.Instance);
        return new Fixture(db, service, credits, queue, quota, ai, tenantId, actorId, programId, contentId);
    }

    private sealed record Fixture(
        AuthoringAiTestDbContext Db,
        AuthoringAiService Service,
        RecordingCredits Credits,
        RecordingQueue Queue,
        Mock<IResourceQuotaEnforcer> Quota,
        RecordingAiOrchestrator Ai,
        Guid TenantId,
        Guid ActorId,
        Guid ProgramId,
        Guid ContentId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class RecordingAiOrchestrator(bool failGeneration, string providerOutput) : IAiOrchestrator
    {
        public int DescribeCalls { get; private set; }
        public int StreamCalls { get; private set; }
        public AiGenerateRequest? LastDescribeRequest { get; private set; }
        public AiGenerateRequest? LastStreamRequest { get; private set; }

        public Task<Result<AiResolvedModelDto>> DescribeGenerateAsync(
            AiExecutionActor actor,
            AiGenerateRequest request,
            CancellationToken cancellationToken = default)
        {
            DescribeCalls++;
            LastDescribeRequest = request;
            return Task.FromResult(Result.Success(new AiResolvedModelDto("OpenAi", "gpt-test", 64)));
        }

        public async Task<Result<AiCompletionResponse>> GenerateForActorStreamingAsync(
            AiExecutionActor actor,
            AiGenerateRequest request,
            Func<string, CancellationToken, ValueTask> onDelta,
            CancellationToken cancellationToken = default)
        {
            StreamCalls++;
            LastStreamRequest = request;
            if (failGeneration)
            {
                return Result.Failure<AiCompletionResponse>(Error.Problem("AI.ProviderFailed", "Provider failed."));
            }

            if (providerOutput == "# Improved lesson")
            {
                await onDelta("# Improved ", cancellationToken);
                await onDelta("lesson", cancellationToken);
            }
            else
            {
                await onDelta(providerOutput, cancellationToken);
            }
            return Result.Success(new AiCompletionResponse(
                "OpenAi", "gpt-test", providerOutput, "stop", new AiUsageDto(12, 5, 17)));
        }

        public Task<Result<AiCompletionResponse>> GenerateForActorStreamingWithReservedQuotaAsync(
            AiExecutionActor actor,
            AiGenerateRequest request,
            Func<string, CancellationToken, ValueTask> onDelta,
            CancellationToken cancellationToken = default) =>
            GenerateForActorStreamingAsync(actor, request, onDelta, cancellationToken);

        public Task<Result<AiCompletionResponse>> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Result<AiCompletionResponse>> GenerateAsync(AiGenerateRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Result<AiCompletionResponse>> GenerateForActorAsync(AiExecutionActor actor, AiGenerateRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingCredits : IAiCreditWalletService
    {
        private readonly Dictionary<Guid, AiCreditReservation> _reservations = [];
        public List<(Guid TenantId, Guid ActorId)> ReservedActors { get; } = [];
        public int ReserveCalls { get; private set; }
        public int ReleaseCalls { get; private set; }

        public Task<AiCreditBalance> GetBalanceAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiCreditBalance(1_000, 0, _reservations.Values.Sum(item => item.SettledSoftUnits)));

        public Task<AiCreditQuote> QuoteAsync(string serviceCode, string provider, string model, int maximumInputTokens, int maximumOutputTokens, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiCreditQuote("test-v1", provider, model, maximumInputTokens, maximumOutputTokens, 100, 1_000_000, 1_000_000));

        public Task<AiCreditReservation> ReserveAsync(Guid runId, Guid tenantId, Guid actorId, string serviceCode, AiCreditQuote quote, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            ReserveCalls++;
            ReservedActors.Add((tenantId, actorId));
            var reservation = AiCreditReservation.Create(
                runId, tenantId, actorId, Guid.NewGuid(), serviceCode, quote.Provider, quote.Model,
                quote.MaximumSoftUnits, idempotencyKey, DateTimeOffset.UtcNow, quote.RateCardVersion,
                quote.InputSoftUnitsPerMillion, quote.OutputSoftUnitsPerMillion);
            _reservations.Add(runId, reservation);
            return Task.FromResult(reservation);
        }

        public Task<AiCreditReservation> SettleAsync(Guid runId, int inputTokens, int outputTokens, string providerUsageId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            var reservation = _reservations[runId];
            reservation.Settle(inputTokens, outputTokens, inputTokens + outputTokens, providerUsageId, idempotencyKey, DateTimeOffset.UtcNow);
            return Task.FromResult(reservation);
        }

        public Task<AiCreditReservation> ReleaseAsync(Guid runId, string reason, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            ReleaseCalls++;
            var reservation = _reservations[runId];
            reservation.Release(reason, idempotencyKey, DateTimeOffset.UtcNow);
            return Task.FromResult(reservation);
        }
    }

    private sealed class RecordingQueue : IAuthoringAiRunQueue
    {
        public List<Guid> Items { get; } = [];

        public ValueTask Enqueue(Guid runId, CancellationToken cancellationToken = default)
        {
            Items.Add(runId);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Guid> ReadAll([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var item in Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }

    private sealed class AuthoringAiTestDbContext(DbContextOptions<AuthoringAiTestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramContentDraft>().HasKey(item => item.Id);
            modelBuilder.Entity<AiAuthoringConversation>().HasKey(item => item.Id);
            modelBuilder.Entity<AiAuthoringMessage>().HasKey(item => item.Id);
            modelBuilder.Entity<AiAuthoringRun>().HasKey(item => item.Id);
            modelBuilder.Entity<AiAuthoringStreamEvent>().HasKey(item => item.Id);
            modelBuilder.Entity<AiAuthoringProposal>().HasKey(item => item.Id);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
