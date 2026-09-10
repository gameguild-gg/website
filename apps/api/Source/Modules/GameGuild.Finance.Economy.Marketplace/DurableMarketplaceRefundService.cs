using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Marketplace;

public enum MarketplaceRefundAuthority
{
    SelfService = 1,
    Operations = 2
}

public sealed record RefundAuthoritativeMarketplaceOrderRequest(
    MarketplaceRefundAuthority Authority,
    Guid SettlementId,
    int Quantity,
    string ReasonCode,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RefundedAt);

public sealed record DurableMarketplaceRefundDebt(
    WalletId ResponsibleWalletId,
    CurrencyCode Currency,
    long Units,
    string EvidenceHash);

public sealed record DurableMarketplaceRefundResult(
    Guid RefundId,
    Guid SettlementId,
    int Quantity,
    int CumulativeRefundedQuantity,
    MarketplaceSettlementStatus SettlementStatus,
    MarketplaceEntitlementStatus EntitlementStatus,
    IReadOnlyList<CoinAmount> Legs,
    IReadOnlyList<DurableMarketplaceRefundDebt> Debts,
    PostingId PostingId,
    long JournalSequence,
    string JournalHash,
    bool IsDuplicate,
    DateTimeOffset RefundedAt);

public interface IDurableMarketplaceRefundService
{
    ValueTask<DurableMarketplaceRefundResult> RefundAsync(
        RefundAuthoritativeMarketplaceOrderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DurableMarketplaceRefundService : IDurableMarketplaceRefundService
{
    private const string RegisteredCapabilityName = "marketplace-refund";
    private readonly DbContext _db;
    private readonly IDurableMarketplacePolicyReader _policies;
    private readonly IActorContextAccessor _actorContexts;
    private readonly IEconomyJurisdictionResolver _jurisdictions;
    private readonly IEconomyProtectedOperationOrchestrator _orchestrator;
    private readonly IRegisteredPostingCapabilityResolver _postingAuthority;
    private readonly IMarketplaceRefundLedgerGateway _ledger;

    public DurableMarketplaceRefundService(
        IApplicationDbContext context,
        IDurableMarketplacePolicyReader policies,
        IActorContextAccessor actorContexts,
        IEconomyJurisdictionResolver jurisdictions,
        IEconomyProtectedOperationOrchestrator orchestrator,
        IRegisteredPostingCapabilityResolver postingAuthority,
        IMarketplaceRefundLedgerGateway ledger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(actorContexts);
        ArgumentNullException.ThrowIfNull(jurisdictions);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(postingAuthority);
        ArgumentNullException.ThrowIfNull(ledger);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable Marketplace refunds require the application's relational DbContext.");
        _policies = policies;
        _actorContexts = actorContexts;
        _jurisdictions = jurisdictions;
        _orchestrator = orchestrator;
        _postingAuthority = postingAuthority;
        _ledger = ledger;
    }

    public async ValueTask<DurableMarketplaceRefundResult> RefundAsync(
        RefundAuthoritativeMarketplaceOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var actor = RequiredActor();
        var jurisdiction = await _jurisdictions.ResolveAsync(
            actor.TenantId, actor.ActorId, null, null, request.RefundedAt, cancellationToken);
        var idempotencyHash = Hash(request.IdempotencyKey.Value);
        var reasonCode = request.ReasonCode.Trim().ToUpperInvariant();
        var reasonHash = Hash(reasonCode);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async transactionToken =>
        {

        var duplicate = await _db.Set<MarketplaceRefundRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.TenantId == actor.TenantId &&
                                         row.IdempotencyKey == idempotencyHash,
                transactionToken);
        if (duplicate is not null)
        {
            if (duplicate.SettlementId != request.SettlementId ||
                duplicate.Quantity != request.Quantity ||
                !string.Equals(duplicate.ReasonHash, reasonHash, StringComparison.Ordinal))
                throw new MarketplaceIdempotencyConflictException(
                    "The idempotency key is already bound to another Marketplace refund.");
            return await MapAsync(duplicate, true, transactionToken);
        }

        var settlement = await _db.Set<MarketplaceSettlementRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.TenantId == actor.TenantId &&
                                         row.Id == request.SettlementId,
                transactionToken)
            ?? throw new MarketplaceRefundException("The Marketplace settlement was not found.");
        if (request.Authority == MarketplaceRefundAuthority.SelfService &&
            settlement.BuyerId != actor.ActorId)
            throw new MarketplaceRefundException(
                "Only the authoritative order buyer can request a self-service refund.");
        if (settlement.Status == MarketplaceSettlementStatus.Refunded ||
            settlement.RefundedQuantity >= settlement.Quantity)
            throw new MarketplaceAlreadyRefundedException(
                "The Marketplace settlement has already been refunded in full.");
        var cumulativeQuantity = checked(settlement.RefundedQuantity + request.Quantity);
        if (cumulativeQuantity > settlement.Quantity)
            throw new MarketplaceRefundException(
                "The requested quantity exceeds the refundable order quantity.");

        var policy = await _policies.GetVersionAsync(
            actor.TenantId, settlement.ProductId, settlement.PolicyVersion, transactionToken);
        if (policy.Policy.SellerId != settlement.SellerId ||
            policy.PlatformFeeWalletId != settlement.PlatformFeeWalletId)
            throw new MarketplaceRefundException(
                "The historical signed policy does not match the persisted settlement.");

        var persistedLegs = await _db.Set<MarketplaceSettlementLegRow>()
            .AsNoTracking()
            .Where(row => row.SettlementId == settlement.Id)
            .OrderBy(row => row.Currency)
            .ToArrayAsync(transactionToken);
        if (persistedLegs.Length == 0)
            throw new MarketplaceRefundException("The settlement has no durable price legs.");
        var refundLegs = persistedLegs.Select(leg =>
        {
            var cumulativeTarget = checked((long)decimal.Truncate(
                (decimal)leg.Units * cumulativeQuantity / settlement.Quantity));
            if (cumulativeTarget < leg.RefundedUnits || cumulativeTarget > leg.Units)
                throw new MarketplaceRefundException(
                    "The persisted Marketplace refund counters are inconsistent.");
            return new PersistedMarketplaceRefundLeg(
                leg.Currency, checked(cumulativeTarget - leg.RefundedUnits));
        }).Where(leg => leg.Units > 0).ToArray();
        if (refundLegs.Length == 0)
            throw new MarketplaceRefundException(
                "The requested quantity does not produce a positive refundable amount.");

        var rootPayloads = await _db.Set<MarketplaceFundingFragmentRow>()
            .AsNoTracking()
            .Where(row => row.SettlementId == settlement.Id)
            .Select(row => row.SelectedRootRanges)
            .ToArrayAsync(transactionToken);
        var sourceRoots = ExtractSourceRoots(rootPayloads);
        var refundId = DeterministicId(settlement.Id, idempotencyHash);
        var postingId = new PostingId(DeterministicId(refundId, "posting"));
        var destinationHash = Hash(settlement.BuyerWalletId.ToString("N"));
        var intent = new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.MarketplaceRefund,
            PostingTemplateKind.MarketplaceRefund,
            new WalletId(settlement.SellerWalletId),
            new WalletId(settlement.BuyerWalletId),
            new CoinAmount(refundLegs[0].Currency, refundLegs[0].Units),
            refundLegs.Select(leg => new RiskCurrencyLeg(leg.Currency, leg.Units)).ToArray(),
            sourceRoots,
            policy.PayloadHash,
            destinationHash,
            request.IdempotencyKey,
            request.RefundedAt,
            DestinationJurisdictionCode: jurisdiction.JurisdictionCode);
        return await _orchestrator.ExecuteAsync(intent, async (authorization, operationToken) =>
        {
            EnsureAuthorization(
                authorization, actor, jurisdiction, policy.PayloadHash, destinationHash);
            var receipt = authorization.Receipt;
            var authority = await _postingAuthority.ResolveAuthorityAsync(
                RegisteredCapabilityName,
                PostingTemplateKind.MarketplaceRefund,
                receipt,
                operationToken);
            var posting = _ledger.Refund(new PersistedMarketplaceRefundRequest(
                authority,
                receipt,
                refundId,
                settlement.Id,
                postingId,
                new IdempotencyKey(idempotencyHash),
                settlement.BuyerId,
                settlement.PolicyVersion,
                request.Quantity,
                cumulativeQuantity,
                refundLegs,
                reasonCode,
                reasonHash,
                request.RefundedAt));
            if (posting.PostingId != postingId)
                throw new RegisteredPostingRejectedException(
                    "The Marketplace writer returned an unexpected refund posting identity.");

            var persisted = await _db.Set<MarketplaceRefundRow>()
                .AsNoTracking()
                .SingleAsync(row => row.Id == refundId, operationToken);
            return await MapAsync(persisted, posting.IsDuplicate, operationToken);
        }, transactionToken);
        }, cancellationToken);
    }

    private async ValueTask<DurableMarketplaceRefundResult> MapAsync(
        MarketplaceRefundRow refund,
        bool duplicate,
        CancellationToken cancellationToken)
    {
        var settlement = await _db.Set<MarketplaceSettlementRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == refund.SettlementId, cancellationToken);
        var legs = await _db.Set<MarketplaceRefundLegRow>()
            .AsNoTracking()
            .Where(row => row.RefundId == refund.Id)
            .OrderBy(row => row.Currency)
            .Select(row => new CoinAmount(row.Currency, row.Units))
            .ToArrayAsync(cancellationToken);
        var debts = await _db.Set<MarketplaceRefundDebtRow>()
            .AsNoTracking()
            .Where(row => row.RefundId == refund.Id)
            .OrderBy(row => row.Currency)
            .ThenBy(row => row.ResponsibleWalletId)
            .Select(row => new DurableMarketplaceRefundDebt(
                new WalletId(row.ResponsibleWalletId), row.Currency, row.AmountUnits, row.EvidenceHash))
            .ToArrayAsync(cancellationToken);
        return new DurableMarketplaceRefundResult(
            refund.Id,
            refund.SettlementId,
            refund.Quantity,
            refund.RefundedQuantity,
            settlement.Status,
            settlement.EntitlementStatus,
            Array.AsReadOnly(legs),
            Array.AsReadOnly(debts),
            new PostingId(refund.PostingId),
            refund.FirstJournalSequence,
            refund.JournalHash,
            duplicate,
            refund.RefundedAt);
    }

    private static IReadOnlyList<SourceStampId> ExtractSourceRoots(IEnumerable<string> payloads)
    {
        var roots = new HashSet<Guid>();
        foreach (var payload in payloads)
        {
            using var document = JsonDocument.Parse(payload);
            CollectRootIds(document.RootElement, roots);
        }
        if (roots.Count == 0)
            throw new MarketplaceRefundException(
                "The settlement funding provenance contains no source roots.");
        return roots.Order().Select(root => new SourceStampId(root)).ToArray();
    }

    private static void CollectRootIds(JsonElement element, ISet<Guid> roots)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (Normalize(property.Name) == "rootsourcestampid" &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(property.Value.GetString(), out var rootId))
                    roots.Add(rootId);
                CollectRootIds(property.Value, roots);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) CollectRootIds(item, roots);
        }
    }

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static void Validate(RefundAuthoritativeMarketplaceOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SettlementId == Guid.Empty)
            throw new ArgumentException("Settlement ID is required.", nameof(request));
        if (!Enum.IsDefined(request.Authority)) throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Quantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReasonCode);
        if (request.ReasonCode.Trim().Length > 100)
            throw new ArgumentOutOfRangeException(nameof(request));
    }

    private ProtectedActor RequiredActor()
    {
        var actor = _actorContexts.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "Marketplace refund requires an authenticated tenant actor.");
        return new ProtectedActor(tenantId, actorId);
    }

    private static void EnsureAuthorization(
        EconomyProtectedOperationAuthorization authorization,
        ProtectedActor actor,
        EconomyJurisdictionResolution jurisdiction,
        string providerHash,
        string destinationHash)
    {
        if (authorization.TenantId != actor.TenantId || authorization.ActorId != actor.ActorId ||
            authorization.JurisdictionCode != jurisdiction.JurisdictionCode ||
            authorization.Receipt.ProviderHash != providerHash ||
            authorization.Receipt.DestinationHash != destinationHash)
            throw new MarketplaceRefundException(
                "The protected operation authorization does not match the Marketplace refund.");
    }

    private static Guid DeterministicId(Guid source, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{source:N}:marketplace:{purpose}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record ProtectedActor(Guid TenantId, Guid ActorId);
}
