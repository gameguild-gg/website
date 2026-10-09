using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using GameGuild.Commerce.Products;
using GameGuild.Commerce.Subscriptions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Analytics;

/// <summary>
///     Aggregates revenue, subscription and capacity metrics (business-intelligence data).
///     Dispatch requires the monetization analytics permission (issue #346, ViewAnalytics)
///     regardless of the transport (REST, GraphQL or in-process senders).
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.ViewAnalytics)]
public sealed record GetProductMetricsQuery(
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    Guid? TenantId = null) : IQuery<ProductMetricsResponse>;

public sealed record ProductMetricsResponse(
    DateTime StartUtc,
    DateTime EndUtc,
    Guid? TenantId,
    ProductCatalogMetrics Catalog,
    ProductRevenueMetrics Revenue,
    ProductSubscriptionMetrics Subscriptions,
    ProductCapacityMetrics Capacity,
    IReadOnlyList<ProductMetricThreshold> Thresholds,
    DateTime GeneratedAtUtc);

public sealed record ProductCatalogMetrics(
    int TotalProducts,
    int PublishedProducts,
    int DraftProducts,
    int Bundles);

public sealed record ProductRevenueMetrics(
    decimal MonthlyRecurringRevenue,
    decimal AnnualRecurringRevenue,
    decimal SalesVolume,
    string Currency);

public sealed record ProductSubscriptionMetrics(
    int TotalSubscribers,
    int ActiveSubscribers,
    int TrialSubscribers,
    int PastDueSubscribers,
    int CancelledSubscribers,
    int CancelledInPeriod,
    decimal ChurnRate,
    decimal RetentionRate);

public sealed record ProductCapacityMetrics(
    int TotalUserLimit,
    long TotalStorageMbLimit,
    long TotalApiCallsLimit,
    int UnlimitedUserPlans,
    int UnlimitedStoragePlans,
    int UnlimitedApiCallPlans);

public sealed record ProductMetricThreshold(
    string Key,
    ProductMetricThresholdStatus Status,
    string Message,
    decimal Value,
    decimal WarningAt,
    decimal CriticalAt);

public enum ProductMetricThresholdStatus
{
    Healthy,
    Warning,
    Critical
}

public sealed class GetProductMetricsQueryHandler(IApplicationDbContext db)
    : IQueryHandler<GetProductMetricsQuery, ProductMetricsResponse>
{
    public async Task<ProductMetricsResponse> Handle(GetProductMetricsQuery request, CancellationToken cancellationToken)
    {
        var (startUtc, endUtc) = NormalizeWindow(request.StartUtc, request.EndUtc);

        var productsQuery = db.Set<Product>()
            .AsNoTracking()
            .Where(product => product.DeletedAt == null);

        if (request.TenantId.HasValue)
        {
            productsQuery = productsQuery.Where(product => product.TenantId == request.TenantId.Value);
        }

        var catalog = new ProductCatalogMetrics(
            TotalProducts: await productsQuery.CountAsync(cancellationToken).ConfigureAwait(false),
            PublishedProducts: await productsQuery.CountAsync(product => product.IsPublished, cancellationToken).ConfigureAwait(false),
            DraftProducts: await productsQuery.CountAsync(product => !product.IsPublished, cancellationToken).ConfigureAwait(false),
            Bundles: await productsQuery.CountAsync(product => product.IsBundle || product.Type == ProductType.Bundle, cancellationToken).ConfigureAwait(false));

        var subscriptionsQuery = db.Set<Subscription>()
            .AsNoTracking()
            .Where(subscription => subscription.DeletedAt == null);

        if (request.TenantId.HasValue)
        {
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.TenantId == request.TenantId.Value);
        }

        var subscriptions = await subscriptionsQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        var activeSubscriptions = subscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .ToList();
        var cancelledInPeriod = subscriptions.Count(subscription =>
            subscription.Status == SubscriptionStatus.Cancelled &&
            subscription.CancelledAt.HasValue &&
            subscription.CancelledAt.Value >= startUtc &&
            subscription.CancelledAt.Value <= endUtc);

        var mrr = activeSubscriptions.Sum(subscription => NormalizeToMonthly(subscription.Amount.Amount, subscription.BillingCycle));
        var salesVolume = subscriptions
            .Where(subscription =>
                subscription.LastPaymentAt.HasValue &&
                subscription.LastPaymentAt.Value >= startUtc &&
                subscription.LastPaymentAt.Value <= endUtc)
            .Sum(subscription => subscription.Amount.Amount);
        var churnDenominator = activeSubscriptions.Count + cancelledInPeriod;
        var churnRate = churnDenominator == 0
            ? 0m
            : decimal.Round(cancelledInPeriod / (decimal)churnDenominator * 100m, 2);
        var retentionRate = decimal.Round(100m - churnRate, 2);

        var planIds = activeSubscriptions
            .Select(subscription => subscription.PlanId)
            .Where(planId => planId != Guid.Empty)
            .Distinct()
            .ToArray();
        var plans = planIds.Length == 0
            ? []
            : await db.Set<SubscriptionPlan>()
                .AsNoTracking()
                .Where(plan => planIds.Contains(plan.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        var plansById = plans.ToDictionary(plan => plan.Id);

        var capacity = CalculateCapacity(activeSubscriptions, plansById);
        var subscriptionMetrics = new ProductSubscriptionMetrics(
            TotalSubscribers: subscriptions.Count,
            ActiveSubscribers: activeSubscriptions.Count,
            TrialSubscribers: subscriptions.Count(subscription => subscription.Status == SubscriptionStatus.Trialing),
            PastDueSubscribers: subscriptions.Count(subscription => subscription.Status == SubscriptionStatus.PastDue),
            CancelledSubscribers: subscriptions.Count(subscription => subscription.Status == SubscriptionStatus.Cancelled),
            CancelledInPeriod: cancelledInPeriod,
            ChurnRate: churnRate,
            RetentionRate: retentionRate);
        var revenue = new ProductRevenueMetrics(
            MonthlyRecurringRevenue: decimal.Round(mrr, 2),
            AnnualRecurringRevenue: decimal.Round(mrr * 12m, 2),
            SalesVolume: decimal.Round(salesVolume, 2),
            Currency: "USD");

        return new ProductMetricsResponse(
            startUtc,
            endUtc,
            request.TenantId,
            catalog,
            revenue,
            subscriptionMetrics,
            capacity,
            BuildThresholds(catalog, subscriptionMetrics, capacity),
            SystemClock.UtcNow);
    }

    private static (DateTime StartUtc, DateTime EndUtc) NormalizeWindow(DateTime? startUtc, DateTime? endUtc)
    {
        var end = AsUtc(endUtc ?? SystemClock.UtcNow);
        var start = AsUtc(startUtc ?? new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc));

        if (start > end)
        {
            throw new ArgumentException("Product metrics start date must be earlier than or equal to the end date.");
        }

        return (start, end);
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static decimal NormalizeToMonthly(decimal amount, BillingCycle cycle)
        => cycle switch
        {
            BillingCycle.Weekly => amount * 52m / 12m,
            BillingCycle.Monthly => amount,
            BillingCycle.Quarterly => amount / 3m,
            BillingCycle.SemiAnnually => amount / 6m,
            BillingCycle.Annually => amount / 12m,
            BillingCycle.Biannually => amount / 24m,
            _ => amount
        };

    private static ProductCapacityMetrics CalculateCapacity(
        IReadOnlyCollection<Subscription> activeSubscriptions,
        IReadOnlyDictionary<Guid, SubscriptionPlan> plansById)
    {
        var totalUsers = 0;
        var totalStorage = 0L;
        var totalApiCalls = 0L;
        var unlimitedUsers = 0;
        var unlimitedStorage = 0;
        var unlimitedApiCalls = 0;

        foreach (var subscription in activeSubscriptions)
        {
            if (!plansById.TryGetValue(subscription.PlanId, out var plan))
            {
                continue;
            }

            if (plan.MaxUsers.HasValue)
                totalUsers += plan.MaxUsers.Value;
            else
                unlimitedUsers++;

            if (plan.MaxStorageMb.HasValue)
                totalStorage += plan.MaxStorageMb.Value;
            else
                unlimitedStorage++;

            if (plan.MaxApiCallsPerMonth.HasValue)
                totalApiCalls += plan.MaxApiCallsPerMonth.Value;
            else
                unlimitedApiCalls++;
        }

        return new ProductCapacityMetrics(
            totalUsers,
            totalStorage,
            totalApiCalls,
            unlimitedUsers,
            unlimitedStorage,
            unlimitedApiCalls);
    }

    private static IReadOnlyList<ProductMetricThreshold> BuildThresholds(
        ProductCatalogMetrics catalog,
        ProductSubscriptionMetrics subscriptions,
        ProductCapacityMetrics capacity)
    {
        return
        [
            new ProductMetricThreshold(
                "churn",
                ToStatus(subscriptions.ChurnRate, 5m, 50m),
                subscriptions.ChurnRate == 0m
                    ? "No subscriber churn in the selected period."
                    : "Subscriber churn needs retention follow-up.",
                subscriptions.ChurnRate,
                5m,
                50m),
            new ProductMetricThreshold(
                "catalog-publication",
                catalog.PublishedProducts == 0 ? ProductMetricThresholdStatus.Warning : ProductMetricThresholdStatus.Healthy,
                catalog.PublishedProducts == 0
                    ? "No published products are visible in the catalog."
                    : "Published product catalog is available.",
                catalog.PublishedProducts,
                1m,
                0m),
            new ProductMetricThreshold(
                "api-capacity",
                capacity.TotalApiCallsLimit == 0 && capacity.UnlimitedApiCallPlans == 0
                    ? ProductMetricThresholdStatus.Warning
                    : ProductMetricThresholdStatus.Healthy,
                capacity.TotalApiCallsLimit == 0 && capacity.UnlimitedApiCallPlans == 0
                    ? "No API-call capacity is configured on active plans."
                    : "API-call capacity is configured on active plans.",
                capacity.TotalApiCallsLimit,
                1m,
                0m)
        ];
    }

    private static ProductMetricThresholdStatus ToStatus(decimal value, decimal warningAt, decimal criticalAt)
    {
        if (criticalAt > 0m && value >= criticalAt)
            return ProductMetricThresholdStatus.Critical;

        return value >= warningAt ? ProductMetricThresholdStatus.Warning : ProductMetricThresholdStatus.Healthy;
    }
}

/// <summary>
///     Exports aggregated revenue/subscription metrics. Exports leave the platform boundary,
///     so dispatch requires the monetization analytics permission (issue #346, ViewAnalytics).
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.ViewAnalytics)]
public sealed record ExportProductMetricsQuery(
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    Guid? TenantId = null,
    ProductMetricsExportFormat Format = ProductMetricsExportFormat.Csv) : IQuery<ProductMetricsExportResponse>;

public enum ProductMetricsExportFormat
{
    Csv,
    Json
}

public sealed record ProductMetricsExportResponse(
    string ContentType,
    string FileName,
    string Content);

public sealed class ExportProductMetricsQueryHandler(
    IQueryHandler<GetProductMetricsQuery, ProductMetricsResponse> metricsHandler)
    : IQueryHandler<ExportProductMetricsQuery, ProductMetricsExportResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<ProductMetricsExportResponse> Handle(ExportProductMetricsQuery request, CancellationToken cancellationToken)
    {
        var metrics = await metricsHandler.Handle(
            new GetProductMetricsQuery(request.StartUtc, request.EndUtc, request.TenantId),
            cancellationToken).ConfigureAwait(false);
        var stamp = metrics.GeneratedAtUtc.ToString("yyyyMMddHHmmss");

        return request.Format switch
        {
            ProductMetricsExportFormat.Json => new ProductMetricsExportResponse(
                "application/json",
                $"product-metrics-{stamp}.json",
                JsonSerializer.Serialize(metrics, JsonOptions)),
            _ => new ProductMetricsExportResponse(
                "text/csv",
                $"product-metrics-{stamp}.csv",
                BuildCsv(metrics))
        };
    }

    private static string BuildCsv(ProductMetricsResponse metrics)
    {
        var rows = new (string Metric, object Value)[]
        {
            ("monthly_recurring_revenue", metrics.Revenue.MonthlyRecurringRevenue),
            ("annual_recurring_revenue", metrics.Revenue.AnnualRecurringRevenue),
            ("sales_volume", metrics.Revenue.SalesVolume),
            ("total_products", metrics.Catalog.TotalProducts),
            ("published_products", metrics.Catalog.PublishedProducts),
            ("draft_products", metrics.Catalog.DraftProducts),
            ("bundles", metrics.Catalog.Bundles),
            ("active_subscribers", metrics.Subscriptions.ActiveSubscribers),
            ("trial_subscribers", metrics.Subscriptions.TrialSubscribers),
            ("cancelled_in_period", metrics.Subscriptions.CancelledInPeriod),
            ("churn_rate", metrics.Subscriptions.ChurnRate),
            ("retention_rate", metrics.Subscriptions.RetentionRate),
            ("total_user_limit", metrics.Capacity.TotalUserLimit),
            ("total_storage_mb_limit", metrics.Capacity.TotalStorageMbLimit),
            ("total_api_calls_limit", metrics.Capacity.TotalApiCallsLimit)
        };

        var builder = new StringBuilder("metric,value");
        foreach (var row in rows)
        {
            builder.AppendLine()
                .Append(row.Metric)
                .Append(',')
                .Append(Convert.ToString(row.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
