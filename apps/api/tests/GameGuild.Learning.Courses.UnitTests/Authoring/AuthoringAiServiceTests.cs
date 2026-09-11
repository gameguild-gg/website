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
        LessonContentFormat lessonFormat = LessonContentFormat.Markdown,
        string? providerOutput = null)
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
            ProgramContentType.Lesson,
            lessonFormat == LessonContentFormat.Video
                ? "https://cdn.example.test/original.mp4"
                : "# Original lesson",
            null,
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
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
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
        public int StreamCalls { get; private set; }

        public Task<Result<AiResolvedModelDto>> DescribeGenerateAsync(
            AiExecutionActor actor,
            AiGenerateRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new AiResolvedModelDto("OpenAi", "gpt-test", 64)));

        public async Task<Result<AiCompletionResponse>> GenerateForActorStreamingAsync(
            AiExecutionActor actor,
            AiGenerateRequest request,
            Func<string, CancellationToken, ValueTask> onDelta,
            CancellationToken cancellationToken = default)
        {
            StreamCalls++;
            request.MaxTokens.Should().Be(64);
            if (failGeneration)
                return Result.Failure<AiCompletionResponse>(Error.Problem("AI.ProviderFailed", "Provider failed."));
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
