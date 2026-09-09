using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.UnitTests.Persistence;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class HardToSoftConversionWorkflowTests
{
    [Fact]
    public void RequestReceiptAndRejection_ExposeTheirExactValues()
    {
        var postingId = Guid.NewGuid();
        var feePostingId = Guid.NewGuid();
        var request = new SelfServiceHardToSoftConversionRequest(100, "conversion-key");
        var receipt = new SelfServiceHardToSoftConversionReceipt(postingId, feePostingId, 17, "journal-hash", true);
        var rejection = new EconomySelfServiceCommandRejectedException("rejected");

        request.PrincipalHardCoinUnits.Should().Be(100);
        request.IdempotencyKey.Should().Be("conversion-key");
        receipt.PrincipalPostingId.Should().Be(postingId);
        receipt.FeePostingId.Should().Be(feePostingId);
        receipt.JournalSequence.Should().Be(17);
        receipt.JournalHash.Should().Be("journal-hash");
        receipt.IsDuplicate.Should().BeTrue();
        rejection.Message.Should().Be("rejected");
    }

    [Fact]
    public void ParseRootIds_CanonicalizesValidIdsAndRejectsEveryMalformedShape()
    {
        var first = Guid.Parse("74000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("74000000-0000-0000-0000-000000000002");

        PostgreSqlHardToSoftConversionWorkflow.ParseRootIds($"[\"{second}\",\"{first}\"]")
            .Should().Equal(first, second);
        PostgreSqlHardToSoftConversionWorkflow.DeterministicGuid("principal", "conversion-key")
            .Should().Be(PostgreSqlHardToSoftConversionWorkflow.DeterministicGuid("principal", "conversion-key"));

        foreach (var sourceRoots in new[]
                 {
                     "",
                     "not-json",
                     "{}",
                     "[]",
                     "[\"not-a-guid\"]",
                     $"[\"{Guid.Empty}\"]",
                     $"[\"{first}\",\"{first}\"]"
                 })
        {
            Action act = () => PostgreSqlHardToSoftConversionWorkflow.ParseRootIds(sourceRoots);
            act.Should().Throw<EconomySelfServiceCommandRejectedException>();
        }
    }

    [Fact]
    public async Task ConvertAsync_RejectsInvalidRequestsBeforeAnyPersistenceWork()
    {
        await using var context = CreateContext();
        var accessor = new ActorContextAccessor();
        var enabled = new EnabledGate();
        var workflow = CreateWorkflow(context, accessor, enabled);
        var valid = Request();

        Func<Task> nullRequest = () => workflow.ConvertAsync(null!, CancellationToken.None);
        Func<Task> cancelled = () => workflow.ConvertAsync(valid, new CancellationToken(canceled: true));
        Func<Task> invalidPrincipal = () => workflow.ConvertAsync(valid with { PrincipalHardCoinUnits = 0 }, CancellationToken.None);
        Func<Task> emptyIdempotencyKey = () => workflow.ConvertAsync(valid with { IdempotencyKey = string.Empty }, CancellationToken.None);

        await nullRequest.Should().ThrowAsync<ArgumentNullException>();
        await cancelled.Should().ThrowAsync<OperationCanceledException>();
        await invalidPrincipal.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await emptyIdempotencyKey.Should().ThrowAsync<ArgumentException>();
        enabled.EnsuredCapabilities.Should().OnlyContain(capability => capability == EconomyValueMovementCapability.ConvertHardToSoft);
    }

    [Fact]
    public async Task ConvertAsync_RequiresTheCapabilityAndAnAuthenticatedTenantActor()
    {
        await using var context = CreateContext();
        var accessor = new ActorContextAccessor();
        var blocked = new EnabledGate(new EconomyValueMovementDisabledException("disabled"));
        var workflow = CreateWorkflow(context, accessor, blocked);

        Func<Task> disabled = () => workflow.ConvertAsync(Request(), CancellationToken.None);
        await disabled.Should().ThrowAsync<EconomyValueMovementDisabledException>();

        var enabled = new EnabledGate();
        workflow = CreateWorkflow(context, accessor, enabled);
        try
        {
            Func<Task> anonymous = () => workflow.ConvertAsync(Request(), CancellationToken.None);
            await anonymous.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            accessor.ClearActorContext();
        }
    }

    [Fact]
    public async Task ConvertAsync_RequiresFreshExternalRiskEvidenceBeforeLookingUpWalletState()
    {
        await using var context = CreateContext();
        var accessor = SetActorContext(out _, out _);
        var verifier = new RejectingEvidenceVerifier();
        var workflow = CreateWorkflow(context, accessor, new EnabledGate(), verifier);

        try
        {
            var act = () => workflow.ConvertAsync(Request(), CancellationToken.None);

            await act.Should().ThrowAsync<ExternalRiskEvidenceException>();
            verifier.Calls.Should().Be(1);
            context.Set<EconomyWalletRow>().Should().BeEmpty();
        }
        finally
        {
            accessor.ClearActorContext();
        }
    }

    [Fact]
    public async Task ConvertAsync_RequiresAnActiveWalletBeforeIssuingTheServerBoundRiskDecision()
    {
        await using var context = CreateContext();
        var accessor = SetActorContext(out var actorId, out var tenantId);
        var issuer = new RejectingRiskDecisionIssuer();
        var workflow = CreateWorkflow(context, accessor, new EnabledGate(), riskDecisionIssuer: issuer);
        var request = Request();

        try
        {
            Func<Task> missingWallet = () => workflow.ConvertAsync(request, CancellationToken.None);
            await missingWallet.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>()
                .WithMessage("*no active Economy wallet*");

            context.Set<EconomyWalletRow>().Add(new EconomyWalletRow
            {
                Id = Guid.NewGuid(),
                OwnerId = actorId,
                TenantId = tenantId,
                State = WalletLifecycleState.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            Func<Task> decisionRejected = () => workflow.ConvertAsync(request, CancellationToken.None);
            await decisionRejected.Should().ThrowAsync<EconomySelfServiceCommandRejectedException>()
                .WithMessage("*issuer rejected*");
            issuer.Requests.Should().ContainSingle().Which.Should().Match<HardToSoftConversionRiskDecisionRequest>(issued =>
                issued.ActorId == actorId && issued.TenantId == tenantId &&
                issued.TotalHardCoinUnits == request.PrincipalHardCoinUnits &&
                issued.PolicyVersion == 7 && issued.JurisdictionCode == "BRA");
        }
        finally
        {
            accessor.ClearActorContext();
        }
    }

    [Fact]
    public async Task ConvertAsync_ComposesTheAuthorizedWriterCallForZeroFeeAndFeeConversions()
    {
        await using var database = await PostgreSqlHardToSoftConversionGatewayTests.CreateDatabaseAsync();
        await using var context = PostgreSqlHardToSoftConversionGatewayTests.CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        await using var connection = new Npgsql.NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        var zeroFee = await ConvertWithPersistedWriterAsync(context, connection, 0);
        var withFee = await ConvertWithPersistedWriterAsync(context, connection, 1);

        zeroFee.FeePostingId.Should().BeNull();
        zeroFee.IsDuplicate.Should().BeFalse();
        zeroFee.JournalSequence.Should().BePositive();
        zeroFee.JournalHash.Should().NotBeNullOrWhiteSpace();
        withFee.FeePostingId.Should().NotBeNull();
        withFee.IsDuplicate.Should().BeFalse();
        withFee.JournalSequence.Should().BeGreaterThan(zeroFee.JournalSequence);
    }

    private static async Task<SelfServiceHardToSoftConversionReceipt> ConvertWithPersistedWriterAsync(
        ApplicationDbContext context,
        Npgsql.NpgsqlConnection connection,
        long feeHardCoinUnits)
    {
        var walletId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var hardLotId = Guid.NewGuid();
        var capabilityId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        var counterId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var key = $"workflow-conversion-{Guid.NewGuid():N}";
        var principalHardCoinUnits = 10L;
        var totalHardCoinUnits = principalHardCoinUnits + feeHardCoinUnits;
        var timestamp = DateTimeOffset.UtcNow;

        await PostgreSqlHardToSoftConversionGatewayTests.SeedAsync(
            connection,
            walletId,
            rootId,
            hardLotId,
            capabilityId,
            decisionId,
            counterId,
            actorId,
            tenantId,
            key,
            totalHardCoinUnits,
            key,
            timestamp,
            $"workflow-capability-{Guid.NewGuid():N}");
        await PostgreSqlHardToSoftConversionGatewayTests.ReserveRiskCounterAsync(
            connection,
            decisionId,
            counterId,
            totalHardCoinUnits,
            timestamp);

        var accessor = SetActorContext(actorId, tenantId);
        try
        {
            var workflow = CreateWorkflow(
                context,
                accessor,
                new EnabledGate(),
                policyResolver: new FixedPolicyResolver(feeHardCoinUnits),
                riskDecisionIssuer: new FixedRiskDecisionIssuer(decisionId, [rootId]));
            return await workflow.ConvertAsync(
                new SelfServiceHardToSoftConversionRequest(
                    principalHardCoinUnits,
                    key),
                CancellationToken.None);
        }
        finally
        {
            accessor.ClearActorContext();
        }
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static SelfServiceHardToSoftConversionRequest Request() => new(
        100,
        $"conversion-{Guid.NewGuid():N}");

    private static PostgreSqlHardToSoftConversionWorkflow CreateWorkflow(
        ApplicationDbContext context,
        ActorContextAccessor accessor,
        IEconomyValueMovementDecisionGate gate,
        IHardToSoftConversionRiskEvidenceVerifier? evidenceVerifier = null,
        IHardToSoftConversionPolicyResolver? policyResolver = null,
        IHardToSoftConversionRiskDecisionIssuer? riskDecisionIssuer = null) =>
        new(
            context,
            accessor,
            gate,
            evidenceVerifier ?? AllowingEvidenceVerifier.Instance,
            policyResolver ?? new FixedPolicyResolver(0),
            riskDecisionIssuer ?? new RejectingRiskDecisionIssuer());

    private static ActorContextAccessor SetActorContext(out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.NewGuid();
        tenantId = Guid.NewGuid();
        return SetActorContext(actorId, tenantId);
    }

    private static ActorContextAccessor SetActorContext(Guid actorId, Guid tenantId)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        return accessor;
    }

    private sealed class EnabledGate(Exception? exception = null) : IEconomyValueMovementDecisionGate
    {
        public List<EconomyValueMovementCapability> EnsuredCapabilities { get; } = [];

        public bool IsEnabled => exception is null;

        public bool IsCapabilityEnabled(EconomyValueMovementCapability capability) => exception is null;

        public void EnsureEnabled()
        {
            if (exception is not null)
                throw exception;
        }

        public void EnsureEnabled(EconomyValueMovementCapability capability)
        {
            EnsuredCapabilities.Add(capability);
            EnsureEnabled();
        }
    }

    private sealed class AllowingEvidenceVerifier : IHardToSoftConversionRiskEvidenceVerifier
    {
        public static readonly AllowingEvidenceVerifier Instance = new();

        public Task<IReadOnlyList<ExternalRiskEvidence>> VerifyAsync(
            Guid actorId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult<IReadOnlyList<ExternalRiskEvidence>>(
            [
                new(ExternalRiskSource.FinancialCrime, 1, now.AddMinutes(-1), now.AddMinutes(5), ExternalRiskOutcome.Allow, "financial-crime"),
                new(ExternalRiskSource.TrustSafety, 1, now.AddMinutes(-1), now.AddMinutes(5), ExternalRiskOutcome.Allow, "trust-safety")
            ]);
        }
    }

    private sealed class RejectingEvidenceVerifier : IHardToSoftConversionRiskEvidenceVerifier
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<ExternalRiskEvidence>> VerifyAsync(
            Guid actorId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new ExternalRiskEvidenceException("External fraud-control evidence did not allow conversion.");
        }
    }

    private sealed class RejectingRiskDecisionIssuer : IHardToSoftConversionRiskDecisionIssuer
    {
        public List<HardToSoftConversionRiskDecisionRequest> Requests { get; } = [];

        public Task<HardToSoftConversionRiskDecision> IssueAsync(
            HardToSoftConversionRiskDecisionRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            throw new EconomySelfServiceCommandRejectedException("The server-bound risk decision issuer rejected this conversion.");
        }
    }

    private sealed class FixedPolicyResolver(long feeHardCoinUnits) : IHardToSoftConversionPolicyResolver
    {
        public ValueTask<HardToSoftConversionPolicyAuthorization> ResolveAsync(
            Guid tenantId,
            Guid actorId,
            long principalHardCoinUnits,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new HardToSoftConversionPolicyAuthorization(
                "BRA", 7, "signed-policy-hash", feeHardCoinUnits, 1_000, 300));
        }
    }

    private sealed class FixedRiskDecisionIssuer(Guid decisionId, IReadOnlyList<Guid> roots)
        : IHardToSoftConversionRiskDecisionIssuer
    {
        public Task<HardToSoftConversionRiskDecision> IssueAsync(
            HardToSoftConversionRiskDecisionRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HardToSoftConversionRiskDecision(decisionId, roots));
        }
    }
}
