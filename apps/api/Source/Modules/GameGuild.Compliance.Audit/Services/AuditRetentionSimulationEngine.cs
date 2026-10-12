namespace GameGuild.Compliance.Audit;

/// <summary>Deterministic daily cohort model. No scenario changes or deletes stored audit data.</summary>
public sealed class AuditRetentionSimulationEngine
{
    private const decimal BytesPerGiB = 1073741824m;
    public static string ModelVersion { get; } = "daily-cohorts-v1";

    public AuditRetentionSimulationReport Simulate(
        ConfigureAuditRetentionRequest configuration, RunAuditRetentionSimulationRequest request,
        AuditRetentionDataSnapshot snapshot, DateTime asOfUtc) =>
        Simulate(configuration, request, snapshot, asOfUtc, CancellationToken.None);

    public AuditRetentionSimulationReport Simulate(
        ConfigureAuditRetentionRequest configuration,
        RunAuditRetentionSimulationRequest request,
        AuditRetentionDataSnapshot snapshot,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        try { return SimulateCore(configuration, request, snapshot, asOfUtc, cancellationToken); }
        catch (OverflowException)
        {
            throw new AuditRetentionValidationException(new Dictionary<string, string[]>
            {
                ["ModelRange"] = ["The measured data and configured forecast exceed the decimal calculation range. Reduce the horizon, growth or prices."]
            });
        }
    }

    private static AuditRetentionSimulationReport SimulateCore(
        ConfigureAuditRetentionRequest configuration, RunAuditRetentionSimulationRequest request,
        AuditRetentionDataSnapshot snapshot, DateTime asOfUtc, CancellationToken cancellationToken)
    {
        AuditRetentionInputValidation.Validate(configuration);
        AuditRetentionInputValidation.Validate(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (asOfUtc.Kind != DateTimeKind.Utc || asOfUtc.Year is < 2 or > 9800)
        {
            throw new ArgumentException("The evidence timestamp must be UTC in years 2 through 9800.", nameof(asOfUtc));
        }
        var today = DateOnly.FromDateTime(asOfUtc);
        if (snapshot.Cohorts.Count > 40000 || snapshot.AccessAges.Count > 40000 ||
            snapshot.Cohorts.Any(cohort => cohort.DateUtc > today || cohort.RecordCount < 0 ||
                cohort.LogicalBytes is < 0 or > 1000000000000000000m) ||
            snapshot.AccessAges.Any(bucket => bucket.AgeDays is < 0 or > 3650000 || bucket.ReadCount < 0) ||
            snapshot.FirstAccessObservationDateUtc > today ||
            snapshot.Cohorts.Sum(cohort => cohort.LogicalBytes) > 1000000000000000000m ||
            snapshot.Cohorts.Sum(cohort => (decimal)cohort.RecordCount) > 1000000000000000000m ||
            snapshot.AccessAges.Sum(bucket => (decimal)bucket.ReadCount) > 1000000000000000000m)
        {
            throw new ArgumentException("Historical evidence is invalid or exceeds the supported cohort limit.", nameof(snapshot));
        }
        var cohorts = snapshot.Cohorts.GroupBy(cohort => cohort.DateUtc)
            .Select(group => new Cohort(group.Key, group.Sum(cohort => (decimal)cohort.RecordCount), group.Sum(cohort => cohort.LogicalBytes)))
            .OrderBy(cohort => cohort.DateUtc).ToList();
        var start = today.AddDays(1);
        var end = start.AddMonths(request.ForecastMonths);
        var evidence = BuildEvidence(snapshot, cohorts, asOfUtc, request.HistoricalDays);
        for (var date = start; date < end; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offset = date.DayNumber - today.DayNumber;
            var factor = request.GrowthModel == AuditRetentionGrowthModel.CompoundAnnual
                ? (decimal)Math.Pow(1d + (double)request.AnnualGrowthPercent!.Value / 100d, offset / 365d)
                : 1m;
            var trendOffset = request.HistoricalDays + offset - (request.HistoricalDays - 1m) / 2m;
            var weekday = evidence.WeeklySeasonality[(int)date.DayOfWeek];
            var bytes = request.GrowthModel == AuditRetentionGrowthModel.HistoricalTrend
                ? Math.Max(0, evidence.AverageDailyLogicalBytes + evidence.DailyLogicalBytesTrend * trendOffset) * weekday.BytesMultiplier
                : evidence.AverageDailyLogicalBytes * factor;
            var records = request.GrowthModel == AuditRetentionGrowthModel.HistoricalTrend
                ? Math.Max(0, evidence.AverageDailyRecords + evidence.DailyRecordsTrend * trendOffset) * weekday.RecordsMultiplier
                : evidence.AverageDailyRecords * factor;
            cohorts.Add(new Cohort(date, records, bytes));
        }

        var baseline = Evaluate(configuration.Baseline, configuration, request, evidence, cohorts, start, end, cancellationToken);
        var scenarios = request.Scenarios.Select(scenario => Compare(
            Evaluate(scenario, configuration, request, evidence, cohorts, start, end, cancellationToken), baseline)).ToArray();
        var candidates = new List<AuditRetentionScenarioResult> { baseline };
        candidates.AddRange(scenarios);
        foreach (var candidate in GenerateCandidates(configuration, evidence))
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.Add(Compare(Evaluate(candidate, configuration, request, evidence, cohorts, start, end, cancellationToken), baseline));
        }
        var canRecommend = configuration.Obligations.Count > 0 && evidence.ObservedReadCount > 0 && evidence.AvailableHistoryDays >= 14;
        var best = canRecommend
            ? candidates.Where(candidate => candidate.ComplianceStatus == "MeetsConfiguredObligations" &&
                    candidate.UnavailableObservedReadsPercent == 0 && candidate.SlowObservedReadsPercent == 0)
                .OrderBy(candidate => candidate.TotalCost).ThenByDescending(candidate => candidate.Scenario.RetentionDays).FirstOrDefault()
            : null;
        var recommendation = new AuditRetentionRecommendation(best, best is null ? null : Money(baseline.TotalCost - best.TotalCost),
            best is not null
                ? "Lowest modeled cost among requested and generated candidates that satisfy configured obligations and all observed access/latency constraints. Review the assumptions before changing a policy."
                : canRecommend
                    ? "No evaluated candidate satisfies all configured obligations and observed access/latency constraints. Resolve conflicting requirements or supply another scenario."
                    : "Automatic optimization requires configured obligations, observed record-access telemetry and at least 14 days of history. Forecasts remain available; no unverified retention reduction is recommended.",
            candidates.Count);

        return new AuditRetentionSimulationReport(ModelVersion, configuration.Currency, request.ForecastMonths,
            request.GrowthModel, request.AnnualGrowthPercent, configuration.StorageOverheadMultiplier, evidence,
            ["The measurement is logical PostgreSQL row bytes for primary AuditLogs and TamperEvidentAuditLogs; indexes, replicas and physical compression are represented only by the configured overhead multiplier.",
             "Forecast periods start at the next UTC midnight. Writes after the evidence capture, including the remaining partial current day, are not present in the measured baseline.",
             "Retention and tier transitions are rounded up to UTC cohort boundaries; a record receives at least the configured number of full days. Storage can be overestimated by up to one day per boundary.",
             "Constant uses the complete historical-window daily average; HistoricalTrend uses linear trends and measured weekday seasonality; CompoundAnnual uses the configured annual rate.",
             "Access-age distribution is held constant; retrieval volume scales with projected audit generation. Latency and prices are administrator-supplied assumptions, not provider guarantees.",
             "Compliance status evaluates the configured obligations and sources only. It is not legal certification. A hold preserves all existing records through its configured inclusive UTC date.",
             "Costs use one configured currency and GiB (2^30 bytes), daily occupancy within complete forecast months, and decimal arithmetic. Taxes, exchange rates, minimum charges and unconfigured provider fees are excluded."],
            baseline, scenarios, recommendation);
    }

    private static AuditRetentionHistoricalEvidence BuildEvidence(
        AuditRetentionDataSnapshot snapshot, List<Cohort> cohorts, DateTime asOfUtc, int historicalDays)
    {
        var today = DateOnly.FromDateTime(asOfUtc);
        var windowStart = today.AddDays(-historicalDays);
        var byDay = cohorts.ToDictionary(cohort => cohort.DateUtc);
        var bytes = new decimal[historicalDays];
        var records = new decimal[historicalDays];
        var weekdayBytes = new decimal[7];
        var weekdayRecords = new decimal[7];
        var weekdayDays = new int[7];
        for (var index = 0; index < historicalDays; index++)
        {
            var date = windowStart.AddDays(index);
            if (byDay.TryGetValue(date, out var cohort)) { bytes[index] = cohort.Bytes; records[index] = cohort.Records; }
            var weekday = (int)date.DayOfWeek;
            weekdayBytes[weekday] += bytes[index];
            weekdayRecords[weekday] += records[index];
            weekdayDays[weekday]++;
        }
        for (var weekday = 0; weekday < 7; weekday++)
        {
            weekdayBytes[weekday] /= weekdayDays[weekday];
            weekdayRecords[weekday] /= weekdayDays[weekday];
        }
        var meanWeekdayBytes = weekdayBytes.Average();
        var meanWeekdayRecords = weekdayRecords.Average();
        var seasonality = Enumerable.Range(0, 7).Select(weekday => new AuditRetentionWeekdayFactor(weekday,
            meanWeekdayBytes == 0 ? 1 : weekdayBytes[weekday] / meanWeekdayBytes,
            meanWeekdayRecords == 0 ? 1 : weekdayRecords[weekday] / meanWeekdayRecords)).ToArray();
        var oldest = cohorts.Count == 0 ? today : cohorts[0].DateUtc;
        var accessDays = snapshot.FirstAccessObservationDateUtc.HasValue
            ? Math.Clamp(today.DayNumber - snapshot.FirstAccessObservationDateUtc.Value.DayNumber + 1, 1, historicalDays)
            : 0;
        var access = snapshot.AccessAges.Where(bucket => bucket.ReadCount > 0).OrderBy(bucket => bucket.AgeDays).ToArray();
        return new AuditRetentionHistoricalEvidence(asOfUtc, snapshot.MeasurementMethod,
            cohorts.Sum(cohort => cohort.Bytes), cohorts.Sum(cohort => cohort.Records), historicalDays,
            Math.Clamp(today.DayNumber - oldest.DayNumber, 0, historicalDays), records.Count(count => count > 0),
            bytes.Average(), records.Average(), Slope(bytes), Slope(records), accessDays,
            access.Sum(bucket => (decimal)bucket.ReadCount), access.Length == 0 ? null : access[^1].AgeDays,
            snapshot.Cohorts, access, seasonality);
    }

    private static decimal Slope(decimal[] samples)
    {
        var meanX = (samples.Length - 1m) / 2;
        decimal numerator = 0, denominator = 0;
        for (var index = 0; index < samples.Length; index++)
        {
            var delta = index - meanX;
            numerator += delta * samples[index];
            denominator += delta * delta;
        }
        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static AuditRetentionScenarioResult Evaluate(
        AuditRetentionScenario scenario, ConfigureAuditRetentionRequest configuration,
        RunAuditRetentionSimulationRequest request, AuditRetentionHistoricalEvidence evidence,
        List<Cohort> cohorts, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var prices = configuration.TierPrices.OrderBy(price => price.Tier).ToArray();
        var events = new Dictionary<int, Delta[]>();
        decimal expiredRecords = 0, expiredBytes = 0;
        var heldUntilExclusive = configuration.PreserveAllRecordsThroughUtcDate?.AddDays(1);
        foreach (var cohort in cohorts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expiration = cohort.DateUtc.AddDays(scenario.RetentionDays + 1);
            if (heldUntilExclusive.HasValue && cohort.DateUtc < start && heldUntilExclusive.Value > expiration)
            {
                expiration = heldUntilExclusive.Value;
            }
            if (cohort.DateUtc < start && expiration <= start) { expiredRecords += cohort.Records; expiredBytes += cohort.Bytes; }
            var boundaries = new[] { 0, Boundary(scenario.HotDays), Boundary(scenario.WarmUntilDays), Boundary(scenario.ColdUntilDays) };
            for (var tier = 0; tier < 4; tier++)
            {
                var from = cohort.DateUtc.AddDays(boundaries[tier]);
                var until = tier == 3 ? expiration : Min(expiration, cohort.DateUtc.AddDays(boundaries[tier + 1]));
                from = Max(from, start);
                until = Min(until, end);
                if (from >= until) { continue; }
                AddEvent(events, from.DayNumber - start.DayNumber, tier, cohort.Bytes * configuration.StorageOverheadMultiplier, cohort.Records);
                AddEvent(events, until.DayNumber - start.DayNumber, tier, -cohort.Bytes * configuration.StorageOverheadMultiplier, -cohort.Records);
            }
        }

        var violations = configuration.Obligations.SelectMany(rule =>
        {
            var reasons = new List<AuditRetentionComplianceViolation>();
            if (scenario.RetentionDays < rule.MinimumRetentionDays)
            {
                reasons.Add(new(rule.Name, rule.Source, $"Retention {scenario.RetentionDays} days is below the configured minimum of {rule.MinimumRetentionDays}."));
            }
            if (scenario.RetentionDays > rule.MaximumRetentionDays)
            {
                reasons.Add(new(rule.Name, rule.Source, $"Retention {scenario.RetentionDays} days exceeds the configured maximum of {rule.MaximumRetentionDays}."));
            }
            if (rule.MaximumRetentionDays.HasValue && heldUntilExclusive > start &&
                cohorts.Any(cohort => cohort.DateUtc < start &&
                    heldUntilExclusive!.Value.DayNumber - cohort.DateUtc.DayNumber - 1 > rule.MaximumRetentionDays.Value))
            {
                reasons.Add(new(rule.Name, rule.Source, "The configured hold preserves existing records beyond this obligation's maximum retention age."));
            }
            return reasons;
        }).ToArray();
        var profile = AssessAccess(scenario, configuration, evidence, prices);
        var active = new Delta[4];
        var months = new List<AuditRetentionMonthForecast>();
        var averageRowBytes = evidence.AverageDailyRecords == 0 ? 0 : evidence.AverageDailyLogicalBytes / evidence.AverageDailyRecords;
        var projected = cohorts.Where(cohort => cohort.DateUtc >= start).ToDictionary(cohort => cohort.DateUtc);
        for (var month = 0; month < request.ForecastMonths; month++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var from = start.AddMonths(month);
            var until = start.AddMonths(month + 1);
            var days = until.DayNumber - from.DayNumber;
            var storageCosts = new decimal[4];
            var retrievalCosts = new decimal[4];
            for (var date = from; date < until; date = date.AddDays(1))
            {
                ApplyEvent(events, date.DayNumber - start.DayNumber, active);
                var generationFactor = evidence.AverageDailyRecords == 0 ? 0 : projected[date].Records / evidence.AverageDailyRecords;
                for (var tier = 0; tier < 4; tier++)
                {
                    storageCosts[tier] += active[tier].Bytes / BytesPerGiB * prices[tier].MonthlyCostPerGiB / days;
                    var reads = evidence.ObservedAccessDays == 0 ? 0 : profile.ReadsByTier[tier] / evidence.ObservedAccessDays * generationFactor;
                    retrievalCosts[tier] += reads * (averageRowBytes / BytesPerGiB) * prices[tier].RetrievalCostPerGiB;
                }
            }
            // Closing occupancy is the last modeled UTC day's balance; the exclusive boundary belongs to the next month.
            var closing = active.ToArray();
            var tiers = Enumerable.Range(0, 4).Select(tier => new AuditRetentionTierForecast((AuditStorageTier)tier,
                closing[tier].Bytes, closing[tier].Records, Money(storageCosts[tier]), Money(retrievalCosts[tier]))).ToArray();
            var storage = tiers.Sum(tier => tier.StorageCost);
            var retrieval = tiers.Sum(tier => tier.RetrievalCost);
            months.Add(new(month + 1, from, until, closing.Sum(tier => tier.Bytes), closing.Sum(tier => tier.Records),
                storage, retrieval, storage + retrieval, configuration.MonthlyBudget,
                configuration.MonthlyBudget.HasValue ? storage + retrieval - configuration.MonthlyBudget : null, tiers));
        }
        var years = months.GroupBy(month => (month.Month - 1) / 12 + 1).Select(group =>
        {
            var total = group.Sum(month => month.TotalCost);
            decimal? budget = configuration.MonthlyBudget * group.Count();
            return new AuditRetentionYearForecast(group.Key, group.Count(), group.Sum(month => month.StorageCost),
                group.Sum(month => month.RetrievalCost), total, budget, total - budget);
        }).ToArray();
        var totalCost = months.Sum(month => month.TotalCost);
        var risks = new List<AuditRetentionRisk>();
        if (violations.Length > 0) { risks.Add(new("CONFIGURED_OBLIGATION_VIOLATION", "High", "The scenario fails one or more configured regulatory or policy obligations.")); }
        if (configuration.Obligations.Count == 0) { risks.Add(new("OBLIGATIONS_UNSPECIFIED", "High", "No obligations are configured; regulatory impact is not assessed.")); }
        if (expiredRecords > 0) { risks.Add(new("INITIAL_RECORD_EXPIRATION", "Medium", $"Applying this scenario would initially expire {expiredRecords} measured records. This simulation deletes nothing.")); }
        if (profile.UnavailablePercent > 0) { risks.Add(new("OBSERVED_DATA_UNAVAILABLE", "High", "The retention period would make some previously accessed age cohorts unavailable after holds expire.")); }
        if (profile.SlowPercent > 0) { risks.Add(new("OBSERVED_LATENCY_EXCEEDED", "High", "Some observed access ages would move to a tier exceeding the configured latency limit.")); }
        if (evidence.ObservedReadCount == 0) { risks.Add(new("ACCESS_TELEMETRY_UNAVAILABLE", "Medium", "No record-read observations are available; retrieval cost and latency cannot be established.")); }
        if (evidence.AvailableHistoryDays < request.HistoricalDays) { risks.Add(new("LIMITED_HISTORY", "Medium", "The requested window predates the oldest measured record; missing days are modeled as zero.")); }
        if (request.ForecastMonths > 12) { risks.Add(new("LONG_HORIZON_UNCERTAINTY", "Medium", "Long-range trends, access distributions and prices may change; compare growth scenarios and refresh the model.")); }
        if (heldUntilExclusive.HasValue && heldUntilExclusive.Value > start) { risks.Add(new("HOLD_OVERRIDES_EXPIRATION", "Medium", "The configured hold extends occupancy for all existing records; review privacy and legal obligations before releasing it.")); }
        if (!configuration.MonthlyBudget.HasValue) { risks.Add(new("BUDGET_UNSPECIFIED", "Low", "No budget is configured; variance is unavailable.")); }
        else if (totalCost > configuration.MonthlyBudget * request.ForecastMonths) { risks.Add(new("PROJECTED_BUDGET_OVERRUN", "Medium", "Modeled total cost exceeds the configured budget over the forecast horizon.")); }
        return new(scenario, configuration.Obligations.Count == 0 ? "NotAssessed" : violations.Length == 0 ? "MeetsConfiguredObligations" : "ViolatesConfiguredObligations",
            violations, expiredRecords, expiredBytes, profile.ExpectedLatency, profile.P95Latency, profile.UnavailablePercent, profile.SlowPercent,
            months.Sum(month => month.StorageCost), months.Sum(month => month.RetrievalCost), totalCost,
            totalCost - configuration.MonthlyBudget * request.ForecastMonths, 0, null, risks, months, years);
    }

    private static AccessProfile AssessAccess(AuditRetentionScenario scenario, ConfigureAuditRetentionRequest configuration,
        AuditRetentionHistoricalEvidence evidence, AuditStorageTierPrice[] prices)
    {
        var reads = new decimal[4];
        decimal unavailable = 0, slow = 0, latency = 0;
        var latencies = new List<(decimal Latency, decimal Reads)>();
        foreach (var bucket in evidence.AccessAges)
        {
            if (bucket.AgeDays > scenario.RetentionDays) { unavailable += bucket.ReadCount; continue; }
            var tier = TierAtAge(scenario, bucket.AgeDays);
            reads[tier] += bucket.ReadCount;
            var tierLatency = prices[tier].ExpectedReadLatencyMilliseconds;
            latency += tierLatency * bucket.ReadCount;
            if (tierLatency > configuration.MaximumReadLatencyMilliseconds) { slow += bucket.ReadCount; }
            latencies.Add((tierLatency, bucket.ReadCount));
        }
        var available = reads.Sum();
        decimal? p95 = null;
        decimal cumulative = 0;
        foreach (var point in latencies.OrderBy(point => point.Latency))
        {
            cumulative += point.Reads;
            if (cumulative >= available * 0.95m) { p95 = point.Latency; break; }
        }
        return new(reads, available == 0 ? null : Money(latency / available), p95,
            evidence.ObservedReadCount == 0 ? null : Money(unavailable / evidence.ObservedReadCount * 100),
            evidence.ObservedReadCount == 0 ? null : Money(slow / evidence.ObservedReadCount * 100));
    }

    private static IEnumerable<AuditRetentionScenario> GenerateCandidates(ConfigureAuditRetentionRequest configuration, AuditRetentionHistoricalEvidence evidence)
    {
        if (configuration.Obligations.Count == 0 || !evidence.OldestObservedAccessAgeDays.HasValue) { yield break; }
        var minimum = configuration.Obligations.Max(rule => rule.MinimumRetentionDays);
        var maximum = configuration.Obligations.Where(rule => rule.MaximumRetentionDays.HasValue)
            .Select(rule => rule.MaximumRetentionDays!.Value).DefaultIfEmpty(36500).Min();
        var accessDays = Math.Clamp(evidence.OldestObservedAccessAgeDays.Value + 1, 1, 36500);
        var retentions = new[] { configuration.Baseline.RetentionDays, minimum, Math.Max(minimum, accessDays), maximum }.Distinct();
        var number = 0;
        foreach (var retention in retentions.Where(value => value >= minimum && value <= maximum))
        {
            // Enumerate which tier retains observed accesses; unobserved older cohorts move to archive.
            for (var tier = 0; tier < 4; tier++)
            {
                var boundary = Math.Min(retention, accessDays);
                yield return new AuditRetentionScenario
                {
                    Name = $"optimized-{++number}", RetentionDays = retention,
                    HotDays = tier == 0 ? boundary : 0,
                    WarmUntilDays = tier <= 1 ? boundary : 0,
                    ColdUntilDays = tier <= 2 ? boundary : 0
                };
            }
            yield return new AuditRetentionScenario
            {
                Name = $"optimized-{++number}", RetentionDays = retention,
                HotDays = retention, WarmUntilDays = retention, ColdUntilDays = retention
            };
        }
    }

    private static AuditRetentionScenarioResult Compare(AuditRetentionScenarioResult result, AuditRetentionScenarioResult baseline) =>
        result with { DifferenceFromBaseline = Money(result.TotalCost - baseline.TotalCost),
            SavingsPercent = baseline.TotalCost == 0 ? null : Money((baseline.TotalCost - result.TotalCost) / baseline.TotalCost * 100) };

    private static int TierAtAge(AuditRetentionScenario scenario, int age) =>
        age < Boundary(scenario.HotDays) ? 0 : age < Boundary(scenario.WarmUntilDays) ? 1 : age < Boundary(scenario.ColdUntilDays) ? 2 : 3;
    private static int Boundary(int days) => days == 0 ? 0 : days + 1;
    private static decimal Money(decimal value) => decimal.Round(value, 8, MidpointRounding.ToEven);
    private static DateOnly Min(DateOnly first, DateOnly second) => first < second ? first : second;
    private static DateOnly Max(DateOnly first, DateOnly second) => first > second ? first : second;

    private static void AddEvent(Dictionary<int, Delta[]> events, int day, int tier, decimal bytes, decimal records)
    {
        if (!events.TryGetValue(day, out var changes)) { events[day] = changes = new Delta[4]; }
        changes[tier] = new(changes[tier].Bytes + bytes, changes[tier].Records + records);
    }

    private static void ApplyEvent(Dictionary<int, Delta[]> events, int day, Delta[] active)
    {
        if (!events.TryGetValue(day, out var changes)) { return; }
        for (var tier = 0; tier < 4; tier++) { active[tier] = new(active[tier].Bytes + changes[tier].Bytes, active[tier].Records + changes[tier].Records); }
    }

    private sealed record Cohort(DateOnly DateUtc, decimal Records, decimal Bytes);
    private readonly record struct Delta(decimal Bytes, decimal Records);
    private sealed record AccessProfile(decimal[] ReadsByTier, decimal? ExpectedLatency, decimal? P95Latency, decimal? UnavailablePercent, decimal? SlowPercent);
}
