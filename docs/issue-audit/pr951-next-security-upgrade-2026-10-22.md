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
