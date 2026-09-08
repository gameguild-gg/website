using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Compliance.KYC.Tests;

public sealed class SumSubKycAmlOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConstructionDtosAndPolicyValidationFailClosed()
    {
        var request = new StartKycAmlRequest(Guid.NewGuid(), "subject", "key", Now);
        new KycAmlOnboarding("applicant", "subject", KycAmlState.Created, Now).ApplicantId.Should().Be("applicant");
        new SumSubWebhookIngestionResult(KycEvidenceIngestionStatus.Published, "event", KycAmlState.Approved, Guid.NewGuid())
            .ProviderEventId.Should().Be("event");
        request.IdempotencyKey.Should().Be("key");

        await using var context = Context();
        var provider = new StubProvider();
        var evidence = new RecordingEvidenceStore();
        var raw = new RecordingRawStore();
        Action nullContext = () => new SumSubKycAmlOrchestrator(null!, provider, evidence, raw, Options.Create(Policy()));
        Action nonDbContext = () => new SumSubKycAmlOrchestrator(new NonDbApplicationContext(), provider, evidence, raw, Options.Create(Policy()));
        Action nullProvider = () => new SumSubKycAmlOrchestrator(context, null!, evidence, raw, Options.Create(Policy()));
        Action nullEvidence = () => new SumSubKycAmlOrchestrator(context, provider, null!, raw, Options.Create(Policy()));
        Action nullRaw = () => new SumSubKycAmlOrchestrator(context, provider, evidence, null!, Options.Create(Policy()));
        Action nullPolicy = () => new SumSubKycAmlOrchestrator(context, provider, evidence, raw, null!);
        nullContext.Should().Throw<ArgumentNullException>();
        nonDbContext.Should().Throw<InvalidOperationException>();
        nullProvider.Should().Throw<ArgumentNullException>();
        nullEvidence.Should().Throw<ArgumentNullException>();
        nullRaw.Should().Throw<ArgumentNullException>();
        nullPolicy.Should().Throw<ArgumentNullException>();

        var service = Service(context, provider, evidence, raw);
        await FluentActions.Awaiting(() => service.StartAsync(request with { TenantId = Guid.Empty }, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => service.StartAsync(request with { SubjectHash = " " }, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => service.StartAsync(request with { IdempotencyKey = " " }, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();

        foreach (var invalid in new[]
                 {
                     Policy(policyVersion: 0),
                     Policy(environment: " "),
                     Policy(levelName: " ")
                 })
        {
            var invalidService = Service(context, provider, evidence, raw, invalid);
            await FluentActions.Awaiting(() => invalidService.StartAsync(request, CancellationToken.None))
                .Should().ThrowAsync<SumSubNotConfiguredException>();
        }
    }

    [Fact]
    public async Task StartAndAccessTokenAreIdempotentAndSubjectBound()
    {
        await using var context = Context();
        var provider = new StubProvider();
        var service = Service(context, provider, new RecordingEvidenceStore(), new RecordingRawStore());
        var tenant = Guid.NewGuid();
        var request = new StartKycAmlRequest(tenant, "subject-hash", "key-1", Now);

        var created = await service.StartAsync(request, CancellationToken.None);
        var replay = await service.StartAsync(request, CancellationToken.None);
        var existing = await service.StartAsync(request with { IdempotencyKey = "key-2" }, CancellationToken.None);

        created.Should().Be(replay).And.Be(existing);
        provider.ApplicantRequests.Should().ContainSingle();
        await FluentActions.Awaiting(() => service.StartAsync(
                request with { SubjectHash = "another-subject" }, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*another subject*");

        var token = await service.CreateAccessTokenAsync(tenant, "subject-hash", 600, CancellationToken.None);
        token.Token.Should().Be("token");
        provider.TokenRequests.Should().ContainSingle().Which.Lifetime.Should().Be(600);
        await FluentActions.Awaiting(() => service.CreateAccessTokenAsync(tenant, "subject-hash", 0, CancellationToken.None))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => service.CreateAccessTokenAsync(tenant, "missing", 60, CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task WebhookPersistsPublishedDuplicateConflictAndOutOfOrderEvidence()
    {
        await using var context = Context();
        var provider = new StubProvider();
        var evidence = new RecordingEvidenceStore();
        var raw = new RecordingRawStore();
        var service = Service(context, provider, evidence, raw);
        var tenant = Guid.NewGuid();
        var onboarding = await service.StartAsync(
            new StartKycAmlRequest(tenant, "subject", "key", Now), CancellationToken.None);
        var payload = Payload(onboarding.ApplicantId, "event-1", "completed", "GREEN");

        var published = await service.IngestWebhookAsync(payload, "digest", "algorithm", Now, Now, CancellationToken.None);
        var duplicate = await service.IngestWebhookAsync(payload, "digest", "algorithm", Now, Now, CancellationToken.None);

        published.Status.Should().Be(KycEvidenceIngestionStatus.Published);
        published.State.Should().Be(KycAmlState.Approved);
        duplicate.Status.Should().Be(KycEvidenceIngestionStatus.Duplicate);
        evidence.Envelopes.Should().ContainSingle().Which.Result.Should().Be(KycEvidenceResult.Approved);
        evidence.Envelopes.Single().JurisdictionCode.Should().Be("BRA");

        var conflicting = Payload(onboarding.ApplicantId, "event-1", "completed", "RED");
        await FluentActions.Awaiting(() => service.IngestWebhookAsync(
                conflicting, "digest", "algorithm", Now, Now, CancellationToken.None))
            .Should().ThrowAsync<KycEvidenceConflictException>();

        var older = Payload(onboarding.ApplicantId, "event-older", "pending", null);
        var deferred = await service.IngestWebhookAsync(
            older, "digest", "algorithm", Now.AddSeconds(-1), Now, CancellationToken.None);
        deferred.Status.Should().Be(KycEvidenceIngestionStatus.Deferred);
        deferred.State.Should().Be(KycAmlState.Approved);
    }

    [Fact]
    public async Task WebhookValidationAndStateMappingAreFailClosed()
    {
        await using var context = Context();
        var provider = new StubProvider();
        var evidence = new RecordingEvidenceStore();
        var service = Service(context, provider, evidence, new RecordingRawStore());
        var tenant = Guid.NewGuid();
        var onboarding = await service.StartAsync(
            new StartKycAmlRequest(tenant, "subject", "key", Now), CancellationToken.None);

        await FluentActions.Awaiting(() => service.IngestWebhookAsync(
                ReadOnlyMemory<byte>.Empty, "digest", "algorithm", Now, Now, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
        foreach (var invalidPayload in new[]
                 {
                     Encoding.UTF8.GetBytes("{}"),
                     Encoding.UTF8.GetBytes("{\"applicantId\":123}"),
                     Encoding.UTF8.GetBytes("{\"applicantId\":\" \"}")
                 })
        {
            await FluentActions.Awaiting(() => service.IngestWebhookAsync(
                    invalidPayload, "digest", "algorithm", Now, Now, CancellationToken.None))
                .Should().ThrowAsync<SumSubProtocolException>();
        }

        provider.VerifyResult = false;
        var invalidSignature = await service.IngestWebhookAsync(
            Payload(onboarding.ApplicantId, "invalid-signature", "pending", null),
            "digest", "algorithm", Now.AddSeconds(1), Now.AddSeconds(1), CancellationToken.None);
        invalidSignature.Status.Should().Be(KycEvidenceIngestionStatus.Rejected);

        provider.VerifyResult = true;
        var unknown = await service.IngestWebhookAsync(
            Payload("unknown-applicant", "unknown", "pending", null),
            "digest", "algorithm", Now.AddSeconds(2), Now.AddSeconds(2), CancellationToken.None);
        unknown.Status.Should().Be(KycEvidenceIngestionStatus.Rejected);
        unknown.State.Should().Be(KycAmlState.NeedsReview);

        var invalidTimestamp = await service.IngestWebhookAsync(
            Payload(onboarding.ApplicantId, "future", "pending", null),
            "digest", "algorithm", Now.AddSeconds(4), Now.AddSeconds(3), CancellationToken.None);
        invalidTimestamp.Status.Should().Be(KycEvidenceIngestionStatus.Rejected);

        var cases = new[]
        {
            (Encoding.UTF8.GetBytes($"{{\"applicantId\":\"{onboarding.ApplicantId}\",\"inspectionId\":\"no-review-status\"}}"), KycAmlState.InReview),
            (PayloadWithoutInspection(onboarding.ApplicantId, "pending", null, includeType: true), KycAmlState.ApplicantPending),
            (PayloadWithoutInspection(onboarding.ApplicantId, "completed", null, includeType: false), KycAmlState.NeedsReview),
            (PayloadWithoutInspection(onboarding.ApplicantId, "completed", "RED", includeType: true), KycAmlState.Rejected),
            (PayloadWithoutInspection(onboarding.ApplicantId, "queued", null, includeType: true), KycAmlState.InReview)
        };
        var second = 10;
        foreach (var (payload, expected) in cases)
        {
            var result = await service.IngestWebhookAsync(
                payload, "digest", "algorithm", Now.AddSeconds(second), Now.AddSeconds(second), CancellationToken.None);
            result.State.Should().Be(expected);
            second++;
        }

        evidence.NextStatus = KycEvidenceIngestionStatus.Deferred;
        var nonPublished = await service.IngestWebhookAsync(
            Payload(onboarding.ApplicantId, "not-published", "completed", "GREEN"),
            "digest", "algorithm", Now.AddSeconds(20), Now.AddSeconds(20), CancellationToken.None);
        nonPublished.Status.Should().Be(KycEvidenceIngestionStatus.Deferred);
    }

    [Fact]
    public async Task ReconciliationMapsEveryProviderStateAndHonorsEvidenceLifetimes()
    {
        await using var context = Context();
        var provider = new StubProvider();
        var evidence = new RecordingEvidenceStore();
        var service = Service(context, provider, evidence, new RecordingRawStore());
        var tenant = Guid.NewGuid();
        await service.StartAsync(new StartKycAmlRequest(tenant, "subject", "key", Now), CancellationToken.None);

        var cases = new[]
        {
            (KycAmlState.Approved, KycEvidenceResult.Approved, TimeSpan.FromDays(30)),
            (KycAmlState.Rejected, KycEvidenceResult.Rejected, TimeSpan.FromDays(2)),
            (KycAmlState.Created, KycEvidenceResult.NeedsReview, TimeSpan.FromDays(2)),
            (KycAmlState.ApplicantPending, KycEvidenceResult.NeedsReview, TimeSpan.FromDays(2)),
            (KycAmlState.InReview, KycEvidenceResult.NeedsReview, TimeSpan.FromDays(2)),
            (KycAmlState.NeedsReview, KycEvidenceResult.NeedsReview, TimeSpan.FromDays(2)),
            (KycAmlState.Expired, KycEvidenceResult.Unavailable, TimeSpan.FromDays(2))
        };
        var offset = 1;
        foreach (var (state, expected, lifetime) in cases)
        {
            provider.StatusState = state;
            var at = Now.AddMinutes(offset++);
            var result = await service.ReconcileAsync(tenant, "subject", at, CancellationToken.None);
            result.Status.Should().Be(KycEvidenceIngestionStatus.Published);
            evidence.Envelopes[^1].Result.Should().Be(expected);
            evidence.Envelopes[^1].ExpiresAt.Should().Be(at.Add(lifetime));
        }

        evidence.NextStatus = KycEvidenceIngestionStatus.Deferred;
        provider.StatusState = KycAmlState.Approved;
        (await service.ReconcileAsync(tenant, "subject", Now.AddHours(1), CancellationToken.None)).Status
            .Should().Be(KycEvidenceIngestionStatus.Deferred);
        await FluentActions.Awaiting(() => service.ReconcileAsync(tenant, "missing", Now, CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();

        provider.StatusJurisdiction = null;
        await FluentActions.Awaiting(() => service.ReconcileAsync(tenant, "subject", Now.AddHours(2), CancellationToken.None))
            .Should().ThrowAsync<SumSubProtocolException>()
            .WithMessage("*verified ISO alpha-3 jurisdiction*");
        provider.StatusJurisdiction = "BRA";

        foreach (var (state, policy) in new[]
                 {
                     (KycAmlState.Approved, Policy(approvedLifetime: TimeSpan.Zero)),
                     (KycAmlState.Rejected, Policy(reviewLifetime: TimeSpan.Zero))
                 })
        {
            await using var invalidContext = Context();
            var invalidProvider = new StubProvider { StatusState = state };
            var invalidService = Service(invalidContext, invalidProvider, new RecordingEvidenceStore(), new RecordingRawStore(), policy);
            var invalidTenant = Guid.NewGuid();
            await invalidService.StartAsync(
                new StartKycAmlRequest(invalidTenant, "subject", Guid.NewGuid().ToString("N"), Now), CancellationToken.None);
            await FluentActions.Awaiting(() => invalidService.ReconcileAsync(invalidTenant, "subject", Now, CancellationToken.None))
                .Should().ThrowAsync<SumSubNotConfiguredException>()
                .WithMessage("*lifetimes*");
        }
    }

    private static SumSubKycAmlOrchestrator Service(
        TestDbContext context,
        StubProvider provider,
        RecordingEvidenceStore evidence,
        RecordingRawStore raw,
        KycPolicyOptions? policy = null) => new(
        context, provider, evidence, raw, Options.Create(policy ?? Policy()));

    private static KycPolicyOptions Policy(
        long policyVersion = 7,
        string environment = "sandbox",
        string levelName = "basic",
        TimeSpan? approvedLifetime = null,
        TimeSpan? reviewLifetime = null) => new()
    {
        PolicyVersion = policyVersion,
        Environment = environment,
        LevelName = levelName,
        ApprovedEvidenceLifetime = approvedLifetime ?? TimeSpan.FromDays(30),
        ReviewEvidenceLifetime = reviewLifetime ?? TimeSpan.FromDays(2)
    };

    private static byte[] Payload(
        string applicantId,
        string inspectionId,
        string reviewStatus,
        string? reviewAnswer)
    {
        var answer = reviewAnswer is null ? string.Empty : $",\"reviewResult\":{{\"reviewAnswer\":\"{reviewAnswer}\"}}";
        return Encoding.UTF8.GetBytes(
            $"{{\"applicantId\":\"{applicantId}\",\"inspectionId\":\"{inspectionId}\",\"type\":\"review\",\"reviewStatus\":\"{reviewStatus}\"{answer}}}");
    }

    private static byte[] PayloadWithoutInspection(
        string applicantId,
        string reviewStatus,
        string? reviewAnswer,
        bool includeType)
    {
        var type = includeType ? ",\"type\":\"review\"" : string.Empty;
        var answer = reviewAnswer is null ? string.Empty : $",\"reviewResult\":{{\"reviewAnswer\":\"{reviewAnswer}\"}}";
        return Encoding.UTF8.GetBytes(
            $"{{\"applicantId\":\"{applicantId}\"{type},\"reviewStatus\":\"{reviewStatus}\"{answer}}}");
    }

    private static TestDbContext Context()
    {
        var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new SumSubEvidenceModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NonDbApplicationContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubProvider : IKycAmlProvider
    {
        private int _applicantSequence;

        public List<KycAmlApplicantRequest> ApplicantRequests { get; } = [];
        public List<(string ExternalUserId, string LevelName, int Lifetime)> TokenRequests { get; } = [];
        public bool VerifyResult { get; set; } = true;
        public KycAmlState StatusState { get; set; } = KycAmlState.Approved;
        public string? StatusJurisdiction { get; set; } = "BRA";

        public Task<KycAmlApplicant> CreateApplicantAsync(KycAmlApplicantRequest request, CancellationToken cancellationToken)
        {
            ApplicantRequests.Add(request);
            return Task.FromResult(new KycAmlApplicant(
                $"applicant-{++_applicantSequence}", request.ExternalUserId, KycAmlState.Created));
        }

        public Task<KycAmlAccessToken> CreateAccessTokenAsync(
            string externalUserId,
            string levelName,
            int lifetimeSeconds,
            CancellationToken cancellationToken)
        {
            TokenRequests.Add((externalUserId, levelName, lifetimeSeconds));
            return Task.FromResult(new KycAmlAccessToken("token", externalUserId));
        }

        public Task<KycAmlStatus> GetStatusAsync(string applicantId, CancellationToken cancellationToken) =>
            Task.FromResult(new KycAmlStatus(applicantId, "external-user", StatusState, StatusJurisdiction));

        public bool VerifyWebhook(
            ReadOnlySpan<byte> rawPayload,
            string suppliedDigest,
            string digestAlgorithm,
            DateTimeOffset issuedAt,
            DateTimeOffset receivedAt) => VerifyResult;
    }

    private sealed class RecordingEvidenceStore : IKycEvidenceStore
    {
        public List<KycEvidenceSubmission> Envelopes { get; } = [];
        public KycEvidenceIngestionStatus NextStatus { get; set; } = KycEvidenceIngestionStatus.Published;

        public ValueTask<KycEvidenceIngestionResult> IngestAsync(
            KycEvidenceSubmission envelope,
            CancellationToken cancellationToken)
        {
            Envelopes.Add(envelope);
            Guid? evidenceId = NextStatus == KycEvidenceIngestionStatus.Published ? Guid.NewGuid() : null;
            var result = new KycEvidenceIngestionResult(NextStatus, evidenceId);
            NextStatus = KycEvidenceIngestionStatus.Published;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingRawStore : IComplianceRawObjectStore
    {
        public ValueTask<ComplianceRawObjectReference> PutAsync(
            string provider,
            string environment,
            string providerEventId,
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken)
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(payload.Span));
            return ValueTask.FromResult(new ComplianceRawObjectReference($"memory://{providerEventId}", hash));
        }
    }
}
