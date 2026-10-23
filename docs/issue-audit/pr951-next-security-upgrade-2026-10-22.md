# PR 951: Next.js security release

## Hosted failure

After the hydration correction was published at `c1a02d1738c97b94dbdca1e01805208fd425f7e0`,
the dependency audit stopped the session acceptance and Web jobs before they could
exercise the application. The complete audit from repository-policy job
`119992771715` identifies two new Next.js advisories, both affecting the locked
`16.3.8` release in the Web app and the Next demo:

- [GHSA-2464-fhr4-w9xw](https://github.com/vercel/next.js/security/advisories/GHSA-2464-fhr4-w9xw):
  the image optimization advisory. The existing Sharp `0.35.5` override meets the
  documented workaround, but the upstream fixed Next release also addresses it.
- [GHSA-p3m5-59mp-jv5r](https://github.com/vercel/next.js/security/advisories/GHSA-p3m5-59mp-jv5r):
  denial of service in App Router Server Actions. The maintainer requires an upgrade;
  the image dependency workaround does not resolve this separate vulnerability.

The checked repository-policy archive is artifact `12447396078`, SHA-256
`d0460664b1f2aca1db5583369823e219a308e3cc622db51fc9068047cab32d46`.

## Source correction

Upgrade the shared Next override and every matching lockfile resolution to
`16.4.1`, the fixed release named by both advisories. Its `@next/env` and eight SWC
platform packages are aligned to that release. The lockfile integrity values were
obtained from each exact version's npm registry metadata. Comparing old and new
manifests found only those internal version changes and the Sharp optional range
moving from `^0.35.4` to `^0.35.5`; the existing resolved Sharp version remains
`0.35.5`. Peer requirements, other dependencies and platform constraints match.

The audit validator continues to reject both advisories. No advisory allowlist,
severity threshold, workflow condition or test assertion is relaxed. Installed
dependency checks now verify every discovered Next consumer's release and the
Sharp and environment packages resolved from that consumer.

## Acceptance still required

The metadata edit was prepared without a local install, SDK, build, test, server,
browser, database or container. Hosted frozen installation must validate the lockfile;
dependency audit, Web tests/build, session HTTP/PostgreSQL/browser acceptance and
the remaining current-source checks must pass before merging. Existing results
from earlier heads are not acceptance of this correction. This PR does not close
the remaining scope of #166 or #278.

## Subsequent dependency audit on the composed source

The next source, `816126c1dff9a54c39ce83b57cd52c984327496e`, includes the
independently delivered SAML base and the hydration fixture correction. Codacy and
the PR agent approved that source. Repository-policy artifact `12450663653`
(SHA-256 `38c9c231ac19c960a4c6e18acd7ca82080e9b47a21c54c25557c284f781dfbc0`)
then identified seven additional advisories across three locked packages:

| Package | Previous | Fixed release | Advisory IDs |
| --- | --- | --- | --- |
| toml | 4.2.0 | [5.0.1](https://github.com/BinaryMuse/toml-node/releases/tag/v5.0.1) | GHSA-4qx8-w7c3-pj3p, GHSA-9j65-hcg3-gc64, GHSA-9qmh-m7c6-f92p |
| dompurify | 3.4.16 | [3.4.17](https://github.com/cure53/DOMPurify/releases/tag/3.4.17) | GHSA-cg2g-xh64-v4q7, GHSA-mrv2-3pqr-j5jr, GHSA-c6gr-qvv4-5v3f |
| ip-address | 10.7.2 | [10.7.4](https://github.com/beaugunderson/ip-address/security/advisories/GHSA-rxp7-6hxx-x8q5) | GHSA-rxp7-6hxx-x8q5 |

The shared overrides and all matching lock resolutions use these fixed versions,
with integrity values from each exact npm registry manifest. TOML and ip-address
add no dependencies and keep their Node requirements. DOMPurify declares
`@types/trusted-types` in both dependency sections; its optional declaration takes
precedence, preserving the existing optional `2.0.7` resolution. The two existing
verified local patches remain independently checked; no new exception is added.

TOML 5 deliberately rejects integers outside JavaScript's safe integer range unless
the caller opts into BigInt, and always rejects values outside signed 64-bit range.
The affected connection-profile consumer uses strings, booleans and ordinary
numeric settings. New hosted checks cover that shape, table arrays, dotted paths,
the explicit BigInt option and rejected oversized integers. Sanitizer checks cover
ordinary content, event-attribute removal and template removal; address checks
cover IPv4, IPv6, zones and malformed long input. These are bounded compatibility
and regression checks, not timing benchmarks or complete reproductions of every
upstream exploit. Seven audit-validator cases ensure the advisories still fail
closed even when unrelated local patches have been verified.

This follow-up was also prepared without executing dependencies or product tests
locally. Its own hosted frozen installation, audit, tests and composed session
acceptance must pass before delivery. Earlier results do not certify this source.

## Installed-consumer check correction

At `3055dd9760d2cfa4547217daf7c9d45fc3bbe04a`, the hosted frozen installation and
dependency audit completed. The subsequent installed-dependency suite passed 61
of 63 cases, including the four new TOML/DOMPurify/ip-address cases. The two Next
consumer cases failed with `ERR_PACKAGE_PATH_NOT_EXPORTED`: Sharp does not expose
the `sharp/package.json` subpath used by the assertion.

The correction resolves Sharp from each actual Next consumer and reads its public
[`versions`](https://sharp.pixelplumbing.com/api-utility/#versions) object. The
assertion still requires Sharp `0.35.5`, alongside Next and `@next/env` `16.4.1`.
The existing native SVG/librsvg checks remain intact. No version assertion is
removed or weakened, and no dependency or workflow setting changes in this fix.
It requires a fresh hosted result; no local product execution was performed.
