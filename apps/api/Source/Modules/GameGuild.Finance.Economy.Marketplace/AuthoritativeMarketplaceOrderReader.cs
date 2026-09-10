using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Commerce.Orders;
using GameGuild.Commerce.Products;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed record AuthoritativeMarketplaceOrderSnapshot(
    Guid TenantId,
    Guid OrderId,
    Guid OrderLineItemId,
    Guid BuyerId,
    Guid ProductId,
    Guid SellerId,
    Guid ProductPricingVersionId,
    int PriceVersionSnapshot,
    int Quantity,
    decimal UnitPriceSnapshot,
    string FiatCurrencySnapshot,
    string SnapshotHash);

public interface IAuthoritativeMarketplaceOrderReader
{
    ValueTask<AuthoritativeMarketplaceOrderSnapshot> ReadAsync(
        Guid tenantId,
        Guid buyerId,
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public sealed class EfAuthoritativeMarketplaceOrderReader : IAuthoritativeMarketplaceOrderReader
{
    private readonly DbContext _db;

    public EfAuthoritativeMarketplaceOrderReader(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Authoritative Marketplace orders require the application's relational DbContext.");
    }

    public async ValueTask<AuthoritativeMarketplaceOrderSnapshot> ReadAsync(
        Guid tenantId,
        Guid buyerId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || buyerId == Guid.Empty || orderId == Guid.Empty)
            throw new ArgumentException("Tenant, buyer and order IDs are required.");
        var order = await _db.Set<Order>()
            .AsNoTracking()
            .Include(item => item.LineItems)
            .SingleOrDefaultAsync(
                item => item.Id == orderId && item.TenantId == tenantId && item.UserId == buyerId,
                cancellationToken)
            ?? throw new MarketplaceOrderSnapshotException(
                "The authoritative order was not found in the actor tenant.");
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Processing))
            throw new MarketplaceOrderSnapshotException(
                "The authoritative order is not eligible for Economy settlement.");
        if (order.LineItems.Count != 1)
            throw new MarketplaceOrderSnapshotException(
                "An Economy marketplace order must contain exactly one immutable line item.");
        var line = order.LineItems.Single();
        if (line.TenantId != tenantId || line.Quantity <= 0 || line.ProductPricingVersionId == Guid.Empty ||
            line.PriceVersionSnapshot <= 0 || line.UnitPriceSnapshot < 0 ||
            string.IsNullOrWhiteSpace(line.CurrencySnapshot))
            throw new MarketplaceOrderSnapshotException(
                "The immutable order line snapshot is incomplete.");
        var product = await _db.Set<Product>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == line.ProductId && item.TenantId == tenantId,
                cancellationToken)
            ?? throw new MarketplaceOrderSnapshotException(
                "The authoritative product was not found in the actor tenant.");
        if (product.CreatorId is not { } sellerId || sellerId == Guid.Empty || sellerId == buyerId)
            throw new MarketplaceOrderSnapshotException(
                "Marketplace self-purchase is prohibited and products require a creator.");

        var canonical = string.Join('|',
            tenantId.ToString("N"),
            order.Id.ToString("N"),
            line.Id.ToString("N"),
            buyerId.ToString("N"),
            line.ProductId.ToString("N"),
            sellerId.ToString("N"),
            line.ProductPricingVersionId.ToString("N"),
            line.PriceVersionSnapshot.ToString(CultureInfo.InvariantCulture),
            line.Quantity.ToString(CultureInfo.InvariantCulture),
            line.UnitPriceSnapshot.ToString(CultureInfo.InvariantCulture),
            line.CurrencySnapshot);
        return new AuthoritativeMarketplaceOrderSnapshot(
            tenantId,
            order.Id,
            line.Id,
            buyerId,
            line.ProductId,
            sellerId,
            line.ProductPricingVersionId,
            line.PriceVersionSnapshot,
            line.Quantity,
            line.UnitPriceSnapshot,
            line.CurrencySnapshot,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    }
}

public sealed class MarketplaceOrderSnapshotException(string message) : InvalidOperationException(message);
