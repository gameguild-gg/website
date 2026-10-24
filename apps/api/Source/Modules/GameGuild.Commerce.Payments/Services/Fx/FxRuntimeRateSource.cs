using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Process-local source of truth feeding the SharedKernel
///     <see cref="IExchangeRateProvider"/> conversion seam (issue #412 spec B). Holds the
///     latest provider rate grid plus the operator override quotations (SharedKernel/Fx manual
///     + fixed-override semantics, integrated rather than duplicated: overrides always win,
///     then the provider grid, then null). Seeds itself lazily from <see cref="FxRateSnapshot"/>
///     rows on first use and refreshes after the configured cache TTL so changes from other
///     processes become visible. The sync worker and admin command also update local state.
/// </summary>
/// <remarks>
///     Derivation decision record (spec B): within a SINGLE provider grid snapshot, cross rates
///     and inverse rates are derived by exact decimal arithmetic — X→Y = grid[Y]/grid[X] and
///     X→base = 1/grid[X], both rounded to 12 significant decimals like
///     <see cref="ExchangeRate.Invert"/>. A market provider publishes one internally consistent
///     quotation set, so this is deterministic arithmetic over audited data, never fabrication,
///     and never mixes providers or dates. This deliberately differs from
///     <see cref="ManualExchangeRateProvider"/>, which serves only exact operator-entered
///     directions because every manual quotation is an individual operator decision. Rates are
///     NEVER derived across grids, providers or dates, and unknown codes return null (fail closed).
/// </remarks>
public sealed class FxRuntimeRateSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FxRuntimeRateSource> _logger;
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly IOptions<FxSyncOptions> _options;
    private volatile bool _seeded;
    private long _refreshAtUtcTicks;
    private long _stateRevision;

    private volatile FxRateSet? _providerSet;
    private volatile IReadOnlyDictionary<(string Base, string Quote), OverrideQuotation> _overrides =
        new Dictionary<(string, string), OverrideQuotation>();

    /// <param name="scopeFactory">Scope factory used for the lazy database seed.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="options">Refresh interval shared with the quotation cache.</param>
    public FxRuntimeRateSource(IServiceScopeFactory scopeFactory, ILogger<FxRuntimeRateSource> logger, IOptions<FxSyncOptions>? options = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options ?? Options.Create(new FxSyncOptions());
    }

    /// <summary>
    ///     Loads persisted quotations on first use and at the configured TTL. Failed loads
    ///     are retried on the next lookup; cancellation propagates to the caller.
    /// </summary>
    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (IsSeedFresh())
        {
            return;
        }

        await _seedLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsSeedFresh())
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var now = SystemClock.UtcNow;
            var revision = Interlocked.Read(ref _stateRevision);

            var providerSet = await BuildLatestProviderSetAsync(context, cancellationToken).ConfigureAwait(false);
            var overrides = await context.Set<FxRateSnapshot>()
                .AsNoTracking()
                .Where(snapshot => snapshot.IsOverride)
                .OrderByDescending(snapshot => snapshot.AsOfUtc)
                .ThenByDescending(snapshot => snapshot.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var overrideMap = new Dictionary<(string Base, string Quote), OverrideQuotation>();
            var seen = new HashSet<(string Base, string Quote)>();
            foreach (var row in overrides)
            {
                var key = (row.BaseCurrency, row.QuoteCurrency);
                if (!seen.Add(key) || row.ExpiresAtUtc <= now)
                {
                    continue; // An expired latest override must not resurrect an older one.
                }

                try
                {
                    overrideMap[key] = new OverrideQuotation(new ExchangeRate(
                        row.BaseCurrency,
                        row.QuoteCurrency,
                        row.Rate,
                        new DateTimeOffset(row.AsOfUtc, TimeSpan.Zero),
                        "manual-override"), row.ExpiresAtUtc);
                }
                catch (ArgumentException exception)
                {
                    _logger.LogWarning(exception, "Skipping invalid persisted FX override {Base}->{Quote}.", row.BaseCurrency, row.QuoteCurrency);
                }
            }

            lock (_stateLock)
            {
                if (_stateRevision != revision)
                {
                    return; // A concurrent local mutation wins; refresh again on the next miss.
                }

                _providerSet = providerSet;
                _overrides = overrideMap;
                Interlocked.Exchange(ref _refreshAtUtcTicks, RefreshAtUtcTicks());
                _seeded = true;
            }
            _logger.LogInformation(
                "FX runtime rate source seeded: {Provider} grid ({Base}, {Count} quotes, effective {Effective:yyyy-MM-dd}) + {Overrides} overrides.",
                providerSet?.ProviderName ?? "none",
                providerSet?.BaseCurrency ?? "-",
                providerSet?.Rates.Count ?? 0,
                providerSet?.EffectiveDateUtc ?? DateTime.MinValue,
                overrideMap.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Seed failure must never crash callers: an unseeded source serves no cross rates,
            // which is exactly the fail-closed posture.
            _logger.LogWarning(exception, "FX runtime rate source refresh failed; retrying on the next lookup.");
        }
        finally
        {
            _seedLock.Release();
        }
    }

    /// <summary>Replaces the provider grid (called by the sync worker after a successful pass).</summary>
    public void ApplyProviderSet(FxRateSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        lock (_stateLock)
        {
            _providerSet = set;
            MarkLocalMutation();
        }
    }

    /// <summary>Upserts an operator override for an exact direction (admin command path).</summary>
    public void ApplyOverride(ExchangeRate rate, DateTime? expiresAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(rate);

        lock (_stateLock)
        {
            _overrides = new Dictionary<(string Base, string Quote), OverrideQuotation>(_overrides)
            {
                [(rate.BaseCurrency, rate.QuoteCurrency)] = new(rate, expiresAtUtc),
            };

            MarkLocalMutation();
        }
    }

    /// <summary>Returns an exact manual quotation only while its persisted expiry allows it.</summary>
    public ExchangeRate? TryGetOverride(string from, string to, DateTime utcNow)
    {
        var key = (from.Trim().ToUpperInvariant(), to.Trim().ToUpperInvariant());
        return _overrides.TryGetValue(key, out var value)
            && (value.ExpiresAtUtc is null || value.ExpiresAtUtc > utcNow)
            ? value.Rate : null;
    }

    /// <summary>
    ///     Resolves a quotation for the exact <paramref name="from"/> → <paramref name="to"/>
    ///     direction: non-expired override first, then derivation within the current provider
    ///     grid. Same-code pairs and unknown legs return null.
    /// </summary>
    public ExchangeRate? TryGetRate(string from, string to, DateTime utcNow)
    {
        var normalizedFrom = from?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedTo = to?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalizedFrom.Length != 3 || normalizedTo.Length != 3 || normalizedFrom == normalizedTo)
        {
            return null;
        }

        var overridden = TryGetOverride(normalizedFrom, normalizedTo, utcNow);
        if (overridden is not null)
        {
            return overridden;
        }

        var set = _providerSet;
        if (set is null)
        {
            return null;
        }

        // Grid semantics: R(c) = units of c per one grid base (R(base) is implicitly 1).
        var fromRate = normalizedFrom == set.BaseCurrency
            ? 1m
            : set.Rates.TryGetValue(normalizedFrom, out var directFrom) ? directFrom : 0m;
        var toRate = normalizedTo == set.BaseCurrency
            ? 1m
            : set.Rates.TryGetValue(normalizedTo, out var directTo) ? directTo : 0m;

        if (fromRate <= 0m || toRate <= 0m)
        {
            return null; // one leg is outside the grid — fail closed rather than route via an unknown currency
        }

        // Cross/inverse derivation inside one grid snapshot (see derivation decision record).
        // Only true divisions get rounded; the exact base→X direction is passed through as published.
        var derived = normalizedFrom == set.BaseCurrency
            ? toRate
            : normalizedTo == set.BaseCurrency
                ? decimal.Round(1m / fromRate, 12, MidpointRounding.AwayFromZero)
                : decimal.Round(toRate / fromRate, 12, MidpointRounding.AwayFromZero);

        if (derived <= 0m)
        {
            return null;
        }

        return new ExchangeRate(
            normalizedFrom,
            normalizedTo,
            derived,
            new DateTimeOffset(set.EffectiveDateUtc, TimeSpan.Zero),
            set.ProviderName);
    }

    /// <summary>Currently active override quotations (read-only view for status queries).</summary>
    public IReadOnlyCollection<ExchangeRate> CurrentOverrides
    {
        get => _overrides.Values.Where(value => value.ExpiresAtUtc is null || value.ExpiresAtUtc > SystemClock.UtcNow)
            .Select(value => value.Rate).ToArray();
    }

    /// <summary>Currently active provider grid, if any (read-only view for status queries).</summary>
    public FxRateSet? CurrentProviderSet { get => _providerSet; }

    private bool IsSeedFresh() => _seeded && SystemClock.UtcNow.Ticks < Interlocked.Read(ref _refreshAtUtcTicks);

    private long RefreshAtUtcTicks() => SystemClock.UtcNow.AddMinutes(Math.Max(0, _options.Value.CacheTtlMinutes)).Ticks;

    private void MarkLocalMutation()
    {
        _stateRevision++;
        if (!_seeded)
        {
            Interlocked.Exchange(ref _refreshAtUtcTicks, RefreshAtUtcTicks());
            _seeded = true;
        }
    }

    private sealed record OverrideQuotation(ExchangeRate Rate, DateTime? ExpiresAtUtc);

    private static async Task<FxRateSet?> BuildLatestProviderSetAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var latestDate = await context.Set<FxRateSnapshot>()
            .AsNoTracking()
            .Where(snapshot => !snapshot.IsOverride)
            .MaxAsync(snapshot => (DateTime?)snapshot.EffectiveDateUtc, cancellationToken)
            .ConfigureAwait(false);

        if (latestDate is null)
        {
            return null;
        }

        var rows = await context.Set<FxRateSnapshot>()
            .AsNoTracking()
            .Where(snapshot => !snapshot.IsOverride && snapshot.EffectiveDateUtc == latestDate.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return null;
        }

        // Prefer the provider that contributed the most quotations for that date; rebuild the
        // grid from its rows. Rows quoting the grid base reconstruct the forward direction;
        // rows based on the grid base reconstruct the inverse.
        var providerGroup = rows
            .GroupBy(row => row.ProviderName)
            .OrderByDescending(group => group.Count())
            .First();

        var gridRows = providerGroup.ToList();
        var baseCurrency = gridRows.GroupBy(row => row.BaseCurrency).OrderByDescending(group => group.Count()).First().Key;
        var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var asOf = DateTime.MinValue;

        foreach (var row in gridRows)
        {
            if (row.Rate <= 0m)
            {
                continue;
            }

            if (row.BaseCurrency == baseCurrency)
            {
                rates[row.QuoteCurrency] = row.Rate;
            }
            else if (row.QuoteCurrency == baseCurrency)
            {
                rates[row.BaseCurrency] = decimal.Round(1m / row.Rate, 12, MidpointRounding.AwayFromZero);
            }

            if (row.AsOfUtc > asOf)
            {
                asOf = row.AsOfUtc;
            }
        }

        if (rates.Count == 0)
        {
            return null;
        }

        return new FxRateSet(
            providerGroup.Key,
            baseCurrency,
            rates,
            new DateTimeOffset(asOf == DateTime.MinValue ? latestDate.Value : asOf, TimeSpan.Zero),
            latestDate.Value,
            RawResponse: string.Empty,
            RawResponseContentType: "application/json");
    }
}
