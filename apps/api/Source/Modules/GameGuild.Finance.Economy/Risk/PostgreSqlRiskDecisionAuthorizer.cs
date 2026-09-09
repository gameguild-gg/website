using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

/// <summary>
/// Verifies that an allow decision was issued by the durable risk authority.
/// The restricted posting writers consume the decision atomically with a unique
/// database constraint; this class never issues or consumes risk decisions.
/// </summary>
public sealed class PostgreSqlRiskDecisionAuthorizer : IRiskDecisionAuthorizer
{
    private readonly DbContext _db;

    public PostgreSqlRiskDecisionAuthorizer(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent Economy risk authorization requires the application's relational DbContext.");
    }

    public RiskAuthorization AuthorizeValueMovement(
        RiskDecisionSnapshot decision,
        ProtectedOperationContext context,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(context);
        var fingerprint = context.Fingerprint();
        RiskDecisionGuard.Validate(decision, fingerprint, now);

        var persisted = _db.Set<EconomyRiskDecisionRow>()
            .AsNoTracking()
            .SingleOrDefault(row => row.Id == decision.Id);

        if (persisted is null ||
            persisted.Outcome != decision.Outcome ||
            !string.Equals(persisted.OperationFingerprint, decision.OperationFingerprint, StringComparison.Ordinal) ||
            persisted.IssuedAt != decision.IssuedAt ||
            persisted.ExpiresAt != decision.ExpiresAt)
            throw new RiskDecisionBindingException(
                "The supplied risk decision is not the durable decision bound to this operation.");

        if (persisted.Outcome != RiskOutcome.Allow)
            throw new RiskAuthorizationDeniedException("Only an Allow risk decision can authorize value movement.");

        return new RiskAuthorization(decision.Id, fingerprint, context.IdempotencyKey, now);
    }
}