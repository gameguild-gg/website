# Password hashing and policy reconciliation — #254 and #255

## Provenance and baseline

The original description was empty. The original title requests password hashing
and verification; the historical closeout also claimed configurable BCrypt cost
and upgrade support. The independent source review reopened #254. Its full history
and #255's separate policy requirements remain preserved.

[Executed baseline](https://github.com/gameguild-gg/gameguild/issues/254#issuecomment-5987292416)
at `11b41bf063181ab821368351be7afe3cff98c53a` failed eight assertions: configured
cost and upgrade decisions, malformed-hash classification, distinct suffixes after
72 UTF-8 bytes and four pre-cancelled operations. Three positive controls passed.
Both collision inputs met the existing 128-character policy. The executable linked
the real source; it did not call HTTP, persistence or external providers.

## Decisions

- Preserve BCrypt for passwords at or below 72 UTF-8 bytes. Read validated
  `BCryptWorkFactor` from shared presentation policy, then the two legacy policy
  locations. Default remains 12; supported generation range is 10–16. Existing
  verification accepts costs 04–16; costs above 16 are rejected rather than doing
  unbounded expensive work. Any imported higher-cost hashes require recovery or
  an explicitly designed migration; no production inventory is claimed. Existing
  supported lower-cost hashes still verify and are flagged for upgrade. Detection
  does not claim that login automatically rewrites hashes.
- Longer new passwords use salted PBKDF2-HMAC-SHA256, 600,000 iterations, 128-bit
  random salt and 256-bit output. The stored modular format carries the algorithm,
  iteration count, salt and hash. It uses the entire UTF-8 input and constant-time
  comparison, with strict format and parameter validation. No pepper or external
  key configuration is introduced. See the [OWASP password storage guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
- A legacy BCrypt hash cannot establish an original suffix beyond its byte limit.
  Long input against that format fails authentication and requires password reset
  to a full-length hash. The historical suffix cannot be restored from the hash.
  A valid 72-byte prefix can still match a legacy hash; without historical length
  metadata it is indistinguishable from an originally 72-byte password. This is
  an explicit legacy limitation, not proof that historical passwords were full
  length. No production data inspection or forced account migration is claimed.
- Password history conservatively rejects long candidates matching the truncated
  legacy prefix, while authentication rejects those long inputs. This avoids
  weakening existing reuse protection during recovery. New full-length hashes
  distinguish their entire input.
- Reject malformed/unsupported hashes without performing attacker-chosen enormous
  work. Async operations check cancellation before and after their synchronous
  computation; cancellation cannot interrupt an already running BCL KDF.
- Keep existing user storage, routes and numeric/password-policy wire contracts.
  Typed configuration exposes the additive cost option. The hasher is not a
  password-policy evaluator during hashing; writing callers retain their policy
  validation, history checks, original-hash guard and session handling.

## Verification status

The initial focused 100-case run is history, not additional coverage. On the
isolated password branch before incorporating PR #686, **5,248 distinct local
cases** passed: authentication 2,091 (31 new), authorization 1,667, SharedKernel
1,371, selected API architecture/security/bearer 106 and PostgreSQL/HTTP 13 (all
new). Repeated runs are not added. The configured full Release solution build
had zero warnings/errors; the final extra scoring test's project then rebuilt
warning/error clean and the full authentication suite passed again.

Actual signup persists different salted hashes for equal passwords; short hashes
carry configured cost 10 and long hashes preserve their entire input. Login
accepts a correct legacy password, rejects incorrect/malformed hashes and rejects
an alternate long suffix. Authenticated change preserves state on a wrong current
password, writes the new full-length hash and retains history. Real reset-token
validation writes the new hash and rejects replay. Ambiguous legacy long input
requires recovery; changing only its suffix cannot evade history rejection.

The complete pre-MFA OpenAPI remains unchanged at 1,296 paths / 1,654 schemas.

After rebasing onto merged PR #686 (`8e1266698`), the combined source revision
`8c7da57a15c26c04ff732ea183416e0ee396b5bc` passed the following fresh checks:

| Suite | Passed cases |
|---|---:|
| Authentication | 2,114 |
| Authorization | 1,667 |
| SharedKernel | 1,371 |
| Selected API architecture/security/bearer | 106 |
| PostgreSQL/HTTP and OpenAPI | 36 |
| Client | 1,115 |
| **Distinct combined local cases** | **6,409** |

The 36 integration cases include 13 new password cases, nine MFA regressions and
14 OpenAPI cases. The 44 new password cases are included in these totals. Earlier
and repeated runs are not added. The full Release solution build passed with zero
warnings/errors. Authentication and SharedKernel assembly copies used by the
test projects match the API output. EF reports no pending model changes.

The entire OpenAPI document equals the accepted MFA export, including all 1,296
paths and 1,654 schemas. Force client regeneration, semantic diff, typecheck and
all 1,115 client cases passed. Generated files only changed their timestamp or
line endings; those regeneration artifacts were restored after comparison.
Local receipts are retained under
`artifacts/test-results/issue-254-password-20261004/local-proof-combined.json`.

[PR #687](https://github.com/gameguild-gg/gameguild/pull/687) targets `develop`.
Its initial Codacy run reported 22 items: brace style, a throwing cost-property
getter and seven alerts interpreting public password-operation route literals as
credentials. The follow-up adds explicit blocks, makes cost validation a method
and derives test endpoint paths from the actual controller's public route
metadata. No scanner rule or gate is suppressed. The full solution rebuilt with
zero warnings/errors, and all 5,294 .NET cases passed again, including all 36
integration/OpenAPI cases. The narrower initial 26-case API selection is retained
but excluded in favor of the complete accepted 106-case selection. Repeated runs
are not added to the 6,409 local total.

## Final acceptance

[PR #687](https://github.com/gameguild-gg/gameguild/pull/687) merged into `develop`
at `abd7ca4f4adafdb76315ce839c1862ee2ee142ae` on 2026-10-05. All applicable
checks, Codacy and four CodeQL analyses passed at accepted head
`afb2090700a020ed7d276e8322ef39ab8d747131`. **#254/#255 are officially
CLOSED/COMPLETED**, with separate [hashing acceptance](https://github.com/gameguild-gg/gameguild/issues/254#issuecomment-5988318761)
and [policy acceptance](https://github.com/gameguild-gg/gameguild/issues/255#issuecomment-5988319209).

Final local coverage is **7,480 distinct cases**: Authentication 2,114,
Authorization 1,667, SharedKernel 1,371, full API unit 1,049, full API integration
164 and client 1,115. Full suites replace their earlier subsets. Case identity
uses definition ID plus display name because the adapter reuses one ID across
five distinct exception/status cases. Prior/repeated/failed runs are not added.
CI selected suites passed **3,649 principal cases** (164 integration, 2,114
Authentication and 1,371 SharedKernel), plus 14 repeated OpenAPI executions.
Full API unit is local evidence, not claimed in this CI selection. Both CI builds
passed with zero warnings/errors. Local complete receipts are retained in
`artifacts/test-results/issue-254-password-20261004/local-proof-full-api.json`.

The `ea329eb9b` CI integration run passed 163 of 164 cases and exposed an existing
shared-fixture assumption in the data-masking test: it searched for newly seeded
users only within the first 20 rows of the collection-wide database. Its follow-up
queries each unique fixture marker through the existing list/search endpoint and
asserts exactly one correct user, retaining both tenant masking assertions. The
integration test project rebuild passed with zero warnings/errors. Full local API
integration (164) and unit (1,049) suites then passed without skips; matching-head
CI integration also passed all 164 cases. The failed CI run is retained and
excluded from accepted totals.
Configured external email delivery is separate
acceptance under #223/#253 and is not established by a synthetic reset-token fixture.

## #255 policy provenance and coverage

The original #255 body was empty; its title requests strength and complexity
rules. Its preserved source review requires current weak/strong/common/length/
sequence checks and enforcement at writing boundaries. Existing freshly executed
tests cover missing uppercase/lowercase/digit/special, common-password rejection,
empty/short input, valid strength and shared/legacy configuration precedence.
The new independent score control uses random distinct character classes and
known added sequence/repetition patterns: expected scores are 100/90/90/80.
This is a heuristic and never a measured entropy or crack-time guarantee.

New HTTP cases reject weak passwords at signup, authenticated change and reset,
preserving the original stored hash. Weak reset rejection does not consume the
real reset token. A configured minimum of 14 rejects length 13 and accepts 14
when composition requirements are disabled; the host's resolved configuration
is checked. These are configured test-host observations, not inspection or
certification of production policy or compromised-credential feeds.
