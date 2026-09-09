using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Integrations.AI;

public sealed class AiCostRiskGate
{
    private static readonly IReadOnlyList<RiskEntityType> EntityTypes = Array.AsReadOnly(
        new[] { RiskEntityType.Account, RiskEntityType.Tenant, RiskEntityType.ProviderObject, RiskEntityType.Session });

    private static readonly IReadOnlyList<RiskLimitDimension> LimitDimensions = Array.AsReadOnly(
        new[]
        {
            RiskLimitDimension.Wallet,
            RiskLimitDimension.IdentityCluster,
            RiskLimitDimension.Tenant,
            RiskLimitDimension.ProviderAccount,
            RiskLimitDimension.DeviceIpAsnCluster,
            RiskLimitDimension.GlobalLossBudget
        });

    private readonly RiskDecisionAuthorizer _decisions;
    private readonly AggregateRiskCounterStore _counters;

    public AiCostRiskGate(RiskDecisionAuthorizer decisions, AggregateRiskCounterStore counters)
    {
        _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        _counters = counters ?? throw new ArgumentNullException(nameof(counters));
    }

    public static IReadOnlyList<RiskEntityType> RequiredEntityTypes => EntityTypes;
    public static IReadOnlyList<RiskLimitDimension> RequiredLimitDimensions => LimitDimensions;

    public AiCostRiskAuthorization Authorize(
        AuthorizeAiServiceCommand command,
        AiServicePriceSnapshot price,
        IReadOnlyList<AiFundingFragment> fundingFragments)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(fundingFragments);
        ArgumentNullException.ThrowIfNull(command.Risk);

        var risk = command.Risk;
        ArgumentNullException.ThrowIfNull(risk.Decision);
        ArgumentNullException.ThrowIfNull(risk.Context);
        ArgumentNullException.ThrowIfNull(risk.EntityCluster);
        ArgumentNullException.ThrowIfNull(risk.Limits);

        var amount = new CoinAmount(CurrencyCode.SoftCoin, price.PriceSoftUnits);
        var roots = fundingFragments.SelectMany(fragment => fragment.SelectedRanges)
            .Select(range => range.Root).Distinct().OrderBy(root => root.Value).ToArray();
        var contextRoots = risk.Context.SourceRoots.Distinct().OrderBy(root => root.Value).ToArray();
        var context = risk.Context;

        if (command.RequestId == Guid.Empty || command.TenantId == Guid.Empty || command.ActorId == Guid.Empty ||
            context.ActorId != command.ActorId || context.Operation != PostingTemplateKind.Burn ||
            context.SourceWalletId != command.SourceWalletId || context.DestinationWalletId != command.ServiceWalletId ||
            context.Amount != amount ||
            !context.CurrencyLegs.SequenceEqual(new[] { new RiskCurrencyLeg(CurrencyCode.SoftCoin, amount.Units) }) ||
            !contextRoots.SequenceEqual(roots) ||
            !string.Equals(context.ProviderReferenceHash,
                ProviderReference(command.RequestId, command.ServiceCode, command.Provider, command.Model),
                StringComparison.Ordinal) ||
            context.EntityGraphVersion != risk.EntityCluster.Version ||
            !string.Equals(context.EntityGraphEvidenceHash, risk.EntityCluster.EvidenceHash, StringComparison.Ordinal))
            throw new AiCostRiskExposureException("The risk decision is not bound to the final AI service charge.");

        var nodes = risk.EntityCluster.Nodes;
        if (EntityTypes.Any(type => nodes.All(node => node.Type != type)) ||
            !HasNode(nodes, RiskEntityType.Account, command.ActorId.ToString("N")) ||
            !HasNode(nodes, RiskEntityType.Tenant, command.TenantId.ToString("N")) ||
            !HasNode(nodes, RiskEntityType.ProviderObject, ProviderAccount(command.Provider, command.Model)) ||
            !HasNode(nodes, RiskEntityType.Session, command.RequestId.ToString("N")))
            throw new AiCostRiskExposureException("AI service entity-graph exposure is incomplete or unbound.");

        var dimensions = risk.Limits.Select(limit => limit.Key.Dimension).ToHashSet();
        if (LimitDimensions.Any(dimension => !dimensions.Contains(dimension)))
            throw new AiCostRiskExposureException("AI service aggregate-limit exposure is incomplete.");

        var subjects = risk.Limits.Select(limit => limit.Key).ToHashSet();
        var requiredSubjects = new[]
        {
            new RiskLimitKey(RiskLimitDimension.Wallet, command.SourceWalletId.Value.ToString("N")),
            new RiskLimitKey(RiskLimitDimension.IdentityCluster, risk.EntityCluster.Id),
            new RiskLimitKey(RiskLimitDimension.Tenant, command.TenantId.ToString("N")),
            new RiskLimitKey(RiskLimitDimension.ProviderAccount, ProviderAccount(command.Provider, command.Model)),
            new RiskLimitKey(RiskLimitDimension.GlobalLossBudget, GlobalLossBudget(command.TenantId))
        };
        if (requiredSubjects.Any(subject => !subjects.Contains(subject)) || risk.CounterReservationId == Guid.Empty)
            throw new AiCostRiskExposureException("AI service limits are not bound to every material subject.");

        var decision = _decisions.AuthorizeValueMovement(risk.Decision, context, command.AuthorizedAt);
        var counter = _counters.Reserve(
            risk.CounterReservationId,
            PostingTemplateKind.Burn,
            amount,
            risk.Limits,
            command.AuthorizedAt);
        return new AiCostRiskAuthorization(decision, counter);
    }

    public static string ProviderAccount(AiProvider provider, string model)
    {
        if (!Enum.IsDefined(provider)) throw new ArgumentOutOfRangeException(nameof(provider));
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return $"{provider}:{model.Trim()}";
    }

    public static string ProviderReference(Guid requestId, string serviceCode, AiProvider provider, string model)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("Request ID cannot be empty.", nameof(requestId));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceCode);
        return $"ai:{requestId:N}:{serviceCode.Trim()}:{ProviderAccount(provider, model)}";
    }

    public static string GlobalLossBudget(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        return $"ai-loss-budget:{tenantId:N}";
    }

    private static bool HasNode(IReadOnlyList<RiskEntityNode> nodes, RiskEntityType type, string subject) =>
        nodes.Any(node => node.Type == type && string.Equals(node.IdentifierHash, subject, StringComparison.Ordinal));
}

public sealed record AiCostRiskAuthorization(
    RiskAuthorization Decision,
    AggregateRiskCounterReservation CounterReservation);
