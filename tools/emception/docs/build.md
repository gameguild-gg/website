# Building from source

Most consumers install `@gameguild/emception-browser`; its default manifest is
the matching, versioned `@gameguild/emception-toolchain` release. Build from
source when changing Emscripten/LLVM/Python, generated glue patches, libraries,
or artifact packaging.

## Supported build environment

The release gate runs only on Linux (`ubuntu-latest`). Local macOS arm64 builds
run the same locked maintenance pipeline and are supported for validation, but
only the Linux gate is a release guarantee. Local Windows builds are useful
during development, but they are best-effort.

Linux prerequisites are Node.js, pnpm, host Python 3, CMake, Ninja, curl,
Brotli, and a C/C++ build environment. The locked EMSDK is downloaded by the
Toolchain itself.

On macOS arm64, install Xcode Command Line Tools, Node.js, pnpm, and the host
build tools before running the pipeline:

```bash
xcode-select --install
brew install cmake ninja brotli
```

Xcode supplies Clang and the system curl. The locked EMSDK supplies its own
Node.js and Python runtimes after the host bootstrap completes.

## Quick build

```bash
pnpm install --frozen-lockfile --ignore-scripts
pnpm --dir tools/emception toolchain doctor
pnpm --dir tools/emception toolchain build all
pnpm --dir tools/emception toolchain release
```

The initial toolchain build is large. Package-only work can use:

```bash
pnpm --dir tools/emception run test:packages
pnpm --dir tools/emception run typecheck:packages
pnpm --dir tools/emception run build:packages
```

### Graphics capability notes

SDL3 is supplied by the version-pinned Emscripten port, which upstream still
classifies as experimental. The build reports that status once and suppresses
the port driver's repeated experimental diagnostic.

The Allegro WebAssembly recipe builds the SDL/OpenGL ES core plus the image,
font, audio, acodec, color, primitives, and memfile addons. Optional native
dependencies that are not shipped in the browser sysroot are disabled
explicitly: FreeImage, PNG, JPEG, WebP, FreeType/TTF, OpenAL/OpenSL, FLAC,
DUMB, OpenMPT, Vorbis, Opus, and MP3. CMake's `NO` capability summary for
these libraries is expected and is not a host prerequisite failure.

## Version ownership

`toolchain/toolchain.config.json` contains policy: Browser ABI, allowed release
channels, CMake constraints, EMSDK component grouping, and overlay ownership.

`toolchain/toolchain.lock.json` contains resolved versions only: immutable
archive URL or commit, SHA-256, EMSDK revision, and workspace content hash.
Builds never search for a latest release and do not accept per-tool version
environment overrides.

Useful maintenance commands:

```bash
pnpm --dir tools/emception toolchain outdated all
pnpm --dir tools/emception toolchain update cmake latest --dry-run
pnpm --dir tools/emception toolchain update cmake latest --verify
pnpm --dir tools/emception toolchain update emsdk latest --verify
```

An EMSDK update resolves LLVM, Binaryen, Python, and SDL together. `--verify`
writes a candidate lock under the disposable cache, builds the affected recipe
graph, applies patches, and replaces the tracked lock atomically only after the
verification succeeds.

Changesets advances the seven public package versions together. The version
preparation command then aligns internal runtime peer minimums with that release
version before generating the pnpm lock. External peers and optional peer
metadata retain their existing contracts. Inconsistent public package versions
stop this synchronization before any manifest is written.

### Reviewing an upstream archive checksum change

GitHub source archives can change bytes for the same commit. Besides compression,
Git `export-subst` can render a different abbreviated hash as the upstream
repository grows. A digest absent from the committed lock still fails before
extraction; builds never replace the lock or accept a checksum automatically.
Review the complete contents against the pinned commit before updating the lock.
For an immutable Git archive, a reviewed equivalent export may be recorded in
`reviewedArchiveVariants`, with its exact SHA-256, Git tree and review evidence.
Ordinary release archives do not support this field. Source receipts record the
actual downloaded digest, including when it is a reviewed variant.
Release `sourceProvenance` retains the primary lock hash and all reviewed variants;
it describes the allowed source identities, while the materialized source receipt
identifies which archive was actually used.
See [GitHub's source archive documentation](https://docs.github.com/en/repositories/working-with-files/using-files/downloading-source-code-archives).

The 2026-10-06 LLVM review preserved commit
`7b58716d96c3ae4c0c4e6f72e29b16137bb6224b`, version `23.0.0git`, and its EMSDK
ownership. Comparing the previous archive with the archive downloaded by the CI
verified 175,014 files. Only `clang/bindings/python/.git_archival.txt` differed:
the describe suffix grew from `g7b58716d96c3a` to `g7b58716d96c3ae4`.
Accounting for that declared export substitution and six CRLF test fixtures
reconstructed the upstream Git tree `34cd378b6fba47d5bd42003cdaf9505179879004`.
The primary reviewed archive SHA-256 is
`0d7fb3e45c6d2916d4ad4613fc3ee099076d088c13e5ca4b9562bab83319a4a5`.
The subsequent CI run served the previous equivalent export instead, with SHA-256
`0791c69319e1861e86ccb438d10b1b1816987f2d99b13d7e95ef08f88f0cedf8`.
Both reviewed exports are pinned for this commit. Their comparison, original Git
blobs and tree identity are recorded in
[the LLVM archive review](../toolchain/reviews/llvm-7b58716d96c3ae4c0c4e6f72e29b16137bb6224b.json).
Any third digest requires a new review; it is rejected even if the URL still names
the same commit.

## Directory ownership

```text
toolchain/
├── toolchain.config.json       # tracked policy
├── toolchain.lock.json         # tracked immutable sources
└── overlays/                   # tracked patches and project-owned code

.cache/toolchain/               # disposable downloads, sources and builds
artifacts/toolchain/            # canonical tools, sysroot, receipts and release
packages/toolchain/cdn/         # npm staging only
packages/core/cdn/              # deprecated compatibility copy
public/cdn/                     # demos and GitHub Pages only
```

Use scoped cleanup instead of deleting directories by hand:

```bash
pnpm --dir tools/emception toolchain clean artifacts
pnpm --dir tools/emception toolchain clean cache
pnpm --dir tools/emception toolchain clean all
```

These commands do not remove `node_modules`, tracked overlays, or external
checkouts outside the Toolchain cache.

## Canonical build and release pipeline

| Phase | Scripts | Output / invariant |
| --- | --- | --- |
| Resolve | lockfile and source manager | checksum-verified sources in the disposable cache |
| Build | named recipes | tools and mutable sysroot under `artifacts/toolchain` |
| Receipt | receipt engine | lock, recipe, overlay, dependency, command, version, and output hashes |
| Freeze | staging recipe | immutable sysroot snapshot with content fingerprint |
| Patch | glue recipe | versioned patches applied only to staged generated glue |
| Manifest | manifest recipe | schema-v2 metadata populated from actual receipts |
| Bundle | bundle recipe | deterministic Brotli archives and hashes |
| Stage | release recipe | npm Toolchain, Core compatibility, and public CDN copies |

`toolchain build` reuses an output only when its receipt identity matches and
every output hash is intact. `toolchain release` validates those dependencies,
then always regenerates the staged snapshot, glue, manifest, bundles, and
publish directories.

## Generated glue policy

Emscripten glue is generated code and may change when the SDK changes. All
required adaptations are centralized in `scripts/lib/glue-patches.mjs` and
executed by `patch:glue`.

The patcher verifies known source markers and matching `.mjs`/`.wasm` pairs. A
missing pair or an unknown generated shape fails the build instead of producing
a release that the IDE would need to repair at runtime.

## Manifest v2 and ABI profiles

The manifest records:

- package artifact version and complete build fingerprint;
- Browser runtime ABI and glue patch-set version;
- versions detected by the recipes, lock hash, receipt hash, and source
  provenance;
- hashes, sizes, executability, and bundle placement for every file;
- each WASM profile's matching glue path, WASM path, imports, exports, and
  pair-derived profile hash.

The Browser validates this metadata before boot. This is the executable
boundary between a generated toolchain release and package code.

## Tool build flags

Each tool is standalone; Emception does not use Emscripten `MAIN_MODULE` /
`SIDE_MODULE`. Tools use modularized ES modules, explicit filesystem/runtime
exports, Asyncify for browser I/O, and isolated linear memory.

## Release ownership

`@gameguild/emception-toolchain` is the canonical artifact package. The
`emception/cdn` copy is a compatibility bridge and must not become a second
independently generated release. Both npm copies are staged byte-for-byte from
the same canonical artifact release.

## Versioning and publication

The seven public packages form one Changesets fixed group. An implementation
PR carries an explicit changeset; after it is merged, the Linux workflow opens
a version PR. That PR is rebuilt using the final version. Once merged, the
workflow packs every package, publishes `@gameguild/emception-toolchain`, waits
for the registry, publishes consumers in dependency order, and creates
`emception-vX.Y.Z`.

The release gate requires package versions, `manifest.artifactVersion`, and the
version embedded in the Browser default manifest URL to be identical.
