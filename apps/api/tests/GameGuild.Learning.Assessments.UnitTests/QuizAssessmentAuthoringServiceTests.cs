using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.QuizAdapter;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class QuizAssessmentAuthoringServiceTests
{
    [Fact]
    public async Task SaveDraft_CreatesTheAssessmentAtomicallyAndDerivesMaxScoreFromQuizEntries()
    {
        await using var harness = await AuthoringHarness.CreateAsync();

        var saved = await harness.SaveDraftAsync(QuizDocument(200));

        saved.IsSuccess.Should().BeTrue();
        saved.Value.AssessmentId.Should().NotBeNull();
        var assessment = await harness.Context.Set<Assessment>().SingleAsync();
        var content = await harness.Context.Set<ProgramContent>().SingleAsync();
        assessment.ContentId.Should().Be(content.Id);
        assessment.MaxScore.Should().Be(ScoreValue.FromUnits(200));
        assessment.ReviewMethods.Should().Be(ReviewMethods.InstructorReview);
        content.JsonBody.Should().Be(QuizDocument(200).GetRawText());

        var stale = await harness.Service.SaveDraftAsync(
            harness.CourseId,
            harness.ContentId,
            harness.ActorId,
            Request(
                content.Version,
                assessment.Version - 1,
                QuizDocument(300),
                "Divergent title"));

        stale.IsFailure.Should().BeTrue();
        stale.Error.Code.Should().Be("AssessmentAuthoring.AssessmentVersion");
        harness.Context.ChangeTracker.Clear();
        (await harness.Context.Set<Assessment>().SingleAsync()).MaxScore
            .Should().Be(ScoreValue.FromUnits(200));
        (await harness.Context.Set<ProgramContent>().SingleAsync()).Title.Should().Be("Quiz");
    }

    [Fact]
    public async Task PrepareAndPublish_AreStableAndPublishTheExactPersistedCandidate()
    {
        await using var harness = await AuthoringHarness.CreateAsync();
        var saved = await harness.SaveDraftAsync(QuizDocument(200));
        var assessmentVersion = saved.Value.AssessmentVersion!.Value;

        var prepared = await harness.Service.PrepareAsync(
            saved.Value.AssessmentId!.Value,
            harness.ActorId,
            new PrepareAssessmentRevisionRequest(assessmentVersion));
        var repeated = await harness.Service.PrepareAsync(
            saved.Value.AssessmentId.Value,
            harness.ActorId,
            new PrepareAssessmentRevisionRequest(assessmentVersion));

        prepared.IsSuccess.Should().BeTrue();
        repeated.IsSuccess.Should().BeTrue();
        repeated.Value.RevisionId.Should().Be(prepared.Value.RevisionId);
        (await harness.Context.Set<AssessmentDefinitionRevision>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AcademicOutboxMessage>()
            .CountAsync(message => message.EventType == "assessment-definition-prepared"))
            .Should().Be(1);
        var candidate = await harness.Context.Set<AssessmentDefinitionRevision>().SingleAsync();
        var authoringBytes = candidate.AuthoringSourceCanonicalJson;
        var snapshotBytes = candidate.ExecutionSnapshotCanonicalJson;
        var snapshotHash = candidate.ExecutionSnapshotHash;

        var published = await harness.Service.PublishAsync(
            saved.Value.AssessmentId.Value,
            harness.ActorId,
            new PublishAssessmentRevisionRequest(candidate.Id, assessmentVersion));

        published.IsSuccess.Should().BeTrue();
        harness.Context.ChangeTracker.Clear();
        var persistedCandidate = await harness.Context.Set<AssessmentDefinitionRevision>().SingleAsync();
        var assessment = await harness.Context.Set<Assessment>().SingleAsync();
        assessment.PublishedDefinitionRevisionId.Should().Be(candidate.Id);
        persistedCandidate.AuthoringSourceCanonicalJson.Should().Be(authoringBytes);
        persistedCandidate.ExecutionSnapshotCanonicalJson.Should().Be(snapshotBytes);
        persistedCandidate.ExecutionSnapshotHash.Should().Be(snapshotHash);

        var changed = await harness.Service.SaveDraftAsync(
            harness.CourseId,
            harness.ContentId,
            harness.ActorId,
            Request(
                (await harness.Context.Set<ProgramContent>().SingleAsync()).Version,
                assessment.Version,
                QuizDocument(300)));
        changed.IsSuccess.Should().BeTrue();
        var state = await harness.Service.GetStateAsync(assessment.Id);
        state.IsSuccess.Should().BeTrue();
        state.Value.Lifecycle.Should().Be("changes-pending");
        state.Value.PublishedMatchesDraft.Should().BeFalse();

        var stalePublish = await harness.Service.PublishAsync(
            assessment.Id,
            harness.ActorId,
            new PublishAssessmentRevisionRequest(candidate.Id, changed.Value.AssessmentVersion!.Value));
        stalePublish.IsFailure.Should().BeTrue();
        stalePublish.Error.Code.Should().Be("AssessmentRevision.ChangesPending");
    }

    [Fact]
    public async Task Unpublish_IsIdempotentAndRetainsTheImmutableRevision()
    {
        await using var harness = await AuthoringHarness.CreateAsync();
        var saved = await harness.SaveDraftAsync(QuizDocument());
        var prepared = await harness.Service.PrepareAsync(
            saved.Value.AssessmentId!.Value,
            harness.ActorId,
            new PrepareAssessmentRevisionRequest(saved.Value.AssessmentVersion!.Value));
        await harness.Service.PublishAsync(
            saved.Value.AssessmentId.Value,
            harness.ActorId,
            new PublishAssessmentRevisionRequest(prepared.Value.RevisionId, saved.Value.AssessmentVersion.Value));
        var publishedVersion = (await harness.Context.Set<Assessment>().SingleAsync()).Version;
        var request = new UnpublishAssessmentRevisionRequest(
            prepared.Value.RevisionId,
            publishedVersion,
            "unpublish-1");

        var first = await harness.Service.UnpublishAsync(saved.Value.AssessmentId.Value, harness.ActorId, request);
        var retry = await harness.Service.UnpublishAsync(saved.Value.AssessmentId.Value, harness.ActorId, request);
        var conflict = await harness.Service.UnpublishAsync(
            saved.Value.AssessmentId.Value,
            harness.ActorId,
            request with { ExpectedAssessmentVersion = publishedVersion + 1 });

        first.IsSuccess.Should().BeTrue();
        retry.IsSuccess.Should().BeTrue();
        conflict.IsFailure.Should().BeTrue();
        conflict.Error.Code.Should().Be("AssessmentRevision.IdempotencyConflict");
        harness.Context.ChangeTracker.Clear();
        (await harness.Context.Set<Assessment>().SingleAsync()).PublishedDefinitionRevisionId.Should().BeNull();
        (await harness.Context.Set<AssessmentDefinitionRevision>().CountAsync()).Should().Be(1);
        (await harness.Context.Set<AcademicOutboxMessage>()
            .CountAsync(message => message.EventType == "assessment-definition-unpublished"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Preflight_ResolvesRetainedAndPublishedRevisionsInTheirRequiredContexts()
    {
        await using var harness = await AuthoringHarness.CreateAsync();
        var saved = await harness.SaveDraftAsync(QuizDocument());
        var prepared = await harness.Service.PrepareAsync(
            saved.Value.AssessmentId!.Value,
            harness.ActorId,
            new PrepareAssessmentRevisionRequest(saved.Value.AssessmentVersion!.Value));
        await harness.Service.PublishAsync(
            saved.Value.AssessmentId.Value,
            harness.ActorId,
            new PublishAssessmentRevisionRequest(prepared.Value.RevisionId, saved.Value.AssessmentVersion.Value));
        var adapters = new RecordingAdapterResolver(harness.Adapters);
        var policies = new RecordingExecutionPolicyResolver(harness.ExecutionPolicies);
        var stages = new RecordingStageHandlerResolver(harness.StageHandlers);
        var preflight = new AssessmentExecutableVersionPreflight(harness.Context, adapters, policies, stages);

        await preflight.ValidateAsync();

        adapters.Contexts.Should().Contain(ReviewExecutionContext.AuthorTest);
        adapters.Contexts.Should().Contain(ReviewExecutionContext.OfficialSubmission);
        stages.Contexts.Should().BeEquivalentTo([
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        ]);
        policies.Contexts.Should().BeEquivalentTo([
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        ]);
    }

    [Fact]
    public async Task Preflight_FailsClosedForMissingExactVersionsAndUnboundSnapshotSource()
    {
        await using var harness = await AuthoringHarness.CreateAsync();
        var saved = await harness.SaveDraftAsync(QuizDocument());
        await harness.Service.PrepareAsync(
            saved.Value.AssessmentId!.Value,
            harness.ActorId,
            new PrepareAssessmentRevisionRequest(saved.Value.AssessmentVersion!.Value));
        var unavailable = new RecordingAdapterResolver(
            harness.Adapters,
            rejectContext: ReviewExecutionContext.AuthorTest);
        var missingVersion = () => new AssessmentExecutableVersionPreflight(
                harness.Context,
                unavailable,
                harness.ExecutionPolicies,
                harness.StageHandlers)
            .ValidateAsync();
        await missingVersion.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unavailable for AuthorTest*");

        var revision = await harness.Context.Set<AssessmentDefinitionRevision>().SingleAsync();
        var snapshot = JsonNode.Parse(revision.ExecutionSnapshotCanonicalJson)!.AsObject();
        snapshot["authoringSource"]!["content"]!["blocks"]!["q1"]!["stem"] = "Tampered";
        var tamperedSnapshot = CanonicalJson.Serialize(
            JsonSerializer.SerializeToElement(snapshot, GradingJson.Options));
        harness.Context.Set<AssessmentDefinitionRevision>().Add(AssessmentDefinitionRevision.Create(
            revision.TenantId,
            revision.AssessmentId,
            revision.RevisionNumber + 1,
            revision.AuthoringSourceCanonicalJson,
            tamperedSnapshot,
            harness.ActorId));
        await harness.Context.SaveChangesAsync();
        var unbound = () => new AssessmentExecutableVersionPreflight(
                harness.Context,
                harness.Adapters,
                harness.ExecutionPolicies,
                harness.StageHandlers)
            .ValidateAsync();

        await unbound.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not bind its authoring source*");
    }

    private static JsonElement QuizDocument(int points = 200)
    {
        using var document = JsonDocument.Parse($$"""
            {
              "schemaVersion": 1,
              "order": [["q1", "quiz"]],
              "blocks": {
                "q1": {
                  "type": "TRUE_FALSE",
                  "stem": "Question",
                  "points": {{points}},
                  "correctAnswer": true,
                  "settings": { "allowRetry": false }
                }
              },
              "grading": { "schemaVersion": 2, "items": { "q1": {} } }
            }
            """);
        return document.RootElement.Clone();
    }

    private static SaveAssessmentDraftRequest Request(
        int contentVersion,
        int? assessmentVersion,
        JsonElement document,
        string title = "Quiz") =>
        new(
            contentVersion,
            assessmentVersion,
            title,
            "quiz",
            null,
            document,
            Visibility.Public,
            true,
            null,
            EstimatedMinutesSource.Auto,
            ReviewMethods.InstructorReview,
            ScoreValue.FromUnits(100));

    private sealed class AuthoringHarness : IAsyncDisposable
    {
        private AuthoringHarness(
            AuthoringTestContext context,
            AssessmentAuthoringService service,
            IAssessmentTypeAdapterResolver adapters,
            IAssessmentExecutionPolicyResolver executionPolicies,
            IReviewStageHandlerResolver stageHandlers,
            Guid courseId,
            Guid contentId,
            Guid actorId)
        {
            Context = context;
            Service = service;
            Adapters = adapters;
            ExecutionPolicies = executionPolicies;
            StageHandlers = stageHandlers;
            CourseId = courseId;
            ContentId = contentId;
            ActorId = actorId;
        }

        public AuthoringTestContext Context { get; }
        public AssessmentAuthoringService Service { get; }
        public IAssessmentTypeAdapterResolver Adapters { get; }
        public IAssessmentExecutionPolicyResolver ExecutionPolicies { get; }
        public IReviewStageHandlerResolver StageHandlers { get; }
        public Guid CourseId { get; }
        public Guid ContentId { get; }
        public Guid ActorId { get; }

        public static async Task<AuthoringHarness> CreateAsync()
        {
            var context = new AuthoringTestContext(
                new DbContextOptionsBuilder<AuthoringTestContext>()
                    .UseInMemoryDatabase($"QuizAssessmentAuthoring_{Guid.NewGuid()}")
                    .Options);
            var courseId = Guid.NewGuid();
            var contentId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            context.Set<ProgramContent>().Add(new ProgramContent
            {
                Id = contentId,
                TenantId = Guid.NewGuid(),
                ProgramId = courseId,
                Title = "Quiz",
                Slug = "quiz",
                Type = ProgramContentType.Questionnaire,
                JsonBody = "{\"schemaVersion\":1,\"order\":[],\"blocks\":{}}",
                LessonFormat = null,
            });
            await context.SaveChangesAsync();

            var contexts = new HashSet<ReviewExecutionContext>
            {
                ReviewExecutionContext.AuthorTest,
                ReviewExecutionContext.OfficialSubmission,
            };
            var registry = new ReviewCapabilityRegistry();
            registry.Register(new ExecutableComponentDescriptor(
                ExecutableComponentKind.AssessmentTypeAdapter,
                QuizAdapterContracts.AdapterKey,
                QuizAdapterContracts.Version,
                contexts));
            registry.Register(new ExecutableComponentDescriptor(
                ExecutableComponentKind.ExecutionPolicy,
                AssessmentExecutionPolicyCapability.Key,
                AssessmentExecutionPolicyCapability.Version,
                contexts));
            var handler = new ControlledInstructorReviewHandler(contexts);
            registry.Register(new ReviewCapabilityDescriptor(
                handler.Method,
                handler.Key,
                handler.Version,
                contexts));
            var projector = new QuizItemProjector();
            var quizAdapter = new QuizAssessmentTypeAdapter(
                new QuizAuthoringAdapter(projector),
                new QuizDeliveryGenerator(),
                new QuizAnswerDecoder(),
                new QuizDeterministicReviewAlgorithm());
            var adapter = new TestAssessmentTypeAdapter(quizAdapter, contexts);
            var adapters = new AssessmentTypeAdapterResolver(registry, [adapter]);
            var executionPolicies = new AssessmentExecutionPolicyResolver(registry);
            var stageHandlers = new ReviewStageHandlerResolver(registry, [handler]);
            var service = new AssessmentAuthoringService(
                context,
                adapters,
                executionPolicies,
                stageHandlers,
                [handler],
                new AcademicOutboxWriter(context, []),
                NullLogger<AssessmentAuthoringService>.Instance);
            return new AuthoringHarness(context, service, adapters, executionPolicies, stageHandlers, courseId, contentId, actorId);
        }

        public Task<Result<AssessmentDraftResult>> SaveDraftAsync(JsonElement document)
        {
            var content = Context.Set<ProgramContent>().Single();
            var assessment = Context.Set<Assessment>().SingleOrDefault();
            return Service.SaveDraftAsync(
                CourseId,
                ContentId,
                ActorId,
                Request(content.Version, assessment?.Version, document));
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class ControlledInstructorReviewHandler(IReadOnlySet<ReviewExecutionContext> contexts)
        : IReviewStageHandler
    {
        public ReviewMethod Method => ReviewMethod.InstructorReview;
        public string Key => "test-instructor-review";
        public string Version => "1";
        public string? ProviderKey => null;
        public string? ProviderPolicyVersion => null;
        public IReadOnlySet<ReviewExecutionContext> Contexts { get; } = contexts;

        public ValueTask<GradeResultV1> ExecuteAsync(
            ReviewStageRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The controlled authoring handler is never executed.");
    }

    private sealed class RecordingAdapterResolver(
        IAssessmentTypeAdapterResolver inner,
        ReviewExecutionContext? rejectContext = null) : IAssessmentTypeAdapterResolver
    {
        private readonly List<ReviewExecutionContext> _contexts = [];
        public IReadOnlyCollection<ReviewExecutionContext> Contexts => _contexts.Distinct().ToArray();

        public IAssessmentTypeAdapter ResolveForAuthoring(ProgramContentType programContentType) =>
            inner.ResolveForAuthoring(programContentType);

        public IAssessmentTypeAdapter Resolve(
            string contentType,
            string key,
            string version,
            ReviewExecutionContext context)
        {
            if (context == rejectContext)
                throw new InvalidOperationException($"Exact component version is unavailable for {context}.");
            _contexts.Add(context);
            return inner.Resolve(contentType, key, version, context);
        }
    }

    private sealed class RecordingExecutionPolicyResolver(IAssessmentExecutionPolicyResolver inner)
        : IAssessmentExecutionPolicyResolver
    {
        private readonly List<ReviewExecutionContext> _contexts = [];
        public IReadOnlyCollection<ReviewExecutionContext> Contexts => _contexts.Distinct().ToArray();

        public void Resolve(string key, string version, ReviewExecutionContext context)
        {
            _contexts.Add(context);
            inner.Resolve(key, version, context);
        }
    }

    private sealed class TestAssessmentTypeAdapter(
        IAssessmentTypeAdapter inner,
        IReadOnlySet<ReviewExecutionContext> contexts) : IAssessmentTypeAdapter
    {
        public string Key => inner.Key;
        public string Version => inner.Version;
        public string ContentType => inner.ContentType;
        public bool IsCurrentForAuthoring => inner.IsCurrentForAuthoring;
        public ProgramContentType ProgramContentType => inner.ProgramContentType;
        public AssessmentType AssessmentType => inner.AssessmentType;
        public SubmissionModality SubmissionModalities => inner.SubmissionModalities;
        public IReadOnlySet<ReviewExecutionContext> Contexts { get; } = contexts;
        public AssessmentAuthoringProjectionV1 ProjectAuthoring(JsonElement authoringDocument) =>
            inner.ProjectAuthoring(authoringDocument);
        public JsonElement GenerateDelivery(JsonElement projectedItem) => inner.GenerateDelivery(projectedItem);
        public JsonElement DecodeResponse(AssessmentResponseEnvelopeV1 envelope) => inner.DecodeResponse(envelope);
        public ValueTask<GradeResultV1> EvaluateDeterministicAsync(
            DeterministicReviewRequest request,
            CancellationToken cancellationToken) => inner.EvaluateDeterministicAsync(request, cancellationToken);
    }

    private sealed class RecordingStageHandlerResolver(IReviewStageHandlerResolver inner)
        : IReviewStageHandlerResolver
    {
        private readonly List<ReviewExecutionContext> _contexts = [];
        public IReadOnlyList<ReviewExecutionContext> Contexts => _contexts;

        public IReviewStageHandler Resolve(ReviewMethod method, string key, string version, ReviewExecutionContext context)
        {
            _contexts.Add(context);
            return inner.Resolve(method, key, version, context);
        }
    }

    private sealed class AuthoringTestContext(DbContextOptions<AuthoringTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<EntityBase<Guid>>()
                         .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                entry.Entity.Version++;
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            new GradingPersistenceModelConfiguration().Configure(modelBuilder);
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(content => content.Program);
                entity.Ignore(content => content.Parent);
                entity.Ignore(content => content.Children);
                entity.Ignore(content => content.ContentInteractions);
            });
        }
    }
}
