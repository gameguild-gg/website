using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Bounties.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties;

public sealed record CreateDurableBountyRequest(
    CoinAmount Amount,
    BountyEligibilityRequirements Eligibility,
    DateTimeOffset ExpiresAt,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record ClaimDurableBountyRequest(
    BountyId BountyId,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record ReclaimDurableBountyRequest(
    BountyId BountyId,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record DurableBountyView(
    BountyId Id,
    Guid PosterId,
    CoinAmount Amount,
    BountyEligibilityRequirements Eligibility,
    int ReclaimFeePpm,
    BountyStatus Status,
    DateTimeOffset PostedAt,
    DateTimeOffset ExpiresAt,
    long Version,
    PersistedBountyTerminalEvent? TerminalEvent);

public interface IDurableBountyApplicationService
{
    ValueTask<DurableBountyView> CreateAsync(
        CreateDurableBountyRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<DurableBountyView> ClaimAsync(
        ClaimDurableBountyRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<DurableBountyView> ReclaimAsync(
        ReclaimDurableBountyRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<DurableBountyView?> FindAsync(
        Guid tenantId,
        BountyId bountyId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<DurableBountyView>> ListAsync(
        Guid tenantId,
        BountyStatus? status,
        CancellationToken cancellationToken = default);
}

public sealed class DurableBountyApplicationService : IDurableBountyApplicationService
{
    private const string EscrowCapabilityName = "bounty-escrow";
    private const string ClaimCapabilityName = "bounty-claim";
    private const string ReclaimCapabilityName = "bounty-reclaim";
    private readonly DbContext _db;
    private readonly IEconomyWalletDirectory _wallets;
    private readonly IBountyPostableLotReader _lots;
    private readonly IBountyEscrowStore _escrows;
    private readonly IBountyTerminalEventStore _terminals;
    private readonly IActorContextAccessor _actorContexts;
    private readonly IEconomyJurisdictionResolver _jurisdictions;
    private readonly IEconomyCapabilityPolicyStore _policies;
    private readonly IEconomyProtectedOperationOrchestrator _orchestrator;
    private readonly IRegisteredPostingCapabilityResolver _postingAuthority;
    private readonly IDurableBountyEscrowPostWorkflow _posts;
    private readonly IDurableBountyClaimWorkflow _claims;
    private readonly IDurableBountyReclaimWorkflow _reclaims;

    public DurableBountyApplicationService(
        IApplicationDbContext context,
        IEconomyWalletDirectory wallets,
        IBountyPostableLotReader lots,
        IBountyEscrowStore escrows,
        IBountyTerminalEventStore terminals,
        IActorContextAccessor actorContexts,
        IEconomyJurisdictionResolver jurisdictions,
        IEconomyCapabilityPolicyStore policies,
        IEconomyProtectedOperationOrchestrator orchestrator,
        IRegisteredPostingCapabilityResolver postingAuthority,
        IDurableBountyEscrowPostWorkflow posts,
        IDurableBountyClaimWorkflow claims,
        IDurableBountyReclaimWorkflow reclaims)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Durable Bounty application flows require the application's relational DbContext.");
        _wallets = wallets ?? throw new ArgumentNullException(nameof(wallets));
        _lots = lots ?? throw new ArgumentNullException(nameof(lots));
        _escrows = escrows ?? throw new ArgumentNullException(nameof(escrows));
        _terminals = terminals ?? throw new ArgumentNullException(nameof(terminals));
        _actorContexts = actorContexts ?? throw new ArgumentNullException(nameof(actorContexts));
        _jurisdictions = jurisdictions ?? throw new ArgumentNullException(nameof(jurisdictions));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _postingAuthority = postingAuthority ?? throw new ArgumentNullException(nameof(postingAuthority));
        _posts = posts ?? throw new ArgumentNullException(nameof(posts));
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
        _reclaims = reclaims ?? throw new ArgumentNullException(nameof(reclaims));
    }

    public async ValueTask<DurableBountyView> CreateAsync(
        CreateDurableBountyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Eligibility);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Amount.Units);
        var actor = RequiredActor();
        var jurisdiction = await _jurisdictions.ResolveAsync(
            actor.TenantId, actor.ActorId, null, null, request.RequestedAt, cancellationToken);
        var policy = await RequiredPolicyAsync(
            actor.TenantId, EconomyValueMovementCapability.BountyEscrow,
            jurisdiction.JurisdictionCode, cancellationToken);
        var settings = ParseEscrowPolicy(policy);
        var lifetime = request.ExpiresAt - request.RequestedAt;
        if (lifetime < settings.MinimumLifetime || lifetime > settings.MaximumLifetime)
            throw new BountyPolicyUnavailableException("Bounty lifetime is outside the signed policy window.");

        var posterWallet = await _wallets.GetOwnerWalletAsync(
            actor.TenantId, actor.ActorId, cancellationToken);
        var escrowWallet = await _wallets.GetWalletAsync(
            actor.TenantId, settings.EscrowWalletId, cancellationToken);
        if (posterWallet.WalletId == escrowWallet.WalletId)
            throw new BountyPolicyUnavailableException("The signed bounty escrow wallet cannot be the poster wallet.");

        var bountyId = DeterministicBountyId(actor.TenantId, request.IdempotencyKey);
        var preview = BountyEscrowPositionFactory.Create(new PostBountyCommand(
            bountyId,
            actor.ActorId,
            posterWallet.WalletId,
            escrowWallet.WalletId,
            request.Amount,
            _lots.Read(posterWallet.WalletId, request.Amount.Currency, request.RequestedAt),
            request.Eligibility,
            settings.ReclaimFeePpm,
            request.RequestedAt,
            request.ExpiresAt,
            request.IdempotencyKey));
        var requestHash = Hash(string.Join('|',
            actor.TenantId.ToString("N"), actor.ActorId.ToString("N"), bountyId.Value.ToString("N"),
            (int)request.Amount.Currency, request.Amount.Units, request.ExpiresAt.UtcTicks,
            settings.ReclaimFeePpm, policy.PayloadHash, request.IdempotencyKey.Value));
        var intent = new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.BountyEscrow,
            PostingTemplateKind.BountyEscrow,
            posterWallet.WalletId,
            escrowWallet.WalletId,
            request.Amount,
            [new RiskCurrencyLeg(request.Amount.Currency, request.Amount.Units)],
            Roots(preview.EscrowFragments),
            policy.PayloadHash,
            Hash(escrowWallet.WalletId.Value.ToString("N")),
            request.IdempotencyKey,
            request.RequestedAt,
            DestinationJurisdictionCode: jurisdiction.JurisdictionCode);
        return await _orchestrator.ExecuteAsync(intent, async (authorization, token) =>
        {
            EnsureAuthorization(authorization, actor, jurisdiction, policy);
            var authority = await _postingAuthority.ResolveAuthorityAsync(
                EscrowCapabilityName, PostingTemplateKind.BountyEscrow,
                authorization.Receipt, token);
            var persisted = await _posts.PostAsync(new DurableBountyEscrowPostRequest(
                bountyId,
                actor.ActorId,
                posterWallet.WalletId,
                escrowWallet.WalletId,
                request.Amount,
                request.Eligibility,
                settings.ReclaimFeePpm,
                request.RequestedAt,
                request.ExpiresAt,
                request.IdempotencyKey,
                requestHash,
                authority,
                new ReserveVersion(authorization.Receipt.ReserveVersion),
                new PolicyVersion(authorization.Receipt.PolicyVersion),
                authorization.Receipt.ReceiptHash), token);
            return Map(persisted, null);
        }, cancellationToken);
    }

    public async ValueTask<DurableBountyView> ClaimAsync(
        ClaimDurableBountyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequiredActor();
        var jurisdiction = await _jurisdictions.ResolveAsync(
            actor.TenantId, actor.ActorId, null, null, request.RequestedAt, cancellationToken);
        var escrow = _escrows.Get(actor.TenantId, request.BountyId);
        var policy = await RequiredPolicyAsync(
            actor.TenantId, EconomyValueMovementCapability.BountyClaim,
            jurisdiction.JurisdictionCode, cancellationToken);
        var claimantWallet = await _wallets.GetOwnerWalletAsync(
            actor.TenantId, actor.ActorId, cancellationToken);
        var intent = new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.BountyClaim,
            PostingTemplateKind.BountyClaim,
            escrow.EscrowWalletId,
            claimantWallet.WalletId,
            escrow.Amount,
            [new RiskCurrencyLeg(escrow.Amount.Currency, escrow.Amount.Units)],
            Roots(escrow.Fragments),
            policy.PayloadHash,
            Hash(claimantWallet.WalletId.Value.ToString("N")),
            request.IdempotencyKey,
            request.RequestedAt,
            DestinationJurisdictionCode: jurisdiction.JurisdictionCode);
        return await _orchestrator.ExecuteAsync(intent, async (authorization, token) =>
        {
            EnsureAuthorization(authorization, actor, jurisdiction, policy);
            var authority = await _postingAuthority.ResolveAuthorityAsync(
                ClaimCapabilityName, PostingTemplateKind.BountyClaim,
                authorization.Receipt, token);
            var terminal = await _claims.ClaimAsync(new DurableBountyClaimRequest(
                request.BountyId,
                actor.ActorId,
                claimantWallet.WalletId,
                request.RequestedAt,
                request.IdempotencyKey,
                authorization.Receipt.ReceiptHash,
                authority,
                new ReserveVersion(authorization.Receipt.ReserveVersion),
                new PolicyVersion(authorization.Receipt.PolicyVersion),
                authorization.Receipt.ReceiptHash), token);
            return Map(_escrows.Get(actor.TenantId, request.BountyId), terminal);
        }, cancellationToken);
    }

    public async ValueTask<DurableBountyView> ReclaimAsync(
        ReclaimDurableBountyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = RequiredActor();
        var jurisdiction = await _jurisdictions.ResolveAsync(
            actor.TenantId, actor.ActorId, null, null, request.RequestedAt, cancellationToken);
        var escrow = _escrows.Get(actor.TenantId, request.BountyId);
        var policy = await RequiredPolicyAsync(
            actor.TenantId, EconomyValueMovementCapability.BountyReclaim,
            jurisdiction.JurisdictionCode, cancellationToken);
        var posterWallet = await _wallets.GetOwnerWalletAsync(
            actor.TenantId, actor.ActorId, cancellationToken);
        var intent = new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.BountyReclaim,
            PostingTemplateKind.BountyReclaim,
            escrow.EscrowWalletId,
            posterWallet.WalletId,
            escrow.Amount,
            [new RiskCurrencyLeg(escrow.Amount.Currency, escrow.Amount.Units)],
            Roots(escrow.Fragments),
            policy.PayloadHash,
            Hash(posterWallet.WalletId.Value.ToString("N")),
            request.IdempotencyKey,
            request.RequestedAt,
            DestinationJurisdictionCode: jurisdiction.JurisdictionCode);
        return await _orchestrator.ExecuteAsync(intent, async (authorization, token) =>
        {
            EnsureAuthorization(authorization, actor, jurisdiction, policy);
            var authority = await _postingAuthority.ResolveAuthorityAsync(
                ReclaimCapabilityName, PostingTemplateKind.BountyReclaim,
                authorization.Receipt, token);
            var terminal = await _reclaims.ReclaimAsync(new DurableBountyReclaimRequest(
                request.BountyId,
                actor.ActorId,
                posterWallet.WalletId,
                request.RequestedAt,
                request.IdempotencyKey,
                authority,
                new ReserveVersion(authorization.Receipt.ReserveVersion),
                new PolicyVersion(authorization.Receipt.PolicyVersion),
                authorization.Receipt.ReceiptHash), token);
            return Map(_escrows.Get(actor.TenantId, request.BountyId), terminal);
        }, cancellationToken);
    }

    public async ValueTask<DurableBountyView?> FindAsync(
        Guid tenantId,
        BountyId bountyId,
        CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var exists = await _db.Set<BountyRow>().AsNoTracking()
            .AnyAsync(row => row.TenantId == tenantId && row.Id == bountyId.Value, cancellationToken);
        if (!exists) return null;
        return Map(_escrows.Get(tenantId, bountyId), _terminals.FindByBounty(tenantId, bountyId));
    }

    public async ValueTask<IReadOnlyList<DurableBountyView>> ListAsync(
        Guid tenantId,
        BountyStatus? status,
        CancellationToken cancellationToken = default)
    {
        ValidateTenant(tenantId);
        var query = _db.Set<BountyRow>().AsNoTracking().Where(row => row.TenantId == tenantId);
        if (status.HasValue) query = query.Where(row => row.Status == status.Value);
        var ids = await query.OrderByDescending(row => row.PostedAt).ThenBy(row => row.Id)
            .Select(row => row.Id).ToArrayAsync(cancellationToken);
        return ids.Select(id =>
        {
            var bountyId = new BountyId(id);
            return Map(_escrows.Get(tenantId, bountyId), _terminals.FindByBounty(tenantId, bountyId));
        }).ToArray();
    }

    private ProtectedActor RequiredActor()
    {
        var actor = _actorContexts.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "A Bounty value operation requires an authenticated tenant actor.");
        return new ProtectedActor(tenantId, actorId);
    }

    private static void EnsureAuthorization(
        EconomyProtectedOperationAuthorization authorization,
        ProtectedActor actor,
        EconomyJurisdictionResolution jurisdiction,
        EconomyCapabilityPolicy policy)
    {
        if (authorization.TenantId != actor.TenantId || authorization.ActorId != actor.ActorId ||
            authorization.JurisdictionCode != jurisdiction.JurisdictionCode ||
            authorization.Receipt.PolicyVersion != policy.Version ||
            authorization.Receipt.ProviderHash != policy.PayloadHash)
            throw new BountyPolicyUnavailableException(
                "The protected operation authorization does not match the selected Bounty policy.");
    }

    private async ValueTask<EconomyCapabilityPolicy> RequiredPolicyAsync(
        Guid tenantId,
        EconomyValueMovementCapability capability,
        string jurisdictionCode,
        CancellationToken cancellationToken)
    {
        var policy = await _policies.CurrentAsync(
            tenantId, capability, jurisdictionCode.Trim().ToUpperInvariant(), cancellationToken);
        return policy is { State: EconomyCapabilityPolicyState.Active }
            ? policy
            : throw new BountyPolicyUnavailableException(
                $"No active signed policy is available for {capability}.");
    }

    private static BountyEscrowPolicySettings ParseEscrowPolicy(EconomyCapabilityPolicy policy)
    {
        try
        {
            using var document = JsonDocument.Parse(policy.CanonicalPayload);
            var root = document.RootElement;
            var escrowWalletId = root.GetProperty("escrowWalletId").GetGuid();
            var reclaimFeePpm = root.GetProperty("reclaimFeePpm").GetInt32();
            var minimumLifetimeSeconds = root.GetProperty("minimumLifetimeSeconds").GetInt64();
            var maximumLifetimeSeconds = root.GetProperty("maximumLifetimeSeconds").GetInt64();
            if (escrowWalletId == Guid.Empty || reclaimFeePpm is < 0 or >= 1_000_000 ||
                minimumLifetimeSeconds <= 0 || maximumLifetimeSeconds < minimumLifetimeSeconds)
                throw new BountyPolicyUnavailableException("The active bounty escrow policy is invalid.");
            return new BountyEscrowPolicySettings(
                new WalletId(escrowWalletId),
                reclaimFeePpm,
                TimeSpan.FromSeconds(minimumLifetimeSeconds),
                TimeSpan.FromSeconds(maximumLifetimeSeconds));
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or
                                           FormatException or OverflowException or JsonException)
        {
            throw new BountyPolicyUnavailableException(
                "The active bounty escrow policy payload is invalid.", exception);
        }
    }

    private static IReadOnlyList<SourceStampId> Roots(IEnumerable<PersistedBountyEscrowFragment> fragments) =>
        fragments.SelectMany(fragment => fragment.SelectedRanges)
            .Select(range => range.Root)
            .Distinct()
            .OrderBy(root => root.Value)
            .ToArray();

    private static IReadOnlyList<SourceStampId> Roots(IEnumerable<BountyEscrowFragment> fragments) =>
        fragments.SelectMany(fragment => fragment.SelectedRanges)
            .Select(range => range.Root)
            .Distinct()
            .OrderBy(root => root.Value)
            .ToArray();

    private static DurableBountyView Map(
        PersistedBountyEscrow escrow,
        PersistedBountyTerminalEvent? terminal) => new(
        escrow.Id,
        escrow.PosterId,
        escrow.Amount,
        escrow.Eligibility,
        escrow.ReclaimFeePpm,
        terminal?.Status ?? escrow.Status,
        escrow.PostedAt,
        escrow.ExpiresAt,
        escrow.Version,
        terminal);

    private static BountyId DeterministicBountyId(Guid tenantId, IdempotencyKey idempotencyKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"economy-bounty-v1|{tenantId:N}|{idempotencyKey.Value}"));
        return new BountyId(new Guid(bytes.AsSpan(0, 16)));
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A non-quarantine tenant ID is required.", nameof(tenantId));
    }

    private sealed record BountyEscrowPolicySettings(
        WalletId EscrowWalletId,
        int ReclaimFeePpm,
        TimeSpan MinimumLifetime,
        TimeSpan MaximumLifetime);

    private sealed record ProtectedActor(Guid TenantId, Guid ActorId);
}

public sealed class BountyPolicyUnavailableException : InvalidOperationException
{
    public BountyPolicyUnavailableException(string message) : base(message) { }
    public BountyPolicyUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
