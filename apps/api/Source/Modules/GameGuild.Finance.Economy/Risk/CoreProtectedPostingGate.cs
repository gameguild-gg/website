using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Risk;

public readonly record struct RiskDecisionId
{
    public RiskDecisionId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Risk decision ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
}

public sealed record RiskPersistenceReadiness(bool SchemaRollupVerified, bool CounterConstraintsVerified)
{
    public bool IsReady => SchemaRollupVerified && CounterConstraintsVerified;
    public static RiskPersistenceReadiness NotReady { get; } = new(false, false);
}

public sealed record ProtectedPostingCommand(
    PostingTemplateKind Operation,
    RiskDecisionId? RiskDecisionId,
    ProtectedOperationContext Context);

public sealed class CoreProtectedPostingGate
{
    private readonly IRiskDecisionAuthorizer _authorizer;

    public CoreProtectedPostingGate(IRiskDecisionAuthorizer authorizer)
    {
        ArgumentNullException.ThrowIfNull(authorizer);
        _authorizer = authorizer;
    }

    public RiskAuthorization Authorize(
        ProtectedPostingCommand command,
        RiskDecisionSnapshot decision,
        RiskPersistenceReadiness readiness,
        ReservePostingAuthorization reserveAuthorization,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(reserveAuthorization);
        if (!readiness.IsReady)
            throw new RiskPersistenceNotReadyException(
                "Protected postings remain disabled until schema and counter constraints are verified.");
        if (command.RiskDecisionId is null)
            throw new MissingRiskDecisionException("Protected postings require a RiskDecisionId.");
        if (command.RiskDecisionId.Value.Value != decision.Id)
            throw new RiskDecisionBindingException("RiskDecisionId does not match the supplied decision snapshot.");
        if (command.Operation != command.Context.Operation)
            throw new RiskDecisionBindingException("Protected posting operation does not match the bound context.");
        if (command.Context.ReserveVersion != reserveAuthorization.Version)
            throw new ReserveAuthorizationException(
                "Protected posting reserve version does not match the active reserve lock.");
        if (command.Context.ReserveAuthorizationEpoch != reserveAuthorization.AuthorizationEpoch)
            throw new ReserveAuthorizationEpochException(
                "Protected posting authorization epoch does not match the active reserve lock.");
        return _authorizer.AuthorizeValueMovement(decision, command.Context, now);
    }
}

public sealed class RiskPersistenceNotReadyException(string message) : InvalidOperationException(message);
public sealed class MissingRiskDecisionException(string message) : InvalidOperationException(message);
