# Legacy ESLint engine repairs

This is a hosted validation candidate for observed crashes in
`codacy/codacy-eslint:9.18.12`. The immutable Linux amd64 digest is pinned in the
Dockerfile and workflow. It does not change the active Codacy configuration,
replace the ESLint 9 root configuration, disable rules, exclude files, upload
issues, or certify issue closure.

The public tag was resolved to this digest on 2026-10-18. Cloud logs identify
the release version; the cloud runtime image digest was not exposed. Package
versions and file hashes must therefore match independently in hosted CI.

## Repairs

- TypeScript sort keys 3.2.0: keep ordering diagnostics when the upstream
  node/comment swap produces overlapping autofix ranges. Refuse only those
  invalid edits; ordinary interface and string-enum fixes remain available.
  Package version and both complete source hashes are checked before repair.
  The candidate contracts cover the observed source and comment overlap;
  hosted acceptance is still required.

- Import 2.29.1: variables without definitions no longer crash namespace-import
  detection. A missing binding still produces the diagnostic without an unsafe
  automatic fix. Existing definition/reference handling is retained.
- Shopify 45.0.0: move the existing message into `meta.messages`, where ESLint
  expects message IDs. The visitor and message text remain unchanged.
- Security Node 1.1.4: a `try/finally` statement has no catch handler. Guard that
  nullable handler and retain the existing missing-handler diagnostic.
- Shopify class properties: a TypeScript declared constructor has no body.
  Accept that declaration while retaining diagnostics on real assignments.
  Dynamic object spreads are not literal properties, and a hole in a literal
  array is not an AST node. Handle both without losing literal diagnostics.
- ES-X typed methods: a top-level exported function's declaration container is
  the parentless source-file root. Do not ask TypeScript for the type of that
  invalid location. The object-expression fallback and real Set method checks
  remain in place.
- React Native raw text: a template literal can contain no expressions. Use
  its literal text for the diagnostic and retain the allowed Text boundary.
- Flow type imports: a namespace specifier cannot be converted into an inline
  named type import. Retain the diagnostic without the invalid autofix.
- Lodash FP: use a dictionary without inherited object properties for imports.
  Calls such as `toString()` are not Lodash imports; genuine chains remain
  detectable.
- JSX accessibility: semantic-tag suggestions require a statically known string
  role. Retain string-role suggestions and separate invalid-role diagnostics.
- Perfectionist JSX sorting: retain the ordering diagnostic when generated fix
  ranges overlap. Normal non-overlapping fixes remain available.

All repaired package versions, complete original file hashes and unique repair
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

The exported `repositoryOptions` candidate removes the wrapper's inherited
directory/file ignores and uses the supported config-file inclusion pattern
`!**/*`. It retains the engine's ignore mechanism instead of relying on a
programmatic option that `.eslintrc` cannot declare. Its rule values and
existing context overrides remain in the received-pattern projection. The
workflow records every tracked Git path using NUL delimiters, selects supported
source extensions without path exclusions, and checks every selected path with
the actual ESLint API. Libraries, tests, examples, vendor sources and dotfolders
must remain admitted. This proves file admission, not completed parsing/linting,
correct framework context or issue closure. Other language scanners remain in
scope independently.

Node compatibility targets come from every tracked package manifest with an
explicit `engines.node`. The root currently requires Node20; the independently
published client retains its declared Node18 range. More specific declarations
take precedence, with no rule disabled. Contracts demonstrate the prior Node16
fallback warning for `node:test`, acceptance under Node20, the retained Node18
restriction and detection of a builtin newer than the Node20.0 minimum.

Flow rules use the plugin's documented `onlyFilesWithFlowAnnotation` setting.
Plain JavaScript executed directly by Node must not be required to contain Flow
type syntax. The static inventory at273d7196 found no Flow annotation tokens in
any of the3279 supported tracked sources. All rule IDs/options and every source
remain in the profile. Hosted controls reproduce the earlier requirement on
unannotated JavaScript, retain missing-type diagnostics after an explicit Flow
declaration, accept a typed declaration and retain malformed-header checks.

The separate complete-source pass installs the repository's frozen workspace
dependencies in hosted CI, then lints every selected file using the repaired
engine. Inline config comments cannot suppress requested diagnostics. Each
started/processed/crashed file is recorded and all remaining files are attempted
after a rule exception. Parser errors, missing rules, exceptions, diagnostics or
unreviewed tool output keep the pass failing. The 30-minute job budget is not a
latency guarantee; an incomplete pass cannot be accepted as a complete scan.
The analysis step has a shorter 20-minute limit, leaving time within the job
budget for the unconditional checkpoint artifact upload after a failure.

Eight independent partitions now cover the complete selected-source list by
index modulo eight. The earlier four-partition scan exceeded the analysis limit
in two partitions, including one generated file that took nearly eleven minutes.
No rule or source is removed and neither time limit is increased. Matrix
fail-fast is disabled so an error in one partition does not cancel the others. A partition can only
certify its own traversal; independent collection must derive the partition
count from the immutable workflow and verify all partitions have
the same source/configuration identity, cover the original list exactly once,
and satisfy the complete-analysis conditions before accepting a full scan.

A separate diagnostic matrix entry repeats the original eight actual files
and four subsequent crash inputs, requiring their immutable input hashes. It
retains only known exception reason codes and dependency frames, with no raw messages or source
frames. This sample cannot certify repository coverage and its counts overlap
the full partitions. The e30d4a35 diagnostic still failed in Import, Lodash FP,
Perfectionist and JSX accessibility. The following candidate changes require
their own hosted acceptance; a passing earlier contract suite does not certify
these changes.

The selected-source pass must resolve its plugins, parsers and import resolvers
from the pinned engine. Installing workspace dependencies on the hosted runner
must not silently substitute them for the engine's modules. Source-qualified
e30d4a35 failures entered the workspace's native `unrs-resolver`, which is absent
from the pinned image dependency lock. The candidate uses an explicit plugin
resolution root and hash/version-checked TypeScript/Vue parser and Node/TypeScript
resolver entrypoints. Existing resolver options, extension mappings, context
overrides and all rules remain present. Hosted controls require a real import
cycle and a missing export through a TypeScript alias to remain detectable,
with a valid alias access accepted.

The inherited JavaScript override disables typed TypeScript rules but leaves
other typed plugin rules enabled. The pinned image's `/tsconfig.json` already
allows JavaScript. An additional parser override supplies that same project for
JavaScript sources, retaining all inherited rule values. Hosted controls must
reproduce the missing-services exception, analyze the actual observed input,
detect a real ESTree-node property check and accept an ordinary object check.
The image config's complete source hash is checked independently in the contract.

The inherited TypeScript wildcard project omits the tracked dotfolder files.
The hosted pass now writes a separate project that extends the unchanged image
config and names every tracked supported source explicitly, with empty include
and exclude lists. Both its bytes and its complete file list must match the
immutable manifest. This also preserves explicit files in build directories and
JS/TS files with the same basename. The earlier omitted inputs stay in the
failed-contract evidence; no coverage assertion is removed.

The pinned parser refuses single-run mode whenever extra file extensions are
registered. Its inherited global JSON/Vue registrations therefore force ordinary
JS/TS into watch-program management. The immutable hosted analysis explicitly
requests single-run mode and clears that registration for standard JS/TS only.
Vue and JSM retain their required watch mode, and JSON imports remain supported
by the TypeScript project's `resolveJsonModule`. Hosted controls and per-extension
mode probes must verify these boundaries; runtime improvement still requires
the actual complete-source pass. No time limit or analysis input is removed.

The standard-source registration is cleared in the new base profile itself.
ESLint8 merges parser-option arrays across overrides, so appending an empty
array cannot clear a nonempty earlier registration. Vue's inherited override
and a separate JSM override retain their own custom extensions. The failing
37-case candidate is retained as evidence of that merge behavior; its mode
and JSON-import assertions remain unchanged.

The hosted pass requires the tracked working tree to match its checkout after
dependency installation and again after analysis. It records the SHA256 and
byte length of each actual input buffer before linting it. Independent evidence
collection must compare those buffers with immutable Git blobs, together with
the checkout tree and candidate head; a matching HEAD alone is insufficient.

Public analysis artifacts contain rule IDs, categories and code-quality
locations without messages, snippets or fixes. Security results have aggregate
rule counts only. Raw tool output is disposable and is not uploaded. Detailed
security evidence and remediation decisions remain in private managed storage.

Future passes also retain output provenance: stream byte counts and hashes,
dependency-only caller frames and known Node deprecation codes. They retain no
output text, exception messages or repository source frames. Original writes,
callbacks and backpressure are forwarded. This identifies the emitting package
without suppressing output; any unreviewed bytes still block certification.

Some rules report invalid options as ordinary diagnostics. Known missing-option
errors from `jsdoc/match-name` and `jsdoc/text-escaping` remain in the diagnostic
stream and also block analysis certification as configuration errors. A missing
source `@param` remains a documentation diagnostic. Correcting these options is
separate work; counting the invalid-option messages does not repair them.

The completed 3,280-source pass exposed an additional `vue/max-len` failure on
the large generated TypeScript declarations. That rule passes complete token
and comment arrays as function arguments, exceeding V8's argument limit.
The repair appends individual items in the original order for all six script,
template and comment combinations. Hosted controls require line-length and
comment diagnostics on large inputs and ordinary short inputs in both template
orders. No generated source, rule, input or threshold is removed.
The comment control explicitly sets the 80-character comment limit so the
comment collection branch executes. The controls use 300,000 comments and
150,000 template elements to exercise the large argument arrays. The unmodified
image must reproduce the overflow before the repaired image is required to emit
the same 80-character-limit diagnostic and accept short input.
An independent byte-replacement control checks all pinned original and repaired
source hashes, preserving untouched UTF-8 bytes. It catches encoding changes in
review artifacts before the image build; the original package and target hashes
remain mandatory.

## Evidence

All thirteen historical crash inputs use a separate tracked `fixtureFile`, copied
byte-for-byte from their recorded `observedAtHead`. Their original hashes remain
unchanged. The original `file` remains the ESLint filename and stays in every
complete source pass. This preserves the original hash and parser context when
normal product fixes change that file. The contracts require unique fixture paths
and verify that these `.txt` copies do not enter current source coverage. The
overlapping diagnostic sample records both paths and validates the fixture bytes;
its results cannot certify current repository coverage. No rule, hash assertion,
source partition, or current product input is removed.

The `Codacy legacy engine contracts` workflow runs in hosted GitHub Actions.
It retains original/repaired TAP output, repair hashes, engine digest, source
head, checkout, tree and profile digest. No local container or product tests
are needed to prepare this candidate.

Upstream sources: [Codacy ESLint](https://github.com/codacy/codacy-eslint/tree/2c47ef11d8e57a327dabb82a9a05a278dbd30ed8),
[Import](https://github.com/un-es/eslint-plugin-i),
[Shopify](https://github.com/Shopify/web-configs), and
[Security Node](https://github.com/gkouziik/eslint-plugin-security-node).
