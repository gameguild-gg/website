namespace GameGuild.Finance.Economy.Risk;

public enum ExternalRiskSource
{
    FinancialCrime = 1,
    TrustSafety = 2
}

public enum ExternalRiskOutcome
{
    Allow = 1,
    Deny = 2,
    Review = 3,
    Blocked = 4,
    Unknown = 5,
    Unavailable = 6
}

public sealed record ExternalRiskEvidence(
    ExternalRiskSource Source,
    long Version,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    ExternalRiskOutcome Outcome,
    string EvidenceHash,
    bool IsAuditable = true);

public sealed record FinancialCrimeRiskInput(
    long Version,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    ExternalRiskOutcome Outcome,
    string EvidenceHash,
    bool IsAuditable)
{
    public ExternalRiskEvidence ToEvidence() =>
        new(ExternalRiskSource.FinancialCrime, Version, IssuedAt, ExpiresAt, Outcome, EvidenceHash, IsAuditable);
}

public sealed record TrustSafetyRiskInput(
    long Version,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    ExternalRiskOutcome Outcome,
    string EvidenceHash,
    bool IsAuditable)
{
    public ExternalRiskEvidence ToEvidence() =>
        new(ExternalRiskSource.TrustSafety, Version, IssuedAt, ExpiresAt, Outcome, EvidenceHash, IsAuditable);
}

public static class ExternalRiskEvidenceValidator
{
    public static IReadOnlyList<ExternalRiskEvidence> RequireFreshAllow(
        IReadOnlyCollection<ExternalRiskEvidence> evidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var required = Enum.GetValues<ExternalRiskSource>();
        var selected = new List<ExternalRiskEvidence>(required.Length);
        foreach (var source in required)
        {
            var candidate = evidence
                .Where(item => item.Source == source)
                .OrderByDescending(item => item.Version)
                .FirstOrDefault();
            if (candidate is null || candidate.Version <= 0 || candidate.IssuedAt > now ||
                candidate.ExpiresAt <= now || candidate.Outcome != ExternalRiskOutcome.Allow ||
                string.IsNullOrWhiteSpace(candidate.EvidenceHash) || !candidate.IsAuditable)
                throw new ExternalRiskEvidenceException($"Fresh Allow evidence is required from {source}.");
            selected.Add(candidate);
        }

        return selected;
    }
}

public sealed class ExternalRiskEvidenceException(string message) : InvalidOperationException(message);
