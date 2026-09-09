using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Risk;

public sealed record PublicRiskDecision(Guid DecisionId, RiskOutcome Outcome, DateTimeOffset RecordedAt);

public sealed record RiskDecisionAuditRecord(
    Guid DecisionId,
    RiskOutcome Outcome,
    IReadOnlyList<RiskReasonCode> ReasonCodes,
    string OperationFingerprint,
    string ActorHash,
    string SourceWalletHash,
    string DestinationWalletHash,
    string ProviderReferenceHash,
    string EntityGraphEvidenceHash,
    DateTimeOffset RecordedAt)
{
    public static RiskDecisionAuditRecord Create(
        RiskDecisionSnapshot decision,
        ProtectedOperationContext context,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(context);
        return new RiskDecisionAuditRecord(
            decision.Id,
            decision.Outcome,
            [.. decision.ReasonCodes],
            context.Fingerprint(),
            Hash(context.ActorId.ToString("N")),
            Hash(context.SourceWalletId.Value.ToString("N")),
            Hash(context.DestinationWalletId.Value.ToString("N")),
            context.ProviderReferenceHash,
            context.EntityGraphEvidenceHash,
            recordedAt);
    }

    public PublicRiskDecision ToPublicView() => new(DecisionId, Outcome, RecordedAt);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
