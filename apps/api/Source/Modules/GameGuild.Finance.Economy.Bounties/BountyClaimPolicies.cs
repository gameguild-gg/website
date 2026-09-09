using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Bounties;

public sealed class BountyEligibilityPolicy
{
    public void EnsureEligible(
        BountyEligibilityRequirements requirements,
        BountyEligibilitySnapshot snapshot,
        Guid claimantId,
        DateTimeOffset claimedAt)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (claimantId == Guid.Empty || snapshot.ClaimantId != claimantId)
            throw new BountyClaimIneligibleException("Eligibility evidence is not bound to the claimant.");
        if (snapshot.ObservedAt > claimedAt || snapshot.ExpiresAt <= claimedAt ||
            snapshot.ExpiresAt <= snapshot.ObservedAt)
            throw new BountyClaimIneligibleException("Eligibility evidence is unavailable or stale.");
        if (requirements.RequiresPrerequisite && !snapshot.PrerequisiteCompleted)
            throw new BountyClaimIneligibleException("The claimant has not completed the required prerequisite.");
        if (snapshot.Reputation < requirements.MinimumReputation)
            throw new BountyClaimIneligibleException("The claimant does not meet the reputation threshold.");
        if (requirements.RequiresInstructorVerification && !snapshot.InstructorVerified)
            throw new BountyClaimIneligibleException("The claimant is not instructor verified.");
    }
}

public sealed record BountyClaimRiskAuthorization(
    RiskAuthorization Decision,
    AggregateRiskCounterReservation Counters);

public sealed class BountyClaimRiskGate
{
    private static readonly IReadOnlyList<RiskEntityType> EntityTypes = Array.AsReadOnly(
        new[]
        {
            RiskEntityType.Account,
            RiskEntityType.Referral,
            RiskEntityType.DeviceRiskToken,
            RiskEntityType.PaymentInstrument,
            RiskEntityType.PayoutDestination,
            RiskEntityType.MarketplaceCounterparty
        });

    private static readonly IReadOnlyList<RiskLimitDimension> LimitDimensions = Array.AsReadOnly(
        new[]
        {
            RiskLimitDimension.Wallet,
            RiskLimitDimension.IdentityCluster,
            RiskLimitDimension.SourceRoot,
            RiskLimitDimension.Destination,
            RiskLimitDimension.CounterpartyPair,
            RiskLimitDimension.DeviceIpAsnCluster
        });

    private readonly RiskDecisionAuthorizer _decisions;
    private readonly AggregateRiskCounterStore _counters;

    public BountyClaimRiskGate(
        RiskDecisionAuthorizer decisions,
        AggregateRiskCounterStore counters)
    {
        _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        _counters = counters ?? throw new ArgumentNullException(nameof(counters));
    }

    public static IReadOnlyList<RiskEntityType> RequiredEntityTypes => EntityTypes;
    public static IReadOnlyList<RiskLimitDimension> RequiredLimitDimensions => LimitDimensions;

    public BountyClaimRiskAuthorization Authorize(
        BountyEscrowPosition bounty,
        Guid claimantId,
        WalletId claimantWalletId,
        BountyClaimRiskApproval approval,
        DateTimeOffset claimedAt)
    {
        ArgumentNullException.ThrowIfNull(bounty);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(approval.Decision);
        ArgumentNullException.ThrowIfNull(approval.Context);
        ArgumentNullException.ThrowIfNull(approval.EntityCluster);
        ArgumentNullException.ThrowIfNull(approval.Limits);

        var context = approval.Context;
        var roots = bounty.EscrowFragments
            .SelectMany(fragment => fragment.SelectedRanges)
            .Select(range => range.Root)
            .Distinct()
            .OrderBy(root => root.Value)
            .ToArray();
        var contextRoots = context.SourceRoots.Distinct().OrderBy(root => root.Value).ToArray();

        if (claimantId == Guid.Empty ||
            context.ActorId != claimantId ||
            context.Operation != PostingTemplateKind.Reclaim ||
            context.SourceWalletId != bounty.EscrowWalletId ||
            context.DestinationWalletId != claimantWalletId ||
            context.Amount != bounty.Amount ||
            context.CurrencyLegs.Count != 1 ||
            context.CurrencyLegs[0] != new RiskCurrencyLeg(bounty.Amount.Currency, bounty.Amount.Units) ||
            !roots.SequenceEqual(contextRoots) ||
            !string.Equals(context.ProviderReferenceHash, ProviderReference(bounty.Id), StringComparison.Ordinal) ||
            context.EntityGraphVersion != approval.EntityCluster.Version ||
            !string.Equals(context.EntityGraphEvidenceHash, approval.EntityCluster.EvidenceHash, StringComparison.Ordinal))
            throw new BountyRiskExposureException("The risk decision is not bound to the final bounty claim.");

        var presentTypes = approval.EntityCluster.Nodes.Select(node => node.Type).ToHashSet();
        if (EntityTypes.Any(type => !presentTypes.Contains(type)))
            throw new BountyRiskExposureException("Bounty claim entity-graph exposure is incomplete.");

        var presentDimensions = approval.Limits.Select(limit => limit.Key.Dimension).ToHashSet();
        if (LimitDimensions.Any(dimension => !presentDimensions.Contains(dimension)))
            throw new BountyRiskExposureException("Bounty claim aggregate-limit exposure is incomplete.");

        var rootSubjects = approval.Limits
            .Where(limit => limit.Key.Dimension == RiskLimitDimension.SourceRoot)
            .Select(limit => limit.Key.SubjectHash)
            .ToHashSet(StringComparer.Ordinal);
        if (roots.Any(root => !rootSubjects.Contains(root.Value.ToString("N"))))
            throw new BountyRiskExposureException("Every bounty source root requires an aggregate limit.");

        if (!approval.Limits.Any(limit =>
                limit.Key.Dimension == RiskLimitDimension.Destination &&
                string.Equals(limit.Key.SubjectHash, claimantWalletId.Value.ToString("N"), StringComparison.Ordinal)) ||
            !approval.Limits.Any(limit =>
                limit.Key.Dimension == RiskLimitDimension.CounterpartyPair &&
                string.Equals(limit.Key.SubjectHash, CounterpartyPair(bounty.PosterId, claimantId), StringComparison.Ordinal)))
            throw new BountyRiskExposureException("Destination and counterparty-pair limits must bind the claim.");

        var decision = _decisions.AuthorizeValueMovement(approval.Decision, context, claimedAt);
        var counters = _counters.Reserve(
            approval.CounterReservationId,
            PostingTemplateKind.Reclaim,
            bounty.Amount,
            approval.Limits,
            claimedAt);
        return new BountyClaimRiskAuthorization(decision, counters);
    }

    public static string ProviderReference(BountyId bountyId) =>
        $"bounty:{bountyId.Value:N}:claim";

    public static string CounterpartyPair(Guid posterId, Guid claimantId)
    {
        if (posterId == Guid.Empty) throw new ArgumentException("Poster ID cannot be empty.", nameof(posterId));
        if (claimantId == Guid.Empty) throw new ArgumentException("Claimant ID cannot be empty.", nameof(claimantId));
        return $"{posterId:N}:{claimantId:N}";
    }
}
