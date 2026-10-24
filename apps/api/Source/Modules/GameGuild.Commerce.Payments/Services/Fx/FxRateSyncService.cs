using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GameGuild.Commerce.Payments;

/// <summary>Outcome of one FX rate sync pass.</summary>
/// <param name="Status">Run outcome.</param>
/// <param name="ProviderName">Provider whose grid was persisted (null on total failure).</param>
/// <param name="RatesApplied">Quotations inserted or updated.</param>
/// <param name="RatesChanged">Quotations whose value changed.</param>
/// <param name="ConsecutiveFailures">Trailing failed-run count after this pass.</param>
/// <param name="EffectiveDateUtc">Effective date of the persisted grid (null on failure).</param>
/// <param name="NewestSnapshotDateUtc">Newest persisted snapshot effective date after the pass.</param>
/// <param name="ErrorMessage">Failure reason on <see cref="FxRateSyncRunStatus.Failed"/>.</param>
public sealed record FxRateSyncResult(
    FxRateSyncRunStatus Status,
    string? ProviderName,
    int RatesApplied,
    int RatesChanged,
    int ConsecutiveFailures,
    DateTime? EffectiveDateUtc,
    DateTime? NewestSnapshotDateUtc,
    string? ErrorMessage = null);

/// <summary>
///     Runs one FX rate sync pass (issue #412 spec B): fetch a rate grid from the configured
///     primary provider with retry + exponential backoff, fail over to the fallback provider
///     when the primary stays unavailable, persist the grid as <see cref="FxRateSnapshot"/> rows
///     and the run (including the raw provider response) as an <see cref="FxRateSyncRun"/>, and
///     keep the <see cref="FxRuntimeRateSource"/> current. Fail-closed throughout: nothing is
///     persisted when no provider answers, changed rates are audit-logged (rate-change audit
///     log), consecutive failures alert operators, and stale data raises a staleness alert.
/// </summary>
public interface IFxRateSyncService
{
    /// <summary>Executes one sync pass now.</summary>
    Task<FxRateSyncResult> SyncAsync(CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IFxRateSyncService"/> implementation.</summary>
public sealed class FxRateSyncService(
    IEnumerable<IFxRateProvider> registeredProviders,
    IServiceProvider serviceProvider,
    IOptions<FxSyncOptions> options,
    FxRuntimeRateSource runtimeSource,
    IFxRateSyncAlerter alerter,
    ILogger<FxRateSyncService> logger) : IFxRateSyncService
{
    /// <inheritdoc />
    public async Task<FxRateSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var startedAt = SystemClock.UtcNow;

        FxRateSet? rateSet = null;
        string? failure = null;

        var candidates = BuildCandidateChain(registeredProviders, settings);
        foreach (var (provider, role) in candidates)
        {
            try
            {
                rateSet = await FetchWithRetryAsync(provider, settings, cancellationToken).ConfigureAwait(false);
                if (rateSet is not null)
                {
                    break;
                }
            }
            catch (Exception exception) when (exception is FxRateProviderUnavailableException or HttpRequestException or TaskCanceledException)
            {
                failure = $"{provider.Name} ({role}): {exception.Message}";
                logger.LogWarning("FX sync fetch from {Provider} ({Role}) failed: {Message}.", provider.Name, role, exception.Message);
            }
        }

        var context = serviceProvider.GetRequiredService<IApplicationDbContext>();

        if (rateSet is null)
        {
            var result = await PersistFailedRunAsync(context, settings, startedAt, failure ?? "No FX provider configured.", cancellationToken).ConfigureAwait(false);

            if (result.ConsecutiveFailures >= settings.AlertAfterConsecutiveFailures)
            {
                await alerter
                    .RaiseSyncFailureAlertAsync(settings.Provider, result.ConsecutiveFailures, failure ?? "no provider", cancellationToken)
                    .ConfigureAwait(false);
            }

            return result;
        }

        return await PersistSuccessfulRunAsync(context, settings, startedAt, rateSet, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<(IFxRateProvider Provider, string Role)> BuildCandidateChain(
        IEnumerable<IFxRateProvider> registeredProviders,
        FxSyncOptions settings)
    {
        var byName = registeredProviders
            .GroupBy(provider => provider.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        IFxRateProvider ResolveConfigured(string name)
        {
            return byName.TryGetValue(name, out var provider)
                ? provider
                : byName[FxSyncOptions.ProviderNone];
        }

        var chain = new List<(IFxRateProvider, string)> { (ResolveConfigured(settings.Provider), "primary") };
        if (!string.IsNullOrWhiteSpace(settings.FallbackProvider)
            && settings.FallbackProvider != FxSyncOptions.ProviderNone
            && settings.FallbackProvider != settings.Provider)
        {
            chain.Add((ResolveConfigured(settings.FallbackProvider), "fallback"));
        }

        return chain;
    }

    /// <summary>
    ///     Fetches a rate set with retry + exponential backoff (issue #412 spec B). The delay
    ///     doubles per attempt from <c>RetryBaseDelaySeconds</c>; transient transport failures
    ///     (HTTP 5xx/network) and timeouts are retried, provider-unavailable conditions (bad
    ///    config/credentials) surface immediately after the loop. Implemented inline rather than
    ///     through a resilience library: the solution pins its dependency set centrally and this
    ///     is the only outbound-retry loop in the module.
    /// </summary>
    private async Task<FxRateSet?> FetchWithRetryAsync(
        IFxRateProvider provider,
        FxSyncOptions settings,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, settings.RetryMaxAttempts);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await provider.GetRatesAsync(asOfUtc: null, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                lastException = exception;
                logger.LogWarning(
                    "FX fetch attempt {Attempt}/{Attempts} from {Provider} failed: {Message}.",
                    attempt,
                    attempts,
                    provider.Name,
                    exception.Message);

                if (attempt < attempts)
                {
                    var delay = TimeSpan.FromSeconds(settings.RetryBaseDelaySeconds * Math.Pow(2, attempt - 1));
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        throw new FxRateProviderUnavailableException(
            provider.Name,
            $"all {attempts} fetch attempts failed: {lastException?.Message ?? "unknown transport failure"}",
            lastException);
    }

    private async Task<FxRateSyncResult> PersistSuccessfulRunAsync(
        IApplicationDbContext context,
        FxSyncOptions settings,
        DateTime startedAt,
        FxRateSet rateSet,
        CancellationToken cancellationToken)
    {
        var now = SystemClock.UtcNow;
        var rates = context.Set<FxRateSnapshot>();
        var effectiveDate = rateSet.EffectiveDateUtc.Date;
        var targetDateKeys = rateSet.Rates.Keys.ToList();

        var existing = await rates
            .Where(snapshot => !snapshot.IsOverride
                               && snapshot.EffectiveDateUtc == effectiveDate
                               && snapshot.ProviderName == rateSet.ProviderName
                               && snapshot.BaseCurrency == rateSet.BaseCurrency)
            .ToDictionaryAsync(snapshot => snapshot.QuoteCurrency, cancellationToken)
            .ConfigureAwait(false);

        var run = new FxRateSyncRun
        {
            ProviderName = rateSet.ProviderName,
            PrimaryProviderName = settings.Provider,
            StartedAtUtc = startedAt,
            CompletedAtUtc = now,
            Status = FxRateSyncRunStatus.Succeeded,
            EffectiveDateUtc = effectiveDate,
            RawResponse = rateSet.RawResponse,
            RawResponseContentType = rateSet.RawResponseContentType,
        };

        await context.Set<FxRateSyncRun>().AddAsync(run, cancellationToken).ConfigureAwait(false);

        var applied = 0;
        var changed = 0;
        var changes = new List<(string Quote, decimal OldRate, decimal NewRate)>();

        foreach (var quoteCurrency in targetDateKeys)
        {
            var value = rateSet.Rates[quoteCurrency];
            if (existing.TryGetValue(quoteCurrency, out var row))
            {
                if (row.Rate != value)
                {
                    changes.Add((quoteCurrency, row.Rate, value));
                    row.Rate = value;
                    row.AsOfUtc = rateSet.AsOfUtc.UtcDateTime;
                    row.SyncRunId = run.Id;
                    row.Touch();
                    changed++;
                }

                applied++;
                continue;
            }

            await rates.AddAsync(new FxRateSnapshot
            {
                BaseCurrency = rateSet.BaseCurrency,
                QuoteCurrency = quoteCurrency,
                Rate = value,
                EffectiveDateUtc = effectiveDate,
                AsOfUtc = rateSet.AsOfUtc.UtcDateTime,
                ProviderName = rateSet.ProviderName,
                Source = rateSet.Source,
                SyncRunId = run.Id,
                IsOverride = false,
            }, cancellationToken).ConfigureAwait(false);
            applied++;
        }

        // Rate-change audit log (spec B): one entry per changed quotation plus the run summary.
        foreach (var (quote, oldRate, newRate) in changes)
        {
            context.Set<AuditTrail>().Add(new AuditTrail
            {
                EntityType = nameof(FxRateSnapshot),
                EntityId = existing.TryGetValue(quote, out var row) ? row.Id : Guid.Empty,
                Action = AuditAction.Updated,
                OldValue = JsonSerializer.Serialize(new { rate = oldRate, effectiveDateUtc = effectiveDate, provider = rateSet.ProviderName }),
                NewValue = JsonSerializer.Serialize(new { rate = newRate, effectiveDateUtc = effectiveDate, provider = rateSet.ProviderName }),
                ChangedBy = Guid.Empty, // system actor: the scheduled sync worker
                ChangedAt = now,
                Metadata = JsonSerializer.Serialize(new { baseCurrency = rateSet.BaseCurrency, quoteCurrency = quote, syncRunId = run.Id }),
            });
        }

        context.Set<AuditTrail>().Add(new AuditTrail
        {
            EntityType = nameof(FxRateSyncRun),
            EntityId = run.Id,
            Action = AuditAction.Created,
            OldValue = null,
            NewValue = JsonSerializer.Serialize(new { provider = rateSet.ProviderName, effectiveDateUtc = effectiveDate, ratesApplied = applied, ratesChanged = changed }),
            ChangedBy = Guid.Empty,
            ChangedAt = now,
            Metadata = JsonSerializer.Serialize(new { primaryProvider = settings.Provider, baseCurrency = rateSet.BaseCurrency }),
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Keep the conversion path current and assess staleness of the newest data.
        await runtimeSource.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        runtimeSource.ApplyProviderSet(rateSet);

        var newestSnapshotDate = await GetNewestSnapshotDateAsync(context, cancellationToken).ConfigureAwait(false)
                                 ?? effectiveDate;
        var staleDays = (now.Date - newestSnapshotDate.Date).TotalDays;
        var stale = staleDays >= settings.StalenessAlertDays;

        if (stale)
        {
            run.Status = FxRateSyncRunStatus.SucceededStale;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await alerter.RaiseStalenessAlertAsync(newestSnapshotDate, settings.StalenessAlertDays, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "FX sync succeeded via {Provider}: {Applied} rates applied, {Changed} changed, effective {Effective:yyyy-MM-dd}{Stale}.",
            rateSet.ProviderName,
            applied,
            changed,
            effectiveDate,
            stale ? " (STALE)" : string.Empty);

        return new FxRateSyncResult(
            run.Status,
            rateSet.ProviderName,
            applied,
            changed,
            ConsecutiveFailures: 0,
            EffectiveDateUtc: effectiveDate,
            NewestSnapshotDateUtc: newestSnapshotDate);
    }

    private async Task<FxRateSyncResult> PersistFailedRunAsync(
        IApplicationDbContext context,
        FxSyncOptions settings,
        DateTime startedAt,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var now = SystemClock.UtcNow;
        var previousFailures = await CountTrailingFailedRunsAsync(context, cancellationToken).ConfigureAwait(false);
        var consecutiveFailures = previousFailures + 1;

        await context.Set<FxRateSyncRun>().AddAsync(new FxRateSyncRun
        {
            ProviderName = settings.Provider,
            PrimaryProviderName = settings.Provider,
            StartedAtUtc = startedAt,
            CompletedAtUtc = now,
            Status = FxRateSyncRunStatus.Failed,
            ConsecutiveFailureCount = consecutiveFailures,
            ErrorMessage = errorMessage.Length <= 4000 ? errorMessage : errorMessage[..4000],
            RawResponse = string.Empty,
            RawResponseContentType = "text/plain",
        }, cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogError(
            "FX sync failed ({Failures} consecutive): {Error}. Cross-currency conversion stays on persisted rates or fails closed.",
            consecutiveFailures,
            errorMessage);

        return new FxRateSyncResult(
            FxRateSyncRunStatus.Failed,
            ProviderName: null,
            RatesApplied: 0,
            RatesChanged: 0,
            ConsecutiveFailures: consecutiveFailures,
            EffectiveDateUtc: null,
            NewestSnapshotDateUtc: await GetNewestSnapshotDateAsync(context, cancellationToken).ConfigureAwait(false),
            ErrorMessage: errorMessage);
    }

    private static async Task<int> CountTrailingFailedRunsAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var recent = await context.Set<FxRateSyncRun>()
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(64)
            .Select(run => run.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var trailing = 0;
        foreach (var status in recent)
        {
            if (status == FxRateSyncRunStatus.Failed)
            {
                trailing++;
            }
            else
            {
                break;
            }
        }

        return trailing;
    }

    private static async Task<DateTime?> GetNewestSnapshotDateAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        return await context.Set<FxRateSnapshot>()
            .AsNoTracking()
            .MaxAsync(snapshot => (DateTime?)snapshot.EffectiveDateUtc, cancellationToken)
            .ConfigureAwait(false);
    }
}
