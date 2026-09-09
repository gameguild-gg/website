using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public sealed record HardToSoftConversionRiskDecisionRequest(
    Guid ActorId,
    Guid TenantId,
    WalletId WalletId,
    Guid ReservationOperationId,
    IdempotencyKey IdempotencyKey,
    long FeeHardCoinUnits,
    long TotalHardCoinUnits,
    long MaximumHardCoinUnitsPerDay,
    int DecisionLifetimeSeconds,
    string JurisdictionCode,
    long PolicyVersion,
    string PolicyHash,
    IReadOnlyList<ExternalRiskEvidence> ExternalEvidence,
    DateTimeOffset RequestedAt);

public sealed record HardToSoftConversionRiskDecision(
    Guid Id,
    IReadOnlyList<Guid> SourceRoots);

public interface IHardToSoftConversionRiskDecisionIssuer
{
    Task<HardToSoftConversionRiskDecision> IssueAsync(
        HardToSoftConversionRiskDecisionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Issues a short-lived conversion decision by calling the restricted database writer.
/// The writer selects and reserves exact FIFO fragments, persists the risk counter
/// reservation, and records the independent evidence as one atomic operation.
/// </summary>
public sealed class PostgreSqlHardToSoftConversionRiskDecisionIssuer(IApplicationDbContext context)
    : IHardToSoftConversionRiskDecisionIssuer
{
    public async Task<HardToSoftConversionRiskDecision> IssueAsync(
        HardToSoftConversionRiskDecisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExternalEvidence);
        if (request.ActorId == Guid.Empty || request.TenantId == Guid.Empty || request.ReservationOperationId == Guid.Empty)
            throw new ArgumentException("The actor, tenant, and reservation operation are required.", nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegative(request.FeeHardCoinUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.TotalHardCoinUnits);
        if (request.MaximumHardCoinUnitsPerDay <= 0)
            throw new EconomySelfServiceCommandRejectedException(
                "HardCoin conversion requires a positive signed daily risk limit before rollout.");
        if (request.DecisionLifetimeSeconds is < 30 or > 900)
            throw new EconomySelfServiceCommandRejectedException(
                "HardCoin conversion decision lifetime must be between 30 and 900 seconds.");
        if (request.TotalHardCoinUnits > request.MaximumHardCoinUnitsPerDay)
            throw new EconomySelfServiceCommandRejectedException(
                "The requested conversion exceeds the signed daily HardCoin risk limit.");
        var jurisdiction = EconomyJurisdictionCode.Require(request.JurisdictionCode, nameof(request));
        if (request.PolicyVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "The signed policy version must be positive.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PolicyHash);

        var evidence = ExternalRiskEvidenceValidator.RequireFreshAllow(request.ExternalEvidence, request.RequestedAt);
        var evidenceItems = evidence.Select(item => (object)new
        {
            source = (int)item.Source,
            version = item.Version,
            issuedAt = item.IssuedAt,
            expiresAt = item.ExpiresAt,
            outcome = (int)item.Outcome,
            evidenceHash = item.EvidenceHash,
            isAuditable = item.IsAuditable
        }).ToList();
        evidenceItems.Add(new
        {
            source = 0,
            version = request.PolicyVersion,
            issuedAt = request.RequestedAt,
            expiresAt = request.RequestedAt.AddSeconds(request.DecisionLifetimeSeconds),
            outcome = (int)ExternalRiskOutcome.Allow,
            evidenceHash = request.PolicyHash.Trim(),
            isAuditable = true,
            kind = "signed-capability-policy",
            jurisdictionCode = jurisdiction
        });
        var evidencePayload = JsonSerializer.Serialize(evidenceItems);

        var receipt = await context.Set<HardToSoftConversionRiskDecisionReceiptRow>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM economy_private.issue_self_service_hard_to_soft_risk_decision_v1(
                    {request.ActorId},
                    {request.TenantId},
                    {request.WalletId.Value},
                    {request.ReservationOperationId},
                    {request.IdempotencyKey.Value},
                    {request.FeeHardCoinUnits},
                    {request.TotalHardCoinUnits},
                    {request.MaximumHardCoinUnitsPerDay},
                    {evidencePayload},
                    {request.RequestedAt},
                    {request.RequestedAt.AddSeconds(request.DecisionLifetimeSeconds)})
                """)
            .AsNoTracking()
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var roots = PostgreSqlHardToSoftConversionWorkflow.ParseRootIds(receipt.SourceRoots);
        return new HardToSoftConversionRiskDecision(receipt.RiskDecisionId, roots);
    }
}
