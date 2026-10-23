# Dynamic & Tiered Pricing Engine (issue #395)

Status of the pricing-engine surfaces on `develop`, and the scope of each delivery. The
rule core (tiers, volume discounts, customer segments, time windows, caching, rule CRUD
and calculation endpoints) landed in PR #779; the unified Payments-side calculus
(`PricingEngine`, one path for base/add-ons/usage/promos/proration/tax/credits) landed
with #824/#817; the pricing-decision ledger and its effectiveness dashboard landed with
#809/#928. The remaining surfaces — usage-metric adjustments, A/B experiments and
automated scheduling — are the subject of the current PR.

## 1. Tiered and volume pricing (PR #779)

- Entities: `PricingRule` + `PricingRuleTier` (`apps/api/Source/Modules/GameGuild.Commerce/Entities/`)
  with progressive per-quantity bands (fixed per-unit price or discount percentage per tier).
- Rule resolution: `PricingEngineService.ResolvePricingRuleAsync` picks the highest-priority
  applicable rule (date window → quantity window → customer segment → daily time window) and
  the discount is capped so a rule can never raise a price.
- Endpoints: `GET/POST/PUT/DELETE api/v{version}/billing/pricing-engine` (+ `:activate`,
  `:deactivate`, `:calculate`), guarded by `products:read` / `products:pricing:manage` +
  `monetization:monetize`.

## 2. Usage-metric dynamic adjustments (current PR)

- `PricingRule` grows a usage gate: `UsageMetricKey` + inclusive `UsageMinValue` /
  `UsageMaxValue` band. A gated rule (type `UsageBased`, or any type with a gate set) only
  applies to calculations that report a value for that metric inside the band; an unreported
  metric fails the gate closed (the rule steps aside for the next applicable rule).
- The calculation request carries `usageMetrics: { "api_calls": 5000, ... }`
  (`POST .../pricing-engine/:calculate`), flowing through `CalculatePricingCommand` into
  `IPricingEngineService.CalculatePriceAsync`.

## 3. A/B pricing experiments (current PR)

- Entities: `PricingExperiment` (status Draft/Running/Paused/Completed, traffic percentage,
  optional date bounds), `PricingExperimentVariant` (control flag, allocation weight,
  discount percentage or fixed per-unit price), `PricingExperimentAssignment` (sticky,
  unique per experiment + bucket key) and `PricingExperimentOutcome` (conversion amounts).
- Allocation is deterministic: SHA-256(experiment id + bucket key) → bucket in [0,1);
  keys above the traffic share are held out, enrolled keys map onto normalized allocation
  ranges (control first). No coordination is needed for stickiness, and replays are
  idempotent.
- Enrollment happens inside the calculation: the calculate command defaults the bucket key
  to the acting user id (or accepts an explicit `experimentBucketKey`), so every signed-in
  price quote is consistently assigned. The arm adjusts the unit price after the pricing
  rule and before promo codes; the control arm leaves the price untouched. The result
  reports `AppliedExperimentId`, `AppliedVariantName` and `ExperimentDiscount`.
- Endpoints under `api/v{version}/billing/pricing-engine/experiments`: list/get/create,
  `:start`/`:pause`/`:complete`, `:assign` (resolve a key's arm), `:convert` (record a
  monetized outcome) and `:analytics` — per-variant assignments, conversions, conversion
  rate, revenue, average order value, revenue-per-assignment and lift vs the control arm.
- Conversion recording is an explicit `monetization:monetize` action used by
  checkout/subscription-start flows.

## 4. Automated rule management and scheduling (current PR)

- `PricingRule.IsScheduled` opts a rule into automated scheduling: the scheduler keeps
  `IsActive` aligned with the rule's `[StartDate, EndDate)` window (active inside,
  inactive outside). Administrator-managed rules are never touched. Every transition runs
  through the repository, bumping the rule version — which changes the engine's cache
  stamp, so the new state is picked up immediately.
- Date-bounded experiments transition automatically: Draft → Running at `StartDate`,
  Running → Completed at `EndDate`.
- `PricingSchedulingBackgroundService` runs the sweep every 5 minutes
  (`PricingScheduling:WorkerEnabled`, `PricingScheduling:WorkerIntervalMinutes`);
  `POST api/v{version}/billing/pricing-engine/scheduling:sweep` runs one pass on demand.

## 5. Caching and analytics (landed + current PR)

- Rule sets are cached per product with a change-stamp key (rule count + max version + max
  updated ticks), invalidated on any rule mutation; plan prices in the unified engine are
  cached per plan/interval/currency.
- Revenue-impact analytics: the pricing-decision ledger (#809) plus the effectiveness
  dashboard (#928) cover discounts and rule impact ledger-wide; experiment-level
  revenue impact is served by the `:analytics` endpoint above.
