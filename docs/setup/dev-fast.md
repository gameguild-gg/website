# Fast Dynamic Development Environment

This guide describes `pnpm run dev:fast`, the preferred environment for
navigating and functionally testing the complete application with lower route
compilation latency.

Unlike the former selective production build, this mode does not maintain a
route allowlist. Any Next.js route can be opened and is compiled on demand.

## Starting

Run from the repository root:

```bash
pnpm run dev:fast
```

The command starts the same Docker infrastructure and watched .NET API used by
the regular development environment. The web application differs in these
ways:

- Turbopack replaces Webpack;
- compiled modules are persisted in `apps/web/.next-fast`;
- the existing React Compiler behavior is preserved;
- OpenAPI polling and the SDK declaration build are skipped;
- an isolated SDK runtime is built once in `dist-fast` without a permanent
  watcher or changes to the regular package output.

The first request to a route group performs its compilation. Later requests
reuse memory and filesystem caches. Routes that have never been visited remain
available and compile automatically when opened.

## Choosing A Mode

Use `pnpm run dev:fast` for regular frontend work and functional testing across
the application. Use `pnpm run dev` when validating Webpack-specific behavior,
or continuous OpenAPI regeneration and SDK rebuilding.

When an API contract changes during a fast-mode session, regenerate the client
and its runtime bundle explicitly:

```bash
pnpm --filter @game-guild/client run generate
pnpm --filter @game-guild/client run build:fast
```

Restart `dev:fast` after rebuilding the client because this mode intentionally
does not keep an SDK watcher alive.

## Assets

Fast mode avoids scanning the complete Emception asset tree on every startup.
It checks only for the published manifest and performs a full synchronization
when that manifest is absent. Run the following command after intentionally
changing the local Emception release:

```bash
pnpm --filter @game-guild/web run sync:emception
```

## Stopping And Cleaning

Press `Ctrl+C` once for a normal shutdown. To stop a detached environment and
its Docker infrastructure, run:

```bash
pnpm run dev:stop
```

To remove generated caches before restarting:

```bash
pnpm run dev:repair
pnpm run dev:fast
```

`dev:repair` removes both `.next` and `.next-fast`. It does not delete local
database or object-storage data. See
[`local-development-cleanup.md`](./local-development-cleanup.md) for the full
recovery procedure.
