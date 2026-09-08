using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

public sealed record CloudPriceQuote(
    string Provider,
    string ServiceCode,
    string MetricCode,
    string Region,
    string Unit,
    string SourceCurrency,
    decimal SourceUnitPrice,
    decimal UsdExchangeRate,
    string ProviderRateId,
    DateTime EffectiveAtUtc,
    DateTime RetrievedAtUtc,
    DateTime? ValidUntilUtc);

public sealed record ActualCloudCostLine(
    Guid? TenantId,
    string Provider,
    string Source,
    string ExternalLineId,
    string ServiceCode,
    string UsageType,
    string? ResourceId,
    DateTime BillingPeriodStartUtc,
    DateTime BillingPeriodEndUtc,
    string SourceCurrency,
    decimal SourceCost,
    decimal UsdCost,
    bool IsForecast,
    bool IsUsdNormalizationPending = false);

public interface ICloudCostIngestionService
{
    Task<int> ImportPriceRatesAsync(IEnumerable<CloudPriceQuote> quotes, CancellationToken cancellationToken = default);
    Task<int> ImportActualCostsAsync(IEnumerable<ActualCloudCostLine> lines, CancellationToken cancellationToken = default);
}

public interface IInternalCostLedgerReader
{
    Task<InternalCostLedgerSummary> SummarizeAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);
}

public sealed record InternalCostLedgerSummary(long UsageEntries, decimal SourceCost, decimal UsdCost);

public sealed class CloudCostIngestionService(IApplicationDbContext context) : ICloudCostIngestionService
{
    public async Task<int> ImportPriceRatesAsync(
        IEnumerable<CloudPriceQuote> quotes,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;
        foreach (var quote in quotes)
        {
            var exists = await context.Set<CloudPriceRate>().AnyAsync(
                    rate => rate.Provider == quote.Provider
                            && rate.ProviderRateId == quote.ProviderRateId
                            && rate.EffectiveAtUtc == quote.EffectiveAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                continue;

            context.Set<CloudPriceRate>().Add(new CloudPriceRate
            {
                Provider = quote.Provider,
                ServiceCode = quote.ServiceCode,
                MetricCode = quote.MetricCode,
                Region = quote.Region,
                Unit = quote.Unit,
                SourceCurrency = quote.SourceCurrency,
                SourceUnitPrice = quote.SourceUnitPrice,
                UsdExchangeRate = quote.UsdExchangeRate,
                ProviderRateId = quote.ProviderRateId,
                EffectiveAtUtc = quote.EffectiveAtUtc,
                RetrievedAtUtc = quote.RetrievedAtUtc,
                ValidUntilUtc = quote.ValidUntilUtc
            });
            imported++;
        }

        if (imported > 0)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return imported;
    }

    public async Task<int> ImportActualCostsAsync(
        IEnumerable<ActualCloudCostLine> lines,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;
        foreach (var line in lines)
        {
            var exists = await context.Set<ActualCloudCostEntry>().AnyAsync(
                    entry => entry.Provider == line.Provider
                             && entry.Source == line.Source
                             && entry.ExternalLineId == line.ExternalLineId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                continue;

            context.Set<ActualCloudCostEntry>().Add(new ActualCloudCostEntry
            {
                TenantId = line.TenantId ?? DurableIntegrationEventTenants.Platform,
                Provider = line.Provider,
                Source = line.Source,
                ExternalLineId = line.ExternalLineId,
                ServiceCode = line.ServiceCode,
                UsageType = line.UsageType,
                ResourceId = line.ResourceId,
                BillingPeriodStartUtc = line.BillingPeriodStartUtc,
                BillingPeriodEndUtc = line.BillingPeriodEndUtc,
                SourceCurrency = line.SourceCurrency,
                SourceCost = line.SourceCost,
                UsdCost = line.UsdCost,
                IsUsdNormalizationPending = line.IsUsdNormalizationPending,
                IsForecast = line.IsForecast,
                ImportedAtUtc = SystemClock.UtcNow
            });
            imported++;
        }

        if (imported > 0)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return imported;
    }
}

public sealed class InternalCostLedgerReader(IApplicationDbContext context) : IInternalCostLedgerReader
{
    public async Task<InternalCostLedgerSummary> SummarizeAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        var query = from valuation in context.Set<InternalCostValuation>()
            join usage in context.Set<InternalUsageLedgerEntry>()
                on valuation.UsageLedgerEntryId equals usage.Id
            where valuation.TenantId == tenantId
                  && usage.OccurredAtUtc >= periodStartUtc
                  && usage.OccurredAtUtc < periodEndUtc
            select valuation;
        var usageEntries = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var sourceCost = await query.SumAsync(value => (decimal?)value.SourceCost, cancellationToken)
            .ConfigureAwait(false) ?? 0;
        var usdCost = await query.SumAsync(value => (decimal?)value.UsdCost, cancellationToken)
            .ConfigureAwait(false) ?? 0;
        return new InternalCostLedgerSummary(usageEntries, sourceCost, usdCost);
    }
}

public sealed class InternalCostValuationService(
    IApplicationDbContext context,
    TimeProvider timeProvider,
    ILogger<InternalCostValuationService> logger)
{
    public async Task<bool> TryValueAsync(
        InternalUsageLedgerEntry usage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rate = await context.Set<CloudPriceRate>()
                .Where(candidate => candidate.Provider == usage.Provider
                                    && candidate.MetricCode == usage.MetricCode
                                    && (candidate.Region == usage.Region || candidate.Region == "global")
                                    && candidate.EffectiveAtUtc <= usage.OccurredAtUtc)
                .OrderByDescending(candidate => candidate.Region == usage.Region)
                .ThenByDescending(candidate => candidate.EffectiveAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (rate is null)
                return false;
            if (rate.UsdExchangeRate <= 0)
                return false;

            var sourceCost = usage.Quantity * rate.SourceUnitPrice;
            var now = timeProvider.GetUtcNow().UtcDateTime;
            context.Set<InternalCostValuation>().Add(new InternalCostValuation
            {
                TenantId = usage.TenantId,
                UsageLedgerEntryId = usage.Id,
                CloudPriceRateId = rate.Id,
                SourceCurrency = rate.SourceCurrency,
                SourceUnitPrice = rate.SourceUnitPrice,
                SourceCost = sourceCost,
                UsdExchangeRate = rate.UsdExchangeRate,
                UsdCost = sourceCost * rate.UsdExchangeRate,
                IsStaleEstimate = rate.ValidUntilUtc.HasValue && rate.ValidUntilUtc.Value < now,
                ValuedAtUtc = now
            });
            usage.ValuationStatus = InternalCostStatuses.Valued;
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Internal cost valuation remains pending for usage {UsageId}", usage.Id);
            return false;
        }
    }
}

public sealed class PendingCostValuationService(
    IApplicationDbContext context,
    InternalCostValuationService valuationService)
{
    public async Task<int> ValuePendingAsync(int limit = 500, CancellationToken cancellationToken = default)
    {
        var pending = await context.Set<InternalUsageLedgerEntry>()
            .Where(entry => entry.ValuationStatus == InternalCostStatuses.Pending)
            .OrderBy(entry => entry.OccurredAtUtc)
            .Take(Math.Clamp(limit, 1, 5_000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var valued = 0;
        foreach (var entry in pending)
        {
            if (await valuationService.TryValueAsync(entry, cancellationToken).ConfigureAwait(false))
                valued++;
        }

        if (pending.Count > 0)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return valued;
    }
}
