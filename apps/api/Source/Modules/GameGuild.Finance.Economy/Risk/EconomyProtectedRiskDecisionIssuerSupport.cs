using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Risk;

internal sealed record EconomyExternalRiskAssessment(
    RiskOutcome Outcome,
    EconomyProtectedOperationState State,
    IReadOnlyList<string> Diagnostics,
    DateTimeOffset ExpiresAt);

internal static class EconomyProtectedRiskDecisionIssuerSupport
{
    internal static EconomyExternalRiskAssessment Assess(
        IReadOnlyList<ExternalRiskEvidence> evidence,
        DateTimeOffset now)
    {
        if (evidence.Count != Enum.GetValues<ExternalRiskSource>().Length ||
            evidence.Select(item => item.Source).Distinct().Count() != evidence.Count ||
            evidence.Any(item => !item.IsAuditable || string.IsNullOrWhiteSpace(item.EvidenceHash) ||
                                 item.Outcome is ExternalRiskOutcome.Unknown or ExternalRiskOutcome.Unavailable))
            return Denied(
                RiskOutcome.Deny,
                EconomyProtectedOperationState.ComplianceUnavailable,
                "Current auditable compliance evidence is unavailable.",
                now);
        if (evidence.Any(item => item.IssuedAt > now || item.ExpiresAt <= now))
            return Denied(
                RiskOutcome.Deny,
                EconomyProtectedOperationState.ComplianceStale,
                "Compliance evidence is stale.",
                now);
        var expiresAt = evidence.Min(item => item.ExpiresAt);
        if (evidence.Any(item => item.Outcome is ExternalRiskOutcome.Deny or ExternalRiskOutcome.Blocked))
            return Denied(
                RiskOutcome.Hold,
                EconomyProtectedOperationState.Hold,
                "Compliance evidence requires a durable hold.",
                expiresAt);
        if (evidence.Any(item => item.Outcome == ExternalRiskOutcome.Review))
            return Denied(
                RiskOutcome.Review,
                EconomyProtectedOperationState.ReviewRequired,
                "A manual risk review is required.",
                expiresAt);
        if (evidence.All(item => item.Outcome == ExternalRiskOutcome.Allow))
            return new EconomyExternalRiskAssessment(
                RiskOutcome.Allow,
                EconomyProtectedOperationState.Ready,
                [],
                expiresAt);
        return Denied(
            RiskOutcome.Deny,
            EconomyProtectedOperationState.Denied,
            "Compliance evidence denied the operation.",
            expiresAt);
    }

    internal static IReadOnlyList<AggregateRiskLimit> MaterializeLimits(
        EconomyProtectedRiskPolicy policy,
        EconomyProtectedRiskDecisionRequest request,
        EntityRiskCluster cluster)
    {
        var limits = policy.Limits.SelectMany(rule => Subjects(rule.Subject, request, cluster)
            .Select(subject => new AggregateRiskLimit(
                new RiskLimitKey(rule.Dimension, subject),
                rule.CounterVersion,
                rule.MaximumUnits,
                rule.Window))).ToArray();
        if (limits.Length == 0 || limits.Select(limit => limit.Key).Distinct().Count() != limits.Length)
            throw new EconomyProtectedRiskPolicyException(
                "The signed risk policy does not materialize unique limits for this operation.");
        return Array.AsReadOnly(limits);
    }

    internal static Guid DeterministicGuid(
        EconomyProtectedRiskDecisionRequest request,
        string purpose)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',
            purpose,
            request.TenantId.ToString("N"),
            request.ActorId.ToString("N"),
            (int)request.Intent.Capability,
            request.Intent.IdempotencyKey.Value)));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    internal static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static IEnumerable<string> Subjects(
        EconomyRiskLimitSubject subject,
        EconomyProtectedRiskDecisionRequest request,
        EntityRiskCluster cluster) => subject switch
        {
            EconomyRiskLimitSubject.SourceWallet => [Hash(request.Intent.SourceWalletId.Value.ToString("N"))],
            EconomyRiskLimitSubject.DestinationWallet => [Hash(request.Intent.DestinationWalletId.Value.ToString("N"))],
            EconomyRiskLimitSubject.IdentityCluster => [cluster.Id],
            EconomyRiskLimitSubject.SourceRoot => request.Intent.SourceRoots.Select(root => Hash(root.Value.ToString("N"))),
            EconomyRiskLimitSubject.Destination => [request.Intent.DestinationHash.Trim()],
            EconomyRiskLimitSubject.Provider => [request.Intent.ProviderReferenceHash.Trim()],
            EconomyRiskLimitSubject.Tenant => [Hash(request.TenantId.ToString("N"))],
            EconomyRiskLimitSubject.CounterpartyPair => [Hash(string.Join('|',
                request.Intent.SourceWalletId.Value.ToString("N"),
                request.Intent.DestinationWalletId.Value.ToString("N")))],
            _ => throw new ArgumentOutOfRangeException(nameof(subject))
        };

    private static EconomyExternalRiskAssessment Denied(
        RiskOutcome outcome,
        EconomyProtectedOperationState state,
        string diagnostic,
        DateTimeOffset expiresAt) => new(outcome, state, [diagnostic], expiresAt);
}
