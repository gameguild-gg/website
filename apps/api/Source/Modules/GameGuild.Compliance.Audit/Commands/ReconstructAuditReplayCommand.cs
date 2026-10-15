using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

/// <summary>Reconstructs the incident timeline of one client-held snapshot (issue #169).
/// The document is revalidated and integrity-checked against its expected digest before
/// any timeline is produced; the investigation access is durably audited.</summary>
public sealed record ReconstructAuditReplayCommand(
    AuditReplaySnapshotDocument Document,
    string Sha256) : ICommand<AuditReplayReconstruction>;

/// <summary>Fail-closed reconstruction dispatch: without a persisted access audit the
/// timeline is never released to the caller.</summary>
public sealed class ReconstructAuditReplayCommandHandler(IAuditReplayReconstructionService service)
    : ICommandHandler<ReconstructAuditReplayCommand, AuditReplayReconstruction>
{
    public Task<AuditReplayReconstruction> Handle(ReconstructAuditReplayCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return service.ReconstructAsync(request.Document, request.Sha256, cancellationToken);
    }
}
