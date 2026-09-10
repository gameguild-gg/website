namespace GameGuild.Finance.Economy.Risk;

public enum ProtectedOperationKind
{
    Payout = 1,
    DestinationChange = 2,
    OwnershipTransfer = 3,
    HoldRelease = 4,
    HighRiskSettlement = 5,
    AdministrativeAdjustment = 6
}

public enum ReauthenticationAssurance
{
    Password = 1,
    MultiFactor = 2,
    HardwareBound = 3
}

public sealed record ReauthenticationEvidence(
    Guid ActorId,
    ProtectedOperationKind Operation,
    string TransactionBinding,
    ReauthenticationAssurance Assurance,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string EvidenceHash);

public static class ReauthenticationEvidenceValidator
{
    public static ReauthenticationEvidence RequireFresh(
        ReauthenticationEvidence evidence,
        Guid actorId,
        ProtectedOperationKind operation,
        string transactionBinding,
        ReauthenticationAssurance minimumAssurance,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionBinding);
        if (evidence.ActorId != actorId || evidence.Operation != operation ||
            !string.Equals(evidence.TransactionBinding, transactionBinding, StringComparison.Ordinal) ||
            evidence.Assurance < minimumAssurance)
            throw new ReauthenticationEvidenceException("Reauthentication evidence is not bound to this protected operation.");
        if (evidence.IssuedAt > now || evidence.ExpiresAt <= now || string.IsNullOrWhiteSpace(evidence.EvidenceHash))
            throw new ReauthenticationEvidenceException("Reauthentication evidence is stale or unauditable.");
        return evidence;
    }
}

public sealed class ReauthenticationEvidenceException(string message) : InvalidOperationException(message);
