# emception

## 4.5.0

### Minor Changes

- [`3e585a6`](https://github.com/gameguild-gg/gameguild/commit/3e585a61e23ef9b8441240e241d6638255cbeadf) Thanks [@mathrmartins](https://github.com/mathrmartins)! - Publish a reproducible, lockfile-driven Emception Toolchain with verified build receipts, source provenance, Linux-only release automation, and a Browser manifest URL pinned to the matching Toolchain version.

## 4.0.0

### Major Changes

- # v4.0.0 — Major infrastructure and frontend rework

    Rewrote the Emception build pipeline, package layout, and frontend
    integrations. This is a synthetic breaking-change declaration — the
    3.x → 4.x major bump covers all changes accumulated since v3.11.0
    that were never tagged as breaking at the time.

    ## Breaking changes
    - Migrated the Emception package stack from npm to pnpm
    - Reorganized `tools/emception/packages/*` into independent publishable units
    - Reworked the worker boot chain and `createEmception()` surface
    - Frontend demos moved under `tools/emception/apps/*` with new names

    Consumers upgrading from 3.x should treat the public API surface as
    changed and re-pin to `emception@4.x` and the `@gameguild/emception-*` scope.
