# Issue #169 — safe snapshot import for the existing replay core

Original issue: https://github.com/gameguild-gg/gameguild/issues/169

This increment reads the guarded capture API's documented schema into the existing AuditReplayTimeline. Initial preparation was reviewed at signed source 4e3fd15d46a48bef54edaa8a756825bebd18cf1a. It remains outside the checkout until the current authentication delivery has completed qualification and merge.

## Behavior

- Hash the original response text as UTF-8 with SHA-256 before parsing. Reserializing an object can change escapes and would produce the wrong digest.
- Limit input to 16 MiB of UTF-8 and reject excessive declared/event counts before mapping. The existing core limits complete supplied selections to 10,000 events.
- Require exactly the versioned safe root/event fields. Missing explicit nullable fields and description, IP, user agent, error or metadata are rejected.
- Require explicit UTC timestamps with seven fraction digits, ordered request bounds, capture time at or after the range, and every event within that inclusive range.
- Validate identifiers, count completeness, duplicate IDs, safe field types, stable timestamp/GUID ordering and exact tenant/global scope through the existing replay core.
- Compare the imported scope to the caller's current authorized tenant before returning the timeline. A global context accepts only a global snapshot.
- Return frozen metadata and event projection. The contained timeline retains the existing controlled filtering, playback, correlation and annotation operations.

The caller must obtain the text and expected digest through the authorized API, preserve the text exactly, and supply its current authorized tenant. A client-side comparison does not replace server authorization or revoke a previously downloaded copy. A digest detects changes against an independently protected expected digest; it does not prove provenance, actor attribution, database completeness, synchronized source clocks or legal admissibility. UserId can identify a subject.

## Checks

- baseline1124-proof.json: 48 passed and one failure in the initial test fixture. Its purported empty GUID had a nonzero prefix and was a valid GUID. The test/source baseline is preserved; the fixture was corrected to the all-zero GUID.
- portable1125-proof.json: 52 source checks passed, zero failed/skipped, using a bounded 128 MiB Node process with no application, browser, worker, SDK or database.
- The checks resolve the module's relative import to the reviewed replay core and strip types. They cover raw JSON escaping, changed bytes, tenant/global mismatch including empty selections, private fields, schema/count/calendar/identifier errors, exact ticks/bounds, 10,000 events and ASCII/multibyte size limits.
- types1127-proof.json: TypeScript 6.0.3, strict/noUncheckedIndexedAccess/exactOptionalPropertyTypes, no emit or diagnostics, for this module and the existing core. This is not the full Next.js build.
- The normal Web suite bridge contains one Vitest check that runs the 52 source checks. Those nested checks must not be added to the full Web suite's test count. Hosted full Web validation remains pending.

## Remaining acceptance

The associated capture packet contains seven prepared C# files and 62 written native cases; it has not been compiled, executed or published. No generated client has yet been delivered for the capture endpoint. This import function is not connected to an actual UI or network request.

Issue #169 remains open for the full ten-criterion matrix in issue169-replay-core-2026-10-11.md: complete authorized evidence acquisition, source-clock evidence, durable owner/tenant sessions, parallel session presentation, guarded filters, playback controls and animation-clock acceptance, accessible navigation/zoom/details, server-backed correlations/journeys/findings, evidence reports, audited durable annotations, recording and authorized sharing.

Next: deliver the capture/import increment through the existing branch, generate clients for the new contract, qualify hosted API/Web tests, then implement durable sessions and the interface using the existing audit and web infrastructure.

## Integrated slice status — 2026-10-11

Authentication PR #808 has merged. The capture, importer, typed raw-response adapter and CORS response header changes are now integrated on the existing branch from develop e3c639a3c24b016dc592d315c89c1e9afbaaf9d2. There are 73 written native capture/CORS cases (31 service, 12 controller, 27 HTTP, 3 policy); they still require hosted compilation and execution. The 52 import and 25 adapter source checks passed separately; native Web validation and endpoint client regeneration remain pending. No UI or durable investigation acceptance is claimed. Preparation history above is retained.
