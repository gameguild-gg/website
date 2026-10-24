using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>One persisted FX quotation as exposed by the admin surface.</summary>
/// <param name="BaseCurrency">Code the rate converts from.</param>
/// <param name="QuoteCurrency">Code the rate converts to.</param>
/// <param name="Rate">Quote units per one base unit.</param>
/// <param name="EffectiveDateUtc">Effective (data) date of the quotation.</param>
/// <param name="AsOfUtc">Moment the provider quoted it.</param>
/// <param name="ProviderName">Producing provider.</param>
/// <param name="Source">Provenance label.</param>
/// <param name="IsOverride">Whether it is an operator override.</param>
/// <param name="ExpiresAtUtc">Override expiry, if any.</param>
public sealed record FxRateDto(
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate,
    DateTime EffectiveDateUtc,
    DateTime AsOfUtc,
    string ProviderName,
    string Source,
    bool IsOverride,
    DateTime? ExpiresAtUtc);

/// <summary>
///     Query (issue #412 spec B): persisted FX quotations with historical/effective-date lookup.
///     Filters: optional pair (exact direction), optional effective-date window
///     (<paramref name="FromEffectiveDateUtc"/>/<paramref name="ToEffectiveDateUtc"/>, inclusive).
///     Ordered newest-effective-first, capped at <paramref name="Limit"/> (default 500, max 2000).
/// </summary>
[AuthorizeRequest("", RequireSystemAdmin = true)]
public sealed record GetFxRatesQuery(
    string? BaseCurrency = null,
    string? QuoteCurrency = null,
    DateTime? FromEffectiveDateUtc = null,
    DateTime? ToEffectiveDateUtc = null,
    int Limit = 500) : IQuery<IReadOnlyList<FxRateDto>>;

/// <summary>Handler for <see cref="GetFxRatesQuery"/>.</summary>
public sealed class GetFxRatesQueryHandler(IApplicationDbContext context, IActorContextAccessor actorContextAccessor) : IQueryHandler<GetFxRatesQuery, IReadOnlyList<FxRateDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FxRateDto>> Handle(GetFxRatesQuery request, CancellationToken cancellationToken)
    {
        FxAdministrationActor.RequireSystemAdmin(actorContextAccessor);
        var limit = Math.Clamp(request.Limit, 1, 2000);
        var query = context.Set<FxRateSnapshot>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.BaseCurrency))
        {
            var baseCurrency = request.BaseCurrency.Trim().ToUpperInvariant();
            query = query.Where(snapshot => snapshot.BaseCurrency == baseCurrency);
        }

        if (!string.IsNullOrWhiteSpace(request.QuoteCurrency))
        {
            var quoteCurrency = request.QuoteCurrency.Trim().ToUpperInvariant();
            query = query.Where(snapshot => snapshot.QuoteCurrency == quoteCurrency);
        }

        if (request.FromEffectiveDateUtc.HasValue)
        {
            query = query.Where(snapshot => snapshot.EffectiveDateUtc >= request.FromEffectiveDateUtc.Value.Date);
        }

        if (request.ToEffectiveDateUtc.HasValue)
        {
            query = query.Where(snapshot => snapshot.EffectiveDateUtc <= request.ToEffectiveDateUtc.Value.Date);
        }

        var rows = await query
            .OrderByDescending(snapshot => snapshot.EffectiveDateUtc)
            .ThenBy(snapshot => snapshot.BaseCurrency)
            .ThenBy(snapshot => snapshot.QuoteCurrency)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => new FxRateDto(
                row.BaseCurrency,
                row.QuoteCurrency,
                row.Rate,
                row.EffectiveDateUtc,
                row.AsOfUtc,
                row.ProviderName,
                row.Source,
                row.IsOverride,
                row.ExpiresAtUtc))
            .ToList();
    }
}

/// <summary>Recent sync runs with the failure counter (admin observability).</summary>
/// <param name="ProviderName">Provider that produced the persisted grid.</param>
/// <param name="PrimaryProviderName">Configured primary provider.</param>
/// <param name="StartedAtUtc">Run start.</param>
/// <param name="CompletedAtUtc">Run completion.</param>
/// <param name="Status">Outcome.</param>
/// <param name="RatesApplied">Quotations persisted.</param>
/// <param name="RatesChanged">Quotations whose value changed.</param>
/// <param name="EffectiveDateUtc">Grid effective date.</param>
/// <param name="ConsecutiveFailureCount">Trailing failed-run count after the run.</param>
/// <param name="ErrorMessage">Failure reason.</param>
public sealed record FxRateSyncRunDto(
    string ProviderName,
    string PrimaryProviderName,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    FxRateSyncRunStatus Status,
    int RatesApplied,
    int RatesChanged,
    DateTime? EffectiveDateUtc,
    int ConsecutiveFailureCount,
    string? ErrorMessage);

/// <summary>
///     FX sync status (issue #412 spec B): recent runs, staleness assessment of the newest
///     persisted snapshot and the currently active grid/override counts — the admin surface
///     for the sync job and the staleness auditing requirement.
/// </summary>
[AuthorizeRequest("", RequireSystemAdmin = true)]
public sealed record GetFxSyncStatusQuery(int RunLimit = 10) : IQuery<FxSyncStatusDto>;

/// <summary>Result of <see cref="GetFxSyncStatusQuery"/>.</summary>
/// <param name="ConfiguredProvider">Configured primary provider.</param>
/// <param name="ConfiguredFallback">Configured fallback provider.</param>
/// <param name="WorkerEnabled">Whether the scheduled worker runs.</param>
/// <param name="NewestSnapshotEffectiveDateUtc">Newest persisted snapshot effective date (null when none).</param>
/// <param name="SnapshotCount">Total persisted quotations.</param>
/// <param name="ActiveOverrideCount">Non-expired overrides.</param>
/// <param name="IsStale">Newest snapshot older than the configured staleness threshold.</param>
/// <param name="StalenessThresholdDays">Configured threshold.</param>
/// <param name="ConsecutiveFailures">Trailing failed-run count.</param>
/// <param name="RecentRuns">Most recent runs, newest first.</param>
public sealed record FxSyncStatusDto(
    string ConfiguredProvider,
    string ConfiguredFallback,
    bool WorkerEnabled,
    DateTime? NewestSnapshotEffectiveDateUtc,
    int SnapshotCount,
    int ActiveOverrideCount,
    bool IsStale,
    int StalenessThresholdDays,
    int ConsecutiveFailures,
    IReadOnlyList<FxRateSyncRunDto> RecentRuns);

/// <summary>Handler for <see cref="GetFxSyncStatusQuery"/>.</summary>
public sealed class GetFxSyncStatusQueryHandler(
    IApplicationDbContext context,
    Microsoft.Extensions.Options.IOptions<FxSyncOptions> options,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetFxSyncStatusQuery, FxSyncStatusDto>
{
    /// <inheritdoc />
    public async Task<FxSyncStatusDto> Handle(GetFxSyncStatusQuery request, CancellationToken cancellationToken)
    {
        FxAdministrationActor.RequireSystemAdmin(actorContextAccessor);
        var settings = options.Value;
        var snapshots = context.Set<FxRateSnapshot>().AsNoTracking();
        var now = SystemClock.UtcNow;

        var newest = await snapshots.Where(snapshot => !snapshot.IsOverride)
            .MaxAsync(snapshot => (DateTime?)snapshot.EffectiveDateUtc, cancellationToken).ConfigureAwait(false);
        var snapshotCount = await snapshots.CountAsync(snapshot => !snapshot.IsOverride, cancellationToken).ConfigureAwait(false);
        var overrideCount = await snapshots.CountAsync(
            snapshot => snapshot.IsOverride && (snapshot.ExpiresAtUtc == null || snapshot.ExpiresAtUtc > now),
            cancellationToken).ConfigureAwait(false);

        var runLimit = Math.Clamp(request.RunLimit, 1, 50);
        var runs = await context.Set<FxRateSyncRun>()
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(runLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var consecutiveFailures = 0;
        foreach (var run in runs)
        {
            if (run.Status == FxRateSyncRunStatus.Failed)
            {
                consecutiveFailures++;
            }
            else
            {
                break;
            }
        }

        return new FxSyncStatusDto(
            settings.Provider,
            settings.FallbackProvider,
            settings.WorkerEnabled,
            newest,
            snapshotCount,
            overrideCount,
            IsStale: newest.HasValue && (now.Date - newest.Value.Date).TotalDays >= settings.StalenessAlertDays,
            settings.StalenessAlertDays,
            consecutiveFailures,
            runs.Select(run => new FxRateSyncRunDto(
                run.ProviderName,
                run.PrimaryProviderName,
                run.StartedAtUtc,
                run.CompletedAtUtc,
                run.Status,
                run.RatesApplied,
                run.RatesChanged,
                run.EffectiveDateUtc,
                run.ConsecutiveFailureCount,
                run.ErrorMessage)).ToList());
    }
}
