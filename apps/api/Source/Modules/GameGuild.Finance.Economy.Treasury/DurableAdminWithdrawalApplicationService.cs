using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Treasury;

public sealed record ProposeAdminWithdrawalCommand(
    Guid TenantId,
    Guid ActorId,
    DateOnly PeriodStart,
    long AmountUnits,
    string DestinationHash,
    string IdempotencyKey,
    ReauthenticationEvidence Reauthentication);

public sealed record ApproveAdminWithdrawalCommand(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    long ExpectedVersion);

public sealed record DispatchAdminWithdrawalCommand(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    long ExpectedVersion,
    ReauthenticationEvidence Reauthentication);

public sealed record ReconcileAdminWithdrawalCommand(
    Guid TenantId,
    Guid ActorId,
    Guid RunId);

public sealed record AdminWithdrawalAuditView(
    Guid RunId,
    bool IntegrityValid,
    IReadOnlyList<AdminWithdrawalAuditEvent> Events);

public interface IDurableAdminWithdrawalApplicationService
{
    ValueTask<AdminWithdrawalRun> ProposeAsync(
        ProposeAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminWithdrawalRun> ApproveAsync(
        ApproveAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminWithdrawalRun> DispatchAsync(
        DispatchAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default);

    Task<AdminWithdrawalRun> ReconcileAsync(
        ReconcileAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default);

    AdminWithdrawalRun Get(Guid tenantId, Guid runId);
    IReadOnlyList<AdminWithdrawalRun> List(Guid tenantId, int limit = 100);
    AdminWithdrawalAuditView Audit(Guid tenantId, Guid runId);
}

public sealed class DurableAdminWithdrawalApplicationService(
    IAdminWithdrawalStore runs,
    IAdminWithdrawalAuditTrail audit,
    IEconomyCapabilityPolicyStore policies,
    ICapabilityPolicySignatureVerifier policySignatureVerifier,
    IEconomyReserveCustodyControlPlane reserves,
    IEconomyWalletDirectory wallets,
    IAdminWithdrawalFencingTokenAllocator fencingTokens,
    IFifoFragmentReservationReader reservationReader,
    IDurableAdminWithdrawalWorkflow workflow,
    IAdminWithdrawalProvider provider,
    IEconomyJurisdictionResolver jurisdictionResolver,
    TimeProvider timeProvider) : IDurableAdminWithdrawalApplicationService
{
    private const long UsdNanosPerHardUnit = 10_000_000;

    public async ValueTask<AdminWithdrawalRun> ProposeAsync(
        ProposeAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateProposal(command);
        var destinationHash = NormalizeHash(command.DestinationHash, nameof(command.DestinationHash));
        var idempotencyKey = new IdempotencyKey(command.IdempotencyKey.Trim());
        var now = timeProvider.GetUtcNow();
        var transactionBinding = TreasuryProtectedOperationBinding.Proposal(
            command.PeriodStart, command.AmountUnits, destinationHash, idempotencyKey.Value);
        var reauthentication = ReauthenticationEvidenceValidator.RequireFresh(
            command.Reauthentication,
            command.ActorId,
            ProtectedOperationKind.AdministrativeAdjustment,
            transactionBinding,
            ReauthenticationAssurance.MultiFactor,
            now);
        var jurisdiction = await jurisdictionResolver.ResolveAsync(
            command.TenantId, command.ActorId, null, null, now, cancellationToken)
            .ConfigureAwait(false);
        var requestHash = Hash(Canonicalize(new
        {
            tenant_id = command.TenantId,
            actor_id = command.ActorId,
            period_start = command.PeriodStart,
            amount_units = command.AmountUnits,
            destination_hash = destinationHash,
            jurisdiction_code = jurisdiction.JurisdictionCode,
            idempotency_key = idempotencyKey.Value
        }));
        var replay = runs.FindReplay(command.TenantId, idempotencyKey.Value, requestHash);
        if (replay is not null) return replay;

        var (policy, executionPolicy) = await LoadPolicyAsync(
            command.TenantId, jurisdiction.JurisdictionCode, now, cancellationToken).ConfigureAwait(false);
        if (command.AmountUnits < executionPolicy.MinimumAmountUnits ||
            command.AmountUnits > executionPolicy.MaximumAmountUnits)
            throw new AdminWithdrawalEligibilityException(
                "Administrative withdrawal amount is outside the signed policy limits.");
        if (!executionPolicy.DestinationHashes.Contains(destinationHash, StringComparer.Ordinal))
            throw new AdminWithdrawalEligibilityException(
                "Administrative withdrawal destination is not allowed by the signed policy.");

        var wallet = await wallets.GetWalletAsync(
            command.TenantId, executionPolicy.PlatformFeeWalletId, cancellationToken).ConfigureAwait(false);
        var reserve = await reserves.CurrentHeadAsync(now, cancellationToken).ConfigureAwait(false);
        EnsureReserveAssetCanCover(reserve, executionPolicy.SourceAssetKey, command.AmountUnits);
        var fencingToken = await fencingTokens.AllocateAsync(cancellationToken).ConfigureAwait(false);
        var run = new AdminWithdrawalRun(
            Guid.NewGuid(),
            command.TenantId,
            idempotencyKey,
            requestHash,
            command.PeriodStart,
            command.ActorId,
            null,
            wallet.WalletId,
            new CoinAmount(CurrencyCode.HardCoin, command.AmountUnits),
            executionPolicy.SourceAssetKey,
            destinationHash,
            AdminWithdrawalRunState.PendingApproval,
            1,
            fencingToken,
            fencingToken,
            reserve.Version,
            reserve.AuthorizationEpoch,
            new PolicyVersion(policy.Version),
            null,
            null,
            now,
            now);
        return await workflow.ReserveAsync(
            new DurableAdminWithdrawalReservationRequest(
                run,
                jurisdiction.JurisdictionCode,
                reauthentication.EvidenceHash,
                executionPolicy.ProviderHash),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<AdminWithdrawalRun> ApproveAsync(
        ApproveAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty || command.RunId == Guid.Empty)
            throw new ArgumentException("Tenant, actor, and withdrawal run are required.", nameof(command));
        if (command.ExpectedVersion <= 0) throw new ArgumentOutOfRangeException(nameof(command));
        return workflow.ApproveAsync(
            new DurableAdminWithdrawalApprovalRequest(
                command.TenantId,
                command.RunId,
                command.ExpectedVersion,
                command.ActorId,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public async Task<AdminWithdrawalRun> DispatchAsync(
        DispatchAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateDispatch(command);
        var run = runs.Get(command.TenantId, command.RunId);
        if (run.State != AdminWithdrawalRunState.Approved || run.Version != command.ExpectedVersion)
            throw new AdminWithdrawalStaleCommandException(
                "Administrative withdrawal is not approved at the requested version.");
        var authorization = ReadAuthorizationSnapshot(run);
        var now = timeProvider.GetUtcNow();
        var transactionBinding = TreasuryProtectedOperationBinding.Dispatch(
            command.RunId, command.ExpectedVersion);
        var reauthentication = ReauthenticationEvidenceValidator.RequireFresh(
            command.Reauthentication,
            command.ActorId,
            ProtectedOperationKind.AdministrativeAdjustment,
            transactionBinding,
            ReauthenticationAssurance.MultiFactor,
            now);
        var (policy, executionPolicy) = await LoadPolicyAsync(
            run.TenantId, authorization.JurisdictionCode, now, cancellationToken).ConfigureAwait(false);
        if (policy.Version != run.PolicyVersion.Value ||
            authorization.PolicyVersion != run.PolicyVersion.Value ||
            !string.Equals(executionPolicy.ProviderHash, authorization.ProviderHash, StringComparison.Ordinal) ||
            !executionPolicy.DestinationHashes.Contains(run.DestinationHash, StringComparer.Ordinal))
            throw new AdminWithdrawalStaleCommandException(
                "The signed Treasury policy changed before dispatch.");

        var reserve = await reserves.CurrentHeadAsync(now, cancellationToken).ConfigureAwait(false);
        if (reserve.Version != run.ReserveVersion ||
            reserve.AuthorizationEpoch != run.ReserveAuthorizationEpoch ||
            authorization.ReserveVersion != run.ReserveVersion.Value)
            throw new AdminWithdrawalStaleCommandException(
                "The authoritative reserve snapshot changed before dispatch.");
        EnsureReserveAssetCanCover(reserve, run.SourceAssetKey, run.Amount.Units);

        var fragments = reservationReader.Read(
            run.Id, PersistedFragmentReservationStatus.Reserved);
        if (fragments.Count == 0 || fragments.Sum(fragment => fragment.Amount.Units) != run.Amount.Units)
            throw new AdminWithdrawalStaleCommandException(
                "Administrative withdrawal FIFO reservations are missing or incomplete.");
        var sourceRoots = fragments.Select(fragment => fragment.RootSourceStampId)
            .Distinct().OrderBy(root => root.Value).ToArray();
        var rootHashes = sourceRoots.Select(root => Hash(root.Value.ToString("N"))).ToArray();
        if (!rootHashes.SequenceEqual(authorization.SourceRootHashes, StringComparer.Ordinal))
            throw new AdminWithdrawalStaleCommandException(
                "Administrative withdrawal source provenance changed before dispatch.");

        return await workflow.BeginDispatchAsync(
            new DurableAdminWithdrawalDispatchRequest(
                run.TenantId,
                run.Id,
                run.Version,
                run.FencingToken,
                run.ExecutionEpoch,
                now,
                command.ActorId,
                authorization.JurisdictionCode,
                reauthentication.EvidenceHash,
                authorization.ProviderHash,
                sourceRoots),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminWithdrawalRun> ReconcileAsync(
        ReconcileAdminWithdrawalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty || command.RunId == Guid.Empty)
            throw new ArgumentException("Tenant, actor, and withdrawal run are required.", nameof(command));
        var run = runs.Get(command.TenantId, command.RunId);
        if (run.State is not (AdminWithdrawalRunState.Dispatching or AdminWithdrawalRunState.Ambiguous))
            throw new AdminWithdrawalStaleCommandException(
                "Only a dispatched or ambiguous withdrawal can be reconciled.");
        var providerEvent = await provider.ReconcileAsync(
            run.TenantId,
            run.Id,
            run.IdempotencyKey.Value + ":dispatch",
            run.ProviderTransferId,
            cancellationToken).ConfigureAwait(false);
        if (providerEvent.Outcome is not (
                AdminWithdrawalProviderOutcome.Succeeded or AdminWithdrawalProviderOutcome.Failed))
            return runs.Get(run.TenantId, run.Id);
        return await workflow.ApplyProviderEventAsync(
            new DurableAdminWithdrawalProviderEventRequest(providerEvent),
            cancellationToken).ConfigureAwait(false);
    }

    public AdminWithdrawalRun Get(Guid tenantId, Guid runId) => runs.Get(tenantId, runId);

    public IReadOnlyList<AdminWithdrawalRun> List(Guid tenantId, int limit = 100) =>
        runs.List(tenantId, limit);

    public AdminWithdrawalAuditView Audit(Guid tenantId, Guid runId)
    {
        _ = runs.Get(tenantId, runId);
        var events = audit.Events(tenantId, runId);
        return new AdminWithdrawalAuditView(runId, audit.Verify(tenantId, runId), events);
    }

    private async ValueTask<(EconomyCapabilityPolicy Policy, AdminWithdrawalExecutionPolicy Parsed)> LoadPolicyAsync(
        Guid tenantId,
        string jurisdiction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var policy = await policies.CurrentAsync(
                tenantId, EconomyValueMovementCapability.AdminWithdrawalExecution,
                jurisdiction, cancellationToken).ConfigureAwait(false)
            ?? await policies.CurrentAsync(
                null, EconomyValueMovementCapability.AdminWithdrawalExecution,
                jurisdiction, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminWithdrawalExecutionDisabledException(
                "No signed Treasury execution policy is active for the jurisdiction.");
        if (policy.State != EconomyCapabilityPolicyState.Active ||
            policy.EffectiveAt > now || policy.ExpiresAt <= now || !policy.ProviderReady)
            throw new AdminWithdrawalExecutionDisabledException(
                "Treasury execution policy or provider readiness is unavailable.");
        if (!string.Equals(Hash(policy.CanonicalPayload), policy.PayloadHash, StringComparison.Ordinal) ||
            !await policySignatureVerifier.VerifyAsync(
                policy.CanonicalPayload, policy.KeyId, policy.Signature, cancellationToken).ConfigureAwait(false))
            throw new AdminWithdrawalExecutionDisabledException(
                "Treasury execution policy signature is invalid.");
        return (policy, ParsePolicy(policy.CanonicalPayload));
    }

    private AdminWithdrawalAuthorizationSnapshot ReadAuthorizationSnapshot(AdminWithdrawalRun run)
    {
        if (!audit.Verify(run.TenantId, run.Id))
            throw new AdminWithdrawalEvidenceException(
                "The administrative-withdrawal audit trail is invalid.");
        var reserved = audit.Events(run.TenantId, run.Id)
            .SingleOrDefault(item => item.Kind == "reserved")
            ?? throw new AdminWithdrawalEvidenceException(
                "The administrative-withdrawal authorization snapshot is missing.");
        try
        {
            var snapshot = JsonSerializer.Deserialize<AdminWithdrawalAuthorizationSnapshot>(reserved.Evidence)
                ?? throw new JsonException("Authorization snapshot is empty.");
            if (snapshot.RequestHash != run.RequestHash ||
                snapshot.PolicyVersion != run.PolicyVersion.Value ||
                snapshot.ReserveVersion != run.ReserveVersion.Value ||
                snapshot.DestinationHash != run.DestinationHash ||
                string.IsNullOrWhiteSpace(snapshot.SubjectReference) ||
                string.IsNullOrWhiteSpace(snapshot.JurisdictionCode) ||
                string.IsNullOrWhiteSpace(snapshot.ProviderHash) ||
                string.IsNullOrWhiteSpace(snapshot.OperationFingerprintHash) ||
                string.IsNullOrWhiteSpace(snapshot.ReauthenticationEvidenceHash) ||
                string.IsNullOrWhiteSpace(snapshot.ReceiptHash) ||
                snapshot.SourceRootHashes.Count == 0)
                throw new AdminWithdrawalEvidenceException(
                    "The administrative-withdrawal authorization snapshot is not bound to the run.");
            return snapshot;
        }
        catch (JsonException exception)
        {
            throw new AdminWithdrawalEvidenceException(
                $"The administrative-withdrawal authorization snapshot is invalid: {exception.Message}");
        }
    }

    private static AdminWithdrawalExecutionPolicy ParsePolicy(string canonicalPayload)
    {
        try
        {
            using var document = JsonDocument.Parse(canonicalPayload);
            var root = document.RootElement;
            var wallet = new WalletId(root.GetProperty("platformFeeWalletId").GetGuid());
            var source = root.GetProperty("sourceAssetKey").GetString();
            var providerHash = root.GetProperty("providerHash").GetString();
            var minimum = root.GetProperty("minimumAmountUnits").GetInt64();
            var maximum = root.GetProperty("maximumAmountUnits").GetInt64();
            var destinations = root.GetProperty("destinationHashes").EnumerateArray()
                .Select(value => NormalizeHash(value.GetString() ?? string.Empty, "destinationHashes"))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (wallet.Value == Guid.Empty || string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(providerHash) || minimum <= 0 || maximum < minimum ||
                destinations.Length == 0)
                throw new JsonException("Treasury policy values are incomplete.");
            return new AdminWithdrawalExecutionPolicy(
                wallet,
                source.Trim(),
                NormalizeHash(providerHash, "providerHash"),
                minimum,
                maximum,
                destinations);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
                                          FormatException or KeyNotFoundException or ArgumentException)
        {
            throw new AdminWithdrawalExecutionDisabledException(
                $"Treasury execution policy payload is invalid: {exception.Message}");
        }
    }

    private static void EnsureReserveAssetCanCover(
        ReserveHead reserve,
        string sourceAssetKey,
        long amountUnits)
    {
        var asset = reserve.AssetAllocations.SingleOrDefault(allocation =>
            allocation.Purpose == ReserveBackingPurpose.HardCoin &&
            string.Equals(allocation.AssetKey, sourceAssetKey, StringComparison.Ordinal));
        long requiredUsdNanos;
        try
        {
            requiredUsdNanos = checked(amountUnits * UsdNanosPerHardUnit);
        }
        catch (OverflowException exception)
        {
            throw new AdminWithdrawalEligibilityException(
                $"Administrative withdrawal amount exceeds reserve arithmetic limits: {exception.Message}");
        }
        if (asset is null || asset.EligibleUsdNanos < requiredUsdNanos)
            throw new ReserveShortfallException(
                "The signed reserve asset allocation cannot cover this administrative withdrawal.");
    }

    private static void ValidateProposal(ProposeAdminWithdrawalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty)
            throw new ArgumentException("Tenant and actor are required.", nameof(command));
        if (command.PeriodStart.Day != 1)
            throw new ArgumentException("Withdrawal period must start on the first day of a month.", nameof(command));
        if (command.AmountUnits <= 0) throw new ArgumentOutOfRangeException(nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.DestinationHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.IdempotencyKey);
        ArgumentNullException.ThrowIfNull(command.Reauthentication);
    }

    private static void ValidateDispatch(DispatchAdminWithdrawalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty || command.RunId == Guid.Empty)
            throw new ArgumentException("Tenant, actor, and run are required.", nameof(command));
        if (command.ExpectedVersion <= 0) throw new ArgumentOutOfRangeException(nameof(command));
        ArgumentNullException.ThrowIfNull(command.Reauthentication);
    }

    private static string NormalizeHash(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 128)
            throw new ArgumentOutOfRangeException(parameterName, "Hashes cannot exceed 128 characters.");
        return normalized;
    }

    private static string Canonicalize<T>(T value) =>
        EconomyCanonicalJson.Serialize(JsonSerializer.SerializeToElement(value));

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record AdminWithdrawalExecutionPolicy(
        WalletId PlatformFeeWalletId,
        string SourceAssetKey,
        string ProviderHash,
        long MinimumAmountUnits,
        long MaximumAmountUnits,
        IReadOnlyList<string> DestinationHashes);
}
