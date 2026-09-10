using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Risk;

public sealed class EconomyTrustedProtectedOperationAuthorizer(
    IEconomyJurisdictionResolver jurisdictionResolver,
    IEconomyProtectedOperationRiskDecisionIssuer riskDecisionIssuer,
    IEconomyCapabilityAuthorizationService capabilityAuthorization,
    IEconomyProtectedOperationTransaction transaction) : IEconomyTrustedProtectedOperationAuthorizer
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Guid tenantId,
        Guid actorId,
        EconomyProtectedOperationIntent intent,
        Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Trusted Economy authority requires tenant and actor IDs.");
        EconomyProtectedOperationOrchestrator.Validate(intent);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        var subjectId = intent.ProtectedSubjectId ?? actorId;
        var jurisdiction = await jurisdictionResolver.ResolveAsync(
            tenantId,
            subjectId,
            intent.ProviderJurisdictionCode,
            intent.DestinationJurisdictionCode,
            intent.RequestedAt,
            cancellationToken).ConfigureAwait(false);
        var fingerprint = EconomyProtectedOperationOrchestrator.Fingerprint(tenantId, actorId, intent);
        var subjectReference = EconomySubjectReference.ForUser(tenantId, subjectId);
        var execution = await transaction.ExecuteAsync(async token =>
        {
            var decision = await riskDecisionIssuer.IssueAsync(
                new EconomyProtectedRiskDecisionRequest(
                    tenantId,
                    actorId,
                    subjectReference,
                    jurisdiction.JurisdictionCode,
                    jurisdiction.EvidenceHash,
                    fingerprint,
                    intent),
                token).ConfigureAwait(false);
            if (decision.Outcome != RiskOutcome.Allow ||
                decision.State != EconomyProtectedOperationState.Ready)
                return ProtectedExecution<TResult>.Rejected(decision);
            if (decision.Id == Guid.Empty)
                throw new InvalidOperationException(
                    "A ready protected operation must have a durable risk decision.");

            var receipt = await capabilityAuthorization.AuthorizeAndConsumeAsync(
                new EconomyCapabilityEvaluationContext(
                    tenantId,
                    actorId,
                    subjectReference,
                    jurisdiction.JurisdictionCode,
                    intent.Capability,
                    decision.Id,
                    fingerprint,
                    intent.ProviderReferenceHash.Trim(),
                    intent.DestinationHash.Trim(),
                    intent.SourceRoots.Select(EconomyProtectedOperationOrchestrator.HashRoot).ToArray(),
                    intent.RequestedAt),
                token).ConfigureAwait(false);
            var authorization = new EconomyProtectedOperationAuthorization(
                tenantId,
                actorId,
                jurisdiction.JurisdictionCode,
                decision.Id,
                fingerprint,
                receipt);
            return ProtectedExecution<TResult>.Succeeded(
                await operation(authorization, token).ConfigureAwait(false),
                decision);
        }, cancellationToken).ConfigureAwait(false);

        if (!execution.Success)
            throw new EconomyProtectedOperationException(
                execution.Decision.State,
                execution.Decision.ReviewId,
                execution.Decision.Diagnostics);
        return execution.Result;
    }

    private sealed record ProtectedExecution<TResult>(
        bool Success,
        TResult Result,
        EconomyProtectedRiskDecision Decision)
    {
        internal static ProtectedExecution<TResult> Succeeded(
            TResult result,
            EconomyProtectedRiskDecision decision) => new(true, result, decision);

        internal static ProtectedExecution<TResult> Rejected(
            EconomyProtectedRiskDecision decision) => new(false, default!, decision);
    }
}
