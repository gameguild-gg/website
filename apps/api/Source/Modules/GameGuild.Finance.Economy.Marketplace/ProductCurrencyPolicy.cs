using System.Numerics;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Marketplace;

public enum ProductCurrencyMode
{
    HardOnly = 1,
    SoftOnly = 2,
    Either = 3,
    FixedMix = 4
}

public enum MarketplaceCurrencyChoice
{
    Hard = 1,
    Soft = 2,
    FixedMix = 3
}

public sealed record MarketplacePriceLegSnapshot
{
    public MarketplacePriceLegSnapshot(
        CurrencyCode currency,
        long units,
        long sellerUnits,
        long platformFeeUnits)
    {
        if (!Enum.IsDefined(currency)) throw new ArgumentOutOfRangeException(nameof(currency));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(units);
        ArgumentOutOfRangeException.ThrowIfNegative(sellerUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(platformFeeUnits);
        if (checked(sellerUnits + platformFeeUnits) != units)
            throw new ArgumentException("Seller proceeds and platform fee must conserve the quoted leg.", nameof(sellerUnits));

        Currency = currency;
        Units = units;
        SellerUnits = sellerUnits;
        PlatformFeeUnits = platformFeeUnits;
    }

    public CurrencyCode Currency { get; }
    public long Units { get; }
    public long SellerUnits { get; }
    public long PlatformFeeUnits { get; }
    public CoinAmount Amount => new(Currency, Units);
}

public sealed record MarketplaceQuoteSnapshot
{
    public MarketplaceQuoteSnapshot(
        Guid productId,
        Guid sellerId,
        long policyVersion,
        ProductCurrencyMode mode,
        IReadOnlyList<MarketplacePriceLegSnapshot> legs)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        if (sellerId == Guid.Empty) throw new ArgumentException("Seller ID cannot be empty.", nameof(sellerId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(policyVersion);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        ArgumentNullException.ThrowIfNull(legs);
        if (legs.Count == 0 || legs.Select(leg => leg.Currency).Distinct().Count() != legs.Count)
            throw new ArgumentException("A quote requires unique currency legs.", nameof(legs));

        ProductId = productId;
        SellerId = sellerId;
        PolicyVersion = policyVersion;
        Mode = mode;
        Legs = Array.AsReadOnly(legs.OrderBy(leg => leg.Currency).ToArray());
    }

    public Guid ProductId { get; }
    public Guid SellerId { get; }
    public long PolicyVersion { get; }
    public ProductCurrencyMode Mode { get; }
    public IReadOnlyList<MarketplacePriceLegSnapshot> Legs { get; }
}

public sealed class ProductCurrencyPolicyVersion
{
    public const int PartsPerMillion = 1_000_000;

    private ProductCurrencyPolicyVersion(
        Guid productId,
        Guid sellerId,
        long version,
        ProductCurrencyMode mode,
        long hardPriceUnits,
        long softPriceUnits,
        int platformFeePpm,
        DateTimeOffset effectiveAt)
    {
        ProductId = productId;
        SellerId = sellerId;
        Version = version;
        Mode = mode;
        HardPriceUnits = hardPriceUnits;
        SoftPriceUnits = softPriceUnits;
        PlatformFeePpm = platformFeePpm;
        EffectiveAt = effectiveAt;
    }

    public Guid ProductId { get; }
    public Guid SellerId { get; }
    public long Version { get; }
    public ProductCurrencyMode Mode { get; }
    public long HardPriceUnits { get; }
    public long SoftPriceUnits { get; }
    public int PlatformFeePpm { get; }
    public DateTimeOffset EffectiveAt { get; }

    public static ProductCurrencyPolicyVersion Create(
        Guid productId,
        Guid sellerId,
        long version,
        ProductCurrencyMode mode,
        long hardPriceUnits,
        long softPriceUnits,
        int platformFeePpm,
        DateTimeOffset effectiveAt)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        if (sellerId == Guid.Empty) throw new ArgumentException("Seller ID cannot be empty.", nameof(sellerId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentOutOfRangeException.ThrowIfNegative(hardPriceUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(softPriceUnits);
        if (platformFeePpm is < 0 or >= PartsPerMillion)
            throw new ArgumentOutOfRangeException(nameof(platformFeePpm));

        var validPrices = mode switch
        {
            ProductCurrencyMode.HardOnly => hardPriceUnits > 0 && softPriceUnits == 0,
            ProductCurrencyMode.SoftOnly => hardPriceUnits == 0 && softPriceUnits > 0,
            ProductCurrencyMode.Either => hardPriceUnits > 0 && softPriceUnits > 0,
            ProductCurrencyMode.FixedMix => hardPriceUnits > 0 && softPriceUnits > 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        if (!validPrices)
            throw new MarketplaceCurrencyPolicyException("Currency prices do not match the accepted-currency mode.");

        return new ProductCurrencyPolicyVersion(
            productId, sellerId, version, mode, hardPriceUnits, softPriceUnits,
            platformFeePpm, effectiveAt);
    }

    public MarketplaceQuoteSnapshot Quote(MarketplaceCurrencyChoice choice)
    {
        if (!Enum.IsDefined(choice)) throw new ArgumentOutOfRangeException(nameof(choice));

        var amounts = (Mode, choice) switch
        {
            (ProductCurrencyMode.HardOnly, MarketplaceCurrencyChoice.Hard) =>
                new[] { new CoinAmount(CurrencyCode.HardCoin, HardPriceUnits) },
            (ProductCurrencyMode.SoftOnly, MarketplaceCurrencyChoice.Soft) =>
                new[] { new CoinAmount(CurrencyCode.SoftCoin, SoftPriceUnits) },
            (ProductCurrencyMode.Either, MarketplaceCurrencyChoice.Hard) =>
                new[] { new CoinAmount(CurrencyCode.HardCoin, HardPriceUnits) },
            (ProductCurrencyMode.Either, MarketplaceCurrencyChoice.Soft) =>
                new[] { new CoinAmount(CurrencyCode.SoftCoin, SoftPriceUnits) },
            (ProductCurrencyMode.FixedMix, MarketplaceCurrencyChoice.FixedMix) =>
                new[]
                {
                    new CoinAmount(CurrencyCode.HardCoin, HardPriceUnits),
                    new CoinAmount(CurrencyCode.SoftCoin, SoftPriceUnits)
                },
            _ => throw new MarketplaceCurrencyPolicyException(
                "The selected currency choice is not accepted by this product policy.")
        };

        var legs = amounts.Select(amount =>
        {
            var fee = CalculateFee(amount.Units, PlatformFeePpm);
            return new MarketplacePriceLegSnapshot(
                amount.Currency, amount.Units, amount.Units - fee, fee);
        }).ToArray();
        return new MarketplaceQuoteSnapshot(ProductId, SellerId, Version, Mode, legs);
    }

    public static long CalculateFee(long units, int feePpm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(units);
        if (feePpm is < 0 or >= PartsPerMillion)
            throw new ArgumentOutOfRangeException(nameof(feePpm));
        return (long)(new BigInteger(units) * feePpm / PartsPerMillion);
    }
}

public sealed class MarketplaceCurrencyPolicyException(string message) : InvalidOperationException(message);
