using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record ProviderEvidencePostingAuthorityRequest(
    string CapabilityName,
    Guid TenantId,
    Guid ActorId,
    WalletId TenantWalletId,
    PostingTemplateKind TemplateKind,
    CoinAmount Amount,
    PolicyVersion PolicyVersion,
    ReserveVersion ReserveVersion,
    long ReserveAuthorizationEpoch,
    long KillSwitchEpoch,
    string OperationFingerprint,
    string ProviderReferenceHash,
    string EvidenceHash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public interface IProviderEvidencePostingAuthorityIssuer
{
    ValueTask<RegisteredPostingAuthority> IssueAsync(
        ProviderEvidencePostingAuthorityRequest request,
        CancellationToken cancellationToken = default);

    ValueTask ConsumeAsync(
        RegisteredPostingAuthority authority,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Issues the one-use writer authority required to record an already-observed provider outcome.
/// This path does not authorize a new external movement: it only recognizes signed terminal
/// evidence for a previously fenced operation.
/// </summary>
public sealed class PostgreSqlProviderEvidencePostingAuthorityIssuer :
    IProviderEvidencePostingAuthorityIssuer
{
    private const long CounterVersion = 1;
    private readonly DbContext _db;

    public PostgreSqlProviderEvidencePostingAuthorityIssuer(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Provider evidence posting authority requires the application's relational DbContext.");
    }

    public async ValueTask<RegisteredPostingAuthority> IssueAsync(
        ProviderEvidencePostingAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var capabilityName = request.CapabilityName.Trim();
        var operationFingerprint = NormalizeHash(request.OperationFingerprint, nameof(request.OperationFingerprint));
        var providerReferenceHash = NormalizeHash(request.ProviderReferenceHash, nameof(request.ProviderReferenceHash));
        var evidenceHash = NormalizeHash(request.EvidenceHash, nameof(request.EvidenceHash));
        var riskDecisionId = DeterministicGuid(
            request.TenantId, operationFingerprint, request.TemplateKind, "provider-risk-decision");
        var counterId = DeterministicGuid(
            request.TenantId, operationFingerprint, request.TemplateKind, "provider-risk-counter");
        var reservationId = DeterministicGuid(
            request.TenantId, operationFingerprint, request.TemplateKind, "provider-risk-reservation");

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
            var capability = await _db.Set<EconomyRegisteredCapabilityRow>()
                .SingleOrDefaultAsync(row => row.Name == capabilityName, cancellationToken)
                .ConfigureAwait(false);
            if (capability is null || !capability.IsEnabled || capability.RevokedAt.HasValue ||
                !Allows(capability.AllowedTemplateKinds, request.TemplateKind))
                throw new RegisteredPostingCapabilityUnavailableException(
                    $"Registered economy capability '{capabilityName}' is unavailable for provider evidence.");

            var walletBelongsToTenant = await _db.Set<EconomyWalletRow>().AsNoTracking()
                .AnyAsync(row => row.Id == request.TenantWalletId.Value &&
                                 row.TenantId == request.TenantId &&
                                 row.State == WalletLifecycleState.Active,
                    cancellationToken).ConfigureAwait(false);
            if (!walletBelongsToTenant)
                throw new RegisteredPostingCapabilityUnavailableException(
                    "Provider evidence is not bound to an active wallet in the operation tenant.");

            var existing = await _db.Set<EconomyRiskDecisionRow>()
                .SingleOrDefaultAsync(row => row.Id == riskDecisionId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                _db.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
                {
                    Id = riskDecisionId,
                    Outcome = RiskOutcome.Allow,
                    OperationFingerprint = operationFingerprint,
                    IdempotencyKey = operationFingerprint,
                    ActorHash = Hash(request.ActorId.ToString("N")),
                    TemplateKind = request.TemplateKind,
                    SourceWalletId = request.TenantWalletId.Value,
                    DestinationWalletId = request.TenantWalletId.Value,
                    Currency = request.Amount.Currency,
                    AmountUnits = request.Amount.Units,
                    CurrencyLegs = JsonSerializer.Serialize(new[]
                    {
                        new { currency = (int)request.Amount.Currency, units = request.Amount.Units }
                    }),
                    SourceRoots = "[]",
                    ProviderReferenceHash = providerReferenceHash,
                    PolicyVersion = request.PolicyVersion.Value,
                    ReserveVersion = request.ReserveVersion.Value,
                    ReserveAuthorizationEpoch = request.ReserveAuthorizationEpoch,
                    FeatureVersion = 1,
                    KillSwitchEpoch = request.KillSwitchEpoch,
                    CounterVersion = CounterVersion,
                    EntityGraphVersion = 0,
                    EntityGraphEvidenceHash = evidenceHash,
                    ReasonCodes = JsonSerializer.Serialize(new[] { "provider-terminal-evidence" }),
                    IssuedAt = request.IssuedAt,
                    ExpiresAt = request.ExpiresAt
                });
                _db.Set<EconomyRiskCounterRow>().Add(new EconomyRiskCounterRow
                {
                    Id = counterId,
                    TenantId = request.TenantId,
                    Dimension = RiskLimitDimension.ProviderAccount,
                    SubjectHash = providerReferenceHash,
                    Operation = request.TemplateKind,
                    Currency = request.Amount.Currency,
                    WindowStartedAt = request.IssuedAt,
                    WindowEndsAt = request.ExpiresAt,
                    CounterVersion = CounterVersion,
                    MaxUnits = request.Amount.Units,
                    UsedUnits = request.Amount.Units,
                    UpdatedAt = request.IssuedAt
                });
                _db.Set<EconomyRiskCounterReservationRow>().Add(new EconomyRiskCounterReservationRow
                {
                    Id = reservationId,
                    ReservationGroupId = reservationId,
                    RiskDecisionId = riskDecisionId,
                    RiskCounterId = counterId,
                    InputFingerprint = operationFingerprint,
                    AmountUnits = request.Amount.Units,
                    ReservedAt = request.IssuedAt,
                    ExpiresAt = request.ExpiresAt,
                    Status = RiskCounterReservationStatus.Reserved
                });
                _db.Set<EconomyRiskAuditEvidenceRow>().Add(new EconomyRiskAuditEvidenceRow
                {
                    Id = DeterministicGuid(
                        request.TenantId, operationFingerprint, request.TemplateKind, "provider-risk-audit"),
                    RiskDecisionId = riskDecisionId,
                    EventKind = "provider-terminal-evidence-authorized",
                    OperationFingerprint = operationFingerprint,
                    EvidenceHash = evidenceHash,
                    Payload = JsonSerializer.Serialize(new
                    {
                        request.TenantId,
                        request.TemplateKind,
                        request.PolicyVersion,
                        request.ReserveVersion,
                        request.ReserveAuthorizationEpoch,
                        request.KillSwitchEpoch,
                        providerReferenceHash
                    }),
                    RecordedAt = request.IssuedAt
                });
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                EnsureReplayMatches(existing, request, operationFingerprint, providerReferenceHash);
                var reservationExists = await _db.Set<EconomyRiskCounterReservationRow>().AsNoTracking()
                    .AnyAsync(row => row.RiskDecisionId == riskDecisionId &&
                                     row.RiskCounterId == counterId &&
                                     row.AmountUnits == request.Amount.Units,
                        cancellationToken).ConfigureAwait(false);
                if (!reservationExists)
                    throw new RegisteredPostingCapabilityUnavailableException(
                        "Provider evidence authority has no durable aggregate-counter reservation.");
            }

            return new RegisteredPostingAuthority(
                capability.Id,
                request.ActorId,
                request.TenantId,
                riskDecisionId,
                operationFingerprint,
                CounterVersion);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ConsumeAsync(
        RegisteredPostingAuthority authority,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (authority.RiskDecisionId == Guid.Empty)
            throw new ArgumentException("Provider evidence authority requires a risk decision.", nameof(authority));

        var rows = await _db.Set<EconomyRiskCounterReservationRow>().AsNoTracking()
            .Where(row => row.RiskDecisionId == authority.RiskDecisionId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Length == 0)
            throw new RegisteredPostingCapabilityUnavailableException(
                "Provider evidence authority has no durable aggregate-counter reservation.");
        if (rows.Any(row => row.Status != RiskCounterReservationStatus.Reserved))
            throw new RiskDecisionReuseException(
                "Provider evidence authority was already consumed or released.");
        if (rows.Any(row => consumedAt < row.ReservedAt || consumedAt >= row.ExpiresAt))
            throw new RegisteredPostingCapabilityUnavailableException(
                "Provider evidence authority is outside its valid lifetime.");

        var reservationGroupId = rows.Select(row => row.ReservationGroupId).Distinct().Single();
        var status = await _db.Database.SqlQuery<int>($"""
                SELECT economy_private.transition_risk_counter_reservation_v1(
                    {reservationGroupId}, {true}, {consumedAt}) AS "Value"
                """)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        if (status != (int)RiskCounterReservationStatus.Consumed)
            throw new RegisteredPostingCapabilityUnavailableException(
                "Provider evidence authority expired before it could be consumed.");
    }

    private static void Validate(ProviderEvidencePostingAuthorityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CapabilityName);
        if (request.TenantId == Guid.Empty || request.ActorId == Guid.Empty ||
            request.TenantWalletId.Value == Guid.Empty)
            throw new ArgumentException("Provider evidence tenant, actor, and wallet are required.", nameof(request));
        if (request.TemplateKind is not (PostingTemplateKind.PayoutSuccess or
            PostingTemplateKind.PayoutFailure or PostingTemplateKind.AdminWithdrawalSuccess or
            PostingTemplateKind.AdminWithdrawalFailure))
            throw new ArgumentOutOfRangeException(nameof(request),
                "Only payout and administrative-withdrawal terminal templates accept provider evidence authority.");
        if (request.Amount.Currency != CurrencyCode.HardCoin || request.Amount.Units <= 0)
            throw new ArgumentOutOfRangeException(nameof(request),
                "Provider evidence authority requires a positive hard-coin amount.");
        if (request.PolicyVersion.Value <= 0 || request.ReserveVersion.Value <= 0 ||
            request.ReserveAuthorizationEpoch <= 0 || request.KillSwitchEpoch < 0)
            throw new ArgumentOutOfRangeException(nameof(request),
                "Provider evidence control-plane versions are invalid.");
        if (request.ExpiresAt <= request.IssuedAt)
            throw new ArgumentException("Provider evidence authority expiry must follow issuance.", nameof(request));
    }

    private static bool Allows(string json, PostingTemplateKind templateKind)
    {
        try
        {
            return (JsonSerializer.Deserialize<int[]>(json) ?? []).Contains((int)templateKind);
        }
        catch (JsonException exception)
        {
            throw new RegisteredPostingCapabilityUnavailableException(
                "Registered provider-evidence capability has an invalid template policy.", exception);
        }
    }

    private static void EnsureReplayMatches(
        EconomyRiskDecisionRow existing,
        ProviderEvidencePostingAuthorityRequest request,
        string operationFingerprint,
        string providerReferenceHash)
    {
        if (existing.Outcome != RiskOutcome.Allow ||
            existing.OperationFingerprint != operationFingerprint ||
            existing.TemplateKind != request.TemplateKind ||
            existing.SourceWalletId != request.TenantWalletId.Value ||
            existing.DestinationWalletId != request.TenantWalletId.Value ||
            existing.Currency != request.Amount.Currency ||
            existing.AmountUnits != request.Amount.Units ||
            existing.ProviderReferenceHash != providerReferenceHash ||
            existing.PolicyVersion != request.PolicyVersion.Value ||
            existing.ReserveVersion != request.ReserveVersion.Value ||
            existing.ReserveAuthorizationEpoch != request.ReserveAuthorizationEpoch ||
            existing.KillSwitchEpoch != request.KillSwitchEpoch ||
            existing.CounterVersion != CounterVersion ||
            existing.IssuedAt != request.IssuedAt ||
            existing.ExpiresAt != request.ExpiresAt)
            throw new RiskDecisionReuseException(
                "Provider evidence operation fingerprint is bound to a different terminal result.");
    }

    private static string NormalizeHash(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 128)
            throw new ArgumentOutOfRangeException(parameterName, "Evidence hashes cannot exceed 128 characters.");
        return normalized;
    }

    private static Guid DeterministicGuid(
        Guid tenantId,
        string operationFingerprint,
        PostingTemplateKind templateKind,
        string suffix)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{tenantId:N}|{operationFingerprint}|{(int)templateKind}|{suffix}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
