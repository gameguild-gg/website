namespace GameGuild.Finance.Economy.Risk;

/// <summary>
/// Requires independent, current fraud-control evidence before a self-service
/// HardCoin to SoftCoin conversion can consume value.
/// </summary>
public interface IHardToSoftConversionRiskEvidenceVerifier
{
    Task<IReadOnlyList<ExternalRiskEvidence>> VerifyAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken);
}

public sealed class HardToSoftConversionRiskEvidenceVerifier(
    IFinancialCrimeRiskInputSource financialCrime,
    ITrustSafetyRiskInputSource trustSafety) : IHardToSoftConversionRiskEvidenceVerifier
{
    public async Task<IReadOnlyList<ExternalRiskEvidence>> VerifyAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("An actor is required.", nameof(actorId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required.", nameof(tenantId));

        cancellationToken.ThrowIfCancellationRequested();
        var observedAt = DateTimeOffset.UtcNow;
        var subjectReference = EconomySubjectReference.ForUser(tenantId, actorId);
        var financialCrimeEvidence = await financialCrime
            .ReadAsync(tenantId, subjectReference, observedAt, cancellationToken)
            .ConfigureAwait(false);
        var trustSafetyEvidence = await trustSafety
            .ReadAsync(tenantId, subjectReference, observedAt, cancellationToken)
            .ConfigureAwait(false);

        return ExternalRiskEvidenceValidator.RequireFreshAllow(
            [financialCrimeEvidence.ToEvidence(), trustSafetyEvidence.ToEvidence()],
            observedAt);
    }

}
