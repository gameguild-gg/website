using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Commerce.Orders;
using GameGuild.Commerce.Products;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Marketplace;

public enum MarketplaceOutboxProcessStatus
{
    NoWork = 0,
    Published = 1,
    Failed = 2
}

public sealed record MarketplaceOutboxProcessResult(
    MarketplaceOutboxProcessStatus Status,
    Guid? MessageId,
    string? Error);

public sealed record MarketplaceOutboxDispatchMessage(
    Guid Id,
    Guid TenantId,
    Guid SettlementId,
    string MessageType,
    JsonElement Payload,
    string PayloadHash,
    DateTimeOffset OccurredAt);

public interface IMarketplaceOutboxHandler
{
    ValueTask HandleAsync(
        MarketplaceOutboxDispatchMessage message,
        CancellationToken cancellationToken);
}

public interface IMarketplaceOutboxProcessor
{
    ValueTask<MarketplaceOutboxProcessResult> ProcessNextAsync(
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlMarketplaceOutboxProcessor : IMarketplaceOutboxProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private readonly DbContext _db;
    private readonly IMarketplaceOutboxHandler _handler;

    public PostgreSqlMarketplaceOutboxProcessor(
        IApplicationDbContext context,
        IMarketplaceOutboxHandler handler)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handler);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Marketplace outbox processing requires the application's relational DbContext.");
        _handler = handler;
    }

    public async ValueTask<MarketplaceOutboxProcessResult> ProcessNextAsync(
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        var normalizedOwner = owner.Trim();
        var row = await ClaimAsync(normalizedOwner, now, cancellationToken);
        if (row is null)
            return new MarketplaceOutboxProcessResult(MarketplaceOutboxProcessStatus.NoWork, null, null);

        try
        {
            var message = ValidateAndMap(row);
            await _handler.HandleAsync(message, cancellationToken);
            row.PublishedAt = now;
            row.LeaseOwner = null;
            row.LeaseExpiresAt = null;
            row.LastError = null;
            await _db.SaveChangesAsync(cancellationToken);
            return new MarketplaceOutboxProcessResult(
                MarketplaceOutboxProcessStatus.Published, row.Id, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = $"{exception.GetType().Name}: {exception.Message}";
            if (error.Length > 1_000) error = error[..1_000];
            row.LeaseOwner = null;
            row.LeaseExpiresAt = null;
            row.LastError = error;
            await _db.SaveChangesAsync(cancellationToken);
            return new MarketplaceOutboxProcessResult(
                MarketplaceOutboxProcessStatus.Failed, row.Id, error);
        }
    }

    private async ValueTask<MarketplaceOutboxRow?> ClaimAsync(
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var row = await _db.Set<MarketplaceOutboxRow>()
            .Where(item => item.PublishedAt == null &&
                           (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now))
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return null;

        row.LeaseOwner = owner;
        row.LeaseExpiresAt = now.Add(LeaseDuration);
        row.AttemptCount = checked(row.AttemptCount + 1);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
        }, cancellationToken);
    }

    private static MarketplaceOutboxDispatchMessage ValidateAndMap(MarketplaceOutboxRow row)
    {
        var actualHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(row.Payload)));
        if (!string.Equals(actualHash, row.PayloadHash, StringComparison.Ordinal))
            throw new MarketplaceOutboxException("Marketplace outbox payload hash is invalid.");

        using var document = JsonDocument.Parse(row.Payload);
        var payload = document.RootElement;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("settlementId", out var settlementProperty) ||
            !settlementProperty.TryGetGuid(out var settlementId) ||
            settlementId != row.SettlementId)
            throw new MarketplaceOutboxException(
                "Marketplace outbox payload is not bound to its settlement.");

        return new MarketplaceOutboxDispatchMessage(
            row.Id, row.TenantId, row.SettlementId, row.MessageType,
            payload.Clone(), row.PayloadHash, row.OccurredAt);
    }
}

public sealed class CommerceMarketplaceOutboxHandler : IMarketplaceOutboxHandler
{
    internal const string GrantMessageType = "marketplace.entitlement.grant.v1";
    internal const string RevokeMessageType = "marketplace.entitlement.revoke.v1";
    private readonly DbContext _db;
    private readonly IEntitlementService _entitlements;
    private readonly IOrderRepository _orders;

    public CommerceMarketplaceOutboxHandler(
        IApplicationDbContext context,
        IEntitlementService entitlements,
        IOrderRepository orders)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entitlements);
        ArgumentNullException.ThrowIfNull(orders);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Marketplace Commerce projection requires the application's relational DbContext.");
        _entitlements = entitlements;
        _orders = orders;
    }

    public async ValueTask HandleAsync(
        MarketplaceOutboxDispatchMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settlement = await _db.Set<MarketplaceSettlementRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == message.SettlementId && row.TenantId == message.TenantId,
                cancellationToken)
            ?? throw new MarketplaceOutboxException("Marketplace settlement was not found in the message tenant.");
        RequirePayloadGuid(message.Payload, "entitlementId", settlement.EntitlementId);

        switch (message.MessageType)
        {
            case GrantMessageType:
                await GrantAsync(message.Payload, settlement, cancellationToken);
                break;
            case RevokeMessageType:
                await RevokeAsync(settlement, cancellationToken);
                break;
            default:
                throw new MarketplaceOutboxException("Marketplace outbox message type is unsupported.");
        }
    }

    private async ValueTask GrantAsync(
        JsonElement payload,
        MarketplaceSettlementRow settlement,
        CancellationToken cancellationToken)
    {
        RequirePayloadGuid(payload, "orderId", settlement.OrderId);
        RequirePayloadGuid(payload, "productId", settlement.ProductId);
        RequirePayloadGuid(payload, "buyerId", settlement.BuyerId);
        var entitlement = await _entitlements.GrantEntitlementAsync(
            settlement.BuyerId,
            settlement.ProductId,
            ProductAcquisitionType.Purchase,
            0,
            "GGC",
            null,
            settlement.OrderId,
            cancellationToken);
        if (!entitlement.Success)
            throw new MarketplaceOutboxException(
                entitlement.ErrorMessage ?? "Commerce rejected the Marketplace entitlement grant.");

        var order = await RequireOrderAsync(settlement, cancellationToken);
        if (order.Status is OrderStatus.Pending or OrderStatus.Processing)
            order.MarkAsPaid("economy-ledger", "gameguild-economy", settlement.PostingId.ToString("N"));
        if (order.Status == OrderStatus.Completed)
            order.MarkAsFulfilled();
        if (!order.IsSuccessfullyCompleted)
            throw new MarketplaceOutboxException(
                "Commerce order is not in a state that can accept Marketplace fulfillment.");
        await _orders.UpdateAsync(order, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask RevokeAsync(
        MarketplaceSettlementRow settlement,
        CancellationToken cancellationToken)
    {
        if (!await _entitlements.RevokeEntitlementAsync(
                settlement.BuyerId,
                settlement.ProductId,
                "Economy Marketplace order refunded",
                cancellationToken))
            throw new MarketplaceOutboxException(
                "Commerce could not find the Marketplace entitlement to revoke.");

        var order = await RequireOrderAsync(settlement, cancellationToken);
        if (order.Status != OrderStatus.Refunded)
            order.ProcessRefund(order.Total, "Economy Marketplace order refunded");
        await _orders.UpdateAsync(order, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask<Order> RequireOrderAsync(
        MarketplaceSettlementRow settlement,
        CancellationToken cancellationToken)
    {
        var order = await _orders.GetWithLineItemsAsync(settlement.OrderId, cancellationToken)
            ?? throw new MarketplaceOutboxException("Commerce order was not found.");
        if (order.TenantId != settlement.TenantId || order.UserId != settlement.BuyerId ||
            order.LineItems.Count != 1 || order.LineItems.Single().Id != settlement.OrderLineItemId ||
            order.LineItems.Single().ProductId != settlement.ProductId)
            throw new MarketplaceOutboxException(
                "Commerce order no longer matches the immutable Marketplace snapshot.");
        return order;
    }

    private static void RequirePayloadGuid(JsonElement payload, string propertyName, Guid expected)
    {
        if (!payload.TryGetProperty(propertyName, out var property) ||
            !property.TryGetGuid(out var actual) || actual != expected)
            throw new MarketplaceOutboxException(
                $"Marketplace outbox {propertyName} binding is invalid.");
    }
}

public sealed class MarketplaceOutboxException(string message) : InvalidOperationException(message);
