# Dependency security patches

## braces 3.0.3

`braces@3.0.3.patch` vendors the five runtime-file changes proposed in
[micromatch/braces PR #72](https://github.com/micromatch/braces/pull/72), pinned to
commit `28d440b5dd449dbf1fe6f3506cf94ecca4d02660`. The package's original MIT license
remains in the installed package.

[CVE-2026-93687 / GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm)
affects the published package's recursive AST walkers. No fixed version was
published when this patch was added on 2026-10-03. The patch bounds parser and AST
nesting at 100, supports stricter limits, and rejects cyclic AST parent chains.

The pnpm lockfile binds both chokidar and micromatch to this exact patch. The audit
validator recognizes only this advisory because registry audit results identify
published versions and do not account for local patches. It preserves the full raw
audit report. The dependency CI gate runs `scripts/ci/tests/braces-security.test.mjs` against every installed
consumer resolution. Missing patches, excessive nesting, cyclic parents, and
incompatible ordinary patterns fail that gate. All other advisories retain the
existing failure policy.

The advisory remains visible in version-based dependency scanners.
`.trivyignore` has no advisory entries, and the mandatory patch regression tests
enforce that empty list. Installed-source mitigation evidence is recorded
separately from external scanner closure. The complete raw registry audit report
remains available, and installation and behavioral verification are still required.

When an upstream fixed release is available, replace the patch with that release,
remove the audit validator exception, and retain the behavioral regression tests.

## sprintf-js 1.1.3

`sprintf-js@1.1.3.patch` bounds numeric precision before `toFixed`,
`toExponential`, and `toPrecision`, preserving formatting inside their supported
ranges. The original package license remains in the installed package.

[CVE-2026-97058 / GHSA-hp3w-g68c-fv3c](https://github.com/advisories/GHSA-hp3w-g68c-fv3c)
affects the published package through 1.1.3. No upstream patched version was
available when its Codacy finding was reviewed on 2026-10-06. The lockfile binds
every workspace consumer to the committed pnpm patch.

The dependency audit validator recognizes only this exact advisory and version
after `scripts/ci/tests/sprintf-security.test.mjs` verifies every installed
consumer. Its regressions exercise excessive precision, precision overflowing to
infinity, the zero-precision general-format boundary, ordinary formatting, and
the supported precision limit. The advisory remains visible in Trivy's
version-based dependency scan; the local patch is separate mitigation evidence.
Tests require `.trivyignore` to contain no advisory entries. Other advisories retain
the existing failure policy, and the complete registry audit report remains available.

When an upstream fixed release is available, replace the patch with that release,
remove the audit validator exception, and retain the behavioral regression tests.
