using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.CostExplorer;
using Amazon.CostExplorer.Model;
using Amazon.Pricing;
using Amazon.Pricing.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Resources;

public sealed class AwsCostAccountingOptions
{
    public const string SectionName = "InternalCostAccounting:Aws";
    public bool Enabled { get; set; }
    public int InitialDelayMinutes { get; set; } = 5;
    public int RefreshIntervalMinutes { get; set; } = 360;
    public string TenantTagKey { get; set; } = "TenantId";
    public List<AwsPriceMapping> PriceMappings { get; set; } = [];
    public Dictionary<string, decimal> UsdExchangeRates { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1
    };
}

public sealed class AwsPriceMapping
{
    public string MetricCode { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public string Region { get; set; } = "global";
    public string Unit { get; set; } = string.Empty;
    public Dictionary<string, string> Filters { get; set; } = [];
}

public interface IAwsPriceListRateSource
{
    Task<IReadOnlyList<CloudPriceQuote>> GetCurrentRatesAsync(CancellationToken cancellationToken = default);
}

public interface IAwsCostExplorerSource
{
    Task<IReadOnlyList<ActualCloudCostLine>> GetCurrentMonthCostsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActualCloudCostLine>> GetForecastAsync(CancellationToken cancellationToken = default);
}

public interface IAwsCur2CostSource
{
    Task<IReadOnlyList<ActualCloudCostLine>> ReadAsync(Stream csvStream, CancellationToken cancellationToken = default);
}

public sealed class AwsPriceListRateSource(
    IAmazonPricing pricing,
    IOptions<AwsCostAccountingOptions> options,
    TimeProvider timeProvider) : IAwsPriceListRateSource
{
    public async Task<IReadOnlyList<CloudPriceQuote>> GetCurrentRatesAsync(
        CancellationToken cancellationToken = default)
    {
        var quotes = new List<CloudPriceQuote>();
        foreach (var mapping in options.Value.PriceMappings)
        {
            var response = await pricing.GetProductsAsync(new GetProductsRequest
            {
                ServiceCode = mapping.ServiceCode,
                FormatVersion = "aws_v1",
                MaxResults = 100,
                Filters = mapping.Filters.Select(filter => new Filter
                {
                    Field = filter.Key,
                    Type = FilterType.TERM_MATCH,
                    Value = filter.Value
                }).ToList()
            }, cancellationToken).ConfigureAwait(false);

            var quote = response.PriceList
                .Select(document => TryParseQuote(
                    document,
                    mapping,
                    timeProvider.GetUtcNow().UtcDateTime,
                    options.Value.UsdExchangeRates))
                .FirstOrDefault(candidate => candidate is not null);
            if (quote is not null)
                quotes.Add(quote);
        }

        return quotes;
    }

    private static CloudPriceQuote? TryParseQuote(
        string json,
        AwsPriceMapping mapping,
        DateTime retrievedAtUtc,
        IReadOnlyDictionary<string, decimal> usdExchangeRates)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("product", out var product)
            || !product.TryGetProperty("sku", out var sku)
            || !document.RootElement.TryGetProperty("terms", out var terms)
            || !terms.TryGetProperty("OnDemand", out var onDemand))
            return null;

        foreach (var offer in onDemand.EnumerateObject())
        {
            if (!offer.Value.TryGetProperty("priceDimensions", out var dimensions))
                continue;

            foreach (var dimension in dimensions.EnumerateObject())
            {
                if (!dimension.Value.TryGetProperty("pricePerUnit", out var pricePerUnit))
                    continue;

                foreach (var currencyPrice in pricePerUnit.EnumerateObject())
                {
                    if (!decimal.TryParse(
                            currencyPrice.Value.GetString(),
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var unitPrice))
                        continue;

                    var currency = currencyPrice.Name.ToUpperInvariant();
                    var usdRate = usdExchangeRates.GetValueOrDefault(currency);
                    return new CloudPriceQuote(
                        "aws",
                        mapping.ServiceCode,
                        mapping.MetricCode,
                        mapping.Region,
                        mapping.Unit,
                        currency,
                        unitPrice,
                        usdRate,
                        $"{sku.GetString()}:{dimension.Name}",
                        retrievedAtUtc,
                        retrievedAtUtc,
                        null);
                }
            }
        }

        return null;
    }
}

public sealed class AwsCostExplorerSource(
    IAmazonCostExplorer costExplorer,
    IOptions<AwsCostAccountingOptions> options,
    TimeProvider timeProvider) : IAwsCostExplorerSource
{
    public async Task<IReadOnlyList<ActualCloudCostLine>> GetCurrentMonthCostsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime.Date;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = now.AddDays(1);
        var response = await costExplorer.GetCostAndUsageAsync(new GetCostAndUsageRequest
        {
            TimePeriod = new DateInterval
            {
                Start = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                End = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            },
            Granularity = Granularity.MONTHLY,
            Metrics = ["UnblendedCost"],
            GroupBy =
            [
                new GroupDefinition { Type = GroupDefinitionType.DIMENSION, Key = "SERVICE" },
                new GroupDefinition { Type = GroupDefinitionType.TAG, Key = options.Value.TenantTagKey }
            ]
        }, cancellationToken).ConfigureAwait(false);

        var lines = new List<ActualCloudCostLine>();
        foreach (var period in response.ResultsByTime)
        {
            foreach (var group in period.Groups)
            {
                if (!group.Metrics.TryGetValue("UnblendedCost", out var metric)
                    || !decimal.TryParse(metric.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    continue;

                var service = group.Keys.ElementAtOrDefault(0) ?? "unknown";
                var tenantId = ParseTenantId(group.Keys.ElementAtOrDefault(1));
                var lineStart = DateTime.SpecifyKind(DateTime.Parse(period.TimePeriod.Start, CultureInfo.InvariantCulture), DateTimeKind.Utc);
                var lineEnd = DateTime.SpecifyKind(DateTime.Parse(period.TimePeriod.End, CultureInfo.InvariantCulture), DateTimeKind.Utc);
                var externalId = StableId($"actual:{period.TimePeriod.Start}:{period.TimePeriod.End}:{service}:{tenantId}");
                var usdRate = options.Value.UsdExchangeRates.GetValueOrDefault(metric.Unit);
                lines.Add(new ActualCloudCostLine(
                    tenantId,
                    "aws",
                    "cost-explorer",
                    externalId,
                    service,
                    "UnblendedCost",
                    null,
                    lineStart,
                    lineEnd,
                    metric.Unit,
                    amount,
                    amount * usdRate,
                    false,
                    usdRate <= 0));
            }
        }

        return lines;
    }

    public async Task<IReadOnlyList<ActualCloudCostLine>> GetForecastAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime.Date;
        var start = now.AddDays(1);
        var end = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        if (start >= end)
            return [];

        var response = await costExplorer.GetCostForecastAsync(new GetCostForecastRequest
        {
            TimePeriod = new DateInterval
            {
                Start = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                End = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            },
            Granularity = Granularity.MONTHLY,
            Metric = Metric.UNBLENDED_COST
        }, cancellationToken).ConfigureAwait(false);
        if (response.Total is null
            || !decimal.TryParse(response.Total.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return [];

        var usdRate = options.Value.UsdExchangeRates.GetValueOrDefault(response.Total.Unit);
        return
        [
            new ActualCloudCostLine(
                DurableIntegrationEventTenants.Platform,
                "aws",
                "cost-explorer",
                StableId($"forecast:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}"),
                "all",
                "UnblendedCostForecast",
                null,
                start,
                end,
                response.Total.Unit,
                amount,
                amount * usdRate,
                true,
                usdRate <= 0)
        ];
    }

    private static Guid? ParseTenantId(string? tagValue)
    {
        if (string.IsNullOrWhiteSpace(tagValue))
            return null;

        var separator = tagValue.LastIndexOf('$');
        var candidate = separator >= 0 ? tagValue[(separator + 1)..] : tagValue;
        return Guid.TryParse(candidate, out var tenantId) ? tenantId : null;
    }

    private static string StableId(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed class AwsCur2CsvCostSource : IAwsCur2CostSource
{
    public async Task<IReadOnlyList<ActualCloudCostLine>> ReadAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(csvStream, Encoding.UTF8, true, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (headerLine is null)
            return [];

        var headers = ParseCsvLine(headerLine);
        var positions = headers.Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
        var lines = new List<ActualCloudCostLine>();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var values = ParseCsvLine(line);
            var externalId = Get(values, positions, "identity_line_item_id");
            var startText = Get(values, positions, "line_item_usage_start_date");
            var endText = Get(values, positions, "line_item_usage_end_date");
            var costText = Get(values, positions, "line_item_unblended_cost");
            if (string.IsNullOrWhiteSpace(externalId)
                || !DateTime.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var start)
                || !DateTime.TryParse(endText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var end)
                || !decimal.TryParse(costText, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost))
                continue;

            var tenantText = Get(values, positions, "resource_tags_user_tenantid");
            var currency = Get(values, positions, "line_item_currency_code") ?? "USD";
            lines.Add(new ActualCloudCostLine(
                Guid.TryParse(tenantText, out var tenantId) ? tenantId : null,
                "aws",
                "cur-2.0",
                externalId,
                Get(values, positions, "product_servicecode") ?? "unknown",
                Get(values, positions, "line_item_usage_type") ?? "unknown",
                Get(values, positions, "line_item_resource_id"),
                DateTime.SpecifyKind(start, DateTimeKind.Utc),
                DateTime.SpecifyKind(end, DateTimeKind.Utc),
                currency,
                cost,
                currency == "USD" ? cost : 0,
                false,
                currency != "USD"));
        }

        return lines;
    }

    private static string? Get(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> positions,
        string name) => positions.TryGetValue(name, out var index) && index < values.Count ? values[index] : null;

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}

public sealed class AwsCostAccountingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IAwsPriceListRateSource priceList,
    IAwsCostExplorerSource costExplorer,
    IOptions<AwsCostAccountingOptions> options,
    ILogger<AwsCostAccountingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
            return;

        await Task.Delay(TimeSpan.FromMinutes(Math.Max(0, options.Value.InitialDelayMinutes)), stoppingToken)
            .ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<ICloudCostIngestionService>();
                await ingestion.ImportPriceRatesAsync(
                    await priceList.GetCurrentRatesAsync(stoppingToken).ConfigureAwait(false),
                    stoppingToken).ConfigureAwait(false);
                await ingestion.ImportActualCostsAsync(
                    await costExplorer.GetCurrentMonthCostsAsync(stoppingToken).ConfigureAwait(false),
                    stoppingToken).ConfigureAwait(false);
                await ingestion.ImportActualCostsAsync(
                    await costExplorer.GetForecastAsync(stoppingToken).ConfigureAwait(false),
                    stoppingToken).ConfigureAwait(false);
                await scope.ServiceProvider.GetRequiredService<PendingCostValuationService>()
                    .ValuePendingAsync(cancellationToken: stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "AWS internal cost synchronization failed");
            }

            await Task.Delay(
                    TimeSpan.FromMinutes(Math.Max(5, options.Value.RefreshIntervalMinutes)),
                    stoppingToken)
                .ConfigureAwait(false);
        }
    }
}
