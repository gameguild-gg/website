namespace GameGuild.Finance.Economy.Operations;

public sealed record EconomyJournalVerificationRunDetails(
    Guid Id,
    long FromSequence,
    long ToSequence,
    string PreviousHash,
    string CurrentHash,
    bool IsValid,
    string? FailureCode,
    long FencingToken,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public sealed record EconomyAnchorOperationalDetails(
    EconomyAnchorOperationalStatus Summary,
    string? DispatchSnapshotHash);

public sealed record EconomyAnchorVerificationOperationalStatus(
    Guid Id,
    string KeyId,
    string ObjectVersion,
    string ETag,
    DateTimeOffset RetainUntil,
    string ObjectHash,
    bool SignatureValid,
    bool ObjectMatches,
    DateTimeOffset VerifiedAt);

public sealed record EconomyProjectionGenerationOperationalDetails(
    EconomyProjectionOperationalStatus Summary,
    long FromSequence,
    bool IsActive,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record EconomyProjectionApprovalAuditEntry(
    Guid Id,
    Guid ActorId,
    string EvidenceHash,
    DateTimeOffset ApprovedAt);

public interface IEconomyLedgerQueryReader
{
    ValueTask<EconomyOperationalPage<EconomyJournalVerificationRunDetails>> ListVerificationsAsync(
        Guid tenantId, int limit, string? cursor, CancellationToken cancellationToken);

    ValueTask<EconomyJournalVerificationRunDetails?> FindVerificationAsync(
        Guid tenantId, Guid verificationId, CancellationToken cancellationToken);

    ValueTask<EconomyOperationalPage<EconomyAnchorOperationalDetails>> ListAnchorsAsync(
        Guid tenantId, int limit, string? cursor, CancellationToken cancellationToken);

    ValueTask<EconomyAnchorOperationalDetails?> FindAnchorAsync(
        Guid tenantId, Guid anchorId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<EconomyAnchorVerificationOperationalStatus>> ReadAnchorVerificationsAsync(
        Guid tenantId, Guid anchorId, CancellationToken cancellationToken);

    ValueTask<EconomyOperationalPage<EconomyProjectionGenerationOperationalDetails>> ListProjectionsAsync(
        Guid tenantId, int limit, string? cursor, CancellationToken cancellationToken);

    ValueTask<EconomyProjectionGenerationOperationalDetails?> FindProjectionAsync(
        Guid tenantId, long generation, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<EconomyProjectionApprovalAuditEntry>> ReadProjectionAuditAsync(
        Guid tenantId, long generation, CancellationToken cancellationToken);
}
