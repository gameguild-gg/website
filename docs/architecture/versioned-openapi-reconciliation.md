# Versioned OpenAPI reconciliation: #144 and #147

## Scope and provenance

Issues [#272](https://github.com/gameguild-gg/gameguild/issues/272) and
[#275](https://github.com/gameguild-gg/gameguild/issues/275) repeat the complete
original descriptions of [#144](https://github.com/gameguild-gg/gameguild/issues/144)
and [#147](https://github.com/gameguild-gg/gameguild/issues/147), respectively.
The comparison includes all 12 functional criteria, technical requirements,
dependencies and Definition of Done before the appended source review.
Both duplicates are native GitHub DUPLICATE links. The canonical issues were
reopened and retain all requirements and historical evidence. Consolidation
does not establish implementation acceptance.

Swagger registered explorer group names but selected controller operations
using only the first version attribute and its major number. Fresh regressions
reproduced missing minor, patch, prerelease, date and custom-format documents.
HTTP tests also reproduced duplicate registration when a controller group was
shared by several versions. Failed runs are retained and excluded from
successful evidence.

## Document names and operation selection

`VersionedOpenApiDocumentCatalog` creates global documents for each discovered
full API version, formatted with `ApiVersioning:GroupNameFormat`.
`VersionedOpenApiDocumentSelector` uses the explorer's resolved version and
action mapping metadata. Manually constructed MVC descriptions fall back to
all controller declarations and explicit action mappings. Version-neutral
actions appear in the appropriate discovered documents; unknown names do not
match controller declarations.

The default `'v'VVV` preserves numeric names such as `v1`, `v1.1`, `v2`,
`v1.2.3` and `v1.2.4-beta.1`. For concurrent date-based versions, use a format
including the date, such as `'v'GVVV`, producing `v2026-10-04`. Formats that
collapse different discovered versions into one name fail with a configuration
error instead of publishing ambiguous documents.

Semantic formatting uses the native tokenizer for quoted literals, repeated
tokens and date/component formatting. `VVV`, `VVVV`, `F`, `FF` and the default
include the complete semantic version; `V`, `VV`, minor and padded tokens retain
their component meanings. `S` exposes the real prerelease, never the internal
patch identity. Numeric and hyphenated SemVer prerelease labels are accepted.
For example, `'Version-'GVVV` creates `Version-1.2.3` for a semantic version and
`Version-2026-10-04` for a date version. Group-date tokens emit nothing for a
numeric version. Literal-only or component formats that collapse distinct
discovered semantic versions are rejected by the document catalog. HTTP tests
use independently written expected names rather than computing them with the
production formatter.

The `ISpanFormattable.TryFormat` override follows the same semantic formatting
contract as `ToString`, including interpolation. A buffer that is too small
returns `false`, reports zero written characters and retains its original
contents. Tests compare both interfaces to independent expected strings and
check exact semantic version paths in the serialized HTTP documents.

Custom controller groups also receive scoped aliases:

- A single-version group keeps its existing unambiguous name, such as
  `/swagger/Reporting/swagger.json`.
- A group shared by versions gets names such as `Administration.v1` and
  `Administration.v1.1`.
- Canonical version names are reserved for global documents. Alias collisions
  receive deterministic numeric suffixes; existing unambiguous names are
  reserved first.
- Aliases contain their group's matching actions plus version-neutral actions;
  global documents contain all groups for that version.

Swagger registration and UI use the same catalog. Configured locales are
appended to each base name, including aliases. Tests retrieve actual HTML and
`index.js`, check each configured URL and request each linked JSON document.
API routes and authorization rules are unchanged by document selection.

## Original functional criteria: #144

| Criterion | Implementation and verification |
| --- | --- |
| URL versioning | Typed readers; semantic routing and version-document HTTP tests. |
| Header versioning | Routed readers, including `X-API-Version` and malformed/unsupported input. |
| Query versioning | Configurable reader; routed `?version=2.0` case. |
| Default when unspecified | Validated options; HTTP fallback enabled/disabled cases. Required URL segments remain required. |
| Semantic Major.Minor.Patch | Parser/options tests, patch routing and serialized patch documents. |
| Date YYYY-MM-DD | Parser/options, date routing/documents and concurrent date catalog selection. |
| Deprecation and sunset | Configured policies; routed supported/deprecated, Sunset and policy-link assertions. |
| Routing to handlers | Reader/routing tests; six distinct action mappings return expected HTTP payloads. |
| Compatibility matrix | Validated matrix and routed `X-API-Compatible-Versions` checks; compatibility declarations do not manufacture compatible contracts. |
| OpenAPI integration | Catalog/selector, serialized version/locale documents and all linked UI URLs. |
| Format consistency | SharedKernel validation/parser cases and rejection of colliding document formats. |
| Prerelease alpha/beta/rc | Parser validation plus HTTP beta mapping/document checks. |

The technical requirements also retain DI registration, concurrent/combined
readers, structured usage logs and bounded version-adoption metrics. Routed
telemetry assertions verify measurements. Configuration, migration guidance
and reproducible version-resolution performance fixtures are documented in
[api-versioning.md](api-versioning.md).

## Original functional criteria: #147

| Criterion | Implementation and verification |
| --- | --- |
| Title/description/version/contact | Typed validation and `OpenApiSetupTests`. |
| JWT Bearer/OAuth2/API Key | Security definition, mapping and SharedKernel consistency tests; HTTP Basic is also supported. |
| Environment servers | Validated URLs/variables, document-filter assertions and environment configuration overlays. |
| Data annotations | Serialized required/minLength/maxLength/pattern HTTP schema checks. |
| All-model examples/descriptions | Full generated-schema traversal and complete-application HTTP coverage; curated examples and structural fallbacks are documented separately. |
| Swagger UI appearance/behavior | Typed UI options, actual HTML/JavaScript and every linked document. |
| OpenAPI 3.0+ | Serialized version assertions in configured, complete and versioned HTTP documents. |
| Domain extensions | Validated structured JSON and serialized configured-filter cases. |
| Languages | Configured metadata/tag/operation/schema/property translation and fallback; version/locale HTTP combinations. |
| Version-specific documents | Full-version/action mapping, alias scoping/collisions and route-to-document correspondence. |
| Endpoint security mapping | Explicit schemes, default Bearer alternatives and anonymous requirement removal. |
| Operation/schema filters | Registered and executed module-tag, response-content, enum/learning-contract, XML, composition and configured transformations. |

Swashbuckle integration, startup validation, multiple documents and inherited
schema properties are checked separately. `ActivitySettings` retains its union
and discriminator. Synthetic fallback examples describe wire structure; they
do not promise valid business input for every domain. Typed configuration
supports curated examples. [openapi-options.md](openapi-options.md) documents
configuration migration and a reproducible full-document performance fixture
with dated benchmark baselines.

## Verification and closeout

Local evidence is retained under
`artifacts/test-results/api-foundation-reconciliation-20261004` in the isolated
worktree. The initial ten selector cases failed against unchanged develop.
Initial HTTP tests exposed the shared-group collision. The corrected selector,
catalog and HTTP/UI suite passed 21 cases before the final expanded run;
468 SharedKernel configuration cases passed. API and edited test-project builds
were warning clean. Interrupted, failed and diagnostic-helper attempts are
excluded from successful evidence.

The complete captured `v1` document is semantically identical to the merged
PR #678 capture: 1,296 paths and 1,654 schemas, canonical JSON SHA-256
`45b42923086a1f05d6a0288c161039965f8b06d92c2fc661232c78be9972d454`.
The generator detected an unchanged specification; `generate:diff` and client
TypeScript checks passed. No generated-source update is needed.

PR [#679](https://github.com/gameguild-gg/gameguild/pull/679) merged at
`d4d3ce14f3abd0f3423f82f34fb6b65f4bc70d67`. The complete solution built with
zero warnings/errors; 1,014 API unit tests and 14 complete-application OpenAPI
HTTP tests passed locally. Exact-head CI passed those 1,014 API cases and 1,340
SharedKernel cases, plus OpenAPI consistency, all required gates, Codacy and all
CodeQL analyses. All 55 primary-checkout local files retained their contents and
statuses, and the merged remote feature branch was removed.

Subsequent review reproduced a separate pre-existing formatter gap: quoted
`Version-` was treated as a version token, group-date tokens leaked into numeric
document names, and literal/component-only formats were ignored. Eighteen of
26 independently specified formatting regressions failed before the correction.
The follow-up uses the native tokenizer and adds semantic collision, legal
prerelease and independent HTTP/UI name checks. At that intermediate snapshot,
the full original canonical scopes remained
open pending verification and merge of the final span-format follow-up.
The final acceptance below supersedes that pending state.

PR [#680](https://github.com/gameguild-gg/gameguild/pull/680) merged at
`f0411fbaa3e2dff76bc4c27225980da1df34e6b7`. Exact-head CI passed 1,022 API unit
cases, 1,370 SharedKernel cases and 14 complete-application OpenAPI HTTP cases
(2,406 total), with zero-warning/error API and integration builds and all
required/security checks accepted. The full HTTP selection is now part of the
API gate and its TRX is uploaded. Local corrected document/reader cases passed
42/42 and SharedKernel passed 1,370/1,370. The full v1 document and generated
client remain unchanged. Local full dependency-build/HTTP attempts that exited
without successful results are excluded; the accepted full HTTP result is CI.

The final review independently reproduced the inherited span/interpolation
gap: 19 of 31 cases failed against unchanged merged #680 code, including
exposure of encoded patch identity and partial writes on insufficient buffers.
The final follow-up aligns the native span interface with semantic `ToString`
and adds exact semantic path assertions. Canonical closure required its fresh
verification and merge, subsequently
completed below.

### Final accepted head and official resolution

PR [#681](https://github.com/gameguild-gg/gameguild/pull/681) merged at
`92842cdd731bd77ee329073eed33c70287f87508` on `2026-10-04T20:19:47Z`. Exact head
`09c838b2a636aa75321145671c8a72d3f689e497` passed 1,022 API unit, 1,371 SharedKernel and 14
complete-application OpenAPI HTTP cases (**2,407 total**). Fresh Release API
and integration builds reported zero warnings/errors. All required gates,
OpenAPI client consistency, Codacy and all four CodeQL analyses passed.
[CI API/HTTP evidence](https://github.com/gameguild-gg/gameguild/actions/runs/37230473447/job/111518939762)
includes the full-application HTTP TRX artifact. Local SharedKernel passed
1,371/1,371 and the final selected API document/reader suite passed 42/42.
The final full-document capture equals the preserved baseline and client
generation/diff/typecheck passed. All 55 primary local files were unchanged.

#144 was officially closed COMPLETED at `2026-10-04T20:20:43Z`;
#147 at `2026-10-04T20:20:54Z`. Their
[versioning acceptance comment](https://github.com/gameguild-gg/gameguild/issues/144#issuecomment-5984012777) and
[OpenAPI acceptance comment](https://github.com/gameguild-gg/gameguild/issues/147#issuecomment-5984014026) preserve the complete original
scope and distinguish native duplicate consolidation from implementation.
All 328 original matrix requirement fields and scope IDs remain preserved.
This completes these two canonical requirements, not the whole issue program.
