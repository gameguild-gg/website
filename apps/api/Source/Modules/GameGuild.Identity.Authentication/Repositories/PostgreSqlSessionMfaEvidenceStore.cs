using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

public sealed class PostgreSqlSessionMfaEvidenceStore(IApplicationDbContext context) : ISessionMfaEvidenceStore
{
    public async Task AddAsync(Guid sessionId, SignInMfaProof proof, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(proof);
        if (sessionId == Guid.Empty || context is not DbContext databaseContext ||
            databaseContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL" ||
            databaseContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("MFA session evidence requires its owning credential transaction.");
        }
        var session = await context.Set<UserSession>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null || session.UserId != proof.SubjectId || !session.IsValid || session.TerminatedAt is not null)
        {
            throw new AuthenticationRequiredException("The authenticated session is unavailable.");
        }
        proof.RequireBinding(proof.SubjectId, proof.TenantId, proof.TokenVersion, proof.FirstFactorVerifiedAt, DateTimeOffset.UtcNow);
        await context.Set<SessionMfaEvidence>().AddAsync(SessionMfaEvidence.Create(sessionId, proof), cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<SessionMfaEvidence?> FindAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty) { throw new ArgumentException("A session is required.", nameof(sessionId)); }
        return context.Set<SessionMfaEvidence>().AsNoTracking().SingleOrDefaultAsync(row => row.SessionId == sessionId, cancellationToken);
    }
}
