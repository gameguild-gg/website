using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Process-local source of truth feeding the SharedKernel
///     <see cref="IExchangeRateProvider"/> conversion seam (issue #412 spec B). Holds the
///     latest provider rate grid plus the operator override quotations (SharedKernel/Fx manual
///     + fixed-override semantics, integrated rather than duplicated: overrides always win,
///     then the provider grid, then null). Seeds itself lazily from <see cref="FxRateSnapshot"/>
///     rows on first use and is refreshed in place by the sync worker and the admin override
///     command, so conversions never hit the database on the hot path.
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
    private volatile bool _seeded;

    private volatile FxRateSet? _providerSet;
    private volatile IReadOnlyDictionary<(string Base, string Quote), ExchangeRate> _overrides =
        new Dictionary<(string, string), ExchangeRate>();

    /// <param name="scopeFactory">Scope factory used for the lazy database seed.</param>
    /// <param name="logger">Logger.</param>
    public FxRuntimeRateSource(IServiceScopeFactory scopeFactory, ILogger<FxRuntimeRateSource> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Seeds the grid and overrides from persisted snapshots exactly once. Failures are
    ///     logged and leave the source empty — fail-closed is the correct posture for FX — the
    ///     next sync pass repopulates it.
    /// </summary>
    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (_seeded)
        {
            return;
        }

        await _seedLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_seeded)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var now = SystemClock.UtcNow;

            var providerSet = await BuildLatestProviderSetAsync(context, cancellationToken).ConfigureAwait(false);
            if (providerSet is not null)
            {
                _providerSet = providerSet;
            }

            var overrides = await context.Set<FxRateSnapshot>()
                .AsNoTracking()
                .Where(snapshot => snapshot.IsOverride)
                .Where(snapshot => snapshot.ExpiresAtUtc == null || snapshot.ExpiresAtUtc > now)
                .OrderByDescending(snapshot => snapshot.EffectiveDateUtc)
                .ThenByDescending(snapshot => snapshot.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var overrideMap = new Dictionary<(string Base, string Quote), ExchangeRate>();
            foreach (var row in overrides)
            {
                var key = (row.BaseCurrency, row.QuoteCurrency);
                if (overrideMap.ContainsKey(key))
                {
                    continue; // newest first: keep the latest override per pair
                }

                try
                {
                    overrideMap[key] = new ExchangeRate(
                        row.BaseCurrency,
                        row.QuoteCurrency,
                        row.Rate,
                        new DateTimeOffset(row.AsOfUtc, TimeSpan.Zero),
                        "manual-override");
                }
                catch (ArgumentException exception)
                {
                    _logger.LogWarning(exception, "Skipping invalid persisted FX override {Base}->{Quote}.", row.BaseCurrency, row.QuoteCurrency);
                }
            }

            _overrides = overrideMap;
            _seeded = true;
            _logger.LogInformation(
                "FX runtime rate source seeded: {Provider} grid ({Base}, {Count} quotes, effective {Effective:yyyy-MM-dd}) + {Overrides} overrides.",
                providerSet?.ProviderName ?? "none",
                providerSet?.BaseCurrency ?? "-",
                providerSet?.Rates.Count ?? 0,
                providerSet?.EffectiveDateUtc ?? DateTime.MinValue,
                overrideMap.Count);
        }
        catch (Exception exception)
        {
            // Seed failure must never crash callers: an unseeded source serves no cross rates,
            // which is exactly the fail-closed posture.
            _logger.LogWarning(exception, "FX runtime rate source seed failed; serving overrides only until the next sync pass.");
            _seeded = true;
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
        _providerSet = set;
        _seeded = true;
    }

    /// <summary>Upserts an operator override for an exact direction (admin command path).</summary>
    public void ApplyOverride(ExchangeRate rate)
    {
        ArgumentNullException.ThrowIfNull(rate);

        var current = _overrides;
        var updated = new Dictionary<(string Base, string Quote), ExchangeRate>(current)
        {
            [(rate.BaseCurrency, rate.QuoteCurrency)] = rate,
        };

        _overrides = updated;
        _seeded = true;
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

        if (_overrides.TryGetValue((normalizedFrom, normalizedTo), out var overridden))
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
    public IReadOnlyCollection<ExchangeRate> CurrentOverrides { get => _overrides.Values.ToArray(); }

    /// <summary>Currently active provider grid, if any (read-only view for status queries).</summary>
    public FxRateSet? CurrentProviderSet { get => _providerSet; }

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
