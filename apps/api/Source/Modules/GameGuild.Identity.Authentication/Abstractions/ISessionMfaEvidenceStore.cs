namespace GameGuild.Identity.Authentication;

public interface ISessionMfaEvidenceStore
{
    Task AddAsync(Guid sessionId, SignInMfaProof proof, CancellationToken cancellationToken);
    Task<SessionMfaEvidence?> FindAsync(Guid sessionId, CancellationToken cancellationToken);
}
