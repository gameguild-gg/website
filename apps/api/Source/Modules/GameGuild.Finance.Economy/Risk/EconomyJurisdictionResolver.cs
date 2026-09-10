namespace GameGuild.Finance.Economy.Risk;

public sealed record EconomyJurisdictionResolution(
    string JurisdictionCode,
    long EvidenceVersion,
    long PolicyVersion,
    string EvidenceHash);

public interface IEconomyJurisdictionResolver
{
    ValueTask<EconomyJurisdictionResolution> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        string? providerJurisdiction,
        string? destinationJurisdiction,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken);
}

public sealed class EconomyJurisdictionResolver(
    IComplianceEvidenceReader evidenceReader) : IEconomyJurisdictionResolver
{
    public async ValueTask<EconomyJurisdictionResolution> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        string? providerJurisdiction,
        string? destinationJurisdiction,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("An actor is required.", nameof(actorId));

        var subjectReference = EconomySubjectReference.ForUser(tenantId, actorId);
        var evidence = await evidenceReader.ReadLatestAsync(
            tenantId,
            subjectReference,
            ComplianceEvidenceKinds.KycAml,
            cancellationToken);

        if (evidence is null ||
            evidence.TenantId != tenantId ||
            !string.Equals(evidence.SubjectHash, subjectReference, StringComparison.Ordinal) ||
            !string.Equals(evidence.EvidenceKind, ComplianceEvidenceKinds.KycAml, StringComparison.Ordinal) ||
            evidence.Result != ComplianceEvidenceResult.Approved ||
            !evidence.SignatureVerified ||
            evidence.IssuedAt > evaluatedAt ||
            evidence.ExpiresAt <= evaluatedAt)
        {
            throw new EconomyJurisdictionUnavailableException(
                "A current, signed and approved KYC jurisdiction is required.");
        }

        var jurisdiction = EconomyJurisdictionCode.NormalizeOptional(evidence.JurisdictionCode)
            ?? throw new EconomyJurisdictionUnavailableException(
                "Approved KYC evidence does not contain a valid jurisdiction.");

        RequireCompatibleMetadata(providerJurisdiction, jurisdiction, "provider");
        RequireCompatibleMetadata(destinationJurisdiction, jurisdiction, "destination");

        return new EconomyJurisdictionResolution(
            jurisdiction,
            evidence.Version,
            evidence.PolicyVersion,
            evidence.EvidenceHash);
    }

    private static void RequireCompatibleMetadata(
        string? candidate,
        string approvedJurisdiction,
        string source)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        var normalized = EconomyJurisdictionCode.NormalizeOptional(candidate);
        if (normalized is null || !string.Equals(normalized, approvedJurisdiction, StringComparison.Ordinal))
        {
            throw new EconomyJurisdictionConflictException(
                $"The {source} jurisdiction conflicts with approved KYC evidence.");
        }
    }
}

public sealed class EconomyJurisdictionUnavailableException(string message)
    : InvalidOperationException(message);

public sealed class EconomyJurisdictionConflictException(string message)
    : InvalidOperationException(message);
