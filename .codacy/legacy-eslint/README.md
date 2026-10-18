# Legacy ESLint engine repairs

This is a hosted validation candidate for the three observed crashes in
`codacy/codacy-eslint:9.18.12`. The immutable Linux amd64 digest is pinned in the
Dockerfile and workflow. It does not change the active Codacy configuration,
replace the ESLint 9 root configuration, disable rules, exclude files, upload
issues, or certify issue closure.

The public tag was resolved to this digest on 2026-10-18. Cloud logs identify
the release version; the cloud runtime image digest was not exposed. Package
versions and file hashes must therefore match independently in hosted CI.

## Repairs

- Import 2.29.1: variables without definitions no longer crash namespace-import
  detection. A missing binding still produces the diagnostic without an unsafe
  automatic fix. Existing definition/reference handling is retained.
- Shopify 45.0.0: move the existing message into `meta.messages`, where ESLint
  expects message IDs. The visitor and message text remain unchanged.
- Security Node 1.1.4: a `try/finally` statement has no catch handler. Guard that
  nullable handler and retain the existing missing-handler diagnostic.

All three package versions, complete original file hashes and unique repair
anchors must match before any image file is written. The original and repaired
images run the same positive and negative contracts. Original crash assertions
remain visible, rather than being replaced by a later green result.

## Effective profile snapshot

`effective-patterns.json` preserves all 2,631 successfully recovered enabled
Codacy pattern IDs and their raw parameter values, including the 247 nonempty
parameter sets. The 185 catalog IDs that returned HTTP 404 are recorded as
unresolved catalog responses, not disabled patterns. The public read-only API
collection took place over an interval and is not an atomic server export.

The hosted inventory contract checks that every recovered ID and parameter name
exists in the pinned engine catalog. This is inventory preservation evidence;
it does not prove file coverage, a complete generated ESLint configuration,
framework context, parameter value interpretation or scanner migration parity.
Those checks remain required before changing the active scanner.

`project-configuration.cjs` prepares the complete received-pattern input using
the pinned catalog's parameter types, then uses the image's own seed defaulting
and configuration creator. It checks every generated rule ID and option before
exporting the candidate to a hosted artifact. Strings, `false`, zero, arrays and
objects retain their types. Unknown IDs, parameters or default types fail.
The contracts also load this complete candidate through the actual ESLint API
for JavaScript and TypeScript. This projection retains and records the wrapper's
inherited ignores/overrides; adequate file coverage and correct framework/runtime
contexts still require separate work. It is not an activated scanner profile.

## Evidence

The `Codacy legacy engine contracts` workflow runs in hosted GitHub Actions.
It retains original/repaired TAP output, repair hashes, engine digest, source
head, checkout, tree and profile digest. No local container or product tests
are needed to prepare this candidate.

Upstream sources: [Codacy ESLint](https://github.com/codacy/codacy-eslint/tree/2c47ef11d8e57a327dabb82a9a05a278dbd30ed8),
[Import](https://github.com/un-es/eslint-plugin-i),
[Shopify](https://github.com/Shopify/web-configs), and
[Security Node](https://github.com/gkouziik/eslint-plugin-security-node).
