# Issue #169 — capture adapter and browser-readable integrity receipt

This slice adds a typed adapter from the existing authenticated API client's raw response transport to the safe replay snapshot importer. It prepares the existing CORS default policy to expose only the capture's `X-Audit-Snapshot-SHA256` response header while preserving its configured origins, methods, request headers and credential settings.

## Contract

- POST only to the guarded `/v1/admin/audit-logs/replay/snapshots` endpoint, with start and end; authentication and tenant transport stay in the configured client.
- Freeze the valid subject and explicit tenant scope before the request. Recheck after every await. Abort, logout or a changed subject/tenant discards the result.
- Read the original Blob bytes and response headers. Require HTTP 200, JSON content type, a bounded body and an integrity receipt. Reject malformed UTF-8, a BOM, altered bytes, private fields, invalid timestamps, duplicates and a mismatched tenant through the existing importer.
- Scope checks do not revoke a previously downloaded copy. Server authorization remains necessary. This adapter does not establish a durable investigation or authenticate event provenance.
- Browser CORS restricts access to the response; the API's administrator authorization enforces access to capture. An authenticated request from a disallowed origin can still reach the endpoint, but its response is not exposed to browser code.

## Preparation evidence on 2026-10-11

The final source passed **25 bounded Node cases**, 0 failed/skipped, using a mocked raw transport. TypeScript 6.0.3 strict checking, including unchecked indexed access and exact optional properties, reported 0 diagnostics. The typecheck reads unchanged public SDK declarations, rather than substituting fake types.

The initial strict check found a possibly undefined first content-type segment. The final source uses optional access and all 25 cases passed again. Previous receipts are preserved.

A Web Vitest bridge runs these same 25 cases; that bridge is not an additional 25 cases. The actual Web CI run remains required.

Three CORS policy cases and eight HTTP cases are written for hosted verification. They have not been compiled or executed locally under the incident hold. The HTTP fixture uses the existing synthetic authenticated actor and real PostgreSQL selection; it does not prove a real JWT login or a browser UI flow.

No local application, database, .NET SDK or browser runner was started.

## Remaining issue #169 acceptance

This slice does not close #169. Durable actor/tenant-bound investigation sessions, saved annotations/bookmarks, accessible UI/navigation/zoom, real-time and parallel-session workflows, cross-system journeys, attack-sequence analysis, reporting, recording/sharing, generated endpoint clients and complete original acceptance remain required.
