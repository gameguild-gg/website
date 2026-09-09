using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Risk;

public enum EconomyProtectedOperationState
{
    Ready = 1,
    ReviewRequired = 2,
    Hold = 3,
    Challenge = 4,
    Denied = 5,
    ComplianceUnavailable = 6,
    ComplianceStale = 7,
    InvalidPolicy = 8,
    ReserveInsufficient = 9
}

public sealed record EconomyProtectedOperationIntent(
    EconomyValueMovementCapability Capability,
    PostingTemplateKind TemplateKind,
    WalletId SourceWalletId,
    WalletId DestinationWalletId,
    CoinAmount Amount,
    IReadOnlyList<RiskCurrencyLeg> CurrencyLegs,
    IReadOnlyList<SourceStampId> SourceRoots,
    string ProviderReferenceHash,
    string DestinationHash,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RequestedAt,
    string? ProviderJurisdictionCode = null,
    string? DestinationJurisdictionCode = null,
    Guid? ProtectedSubjectId = null);

public sealed record EconomyProtectedRiskDecisionRequest(
    Guid TenantId,
    Guid ActorId,
    string SubjectReference,
    string JurisdictionCode,
    string JurisdictionEvidenceHash,
    string OperationFingerprint,
    EconomyProtectedOperationIntent Intent);

public sealed record EconomyProtectedRiskDecision(
    Guid Id,
    RiskOutcome Outcome,
    EconomyProtectedOperationState State,
    Guid? ReviewId,
    IReadOnlyList<string> Diagnostics);

public sealed record EconomyProtectedOperationAuthorization(
    Guid TenantId,
    Guid ActorId,
    string JurisdictionCode,
    Guid RiskDecisionId,
    string OperationFingerprint,
    CapabilityAuthorizationReceipt Receipt);

public interface IEconomyProtectedOperationRiskDecisionIssuer
{
    ValueTask<EconomyProtectedRiskDecision> IssueAsync(
        EconomyProtectedRiskDecisionRequest request,
        CancellationToken cancellationToken);
}

public interface IEconomyProtectedOperationTransaction
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}

public interface IEconomyProtectedOperationOrchestrator
{
    Task<TResult> ExecuteAsync<TResult>(
        EconomyProtectedOperationIntent intent,
        Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}

public interface IEconomyTrustedProtectedOperationAuthorizer
{
    Task<TResult> ExecuteAsync<TResult>(
        Guid tenantId,
        Guid actorId,
        EconomyProtectedOperationIntent intent,
        Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}

public sealed class EconomyProtectedOperationException : InvalidOperationException
{
    public EconomyProtectedOperationException(
        EconomyProtectedOperationState state,
        Guid? reviewId,
        IReadOnlyList<string> diagnostics)
        : base(diagnostics.Count == 0
            ? $"The protected Economy operation was rejected with state {state}."
            : diagnostics[0])
    {
        State = state;
        ReviewId = reviewId;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public EconomyProtectedOperationState State { get; }
    public Guid? ReviewId { get; }
    public IReadOnlyList<string> Diagnostics { get; }
}
