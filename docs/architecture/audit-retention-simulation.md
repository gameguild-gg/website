# Audit retention simulation

Issue: [#194](https://github.com/gameguild-gg/gameguild/issues/194).

## API and authorization

The canonical versioned route is `/v1/audit/retention-simulation`. The originally requested `/api/audit/retention-simulation` route is also supported.

| Method | Suffix | Result |
| --- | --- | --- |
| GET | `/configuration` | Current tenant's configuration and revision; 404 if absent |
| PUT | `/configuration` | Validate and save configuration with `expectedRevision`; 409 on stale/concurrent writes |
| POST | none | Capture current evidence, evaluate scenarios, and save the immutable run; 404 if no configuration exists |
| GET | none | List tenant's run summaries with `skip >= 0`, `1 <= take <= 100` |
| GET | `/{id}` | Saved configuration, request, evidence and report; 404 for another tenant's ID |

All operations require an authenticated tenant administrator and a nonempty actor tenant and user ID. These identities come from `IActorContextAccessor`. System administrators still select a tenant through their actor context. Neither request accepts a tenant or acting user ID. Mutations pass through guarded CQRS commands and the durable operation-event pipeline; configuration and run creation also write central audit events. The existing API rate-limit policy applies.

The simulation does not delete logs, change enforced retention, move objects between storage services, or incur provider charges. There is no public update/delete operation for a saved run. This API-level immutability is distinct from the separate cryptographically signed audit chain.

## Configure

First retrieve the current revision (use `0` when no configuration exists), then submit:

```json
{
  "expectedRevision": 0,
  "currency": "BRL",
  "storageOverheadMultiplier": 1.5,
  "monthlyBudget": 500,
  "maximumReadLatencyMilliseconds": 1000,
  "baseline": { "name": "current", "retentionDays": 365, "hotDays": 30, "warmUntilDays": 90, "coldUntilDays": 180 },
  "tierPrices": [
    { "tier": "Hot", "monthlyCostPerGiB": 10, "retrievalCostPerGiB": 0, "expectedReadLatencyMilliseconds": 1 },
    { "tier": "Warm", "monthlyCostPerGiB": 5, "retrievalCostPerGiB": 0.1, "expectedReadLatencyMilliseconds": 10 },
    { "tier": "Cold", "monthlyCostPerGiB": 2, "retrievalCostPerGiB": 0.5, "expectedReadLatencyMilliseconds": 100 },
    { "tier": "Archive", "monthlyCostPerGiB": 1, "retrievalCostPerGiB": 1, "expectedReadLatencyMilliseconds": 10000 }
  ],
  "obligations": [
    { "name": "Internal approved policy", "source": "Tenant policy document and revision", "minimumRetentionDays": 90, "maximumRetentionDays": 730 }
  ],
  "preserveAllRecordsThroughUtcDate": null
}
```

Prices and policy numbers above are examples, not market quotes or regulatory requirements. Configure the applicable prices, obligations and their source documents. An inclusive UTC hold date preserves all existing records through that date; it does not extend future cohorts. A hold that conflicts with a configured maximum retention age is reported as a violation.

Configuration validation requires exactly one price for each tier, ordered scenario boundaries `0 <= hotDays <= warmUntilDays <= coldUntilDays <= retentionDays`, retention of 1–36,500 days, and valid numeric bounds. Sources and names are bounded. Currency is one three-letter uppercase code; no foreign-exchange conversion is performed.

## Run

```json
{
  "historicalDays": 90,
  "forecastMonths": 36,
  "growthModel": "CompoundAnnual",
  "annualGrowthPercent": 20,
  "scenarios": [
    { "name": "180-day proposal", "retentionDays": 180, "hotDays": 7, "warmUntilDays": 30, "coldUntilDays": 90 },
    { "name": "365-day proposal", "retentionDays": 365, "hotDays": 7, "warmUntilDays": 60, "coldUntilDays": 180 }
  ]
}
```

Use 14–365 historical days, 1–120 forecast months and 1–10 uniquely named scenarios. `Constant` and `HistoricalTrend` omit `annualGrowthPercent`. `CompoundAnnual` requires a rate from -95% to 300%.

## Measurement and model (`daily-cohorts-v1`)

The host adapter captures a repeatable-read PostgreSQL snapshot on an independent connection. Tenant-parameterized SQL aggregates actual record counts and `pg_column_size(row)` by UTC date across `AuditLogs.CreatedAt` and `TamperEvidentAuditLogs.Timestamp`. Soft-deleted rows still occupy storage and are included. Audit payloads are never returned to the forecaster. This logical-row measurement is not the table's physical disk size; indexes, replicas and compression are represented only by the explicit overhead multiplier.

Actual returned rows are observed in audit list/date searches, streams/CSV/JSON exports, action-type searches/exports, unified general-audit searches and signed-audit reads. Observations copy only the tenant, record date, read count and observation timestamp. A partially consumed stream records only rows already yielded. Reads outside the actor's tenant are excluded. Internal/background accesses without an authenticated tenant context are not user-access telemetry. A telemetry storage failure is logged and does not break an authorized read; absent observations are reported as unknown. Observations begin after this feature is deployed; no historical reads are invented.

Daily generation samples cover the complete configured window ending before the partial capture day; missing dates are zero, with limited-history warnings. The evidence includes measured cohorts, averages, linear slopes, weekday factors and actual accessed-age buckets. Future periods begin at the next UTC midnight. Models are:

- `Constant`: complete-window average daily records and logical bytes.
- `HistoricalTrend`: nonnegative linear projections multiplied by measured, normalized weekday factors.
- `CompoundAnnual`: average multiplied by `(1 + rate/100)^(elapsedDays/365)`.

Each cohort moves through hot, warm, cold and archive, then expires according to the scenario. Date-only transitions round up so records receive at least the configured number of full days. This can overestimate storage by up to one day per boundary. Existing holds extend occupancy. Closing occupancy is the balance on the last modeled UTC day of the month; the exclusive end date starts the next period.

Each complete rolling forecast month sums daily billable occupancy in GiB (`2^30` bytes), multiplied by the tier's monthly price divided by the number of days in that period. Retrieval cost uses observed read-age frequency, measured average row bytes, projected generation growth and configured retrieval prices. Access-age distribution stays constant. Expected/p95 latency uses configured tier latency and actual accessed ages, with unavailable reads and latency violations reported separately. Missing access telemetry makes those assessments unknown. Year totals sum the actual month totals, including a partial final forecast year. Costs use decimal arithmetic and round to eight decimal places. Taxes, exchange rates, provider minimum fees and other unconfigured costs are excluded.

Evidence is bounded to 40,000 daily/age buckets and aggregate bytes/read/record counts of `10^18`; unreasonable data fails explicitly rather than truncating a forecast. Cancellation is observed during capture and cohort evaluation.

## Comparison, optimization and risk

Every requested scenario returns monthly and yearly tier costs, baseline differences and savings, budget variance, configured obligation violations and policy-change risks. Empty obligations yield `NotAssessed`; the API never assumes a fixed legal minimum or certifies legal compliance.

The optimizer compares the baseline, requested scenarios and bounded generated candidates. Candidates use configured retention bounds and observed access ages, and vary the tier preserving observed access while archiving older cohorts. It recommends the lowest modeled cost among candidates satisfying all configured obligations and every observed availability/latency constraint. This is a comparison of evaluated candidates, not a guarantee of a global optimum. Automatic recommendations require obligations, access observations and at least 14 days of measured history. If these are missing or requirements conflict, the report explains why no suggestion is available.

Risks include initial record expiration, unavailable observed age cohorts, excessive configured latency, hold/maximum-retention conflicts, budget overruns, unspecified obligations/budget, missing telemetry, limited history and long-horizon uncertainty. Administrators review these results before separately approving any real policy change.

The legacy manually populated `RetentionPolicySimulation` model remains compatible with existing callers; it is not the historical-data or compliance evidence path for this API.

## Traceability and validation

| #194 criterion | Implementation and focused proof |
| --- | --- |
| What-if scenarios | Validated typed scenarios; engine baseline/scenario comparisons; HTTP run and saved retrieval |
| Historical patterns/trends | PostgreSQL measured cohorts, complete-window slopes and weekday factors; deterministic trend/growth tests |
| Compliance impact | Administrator-sourced minimum/maximum obligations and hold conflicts; configured violation tests |
| Access-based recommendations | Actual read-age observations, partial-stream test, latency/availability constraints |
| Multi-year configurable growth | Three growth models, 1–120 months; monthly/yearly total tests |
| Four storage tiers | Occupancy transitions and storage/retrieval prices; four-tier arithmetic tests |
| Financial comparisons | Baseline differences, savings, monthly/yearly totals; deterministic comparisons |
| Budget impact | Optional monthly budget, variance and overrun risk; budget tests |
| Compliance-aware optimizer | Candidate constraints and explicit unknown/conflict outcomes; optimizer tests |
| Policy-change risk | Expiration, holds, access, latency, history and horizon risks; focused risk tests |

Migration `20261003203938_AuditRetentionSimulation` adds configuration, run and access-observation tables with tenant indexes and a unique per-tenant configuration. It changes no existing audit table. Tests additionally cover anonymous/user denial, actor-derived identity, cross-tenant 404/list isolation, optimistic revision conflicts, persisted snapshots after configuration changes, unchanged stored logs and OpenAPI contracts. The generated API client must be refreshed from the captured current API document whenever these contracts change.
