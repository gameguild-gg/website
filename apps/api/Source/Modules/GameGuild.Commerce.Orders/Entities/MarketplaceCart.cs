using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Orders;

public enum MarketplaceCartState
{
    Active = 0,
    CheckedOut = 1,
    Abandoned = 2
}

[Table("marketplace_carts")]
[Index(nameof(TenantId), nameof(UserId), nameof(State))]
public sealed class MarketplaceCart : EntityBase
{
    public Guid UserId { get; private set; }
    public MarketplaceCartState State { get; private set; }
    public DateTime? CheckedOutAt { get; private set; }
    public ICollection<MarketplaceCartItem> Items { get; private set; } = new List<MarketplaceCartItem>();

    public static MarketplaceCart Create(Guid tenantId, Guid userId)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Tenant and user are required for a marketplace cart.");
        }

        return new MarketplaceCart { TenantId = tenantId, UserId = userId, State = MarketplaceCartState.Active };
    }

    public MarketplaceCartItem AddItem(
        Guid productId,
        Guid productPricingId,
        Guid productPricingVersionId,
        int quantity,
        string idempotencyKey)
    {
        EnsureActive();
        if (productId == Guid.Empty || productPricingId == Guid.Empty || productPricingVersionId == Guid.Empty)
        {
            throw new ArgumentException("Product and immutable pricing identifiers are required.");
        }

        if (quantity is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var duplicate = Items.SingleOrDefault(item => item.IdempotencyKey == idempotencyKey);
        if (duplicate is not null)
        {
            return duplicate;
        }

        var existing = Items.SingleOrDefault(item => item.ProductPricingVersionId == productPricingVersionId);
        if (existing is not null)
        {
            existing.SetQuantity(checked(existing.Quantity + quantity));
            Touch();
            return existing;
        }

        var item = MarketplaceCartItem.Create(
            Id, TenantId!.Value, productId, productPricingId, productPricingVersionId, quantity, idempotencyKey);
        Items.Add(item);
        Touch();
        return item;
    }

    public void SetQuantity(Guid itemId, int quantity)
    {
        EnsureActive();
        var item = Items.SingleOrDefault(candidate => candidate.Id == itemId)
            ?? throw new KeyNotFoundException("Cart item not found.");
        item.SetQuantity(quantity);
        Touch();
    }

    public void RemoveItem(Guid itemId)
    {
        EnsureActive();
        var item = Items.SingleOrDefault(candidate => candidate.Id == itemId)
            ?? throw new KeyNotFoundException("Cart item not found.");
        Items.Remove(item);
        Touch();
    }

    public void MarkCheckedOut(DateTime checkedOutAt)
    {
        EnsureActive();
        if (Items.Count == 0)
        {
            throw new InvalidOperationException("An empty cart cannot be checked out.");
        }

        State = MarketplaceCartState.CheckedOut;
        CheckedOutAt = checkedOutAt;
        Touch();
    }

    private void EnsureActive()
    {
        if (State != MarketplaceCartState.Active)
        {
            throw new InvalidOperationException("Only an active cart can be changed.");
        }
    }
}

[Table("marketplace_cart_items")]
[Index(nameof(CartId), nameof(ProductPricingVersionId), IsUnique = true)]
[Index(nameof(CartId), nameof(IdempotencyKey), IsUnique = true)]
public sealed class MarketplaceCartItem : EntityBase
{
    public Guid CartId { get; private set; }
    public MarketplaceCart Cart { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Guid ProductPricingId { get; private set; }
    public Guid ProductPricingVersionId { get; private set; }
    public int Quantity { get; private set; }
    [MaxLength(100)] public string IdempotencyKey { get; private set; } = string.Empty;

    internal static MarketplaceCartItem Create(
        Guid cartId,
        Guid tenantId,
        Guid productId,
        Guid productPricingId,
        Guid productPricingVersionId,
        int quantity,
        string idempotencyKey) => new()
        {
            CartId = cartId,
            TenantId = tenantId,
            ProductId = productId,
            ProductPricingId = productPricingId,
            ProductPricingVersionId = productPricingVersionId,
            Quantity = quantity,
            IdempotencyKey = idempotencyKey
        };

    internal void SetQuantity(int quantity)
    {
        if (quantity is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        Quantity = quantity;
        Touch();
    }
}
