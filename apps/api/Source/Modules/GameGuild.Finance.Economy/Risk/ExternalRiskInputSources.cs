namespace GameGuild.Finance.Economy.Risk;

public interface IFinancialCrimeRiskInputSource
{
    ValueTask<FinancialCrimeRiskInput> ReadAsync(
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);

    ValueTask<FinancialCrimeRiskInput> ReadAsync(
        Guid tenantId,
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        return ReadAsync(opaqueSubjectReference, observedAt, cancellationToken);
    }
}

public interface ITrustSafetyRiskInputSource
{
    ValueTask<TrustSafetyRiskInput> ReadAsync(
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);

    ValueTask<TrustSafetyRiskInput> ReadAsync(
        Guid tenantId,
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        return ReadAsync(opaqueSubjectReference, observedAt, cancellationToken);
    }
}
