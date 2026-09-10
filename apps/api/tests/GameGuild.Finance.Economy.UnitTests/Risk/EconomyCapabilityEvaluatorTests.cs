using FluentAssertions;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class EconomyCapabilityEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<EconomyCapabilityControlPlaneSnapshot, EconomyCapabilityReadinessStatus> DeniedSnapshots =>
        new()
        {
            { ReadySnapshot() with { KillSwitchActive = true }, EconomyCapabilityReadinessStatus.KillSwitchActive },
            { ReadySnapshot() with { HasActivePolicy = false }, EconomyCapabilityReadinessStatus.Disabled },
            { ReadySnapshot() with { PolicySignatureValid = false }, EconomyCapabilityReadinessStatus.InvalidPolicy },
            { ReadySnapshot() with { PolicyExpiresAt = Now }, EconomyCapabilityReadinessStatus.InvalidPolicy },
            { ReadySnapshot() with { JurisdictionAllowed = false }, EconomyCapabilityReadinessStatus.JurisdictionBlocked },
            { ReadySnapshot() with { ComplianceAvailable = false }, EconomyCapabilityReadinessStatus.ComplianceUnavailable },
            { ReadySnapshot() with { ComplianceExpiresAt = Now }, EconomyCapabilityReadinessStatus.ComplianceStale },
            { ReadySnapshot() with { ManualReviewRequired = true }, EconomyCapabilityReadinessStatus.ReviewRequired },
            { ReadySnapshot() with { LedgerHealthy = false }, EconomyCapabilityReadinessStatus.LedgerUnhealthy },
            { ReadySnapshot() with { ProjectionMatches = false }, EconomyCapabilityReadinessStatus.ProjectionMismatch },
            { ReadySnapshot() with { ReserveSufficient = false }, EconomyCapabilityReadinessStatus.ReserveInsufficient },
            { ReadySnapshot() with { ReserveExpiresAt = Now }, EconomyCapabilityReadinessStatus.ReserveInsufficient },
            { ReadySnapshot() with { CustodyReconciled = false }, EconomyCapabilityReadinessStatus.CustodyUnreconciled },
            { ReadySnapshot() with { AnchorValid = false }, EconomyCapabilityReadinessStatus.AnchorInvalid },
            { ReadySnapshot() with { AnchorExpiresAt = Now }, EconomyCapabilityReadinessStatus.AnchorInvalid },
            { ReadySnapshot() with { ProviderReady = false }, EconomyCapabilityReadinessStatus.ProviderNotReady }
        };

    [Theory]
    [MemberData(nameof(DeniedSnapshots))]
    public async Task EvaluateAsyncFailsClosedWithoutIssuingReceipt(
        EconomyCapabilityControlPlaneSnapshot snapshot,
        EconomyCapabilityReadinessStatus expected)
    {
        var store = new StubControlPlaneStore(snapshot);
        var signer = new StubReceiptSigner();
        var evaluator = new EconomyCapabilityEvaluator(store, signer);

        var result = await evaluator.EvaluateAsync(Context(), CancellationToken.None);

        result.State.Should().Be(expected);
        result.Receipt.Should().BeNull();
        result.Diagnostics.Should().ContainSingle();
        store.Persisted.Should().BeNull();
        signer.Payload.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(DeniedSnapshots))]
    public async Task ReadinessInspectorReportsDenialWithoutIssuingReceipt(
        EconomyCapabilityControlPlaneSnapshot snapshot,
        EconomyCapabilityReadinessStatus expected)
    {
        var store = new StubControlPlaneStore(snapshot);
        var inspector = new EconomyCapabilityReadinessInspector(store);

        var result = await inspector.InspectAsync(Context(), CancellationToken.None);

        result.State.Should().Be(expected);
        result.Receipt.Should().BeNull();
        result.Diagnostics.Should().ContainSingle();
        store.Persisted.Should().BeNull();
    }

    [Fact]
    public async Task ReadinessInspectorReportsReadyWithoutSigningOrPersistingReceipt()
    {
        var store = new StubControlPlaneStore(ReadySnapshot());
        var inspector = new EconomyCapabilityReadinessInspector(store);

        var result = await inspector.InspectAsync(Context(), CancellationToken.None);

        result.State.Should().Be(EconomyCapabilityReadinessStatus.Ready);
        result.IsReady.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Receipt.Should().BeNull();
        store.Persisted.Should().BeNull();
    }

    [Fact]
    public async Task ReadinessInspectorValidatesContextAndDurableRiskDecision()
    {
        FluentActions.Invoking(() => new EconomyCapabilityReadinessInspector(null!))
            .Should().Throw<ArgumentNullException>();

        var inspector = new EconomyCapabilityReadinessInspector(
            new StubControlPlaneStore(ReadySnapshot()));
        await FluentActions.Awaiting(() => inspector.InspectAsync(null!, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();

        var mismatched = new EconomyCapabilityReadinessInspector(
            new StubControlPlaneStore(ReadySnapshot() with { RiskDecisionId = Guid.NewGuid() }));
        var result = await mismatched.InspectAsync(Context(), CancellationToken.None);

        result.State.Should().Be(EconomyCapabilityReadinessStatus.ReviewRequired);
        result.Receipt.Should().BeNull();
        result.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("risk decision");
    }

    [Fact]
    public async Task EvaluateAsyncIssuesSignedDurableReceiptForReadySnapshot()
    {
        var snapshot = ReadySnapshot();
        var store = new StubControlPlaneStore(snapshot);
        var signer = new StubReceiptSigner();
        var evaluator = new EconomyCapabilityEvaluator(store, signer);

        var result = await evaluator.EvaluateAsync(Context(), CancellationToken.None);

        result.State.Should().Be(EconomyCapabilityReadinessStatus.Ready);
        result.Diagnostics.Should().BeEmpty();
        result.Receipt.Should().NotBeNull();
        result.Receipt!.TenantId.Should().Be(Context().TenantId);
        result.Receipt.ActorId.Should().Be(Context().ActorId);
        result.Receipt.PolicyVersion.Should().Be(7);
        result.Receipt.ReserveVersion.Should().Be(11);
        result.Receipt.RiskDecisionId.Should().Be(snapshot.RiskDecisionId);
        result.Receipt.KillSwitchEpoch.Should().Be(13);
        result.Receipt.JurisdictionCode.Should().Be("BR");
        result.Receipt.ProviderHash.Should().Be("provider-hash");
        result.Receipt.DestinationHash.Should().Be("destination-hash");
        result.Receipt.SourceRootHashes.Should().Equal("root-a", "root-b");
        result.Receipt.KeyId.Should().Be("kms-key-1");
        result.Receipt.Signature.Should().Be("signature");
        result.Receipt.IssuedAt.Should().Be(Now);
        result.Receipt.ExpiresAt.Should().Be(Now.AddMinutes(5));
        result.Receipt.ReceiptHash.Should().HaveLength(64);
        store.Persisted.Should().BeSameAs(result.Receipt);
        signer.Payload.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EvaluateAsyncUsesEarliestEvidenceExpiry()
    {
        var snapshot = ReadySnapshot() with
        {
            PolicyExpiresAt = Now.AddMinutes(9),
            ComplianceExpiresAt = Now.AddMinutes(8),
            ReserveExpiresAt = Now.AddMinutes(7),
            AnchorExpiresAt = Now.AddMinutes(6)
        };
        var evaluator = new EconomyCapabilityEvaluator(
            new StubControlPlaneStore(snapshot),
            new StubReceiptSigner());

        var result = await evaluator.EvaluateAsync(Context(), CancellationToken.None);

        result.Receipt!.ExpiresAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public async Task EvaluateAsyncRejectsInvalidInputsAndInconsistentReadySnapshot()
    {
        var evaluator = new EconomyCapabilityEvaluator(
            new StubControlPlaneStore(ReadySnapshot()),
            new StubReceiptSigner());

        await FluentActions.Awaiting(() => evaluator.EvaluateAsync(null!, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        FluentActions.Invoking(() => new EconomyCapabilityEvaluator(null!, new StubReceiptSigner()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new EconomyCapabilityEvaluator(
                new StubControlPlaneStore(ReadySnapshot()), null!))
            .Should().Throw<ArgumentNullException>();

        var invalid = new EconomyCapabilityEvaluator(
            new StubControlPlaneStore(ReadySnapshot() with { RiskDecisionId = Guid.Empty }),
            new StubReceiptSigner());
        await FluentActions.Awaiting(() => invalid.EvaluateAsync(Context(), CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiptConsumptionRequiresExactBindingCurrentEpochAndSingleUse()
    {
        var store = new StubControlPlaneStore(ReadySnapshot());
        var evaluator = new EconomyCapabilityEvaluator(store, new StubReceiptSigner());
        var receipt = (await evaluator.EvaluateAsync(Context(), CancellationToken.None)).Receipt!;

        await store.ConsumeAsync(
            receipt.Id,
            Context().OperationFingerprint,
            Context().TenantId,
            Context().ActorId,
            receipt.KillSwitchEpoch,
            Now.AddSeconds(1),
            CancellationToken.None);

        await FluentActions.Awaiting(() => store.ConsumeAsync(
                receipt.Id,
                Context().OperationFingerprint,
                Context().TenantId,
                Context().ActorId,
                receipt.KillSwitchEpoch,
                Now.AddSeconds(2),
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<CapabilityReceiptConsumptionException>();
    }

    [Fact]
    public async Task AuthorizationServiceConsumesReadyReceiptAndRejectsDeniedEvaluation()
    {
        var receipt = (await new EconomyCapabilityEvaluator(
                new StubControlPlaneStore(ReadySnapshot()), new StubReceiptSigner())
            .EvaluateAsync(Context(), CancellationToken.None)).Receipt!;
        var store = new CapturingConsumptionStore();
        var service = new EconomyCapabilityAuthorizationService(
            new StubEvaluator(new EconomyCapabilityEvaluationResult(
                EconomyCapabilityReadinessStatus.Ready, [], receipt)), store);

        var authorized = await service.AuthorizeAndConsumeAsync(Context(), CancellationToken.None);

        authorized.Should().BeSameAs(receipt);
        store.Consumed.Should().Be((receipt.Id, receipt.OperationFingerprint, receipt.TenantId,
            receipt.ActorId, receipt.KillSwitchEpoch, Context().EvaluatedAt));
        await FluentActions.Awaiting(() => service.AuthorizeAndConsumeAsync(null!, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();

        var denied = new EconomyCapabilityAuthorizationService(
            new StubEvaluator(new EconomyCapabilityEvaluationResult(
                EconomyCapabilityReadinessStatus.ComplianceUnavailable, ["compliance unavailable"], null)), store);
        var deniedException = await FluentActions.Awaiting(() =>
                denied.AuthorizeAndConsumeAsync(Context(), CancellationToken.None).AsTask())
            .Should().ThrowAsync<EconomyCapabilityAuthorizationException>();
        deniedException.Which.State.Should().Be(EconomyCapabilityReadinessStatus.ComplianceUnavailable);
        deniedException.Which.Diagnostics.Should().Equal("compliance unavailable");
        deniedException.Which.Message.Should().Be("compliance unavailable");

        var emptyDiagnostics = new EconomyCapabilityAuthorizationException(
            EconomyCapabilityReadinessStatus.Disabled, []);
        emptyDiagnostics.Message.Should().Contain("Disabled");
        emptyDiagnostics.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ReceiptConsumptionExceptionPreservesDatabaseFailure()
    {
        var inner = new InvalidOperationException("database failure");

        var exception = new CapabilityReceiptConsumptionException("receipt failure", inner);

        exception.Message.Should().Be("receipt failure");
        exception.InnerException.Should().BeSameAs(inner);
    }

    private static EconomyCapabilityEvaluationContext Context() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "wallet:opaque-subject",
        "BR",
        EconomyValueMovementCapability.PayoutExecution,
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        "operation-fingerprint",
        "provider-hash",
        "destination-hash",
        ["root-a", "root-b"],
        Now);

    private static EconomyCapabilityControlPlaneSnapshot ReadySnapshot() => new(
        HasActivePolicy: true,
        PolicySignatureValid: true,
        PolicyVersion: 7,
        PolicyExpiresAt: Now.AddMinutes(10),
        JurisdictionAllowed: true,
        ComplianceAvailable: true,
        ComplianceExpiresAt: Now.AddMinutes(10),
        ManualReviewRequired: false,
        RiskDecisionId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
        LedgerHealthy: true,
        ProjectionMatches: true,
        ReserveSufficient: true,
        ReserveVersion: 11,
        ReserveExpiresAt: Now.AddMinutes(10),
        CustodyReconciled: true,
        AnchorValid: true,
        AnchorExpiresAt: Now.AddMinutes(10),
        ProviderReady: true,
        KillSwitchActive: false,
        KillSwitchEpoch: 13);

    private sealed class StubReceiptSigner : ICapabilityReceiptSigner
    {
        public string? Payload { get; private set; }

        public ValueTask<CapabilityReceiptSignature> SignAsync(
            string canonicalPayload,
            CancellationToken cancellationToken)
        {
            Payload = canonicalPayload;
            return ValueTask.FromResult(new CapabilityReceiptSignature("kms-key-1", "signature"));
        }
    }

    private sealed class StubEvaluator(EconomyCapabilityEvaluationResult result) : IEconomyCapabilityEvaluator
    {
        public ValueTask<EconomyCapabilityEvaluationResult> EvaluateAsync(
            EconomyCapabilityEvaluationContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    private sealed class CapturingConsumptionStore : IEconomyCapabilityControlPlaneStore
    {
        public (Guid ReceiptId, string Fingerprint, Guid Tenant, Guid Actor, long Epoch, DateTimeOffset At)? Consumed { get; private set; }

        public ValueTask<EconomyCapabilityControlPlaneSnapshot> ReadSnapshotAsync(
            EconomyCapabilityEvaluationContext context,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask PersistReceiptAsync(
            CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask ConsumeAsync(
            Guid receiptId,
            string operationFingerprint,
            Guid tenantId,
            Guid actorId,
            long currentKillSwitchEpoch,
            DateTimeOffset consumedAt,
            CancellationToken cancellationToken)
        {
            Consumed = (receiptId, operationFingerprint, tenantId, actorId, currentKillSwitchEpoch, consumedAt);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubControlPlaneStore(EconomyCapabilityControlPlaneSnapshot snapshot)
        : IEconomyCapabilityControlPlaneStore
    {
        private readonly HashSet<Guid> _consumed = [];

        public CapabilityAuthorizationReceipt? Persisted { get; private set; }

        public ValueTask<EconomyCapabilityControlPlaneSnapshot> ReadSnapshotAsync(
            EconomyCapabilityEvaluationContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(snapshot);

        public ValueTask PersistReceiptAsync(
            CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken)
        {
            Persisted = receipt;
            return ValueTask.CompletedTask;
        }

        public ValueTask ConsumeAsync(
            Guid receiptId,
            string operationFingerprint,
            Guid tenantId,
            Guid actorId,
            long currentKillSwitchEpoch,
            DateTimeOffset consumedAt,
            CancellationToken cancellationToken)
        {
            if (Persisted is null || Persisted.Id != receiptId ||
                Persisted.OperationFingerprint != operationFingerprint ||
                Persisted.TenantId != tenantId || Persisted.ActorId != actorId ||
                Persisted.KillSwitchEpoch != currentKillSwitchEpoch ||
                consumedAt >= Persisted.ExpiresAt || !_consumed.Add(receiptId))
                throw new CapabilityReceiptConsumptionException("The capability receipt is not consumable.");

            return ValueTask.CompletedTask;
        }
    }
}
