using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Risk;

public interface IRiskDecisionAuthorizer
{
    RiskAuthorization AuthorizeValueMovement(
        RiskDecisionSnapshot decision,
        ProtectedOperationContext context,
        DateTimeOffset now);
}

public sealed class RiskDecisionAuthorizer : IRiskDecisionAuthorizer
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RiskAuthorization> _authorizations = [];

    public IReadOnlyList<RiskAuthorization> Authorizations
    {
        get
        {
            lock (_gate) return [.. _authorizations.Values];
        }
    }

    public RiskAuthorization AuthorizeValueMovement(
        RiskDecisionSnapshot decision,
        ProtectedOperationContext context,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(context);
        var fingerprint = context.Fingerprint();

        lock (_gate)
        {
            if (_authorizations.TryGetValue(decision.Id, out var existing))
            {
                if (existing.OperationFingerprint == fingerprint &&
                    existing.IdempotencyKey == context.IdempotencyKey)
                    return existing;

                throw new RiskDecisionReuseException("A risk decision cannot authorize a different operation.");
            }

            RiskDecisionGuard.Validate(decision, fingerprint, now);
            if (decision.Outcome != RiskOutcome.Allow)
                throw new RiskAuthorizationDeniedException("Only an Allow risk decision can authorize value movement.");

            var authorization = new RiskAuthorization(decision.Id, fingerprint, context.IdempotencyKey, now);
            _authorizations.Add(decision.Id, authorization);
            return authorization;
        }
    }
}
public sealed class RiskHoldCoordinator
{
    public HoldContract CreateHold(
        RiskDecisionSnapshot decision,
        ProtectedOperationContext context,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(context);
        RiskDecisionGuard.Validate(decision, context.Fingerprint(), now);
        if (decision.Outcome != RiskOutcome.Hold)
            throw new RiskAuthorizationDeniedException("Only a Hold risk decision can create a nonspendable hold.");

        return new HoldContract(
            HoldId.New(), context.SourceWalletId, context.Amount, HoldReason.RiskReview,
            HoldStatus.Active, now, null);
    }
}

internal static class RiskDecisionGuard
{
    internal static void Validate(RiskDecisionSnapshot decision, string fingerprint, DateTimeOffset now)
    {
        if (decision.IssuedAt > now || decision.ExpiresAt <= now)
            throw new RiskDecisionExpiredException("Risk decision is not valid at the operation timestamp.");
        if (!string.Equals(decision.OperationFingerprint, fingerprint, StringComparison.Ordinal))
            throw new RiskDecisionBindingException("Risk decision is not bound to this operation context.");
    }
}
