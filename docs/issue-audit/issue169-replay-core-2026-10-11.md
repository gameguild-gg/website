# Issue #169 — chronological replay core and acceptance matrix

Original issue: https://github.com/gameguild-gg/gameguild/issues/169
Initial preparation was based on PR #791 head a5dfdb7c5b2c22d734de581696478e26ebbe7329. The sequence-analysis update was reviewed against current owned head 048a22a5183d35768d1496c449603e2ecd3d1d3c, incorporating develop ec7d866a700c8bb004332aded115673e9c64f99c; destinations are still absent. All four new destination paths were rechecked before integration.
This increment supplies the replay state and automated source checks. Issue #169 remains open until all original criteria below have accepted implementation evidence.

## Prepared files

- apps/web/src/lib/audit/replay-timeline.ts
- apps/web/scripts/audit-replay-timeline.test.mjs
- apps/web/src/lib/audit/replay-timeline.test.ts (normal Web suite bridge)
- docs/issue-audit/issue169-replay-core-2026-10-11.md (full acceptance matrix)

The module accepts a deliberately limited projection of existing AuditLogDto fields. It preserves .NET timestamp precision to 100 ns, converts explicit timezone offsets, sorts ties by normalized event ID, checks tenant consistency before filters, rejects duplicate IDs and incomplete supplied result counts, and avoids silently truncating datasets. The current in-memory ceiling is 10,000 events. Larger investigations require a server paging/snapshot design, rather than calling this preparation complete.

Filtering combines dimensions with AND. Time bounds are inclusive; omitted selections match all, explicitly empty selections match none. Filter changes reset playback without losing stored bookmarks. Playback is driven by an injected monotonic clock; the adapter owns timers. It supports speed adjustment, seeking, event stepping, and pause/resume. Correlation uses non-null request/session IDs and the same resource type plus ID. Export omits raw description, IP, user agent, error message and metadata fields. IDs and investigator notes still need authorized access and privacy controls.

## Original acceptance criteria

| Original criterion | Prepared increment | Work still required for acceptance |
| --- | --- | --- |
| 1. Chronological stream with precise timestamp resolution and synchronization | Explicit-offset parsing, calendar validation, 100 ns ordering and stable ties | Authorized complete server snapshot, consistent source clocks, native integration tests |
| 2. Multi-user session reconstruction and interactions | Immutable user/session groups and related-event selection | Backend dataset completeness and interactive parallel session presentation |
| 3. Focused event filtering and isolation | User, session, action, typed resource and inclusive time filters; tenant check before selection | Guarded API filters, persisted replay selection and isolation integration tests |
| 4. Real-time and accelerated playback | Clock-driven playback, speed controls, pause, seek and step | UI controls, real animation-clock tests and functional acceptance |
| 5. Interactive visualization with navigation, zoom and exploration | Position lookup, inclusive viewport and binary-search frames | Actual accessible timeline interface, zoom and event detail integration |
| 6. Event correlation identifying relationships | Correlation/session IDs and same typed resource | Server-backed correlations, interaction view and evidence presentation |
| 7. User journeys across systems | Chronological groups retaining actor, session and resource identifiers | Multi-session cross-system journey presentation and backend evidence completeness |
| 8. Automated attack sequence recognition and threat identification | Explicit action/outcome sequences with repetition thresholds, inclusive windows and isolated user/session/request/typed-resource groups; each signal retains exact event IDs and rule definition | Server-backed investigator configuration and complete source evidence, threat classifications and native acceptance; a sequence match is not a confirmed attack |
| 9. Comprehensive investigation report and evidence | Deterministic JSON projection, journey event references and current-view bookmarks | Authorized evidence summaries, durable IDs and source provenance, report/export endpoints and tests |
| 10. Bookmarks and annotations | Immutable in-memory add/update/remove and filtered display | Authenticated durable annotations, tenant/actor isolation, audit trail and integration tests |

The original proposed solution additionally mentions playback recording for expert testimony and sharing investigation results externally. Those requirements remain in scope. This preparation neither records a video nor proves legal admissibility.

## Verification

- portable713.tap and portable713-normalized-proof.json retain the original four failures before fixes.
- portable713-source.ts preserves the source used by that first run.
- portable718.tap / portable718-proof.json: 46 passed, 0 failed, 0 skipped.
- types715-proof.json: TypeScript 6.0.3, strict plus noUncheckedIndexedAccess and exactOptionalPropertyTypes, no diagnostics, no emit; standalone module only.
- Tests import the prepared source through Node's type stripping. They exercise deterministic clocks and data; they start no app, browser, worker, Wasm runtime, database or .NET SDK.
- These checks do not replace the full repository Web build or native functional acceptance.

## Remaining implementation order

PR #791, #797 and #800 are accepted and merged into develop. PR #800 supplied the legacy snapshots with 8361 hosted native tests passing, no failed or skipped tests. This core is the next increment; it does not close #169.

1. Finish matching-head hosted Web validation of this core and its Vitest bridge.
2. Implement guarded replay endpoints and durable tenant/actor-scoped replay sessions using the existing audit infrastructure. Snapshot completeness must be explicit across pages and concurrent inserts.
3. Add durable, audited bookmarks/notes, evidence provenance, investigator-configured sequence findings, recording/export and an accessible interface using the existing web containers.
4. Regenerate clients when API contracts change. Complete native functional acceptance before closing #169 with the full requirement matrix and evidence.

The existing IAuditReplayService delegate and AuditReplaySession entity still need concrete implementation, registration and storage. Their private filters and tenant representation must be reconciled without bypassing guards or migrations.

## Sequence-analysis increment — 2026-10-11

The existing module now supports explicit investigator rules; it does not infer that a permission subject was the actor, inspect private metadata, or run automated responses. Each step matches exact action names, an optional boolean outcome, and 1–64 repeated occurrences; expanded sequences require distinct ordered events. Group selection is explicit and null identifiers cannot join anonymous actors. Resource grouping includes both type and ID. Other actions may interleave. One deterministic, latest-start evidence chain is returned per matching terminal event and rule, with the exact normalized rule definition and event IDs.

Window boundaries are inclusive at .NET's 100 ns event precision. Bounds are 32 rules, 16 steps, 64 expanded events, 1–86,400,000 ms windows, the existing 10,000 supplied events, and 1,000 findings. Overflow rejects the entire result explicitly. The algorithm retains a latest-start dynamic prefix for each length, updates in descending order to prevent event reuse, and has bounded work proportional to rules × events × 64. Findings apply only to the selected view; filters can hide evidence. They are investigative signals and do not certify attacks, source completeness or actor attribution.

- portable838-proof.json: 75 source checks passed. types838-proof.json retained the three initial strict inference errors.
- portable839-proof.json: **76 passed, 0 failed, 0 skipped**, including the original 46, sequence false-positive/isolation/window/overflow cases, the maximum 10,000-event dataset and 200 comparisons to an exhaustive small-sequence oracle.
- types839-proof.json: TypeScript 6.0.3, strict/noUncheckedIndexedAccess/exactOptionalPropertyTypes, no diagnostics; standalone source only.
- sparse selectors now reject explicitly. Both source and initial test baselines are preserved separately.
- These files remain outside the repository checkout. No app, browser, native runtime, SDK or database was started. No issue is closed by these checks.

## Validation increment — 2026-10-11

- portable908-proof.json records the eight added regression failures against the original prepared source: 76 passed / 8 failed.
- Events and filter selectors now validate sparse arrays fully through Array.from. Failed filter changes preserve the selection, revision, playback and annotations.
- In-memory bookmarks have a 1000-entry bound; overflow rejects atomically while edits and removal remain available.
- portable909-proof.json: **84 passed, 0 failed, 0 skipped**. The maximum dataset and the 200 exhaustive sequence comparisons remain in the suite.
- types910-proof.json: strict TypeScript 6.0.3, no diagnostics; standalone source only.
- The new Vitest bridge runs the source checks through a 128 MiB, 10-second Node process. Existing Web validation discovers it without changing the workflow or test selection. Its integration is still unverified until matching-head hosted Web validation passes.
- The preparation and failure baselines are preserved outside the checkout. This increment contains the core, source tests and normal Web suite bridge; durable annotations, actual UI and authorized data remain pending.

The bridge registration was checked with a mock (bridge918-proof.json): four assertions passed while the real bounded Node runner executed all 84 source cases. This was not actual Vitest execution or native functional acceptance. Matching-head hosted Web verification remains required.
