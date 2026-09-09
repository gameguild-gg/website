using System.Data;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed record SettleAuthoritativeMarketplaceOrderRequest(
    Guid OrderId,
    MarketplaceCurrencyChoice CurrencyChoice,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset SettledAt);

public sealed record DurableMarketplaceSettlementResult(
    Guid SettlementId,
    Guid OrderId,
    Guid ProductId,
    Guid BuyerId,
    Guid SellerId,
    MarketplaceSettlementStatus Status,
    MarketplaceEntitlementStatus EntitlementStatus,
    IReadOnlyList<MarketplacePriceLegSnapshot> Legs,
    PostingId PostingId,
    long JournalSequence,
    string JournalHash,
    bool IsDuplicate,
    DateTimeOffset SettledAt);

public interface IDurableMarketplaceSettlementService
{
    ValueTask<DurableMarketplaceSettlementResult> SettleAsync(
        SettleAuthoritativeMarketplaceOrderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DurableMarketplaceSettlementService : IDurableMarketplaceSettlementService
{
    private const string RegisteredCapabilityName = "marketplace-settlement";
    private readonly DbContext _db;
    private readonly IAuthoritativeMarketplaceOrderReader _orders;
    private readonly IDurableMarketplacePolicyReader _policies;
    private readonly IEconomyWalletDirectory _wallets;
    private readonly IMarketplaceFifoReservationGateway _reservations;
    private readonly IActorContextAccessor _actorContexts;
    private readonly IEconomyJurisdictionResolver _jurisdictions;
    private readonly IEconomyProtectedOperationOrchestrator _orchestrator;
    private readonly IRegisteredPostingCapabilityResolver _postingAuthority;
    private readonly IMarketplaceSettlementLedgerGateway _ledger;

    public DurableMarketplaceSettlementService(
        IApplicationDbContext context,
        IAuthoritativeMarketplaceOrderReader orders,
        IDurableMarketplacePolicyReader policies,
        IEconomyWalletDirectory wallets,
        IMarketplaceFifoReservationGateway reservations,
        IActorContextAccessor actorContexts,
        IEconomyJurisdictionResolver jurisdictions,
        IEconomyProtectedOperationOrchestrator orchestrator,
        IRegisteredPostingCapabilityResolver postingAuthority,
        IMarketplaceSettlementLedgerGateway ledger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(wallets);
        ArgumentNullException.ThrowIfNull(reservations);
        ArgumentNullException.ThrowIfNull(actorContexts);
        ArgumentNullException.ThrowIfNull(jurisdictions);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(postingAuthority);
        ArgumentNullException.ThrowIfNull(ledger);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable Marketplace settlement requires the application's relational DbContext.");
        _orders = orders;
        _policies = policies;
        _wallets = wallets;
        _reservations = reservations;
        _actorContexts = actorContexts;
        _jurisdictions = jurisdictions;
        _orchestrator = orchestrator;
        _postingAuthority = postingAuthority;
        _ledger = ledger;
    }

    public async ValueTask<DurableMarketplaceSettlementResult> SettleAsync(
        SettleAuthoritativeMarketplaceOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var actor = RequiredActor();
        var jurisdiction = await _jurisdictions.ResolveAsync(
            actor.TenantId, actor.ActorId, null, null, request.SettledAt, cancellationToken);
        var idempotencyHash = Hash(request.IdempotencyKey.Value);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async transactionToken =>
        {
        var order = await _orders.ReadAsync(
            actor.TenantId, actor.ActorId, request.OrderId, transactionToken);
        var duplicate = await _db.Set<MarketplaceSettlementRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.TenantId == actor.TenantId && row.OrderId == request.OrderId,
                transactionToken);
        if (duplicate is not null)
        {
            if (!string.Equals(duplicate.IdempotencyKey, idempotencyHash, StringComparison.Ordinal))
                throw new MarketplaceIdempotencyConflictException(
                    "The authoritative order is already bound to another settlement request.");
            return await MapAsync(duplicate, true, transactionToken);
        }

        var policy = await _policies.GetEffectiveAsync(
            actor.TenantId, order.ProductId, request.SettledAt, transactionToken);
        if (policy.Policy.SellerId != order.SellerId)
            throw new MarketplaceOrderSnapshotException(
                "The signed coin policy does not match the authoritative product creator.");
        var quote = ScaleQuote(
            policy.Policy.Quote(request.CurrencyChoice),
            order.Quantity,
            policy.Policy.PlatformFeePpm);
        var buyerWallet = await _wallets.GetOwnerWalletAsync(
            actor.TenantId, order.BuyerId, transactionToken);
        var sellerWallet = await _wallets.GetOwnerWalletAsync(
            actor.TenantId, order.SellerId, transactionToken);
        var platformWallet = await _wallets.GetWalletAsync(
            actor.TenantId, new WalletId(policy.PlatformFeeWalletId), transactionToken);
        if (buyerWallet.WalletId == sellerWallet.WalletId ||
            buyerWallet.WalletId == platformWallet.WalletId ||
            sellerWallet.WalletId == platformWallet.WalletId)
            throw new MarketplaceOrderSnapshotException(
                "Marketplace settlement wallets must be distinct.");

        var settlementId = DeterministicId(order.OrderId, "settlement");
        var postingId = new PostingId(DeterministicId(settlementId, "posting"));
        var entitlementId = DeterministicId(settlementId, "entitlement");
        var reservations = _reservations.Reserve(new MarketplaceFifoReservationRequest(
            settlementId,
            buyerWallet.WalletId,
            quote.Legs.Select(leg => leg.Amount).ToArray(),
            request.SettledAt));
        var sourceRoots = reservations
            .Select(item => item.RootSourceStampId)
            .Distinct()
            .OrderBy(root => root.Value)
            .ToArray();
        var destinationHash = Hash(
            $"{sellerWallet.WalletId.Value:N}:{platformWallet.WalletId.Value:N}");
        var intent = new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.MarketplaceSettlement,
            PostingTemplateKind.MarketplaceSettlement,
            buyerWallet.WalletId,
            sellerWallet.WalletId,
            quote.Legs[0].Amount,
            quote.Legs.Select(leg => new RiskCurrencyLeg(leg.Currency, leg.Units)).ToArray(),
            sourceRoots,
            policy.PayloadHash,
            destinationHash,
            request.IdempotencyKey,
            request.SettledAt,
            DestinationJurisdictionCode: jurisdiction.JurisdictionCode);
        return await _orchestrator.ExecuteAsync(intent, async (authorization, operationToken) =>
        {
            EnsureAuthorization(
                authorization, actor, jurisdiction, policy.PayloadHash, destinationHash);
            var receipt = authorization.Receipt;
            var authority = await _postingAuthority.ResolveAuthorityAsync(
                RegisteredCapabilityName,
                PostingTemplateKind.MarketplaceSettlement,
                receipt,
                operationToken);
            var posting = _ledger.Settle(new PersistedMarketplaceSettlementRequest(
                authority,
                receipt,
                settlementId,
                postingId,
                new IdempotencyKey(idempotencyHash),
                new PersistedMarketplaceOrderSnapshot(
                    order.OrderId,
                    order.OrderLineItemId,
                    order.ProductId,
                    order.ProductPricingVersionId,
                    order.PriceVersionSnapshot,
                    order.Quantity,
                    order.UnitPriceSnapshot,
                    order.FiatCurrencySnapshot,
                    order.SnapshotHash),
                order.BuyerId,
                buyerWallet.WalletId,
                order.SellerId,
                sellerWallet.WalletId,
                platformWallet.WalletId,
                quote.PolicyVersion,
                (int)quote.Mode,
                quote.Legs.Select(leg => new PersistedMarketplacePriceLeg(
                    leg.Currency, leg.Units, leg.SellerUnits, leg.PlatformFeeUnits)).ToArray(),
                reservations.Select(item => item.Id).ToArray(),
                entitlementId,
                request.SettledAt + policy.RefundHold,
                request.SettledAt));
            if (posting.PostingId != postingId)
                throw new RegisteredPostingRejectedException(
                    "The Marketplace writer returned an unexpected posting identity.");

            var persisted = await _db.Set<MarketplaceSettlementRow>()
                .AsNoTracking()
                .SingleAsync(row => row.Id == settlementId, operationToken);
            return await MapAsync(persisted, posting.IsDuplicate, operationToken);
        }, transactionToken);
        }, cancellationToken);
    }

    private async ValueTask<DurableMarketplaceSettlementResult> MapAsync(
        MarketplaceSettlementRow row,
        bool duplicate,
        CancellationToken cancellationToken)
    {
        var legs = await _db.Set<MarketplaceSettlementLegRow>()
            .AsNoTracking()
            .Where(leg => leg.SettlementId == row.Id)
            .OrderBy(leg => leg.Currency)
            .Select(leg => new MarketplacePriceLegSnapshot(
                leg.Currency, leg.Units, leg.SellerUnits, leg.PlatformFeeUnits))
            .ToArrayAsync(cancellationToken);
        return new DurableMarketplaceSettlementResult(
            row.Id,
            row.OrderId,
            row.ProductId,
            row.BuyerId,
            row.SellerId,
            row.Status,
            row.EntitlementStatus,
            Array.AsReadOnly(legs),
            new PostingId(row.PostingId),
            row.JournalSequence,
            row.JournalHash,
            duplicate,
            row.SettledAt);
    }

    private static MarketplaceQuoteSnapshot ScaleQuote(
        MarketplaceQuoteSnapshot unitQuote,
        int quantity,
        int platformFeePpm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        var legs = unitQuote.Legs.Select(leg =>
        {
            var units = checked(leg.Units * quantity);
            var fee = ProductCurrencyPolicyVersion.CalculateFee(units, platformFeePpm);
            return new MarketplacePriceLegSnapshot(leg.Currency, units, units - fee, fee);
        }).ToArray();
        return new MarketplaceQuoteSnapshot(
            unitQuote.ProductId,
            unitQuote.SellerId,
            unitQuote.PolicyVersion,
            unitQuote.Mode,
            legs);
    }

    private static void Validate(SettleAuthoritativeMarketplaceOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrderId == Guid.Empty)
            throw new ArgumentException("Order ID is required.", nameof(request));
        if (!Enum.IsDefined(request.CurrencyChoice))
            throw new ArgumentOutOfRangeException(nameof(request));
    }

    private ProtectedActor RequiredActor()
    {
        var actor = _actorContexts.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "Marketplace settlement requires an authenticated tenant actor.");
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
            throw new MarketplaceOrderSnapshotException(
                "The protected operation authorization does not match the Marketplace settlement.");
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
