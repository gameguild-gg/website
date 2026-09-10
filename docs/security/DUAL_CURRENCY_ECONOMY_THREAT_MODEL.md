# Dual-Currency Economy Threat Model

Date: 2026-07-16
Status: Proposed security baseline
Architecture: `docs/architecture/DUAL_CURRENCY_ECONOMY.md`
Source: `docs/papers/dual-currency-economy-whitepaper.md`

## Security Objective

The economy must prevent unauthorized value creation, loss, conversion, withdrawal, and concealment. When certainty is unavailable, the system must preserve auditability and choose the lower available or withdrawable amount.

No web implementation can guarantee zero ad fraud. The security objective is to make abuse expensive, detect it quickly, cap financial exposure, and stop issuance automatically before loss becomes unbounded.

## Protected Assets

- External custodial cash and receivables.
- Hard-coin claims at `100 HC/USD` and noncashable soft-credit claims at `100,000 SC/USD`.
- Purchased-versus-earned hard-coin provenance.
- Immutable source stamps, authoritative confirmation times, fixed 120-day earned-hard maturity, and payout eligibility.
- Escrowed value and refund provenance.
- Platform fee claims and reserve classifications.
- Stripe, ad-network, and KYC provider credentials and events.
- Journal integrity, chain head, idempotency records, and reconciliation evidence.
- User identity, KYC data, risk signals, and device-linkage data.
- Administrative permissions and adjustment history.

## Actors And Adversaries

| Actor                   | Security concern                                                                                                          |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Normal user             | Accidental duplicate requests, compromised session, incorrect balance assumptions                                         |
| Malicious user          | Arbitrary wallet access, replay, double spend, forged client price, refund abuse                                          |
| Colluding accounts      | Purchased-to-earned laundering, bounty self-dealing, ad farms, payout evasion                                             |
| Malicious seller        | Fake delivery, refund evasion, rapid payout, related-account purchases                                                    |
| Bot or farm operator    | Automated playback, account/device rotation, proxy use, timing manipulation                                               |
| Provider impersonator   | Forged or replayed Stripe/ad/KYC webhook                                                                                  |
| Account takeover actor  | Changes credentials, MFA, payout destinations, ownership, or email before payout                                          |
| Laundering ring         | Uses related accounts, projects, bounties, refunds, and marketplace sales to move source value into withdrawable proceeds |
| Malicious administrator | Unauthorized adjustment, hold release, payout override, evidence deletion                                                 |
| Database attacker       | Historical entry update/delete, chain-head rewrite, projection manipulation                                               |
| Compromised worker      | Duplicate event handling, stale policy use, out-of-order transitions                                                      |

## Trust Boundaries

1. Browser to GameGuild API.
2. Authenticated actor to an addressed wallet, order, bounty, or payout.
3. GameGuild API to PostgreSQL.
4. GameGuild to Stripe and Stripe Connect.
5. GameGuild to ad networks and report ingestion.
6. GameGuild to KYC/risk providers.
7. Platform administrator to privileged economy operations.
8. Mutable projections to the immutable journal source of truth.
9. Risk decision service to Economy Core posting authority.
10. Trust/Safety and FinancialCrime case state to protected financial operations.

## Existing P0 Findings

These pre-existing conditions must be fixed before any economy feature is enabled:

1. Legacy wallet mutation endpoints accept arbitrary user IDs without sufficient actor or administrator binding.
2. Payment listing and refund/cancel/retry operations require complete object-level and tenant authorization.
3. Stripe webhook ingress must verify the signature and timestamp before durable acceptance or processing.
4. Provider simulation must fail closed in staging and production.
5. Payment amount, currency, tenant, product, order, and subscription must be resolved from authoritative server state, not trusted from the request.
6. Legacy financial records are mutable and the runtime database role currently has broad update/delete capability.
7. Provider calls and internal state changes lack a complete inbox/outbox and recovery contract.
8. Protected financial operations need a single risk-decision contract; scattered local checks are not sufficient.
9. Payout, ownership, email, MFA, and bank/payout-destination changes need transaction-bound reauthentication and cooldown enforcement.
10. Related-account graph, aggregate exposure limits, and manual-review evidence must exist before marketplace settlement, bounty payout, ad issuance, or creator cash-out.

These are security remediation tasks, not optional economy enhancements.

## Threats And Controls

### Unauthorized Wallet Access

Threats:

- User reads or changes another user's wallet by replacing a route ID.
- Tenant administrator reaches a platform or unrelated-tenant wallet.
- Client calls internal credit/debit operations directly.

Controls:

- User wallet routes derive the owner from the authenticated actor.
- No public endpoint exposes generic credit, debit, or transfer commands.
- Administrative routes require platform-level named permissions and resource-scope checks.
- Every command records actor, tenant/context, correlation, reason, and idempotency key.
- The economy core repeats actor, tenant, object ownership, state, and operation-capability authorization for every public or internal command/query; leaf-module and controller checks are not sufficient.
- Authorization tests cover every actor/resource matrix, including missing tenant context.

### Double Spend And Concurrency

Threats:

- Two requests spend the same available fragment.
- Fixed-mix checkout commits one currency leg but not the other.
- Refund and payout reserve the same seller proceeds concurrently.
- A mutable aggregate projection is changed without preserving which source-lot fragments funded the movement.

Controls:

- One PostgreSQL transaction for posting, allocation, projection, idempotency, and outbox.
- Canonical row-lock ordering for wallet, account, lot, hold, and chain-head projections.
- Aggregate balances are rebuildable projections only; every debit allocates exact integer fragments from source-stamped lots and maps each output back to its parent fragments.
- Every fragment carries immutable root trace-unit intervals. Splits partition intervals deterministically; mixed-root merges retain separate ranges and cannot anonymize ancestry.
- Unique scoped idempotency constraints.
- Zero-sum validation plus database-enforced posting-template type/version, account shape, signs, provenance, conversion ratio, issuance cap, and authority before insert.
- Payout, refund, dispute, and spend serialize against the same lot projection.
- Every allocator locks/checks all represented root-reversal epochs in canonical order. A reversal freezes its root before descendant traversal, so a concurrent transfer cannot escape discovery.
- Refund/failure/reversal follows the allocation and descendant-lineage graph, restores or retires exact fragments, and records unrecoverable portions as debt/receivable/loss through new postings rather than assigning a balance.
- PostgreSQL integration tests use real concurrent connections, not only in-memory EF tests.

### Ledger Tampering

Threats:

- Application or DBA updates/deletes an entry.
- Attacker rewrites an entry and recomputes later hashes.
- Projection is manipulated to show more withdrawable value.

Controls:

- Append-only entities do not inherit mutable/soft-delete base classes.
- General runtime role cannot insert, update, or delete any integrity-bearing economy state, including journal/source/lineage records, projections, holds, chain head, reserve head, provider mappings, idempotency state, or outbox operation versions.
- A separate economy-writer role can only execute registered security-definer posting/transition interfaces that validate templates, authorization context, budgets, reserve headroom, and idempotency. Procedure owners cannot log in, execute ACLs are explicit, objects are schema-qualified, and each procedure pins a trusted `search_path`.
- Update/delete denial triggers provide defense in depth.
- Global chain head serializes entry order.
- Periodic verifier checks hashes, continuity, group balance, allocations, and projections.
- Chain heads are signed with independent KMS credentials and exported to immutable/WORM storage every 1,000 entries or five minutes, whichever occurs first.
- Payout and admin-withdrawal execution require a verified independent anchor that covers the eligibility snapshot's exact chain sequence; an on-demand anchor is created and verified when the periodic anchor trails it.
- The on-demand anchor also signs the canonical dispatch snapshot hash covering payee/provider mapping, amount, exact fragment/root ranges, provenance/lineage, holds, KYC, debt, reserve, chain, command, fencing, and kill-switch versions. Dispatch rejects any snapshot mismatch.
- Withdrawable enforcement uses the lower value when projection and recompute disagree.

Hash chaining detects alteration; it does not by itself prevent a privileged attacker from rewriting the whole chain. Restricted database roles, independent anchors, immutable logs, and separation of duties are therefore required.

### Replay And Idempotency

Threats:

- Client retries credit or transfer requests.
- Provider delivers one event multiple times.
- Ad completion token is replayed.
- A worker crashes after provider success but before local completion.

Controls:

- Stable operation-scoped idempotency keys with persisted results.
- Unique provider event ID plus provider account/environment scope.
- Single-use ad nonce consumed atomically with the reward posting.
- Durable inbox before asynchronous processing.
- Durable outbox before provider calls.
- Provider reconciliation discovers success after an ambiguous timeout.
- Duplicate requests return the original result and never repeat value movement.

### Stripe Webhook Forgery And Ordering

Threats:

- Forged event changes a payment, subscription, dispute, or payout.
- Valid event is replayed outside tolerance.
- Out-of-order events regress state.
- Failed internal processing is acknowledged and never retried.

Controls:

- Verify Stripe signature with the correct endpoint secret and timestamp tolerance.
- Separate secrets by environment and endpoint.
- Persist only valid events to the durable inbox.
- Return 2xx after durable acceptance, 4xx for invalid signature, and 5xx when acceptance cannot be persisted.
- Bind every event to an immutable local provider-object mapping and verify environment/livemode, connected account, tenant, amount, currency, cumulative paid/refunded/disputed amounts, and supported event schema.
- Enforce one provider/environment/account/object/monetary-leg identity globally and atomically prove `SUM(minted) <= authoritative confirmed amount`, even when different internal commands reference the same provider object.
- State machines accept valid monotonic provider transitions and retain ignored out-of-order evidence; partial refunds and disputes use cumulative monetary invariants rather than a simple status ordering.
- High-impact transitions fetch authoritative provider state before posting when policy requires it.
- Scheduled reconciliation compares local state to Stripe objects and event history.
- Raw payload retention is minimized, encrypted where necessary, and governed by a retention policy.

### Client-Controlled Pricing

Threats:

- Buyer submits a lower amount or different currency.
- Product or fee policy changes during checkout.
- Fixed-mix legs are recomputed inconsistently.

Controls:

- Server resolves product, seller policy, entitlement, tenant, tax, and prices.
- Order snapshots every price, currency leg, fee policy version, and seller preference.
- Payment request references the authoritative order and cannot override totals.
- Settlement validates the unchanged snapshot and atomically posts all legs.

### Pending Deposit Finality

Threats:

- User spends, transfers, converts, escrows, or withdraws a deposit before the provider confirms it.
- A delayed failure leaves an observed funding claim in the user-facing aggregate projection.
- Newer pending value is selected while older confirmed value exists.

Controls:

- `Observed` creates a source stamp, append-only evidence, and visible nonmonetary pending claim. It creates no journal credit or coin lot.
- `Confirmed` atomically closes the pending claim, posts the mint, and creates exactly one root confirmed lot; `Failed` or `Expired` closes the claim without a monetary posting.
- `total` may display active pending claims, while every authorization uses `availableToSpend` derived exclusively from eligible confirmed fragments.
- Spend, conversion, transfer, and escrow select oldest eligible confirmed lots by `confirmed_at`, then journal sequence.
- Payout additionally requires earned provenance, 120-day maturity, and every hold/debt/KYC/reserve gate.
- Pending and confirmation transitions are idempotent, monotonic, provider-bound, and append-only. Database constraints make provider/environment/account/object/monetary-leg identity unique and prevent cumulative confirmed credits from exceeding authoritative provider value.

### Provenance Laundering

Threats:

- Purchased hard coins become withdrawable through escrow reclaim.
- Colluding accounts convert purchased hard into earned hard and immediately cash out.
- Soft coins become hard through resale or self-dealing.
- A forged, missing, reused, or backdated source stamp makes an unconfirmed credit appear mature.

Controls:

- Immutable debit-to-credit-lot allocation and exact parent-fragment-to-child-lot lineage.
- Every inbound credit lot references one immutable source stamp containing provenance, opaque source/provider fact, confirmation kind/time, policy version, and hash.
- External credits are minted only after authoritative provider confirmation is bound to the pending operation; one confirmed source leg creates one root lot.
- Reclaim/refund restores original provenance.
- Claim/sale creates earned proceeds but applies maturity and risk holds.
- Every new earned-hard lot sets `matures_at = confirmed_at + 120 days`; a new sale, bounty, or transfer creates a new clock.
- No command, permission, or administrative adjustment can accelerate that maturity. A correction reverses the lot and creates a new correctly stamped lot.
- Related-account/device/payment-identity graph contributes to payout risk.
- Self-purchase and controlled-account transfers are blocked or reviewed.
- Soft-paid proceeds remain soft and never become hard.
- Payout selects only source-verified, confirmed, at-least-120-day-old, unheld earned-hard lots.

### Refunds, Disputes, And Negative Debt

Threats:

- Seller withdraws before refund or dispute.
- Provider reversal arrives after payout.
- Reversal silently fails because balance cannot go negative.
- A card chargeback reverses a root mint after its fragments have been split, transferred, converted, escrowed, or paid as fees.
- A partial chargeback selectively targets easier-to-recover descendants or reverses the same fragment twice.

Controls:

- Refund/dispute freezes exact reachable descendant fragments by traversing the root mint's lineage.
- Partial reversal consumes the deterministic root interval `[previous cumulative, new cumulative)` in canonical trace units; replay is empty, regression is rejected, and unrelated-root ranges remain untouched.
- Provider event resolution either releases the hold or posts a reversing group.
- Payout cannot allocate held lots.
- The writer proves recovered fragments plus debt, responsible-party receivable, and policy-versioned platform loss equal the exact cumulative provider-reversed root amount.
- Payout/refund/dispute workers serialize on the same fragment rows. Dispatch atomically performs the `reserved -> dispatching` compare-and-swap, claims the outbox command, and records chain, reserve, command, and kill-switch versions under those locks.
- A stale outbox command is cancelled when the refund, dispute, or containment decision linearizes first. An event committed after dispatch becomes a bound compensation/debt workflow because an external request may already be irreversible.
- Provider payout failure releases the same reserved fragments. Provider success burns those exact fragments; neither path creates a replacement maturity clock.
- A dispatching operation with an ambiguous provider outcome remains reserved until authoritative reconciliation; outage handling may release only pre-dispatch or authoritatively failed reservations.
- Post-payout or otherwise irrecoverable loss creates an explicit debt position and blocks new payout/spend according to policy.
- Reserve and platform loss accounts absorb provider finality gaps without mutating history.
- Debt collection, recovery, and write-off require explicit policy and audit events.

### Administrative Abuse

Threats:

- Administrator mints value through an adjustment.
- Administrator releases their own hold or approves a related payout.
- Audit evidence is removed.

Controls:

- No direct database balance adjustment.
- Adjustments are typed posting commands with reason and external ticket/reference.
- Dual approval above configured thresholds.
- Initiator cannot approve the same operation.
- Related-user and self-target checks.
- Immutable audit events and alerts on every privileged adjustment.
- Break-glass permission is time-bound, separately logged, and reviewed.

### Account Takeover And Protected Changes

Threats:

- Attacker signs in with stolen credentials and changes payout destination before requesting withdrawal.
- Attacker disables MFA, changes email, or transfers ownership to bypass later notifications.
- Attacker waits for 120-day maturity, then drains old confirmed earned-hard fragments.
- User session is valid but device, IP, ASN, credential age, or recent security-change context is high risk.

Controls:

- Payout, bank/payout-destination changes, account ownership transfer, email change, MFA reset, high-risk settlement, hold release, and administrative adjustment require transaction-bound reauthentication.
- Passkey/WebAuthn or equivalent phishing-resistant verification is preferred for payout and destination changes; weaker factors require a higher risk score, longer cooldown, or manual review.
- Protected-change cooldowns block payout reservation/dispatch and high-risk value movement after new device login, password reset, MFA reset, email change, ownership transfer, identity update, or payout destination change.
- A notification is sent through verified out-of-band channels for protected changes. Notification delivery is not sufficient authority, but failed delivery can raise the risk outcome.
- The risk decision binds user, session, device-risk token, destination, source-root set, amount, policy version, cooldown state, and reauthentication evidence. Any material change requires a new decision.
- Support cannot override cooldown or step-up requirements without a time-bound, dual-approved, audited risk decision.

### Transaction Risk Decision Bypass

Threats:

- A capability module calls the economy writer without going through Risk.
- A stale `Allow` decision is replayed after policy, reserve, destination, or kill-switch state changes.
- A decision for one amount, destination, source-root set, or user is reused for another.
- Risk service outage causes fallback allow behavior.

Controls:

- Economy Core rejects every protected operation without a fresh immutable `RiskDecisionId`.
- Core independently verifies the decision snapshot against the final actor, template, amount, currency legs, source roots, destination, provider reference, policy version, reserve version, feature flag, and kill-switch epoch.
- Risk decisions expire quickly and are single-use where the operation consumes value, counter capacity, provider proof, or destination authority.
- `Allow`, `Challenge`, `Hold`, `Review`, and `Deny` are terminal decision outcomes; only `Allow` can authorize value movement, and only `Hold` can authorize nonspendable protective holds.
- Risk dependency failure, stale policy, unknown entity graph, unavailable aggregate counters, or unauditable decision evidence fails closed.
- Integration tests assert that direct posting attempts without a decision, stale decisions, and mismatched decisions cannot reach the journal writer.

### Related-Account Abuse And Wash Trading

Threats:

- Related accounts buy each other's products or bounty claims to turn purchased hard or promotional value into earned-hard proceeds.
- Colluding accounts rotate payment instruments, payout destinations, devices, IP ranges, or referrals to stay below per-user caps.
- A seller artificially inflates sales, refunds, ratings, or ad engagement to unlock payouts or visibility.
- A ring splits value into many small fragments so no single wallet crosses a threshold.

Controls:

- Risk maintains a privacy-preserving entity graph across account, tenant, KYC identity, payment instrument, bank/payout destination, device-risk token, IP/prefix, ASN, referral, project, product, counterparty, and provider object.
- Aggregate limits apply to wallet, identity cluster, source root, destination, counterparty pair, product, tenant, provider account, device/IP/ASN cluster, and global loss-budget dimensions.
- Fragmentation does not reset limits. Limits and exposure follow root lineage and entity clusters, not just visible balances.
- Self-purchase, controlled-account purchase, circular trade, refund-loop, bounty-cycling, and related-destination patterns are blocked or routed to manual review before settlement or payout.
- Dynamic rolling reserves may hold earned-hard fragments beyond the 120-day minimum when seller, product, dispute, provider, jurisdiction, or entity-cluster risk requires it.
- Risk holds are explicit, source-linked, and auditable. Hold release requires a fresh decision and cannot be implied by maturity alone.

### Ad Reward Farming

Threats:

- Headless browser sends fake milestones.
- Real browser accelerates playback or hides the tab.
- Token is replayed across users or devices.
- Many accounts share a device, IP, payment identity, or timing pattern.
- Human farms stay just below simple caps.
- Provider callback proves completion but not exact earned revenue for that video.

Pre-reward controls:

- Signed short-lived token bound to user, session, creative, duration, nonce, and risk context.
- Strict event order and physically possible wall-clock timing.
- Visibility/focus telemetry with a configurable tolerance, not zero tolerance.
- Single-use completion claim.
- Account age and verification tiers.
- Per-user, account, device-risk token, IP/prefix, ASN, and network velocity limits.
- Challenge/risk provider integration at configurable thresholds.
- Reward quote and policy snapshot generated only by the server.
- Independent provider-side completion evidence is required for immediate issuance; networks without it remain disabled or defer minting until an independently verified report. Client milestones alone never authorize issuance.
- Per-user, device, network, and global quota/loss-budget counters are consumed atomically with the reward posting.
- Entity-graph and aggregate exposure checks include related accounts, payout destinations, payment instruments, device/IP/ASN clusters, referral clusters, and source-root history.
- Issuance fails closed when provider evidence, fraud scoring, counters, reports, or funded budgets are unavailable or stale.

Post-reward controls:

- Correlation analysis across account, device, IP, timing, payout, and marketplace relationships.
- Network-level estimated-versus-actual variance monitoring.
- Reward caps and daily loss budget.
- Risk holds or account freeze on suspicious patterns.
- Manual review queue with evidence and appeal path.
- Provider-report variance, cluster-level yield, and invalid-traffic adjustments update future reward policy and may put high-risk clusters into hold/review without rewriting already posted rewards.

Automatic containment:

- Disable one network when its report is stale or variance is excessive.
- Reduce reward rate or widen buffer for deteriorating reconciliation.
- Disable rewards for one risk segment without disabling all wallet reads.
- Global soft-issuance kill switch.

The system must not claim that fingerprinting proves one physical person. Device and network signals are risk evidence, not identity facts.

### Ad Revenue Estimate Risk

Threats:

- Provider callback reports no monetary value or reports gross rather than net value.
- Cold-start eCPM is optimistic.
- Invalid traffic is clawed back in a later report.
- Report ingestion is delayed or duplicated.

Controls:

- Reward uses trailing net eCPM or explicitly marked provider estimate.
- Contracted revenue share is applied before the estimate is trusted.
- Cold-start policy uses a wider buffer and strict soft-issuance cap.
- Estimated USD nanos convert at the fixed `100,000 SC/USD` rate using checked integer arithmetic and server-side rounding.
- Actual reports reconcile unique provider/report/version batches.
- Reconciliation changes future rates and treasury valuation, never past rewards.
- Stale reporting automatically blocks or reduces issuance.
- Fixed-parity reserve, ad-variance buffer, and funded fraud-loss budget cover the estimate-to-actual gap.

### Precision And Rounding Abuse

Threats:

- Repeated tiny claims exploit rounding direction.
- Client-controlled decimals, overflows, or inconsistent conversion paths mint extra SC.
- Batching behavior changes total reward entitlement.

Controls:

- Lots, allocations, postings, and projections use integer HC and SC only; `100 HC = USD 1.00`, `100,000 SC = USD 1.00`, and `1 HC = 1,000 SC`.
- Rate calculations use USD nanos with checked `Int128` intermediates; no IEEE floating point or client-supplied reward amount is accepted.
- Revenue share, buffer, and fixed-parity conversion are represented as one rational numerator with one final division. The canonical-denominator remainder is retained in an idempotent wallet-level accumulator, so intermediate floors, splitting, batching, policy rollover, or network retirement cannot change or strand entitlement.
- Conversion principal is exact; fees are separate postings and cannot alter parity.
- Property tests cover boundary values, overflow rejection, rounding monotonicity, split-versus-batch equivalence, and reversals.

### Reserve And Custody Misstatement

Threats:

- Same cash is counted as both user backing and admin reserve claim.
- The same asset is pledged simultaneously to hard and soft liabilities.
- Soft face value, stressed provider cost, or operating buffers are understated.
- Company withdraws immature fee revenue.

Controls:

- Treasury distinguishes external assets, user liabilities, platform claims, clearing, and restricted backing.
- Reserve is a classification/snapshot, not a spendable wallet credit.
- Soft reserve is `max(fixed face value, stressed expected redemption cost)` plus ad-variance, fraud-loss, provider/FX, and operating-liquidity buffers, rounded upward.
- Stressed expected redemption cost values each open authorization against its selected service, all unreserved outstanding soft against the highest enabled stressed cost-per-SC ratio, and adds irreversible in-flight provider cost without double counting reserved units.
- Service authorization requires a snapshotted SC price that covers stressed provider cost and the configured minimum gross margin.
- Margin policy enforces `0 <= margin_ppm < 1,000,000`; missing, stale, zero-priced, or invalid service inputs fail authorization closed.
- Hard reserve includes face-value user/escrow/payout-clearing liabilities plus chargeback, refund, settlement, and liquidity buffers.
- Eligible external assets have explicit settlement-finality and haircut rules; unconfirmed or disputed amounts are excluded.
- Core owns the authoritative reserve head and exclusive asset-allocation lock. Treasury proposes calculations but cannot mutate the active version. Issuance, payout dispatch, refund, and admin withdrawal serialize against that fresh Core version.
- Monthly withdrawal allocates exact matured fee lots and checks post-withdrawal coverage.
- Custody reconciliation blocks withdrawals when unexplained variance is nonzero.

### Financial Crime And Platform Infractions

Threats:

- A sanctioned, blocked, underage, or unsupported-jurisdiction user receives payouts or sells products.
- A product, project, bounty, or seller violates platform rules but still monetizes through the economy.
- Compliance or Trust/Safety cases are ignored because the ledger sees only a valid balance.
- Operators conflate financial-crime legal holds with general support or content moderation flags.

Controls:

- `GameGuild.Compliance.FinancialCrime` owns KYC aggregation, sanctions/PEP/adverse-media status, monitoring cases, jurisdiction restrictions, and compliance hold inputs.
- `GameGuild.TrustSafety` owns platform abuse, prohibited products, content/project enforcement, marketplace integrity, and nonfinancial account restrictions.
- `GameGuild.Finance.Economy.Risk` consumes both inputs and returns one protected-operation decision to Core. Neither Compliance nor Trust/Safety mutates journal state.
- Payout and high-risk marketplace settlement require current financial-crime status. Product monetization and project/bounty settlement require current Trust/Safety status.
- Compliance holds, Trust/Safety holds, and risk holds are typed separately, visible to authorized operators, and released only by the owning policy path plus a fresh risk decision.
- Every compliance or Trust/Safety read that exposes protected personal or enforcement data is audited before data is released.

### Privacy And KYC

Threats:

- Raw identity documents or payment payloads leak through logs or broad database access.
- Stable device fingerprints become ungoverned tracking identifiers.
- Risk data is retained indefinitely.

Controls:

- KYC provider owns identity-document storage whenever possible.
- GameGuild stores provider references, status, reason codes, and minimum required evidence.
- Encrypt sensitive provider identifiers and payload fragments.
- Tokenize device/network signals with KMS-keyed HMAC and managed key rotation; do not rely on reversible raw identifiers or salt-only hashes for enumerable values.
- Append-only records contain opaque surrogate references only. PII and provider identifiers remain in access-controlled, retention-governed mappings that can be deleted when legally permitted.
- Define retention and deletion schedules by data class and legal requirement.
- Restrict access to KYC and risk data separately from ordinary support/admin permissions.
- Immutably audit every privileged KYC/risk read and export with actor, tenant, purpose, scope, and result; require independent approval for bulk export and alert on anomalous access.
- Persist and verify the privileged-access audit before releasing protected data. Audit-writer failure blocks the read/export rather than producing an unaudited response.
- Redact structured logs and error responses.

## Security Test Matrix

### Unit And Contract Tests

- Every posting template balances per currency.
- Every template rejects wrong cardinality, account purpose, sign, provenance, authority, cap, reserve version, and conversion ratio even when the submitted entries balance.
- Source-stamp tests reject absent, reused, mismatched, unconfirmed, future-dated, backdated, hash-invalid, duplicate-provider-leg, over-confirmed, and early-maturity inputs at the database writer boundary.
- Root-range tests prove deterministic interval partitioning, hard-to-soft trace-quanta mapping, mixed-root merge preservation, partial cumulative reversal selection, replay idempotency, and unrelated-root isolation.
- Pending-deposit tests show the nonmonetary claim in `total`/`pendingConfirmation`, prove no credit lot or mint posting exists, and reject spend, conversion, transfer, escrow, and payout until authoritative confirmation.
- FIFO tests prove an older eligible confirmed fragment is allocated before every newer confirmed fragment; pending claims are not candidates at all.
- Earned hard is not withdrawable one tick before 120 days and becomes eligible at the exact boundary only when every other gate passes.
- An active hold remains effective and payout-blocking before, at, and after the 120-day boundary.
- `100 HC = USD 1.00`, `100,000 SC = USD 1.00`, and `1 HC = 1,000 SC` hold at boundaries without overflow or floating-point drift.
- Soft-to-hard conversion has no command, endpoint, handler, or posting template.
- Purchased hard cannot be selected for payout.
- Policy versions are immutable and effective-date selection is deterministic.
- Ad tokens reject bad signature, expiry, user mismatch, creative mismatch, and replay.
- Protected-operation tests reject missing, expired, reused, stale-policy, stale-reserve, stale-kill-switch, actor-mismatched, destination-mismatched, amount-mismatched, source-root-mismatched, and wrong-outcome risk decisions.
- Risk outcome tests prove `Challenge`, `Review`, and `Deny` cannot move value and `Hold` can only create or preserve nonspendable holds.
- ATO tests enforce step-up reauthentication and cooldown after password reset, MFA reset, email change, ownership transfer, payout-destination change, new-device login, and high-risk session elevation.
- Limit tests prove aggregate exposure is enforced across wallet, identity cluster, source root, destination, counterparty pair, product, tenant, provider account, device/IP/ASN cluster, and global loss budget.
- Compliance/TrustSafety tests prove protected operations fail closed when required status is blocked, stale, unknown, or unauditable.

### PostgreSQL Integration Tests

- Concurrent spend permits at most one winner when funds are insufficient for both.
- Partial spend preserves the unallocated source-lot remainder and maps every child fragment to its parents. A root reversal follows hard and converted-soft descendants, balances each currency leg without reminting a retired root, and exactly partitions root-equivalent value into recovery, debt/receivable, and loss.
- Concurrent allocation versus reversal proves the root epoch freezes descendants before traversal; stale allocators fail and cannot move targeted ranges.
- Fixed-mix checkout commits both currency legs or neither.
- Payout versus refund/dispute serializes on the same lots.
- Duplicate idempotency keys produce one posting group.
- General runtime role cannot directly mutate any immutable or integrity-bearing mutable table; the economy writer can execute only registered posting/transition procedures with hardened ownership, ACL, and `search_path`.
- Trigger rejects mutation under an over-granted test role.
- Tampering breaks chain verification.
- Independent signed-anchor mismatch or failure to cover the payout eligibility sequence blocks payout execution even when the latest anchor is recent.
- Payout dispatch fails when the signed anchor does not bind the exact canonical eligibility snapshot or any protected field changed after anchoring.
- Full recompute detects projection corruption and enforces the lower balance.
- Risk-decision uniqueness and counter-consumption tests prove one decision cannot authorize two value movements and concurrent requests cannot overspend aggregate exposure.
- Entity-graph integration tests prove fragment splitting, multi-wallet routing, related-destination changes, and circular counterparty pairs do not reset velocity or exposure limits.

### Provider Tests

- Forged, stale, wrong-secret, and malformed Stripe signatures are rejected.
- Duplicate and out-of-order provider events are idempotent and monotonic.
- Wrong environment, Connect account, tenant, provider object, amount, currency, or cumulative refund/dispute value is rejected.
- Timeout after provider success recovers through reconciliation.
- Missing webhook is discovered by provider-object reconciliation.
- Ad report duplicate/version correction cannot reconcile the same batch twice.

### End-To-End Tests

- Top up purchased hard, spend it, refund it, and verify provenance restoration.
- Top up purchased hard, split it across recipients/fees/escrow/conversion, process full and partial provider chargebacks, and verify every root fraction is recovered or represented by explicit debt/receivable/loss.
- Sell a product, verify its source stamp, advance exactly 120 days from authoritative confirmation, request payout, and complete provider reconciliation.
- Open a dispute before maturity and verify payout remains blocked.
- Lose a post-payout dispute and verify debt plus reserve impact.
- Complete an ad session once and verify replay receives no second reward.
- Reconcile an ad batch below estimate and verify the user reward remains unchanged.
- Verify split-versus-batched ad entitlement produces the same SC total and atomically consumes every quota/loss budget.
- Attempt related-account self-purchase, bounty cycling, circular trade, refund loop, and shared payout-destination laundering; verify settlement or payout is blocked or sent to review before value leaves the platform.
- Change payout destination, then attempt payout before cooldown expires; verify the payout is blocked even when fragments are older than 120 days.
- Increase provider cost beyond the commercial margin and verify new service authorization fails while existing balances and parity remain unchanged.
- Exercise exact margin equality, one-nano/one-SC-below boundaries, overflow, stale/unknown cost inputs, reserved-service mix, and worst-case unreserved-soft valuation.
- Hold/freeze/reserve soft fragments and prove every confirmed unconsumed unit remains in face-value liability until authoritative burn/consumption.
- Post, claim, reclaim, expire, and race a bounty.

### Containment Tests

- Provider/KYC outage accepts signed evidence into the inbox but blocks affected external dispatch.
- Ambiguous payout timeout keeps exact fragments reserved until authoritative reconciliation; only pre-dispatch or confirmed-failure paths release them.
- Reserve or margin staleness blocks new cost-bearing value while allowing only provably liability-reducing templates.
- Projection mismatch enforces the lower recomputed availability and blocks the affected wallet until rebuild succeeds.
- Writer, chain, anchor, source-evidence, or lineage corruption permits quarantine intake and forensic reads only; all posting and provider dispatch remain blocked.
- Quarantined refund/dispute obligations post only after integrity recovery and a verified anchor covering the recovery group.
- No administrator or break-glass path can bypass the predicate-to-capability matrix.
- Privileged KYC/risk read and bulk export return no protected data when durable audit acceptance or verification fails.
- Risk service outage, stale entity graph, stale aggregate counters, or unavailable Compliance/TrustSafety status blocks protected operations while preserving safe reads and evidence intake.
- Manual-review intake accepts evidence and creates holds without creating spendable value when a decision would otherwise be unknown.

## Operational Gates

Before soft issuance:

- Ad-session verification and idempotency tests pass.
- Per-user/device/network caps and global kill switch are configured.
- Risk decision service, entity graph, cluster limits, fraud-loss budgets, and manual-review queues are healthy and fail closed.
- Revenue report staleness and variance alerts are active.
- Fixed-parity soft reserve, stressed redemption cost, minimum commercial margin, variance buffer, fraud-loss budget, provider/FX buffer, and liquidity buffer are computed and funded.
- Provider evidence, fraud service, atomic counters, independent ledger anchor, and report freshness all fail closed.

Before hard top-up or spend:

- Wallet and payment authorization remediation is deployed.
- Authoritative pricing is enforced.
- Stripe signature verification and durable inbox are deployed.
- Ledger immutability, chain verifier, projection reconciliation, and provider simulation guards pass.
- Fixed-parity hard reserve and chargeback/refund buffers are current and funded.

Before payout:

- Legal and Terms approval is recorded.
- Connect/KYC onboarding and provider reconciliation pass in the target jurisdiction.
- Transaction-bound reauthentication, payout-destination cooldown, protected-change alerts, sanctions/financial-crime status, related-account graph, and dynamic rolling reserve policies are active.
- Maturity, holds, debt, reserve, and custody tests pass.
- Payout fencing, stale-command cancellation, provider-object binding, and independent anchor checks pass.
- Dual-control administration is enabled.
- Incident runbook and payout kill switch are exercised.

## Residual Risks

- Sophisticated human ad farms cannot be eliminated completely.
- Card disputes may arrive after the configured hold window.
- Provider outages can make balance finality temporarily ambiguous.
- Entity graphs can create false positives and false negatives; they must be governed with reason codes, appeal paths, and monitored precision/recall rather than treated as identity proof.
- Legal classification for stored value, rewards, marketplace proceeds, and payout eligibility may require stronger KYC/AML controls or product changes in some jurisdictions.
- A fully privileged database and application compromise may corrupt both data and chain head; independent anchors and backups reduce detection risk but do not replace infrastructure security.

These risks must be accepted explicitly with measurable loss limits and operational owners before activation.
