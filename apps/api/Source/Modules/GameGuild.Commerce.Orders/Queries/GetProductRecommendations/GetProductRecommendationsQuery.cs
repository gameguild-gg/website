using FluentValidation;
using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GameGuild.Commerce.Orders;

/// <summary>
///     Recommendation item: a product co-purchased with the anchor product.
/// </summary>
/// <param name="ProductId">Recommended product ID</param>
/// <param name="Name">Product name</param>
/// <param name="CoOrderCount">How many completed orders contained both products</param>
public sealed record CoPurchaseRecommendationDto(
    Guid ProductId,
    string Name,
    int CoOrderCount
);

/// <summary>
///     A bundle that contains the anchor product, with the price-optimization hint.
/// </summary>
/// <param name="BundleId">Bundle product ID</param>
/// <param name="Name">Bundle name</param>
/// <param name="Savings">Bundle-price optimization hint (bundle price vs sum of parts)</param>
public sealed record BundleRecommendationDto(
    Guid BundleId,
    string Name,
    BundleSavingsHint? Savings
);

/// <summary>
///     Optional ("complete the bundle") item for a bundle anchor product.
/// </summary>
/// <param name="ProductId">Optional component product ID</param>
/// <param name="Name">Component product name</param>
public sealed record OptionalBundleItemDto(
    Guid ProductId,
    string Name
);

/// <summary>
///     Rule-based product recommendations (issue #401, v1): deterministic
///     "frequently bought together" from completed-order co-occurrence plus
///     bundle-savings hints. Machine-learning personalization is explicitly deferred
///     (see docs/billing/product-catalog-bundling.md).
/// </summary>
/// <param name="ProductId">Anchor product ID</param>
/// <param name="FrequentlyBoughtTogether">Products co-purchased with the anchor, ranked by co-order count</param>
/// <param name="BundleSuggestions">Published bundles containing the anchor, with savings hints</param>
/// <param name="CompleteTheBundle">Optional components of the anchor (when it is a bundle)</param>
/// <param name="Cached">Whether this response was served from the recommendation cache</param>
public sealed record ProductRecommendationsDto(
    Guid ProductId,
    List<CoPurchaseRecommendationDto> FrequentlyBoughtTogether,
    List<BundleRecommendationDto> BundleSuggestions,
    List<OptionalBundleItemDto> CompleteTheBundle,
    bool Cached
);

/// <summary>
///     Query for rule-based product recommendations (issue #401).
/// </summary>
/// <param name="ProductId">Anchor product ID</param>
/// <param name="Take">Maximum frequently-bought-together items to return (1–20)</param>
public sealed record GetProductRecommendationsQuery(Guid ProductId, int Take = 5)
    : IQuery<ProductRecommendationsDto?>;

/// <summary>Validator for <see cref="GetProductRecommendationsQuery"/>.</summary>
public sealed class GetProductRecommendationsQueryValidator : AbstractValidator<GetProductRecommendationsQuery>
{
    public GetProductRecommendationsQueryValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Take).InclusiveBetween(1, 20);
    }
}

/// <summary>
///     Handler for <see cref="GetProductRecommendationsQuery"/>.
///     Deterministic v1 rules (no ML):
///     <list type="bullet">
///         <item>Frequently-bought-together: products sharing completed orders with the anchor, ranked by co-order count.</item>
///         <item>Bundle suggestions: published bundles containing the anchor, each with a sum-of-parts savings hint.</item>
///         <item>Complete-the-bundle: optional components of the anchor when it is itself a bundle.</item>
///     </list>
///     Results are cached per (product, take) for five minutes in the sized platform memory cache.
/// </summary>
public sealed class GetProductRecommendationsQueryHandler(
    IApplicationDbContext dbContext,
    IBundleSavingsCalculator bundleSavingsCalculator,
    IMemoryCache cache)
    : IQueryHandler<GetProductRecommendationsQuery, ProductRecommendationsDto?>
{
    /// <summary>Completed order states whose line items feed co-occurrence.</summary>
    private static readonly List<OrderStatus> CompletedStatuses = [OrderStatus.Completed, OrderStatus.Fulfilled];

    /// <summary>Upper bound of historic orders scanned per anchor (recency not ranked in v1).</summary>
    private const int MaxOrdersScanned = 500;

    /// <summary>Cache duration for recommendation results.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public async Task<ProductRecommendationsDto?> Handle(
        GetProductRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var anchor = await dbContext.Set<Product>()
            .Where(p => p.Id == request.ProductId && p.DeletedAt == null)
            .Select(p => new { p.Id, p.IsPublished, p.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null)
            return null;

        var cacheKey = $"product-recommendations:{request.ProductId:n}:{request.Take}";
        if (cache.TryGetValue(cacheKey, out ProductRecommendationsDto? cached) && cached is not null)
            return cached with { Cached = true };

        var result = await BuildRecommendationsAsync(request, cancellationToken).ConfigureAwait(false);

        // The platform memory cache is registered with a SizeLimit; every entry must
        // declare its size or the Set call throws at runtime.
        cache.Set(
            cacheKey,
            result,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = Math.Max(1, result.FrequentlyBoughtTogether.Count
                    + result.BundleSuggestions.Count
                    + result.CompleteTheBundle.Count)
            });

        return result;
    }

    private async Task<ProductRecommendationsDto> BuildRecommendationsAsync(
        GetProductRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var frequentlyBought = await GetFrequentlyBoughtTogetherAsync(request, cancellationToken).ConfigureAwait(false);
        var bundleSuggestions = await GetBundleSuggestionsAsync(request, cancellationToken).ConfigureAwait(false);
        var completeTheBundle = await GetCompleteTheBundleAsync(request, cancellationToken).ConfigureAwait(false);

        return new ProductRecommendationsDto(
            request.ProductId,
            frequentlyBought,
            bundleSuggestions,
            completeTheBundle,
            Cached: false);
    }

    private async Task<List<CoPurchaseRecommendationDto>> GetFrequentlyBoughtTogetherAsync(
        GetProductRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var orderIds = await dbContext.Set<Order>()
            .Where(o => CompletedStatuses.Contains(o.Status))
            .Where(o => o.LineItems.Any(line => line.ProductId == request.ProductId))
            .Select(o => o.Id)
            .Take(MaxOrdersScanned)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (orderIds.Count == 0)
            return [];

        var coOccurrences = await dbContext.Set<OrderLineItem>()
            .Where(line => orderIds.Contains(line.OrderId) && line.ProductId != request.ProductId)
            .GroupBy(line => line.ProductId)
            .Select(group => new { ProductId = group.Key, CoOrderCount = group.Count() })
            .OrderByDescending(summary => summary.CoOrderCount)
            .ThenBy(summary => summary.ProductId)
            .Take(request.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (coOccurrences.Count == 0)
            return [];

        var candidateIds = coOccurrences.Select(summary => summary.ProductId).ToList();
        var publishedNames = await dbContext.Set<Product>()
            .Where(p => candidateIds.Contains(p.Id) && p.DeletedAt == null && p.IsPublished)
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken)
            .ConfigureAwait(false);

        return coOccurrences
            .Where(summary => publishedNames.ContainsKey(summary.ProductId))
            .Select(summary => new CoPurchaseRecommendationDto(
                summary.ProductId,
                publishedNames[summary.ProductId],
                summary.CoOrderCount))
            .ToList();
    }

    private async Task<List<BundleRecommendationDto>> GetBundleSuggestionsAsync(
        GetProductRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var bundleIds = await dbContext.Set<ProductBundleItem>()
            .Where(item => item.IncludedProductId == request.ProductId && item.DeletedAt == null)
            .Select(item => item.BundleProductId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (bundleIds.Count == 0)
            return [];

        var bundles = await dbContext.Set<Product>()
            .Where(p => bundleIds.Contains(p.Id) && p.DeletedAt == null && p.IsPublished && p.IsBundle)
            .Include(p => p.BundleItems)
            .Include(p => p.Pricing)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (bundles.Count == 0)
            return [];

        var componentIds = bundles
            .SelectMany(bundle => bundle.BundleItems.Select(item => item.IncludedProductId))
            .Distinct()
            .ToList();
        var components = await dbContext.Set<Product>()
            .Where(p => componentIds.Contains(p.Id) && p.DeletedAt == null)
            .Include(p => p.Pricing)
            .ToDictionaryAsync(p => p.Id, cancellationToken)
            .ConfigureAwait(false);

        return bundles
            .OrderBy(bundle => bundle.Name)
            .Select(bundle => new BundleRecommendationDto(
                bundle.Id,
                bundle.Name,
                bundleSavingsCalculator.Calculate(bundle, components)))
            .ToList();
    }

    private async Task<List<OptionalBundleItemDto>> GetCompleteTheBundleAsync(
        GetProductRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var anchor = await dbContext.Set<Product>()
            .Where(p => p.Id == request.ProductId && p.DeletedAt == null)
            .Include(p => p.BundleItems)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null || !anchor.IsBundle)
            return [];

        var optionalIds = anchor.BundleItems
            .Where(item => !item.IsRequired)
            .Select(item => item.IncludedProductId)
            .ToList();
        if (optionalIds.Count == 0)
            return [];

        var names = await dbContext.Set<Product>()
            .Where(p => optionalIds.Contains(p.Id) && p.DeletedAt == null && p.IsPublished)
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken)
            .ConfigureAwait(false);

        return optionalIds
            .Where(names.ContainsKey)
            .Select(id => new OptionalBundleItemDto(id, names[id]))
            .ToList();
    }
}
