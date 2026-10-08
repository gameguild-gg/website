# Local Development Cleanup and Recovery

This guide explains how to stop, diagnose, and recover the local environment
started by `pnpm run dev`. Run all commands from the repository root.

For the faster dynamic application environment, see
[`dev-fast.md`](./dev-fast.md).

## How the local environment works

`pnpm run dev` starts two groups of processes:

1. Docker Compose runs PostgreSQL, Redis, Garage, and `garage-init`;
2. the host machine runs the API, web application, and generated-client
   watchers.

| Component                  | Default execution     | Local port            |
| -------------------------- | --------------------- | --------------------- |
| Web                        | local Next.js process | `3000`                |
| API                        | local .NET process    | `8080`                |
| PostgreSQL                 | Docker Compose        | `5432`                |
| Redis                      | Docker Compose        | `6379`                |
| Garage S3                  | Docker Compose        | `3900`                |
| Garage RPC, web, and admin | Docker Compose        | `3901` through `3903` |

The Compose `api` and `web` containers belong to the `app` profile and are not
started by the standard `pnpm run dev` command. Do not run `pnpm run dev` and
`pnpm run dev:compose` at the same time because both may contend for ports
`3000` and `8080`.

## Normal shutdown

In the terminal running the environment, press `Ctrl+C` once and wait for the
`[dev] shutting down...` message. The orchestrator stops the API, web process,
and client watchers.

Infrastructure containers intentionally remain active to make the next startup
faster. To stop those containers as well, run:

```bash
pnpm run dev:stop
```

This command:

- asks a registered `dev` or `dev:fast` orchestrator to stop its complete
  process tree, including a build that has not opened its web port yet;
- waits for graceful shutdown and then stops any orphaned listeners on ports
  `3000` and `8080` as a fallback;
- runs `docker compose down --remove-orphans`;
- preserves local volumes and their data.

Avoid pressing `Ctrl+C` twice in quick succession. The second interrupt forces
an immediate exit and may prevent child processes from shutting down cleanly.

## Recommended recovery

Use the following sequence after a previous execution left orphaned processes,
after a large merge, or when `.next`, generated-client, or .NET artifacts have
become stale:

```bash
pnpm run dev:repair
pnpm run dev
```

`dev:repair` runs `dev:stop` and removes only regenerable artifacts:

- `.turbo`;
- `apps/web/.next`, `apps/web/.next-fast`, and `apps/web/.turbo`;
- `apps/web` `tsconfig*.tsbuildinfo` files;
- `packages/infrastructure/client/dist`, `dist-fast`, and its Turbo cache;
- API `bin` and `obj` directories.

It does not remove:

- `.env` files or local configuration;
- `node_modules`;
- Docker volumes;
- PostgreSQL, Redis, or Garage data;
- tracked files.

The first startup after a repair may take longer because the API, client, and
web application must be rebuilt.

## Full local data reset

Use this option only when all local data can be discarded:

```bash
pnpm run dev:reset:data
pnpm run dev
```

In addition to removing generated artifacts, this command runs
`docker compose down --remove-orphans --volumes` and deletes the local volumes
declared by this project, including:

- the PostgreSQL database;
- Redis state;
- Garage objects;
- local data-protection keys.

Port, cache, or compilation errors do not require a database reset. Inspect the
logs before deleting volumes because of an API error. Do not use a reset to
hide defects in migrations or the EF model.

## Reinstalling dependencies

If an error indicates missing or inconsistent dependencies after a lockfile
change, first run:

```bash
pnpm install --frozen-lockfile
```

If there is evidence that the installation itself is corrupted, perform a deep
cleanup:

```bash
pnpm run clean
pnpm install --frozen-lockfile
pnpm run dev
```

`pnpm run clean` removes `node_modules`, `dist`, `dist-fast`, `coverage`,
`build`, `.next`, and `.turbo` throughout the monorepo. It is more expensive
than `dev:repair` and should not be part of the normal daily workflow.

## Diagnostics

### Containers

```bash
pnpm run dev:compose:ps
docker compose -f compose.yaml ps -a
docker compose -f compose.yaml logs --tail=200 postgres redis garage garage-init
```

In the standard mode, the API runs on the host. Its errors therefore appear in
the `pnpm run dev` terminal, not in `docker compose logs api`.

### Ports

```bash
lsof -nP -iTCP:3000 -sTCP:LISTEN
lsof -nP -iTCP:8080 -sTCP:LISTEN
lsof -nP -iTCP:5432 -sTCP:LISTEN
lsof -nP -iTCP:6379 -sTCP:LISTEN
lsof -nP -iTCP:3900 -sTCP:LISTEN
```

To stop the host-side development environment without stopping Compose, run:

```bash
pnpm run kill:ports
```

The command first requests a graceful shutdown from a registered or discovered
`dev` or `dev:fast` orchestrator. It then checks each port independently,
restricts the fallback selection to TCP sockets in the `LISTEN` state, and
forces termination only when a process does not exit within the grace period.
Prefer `Ctrl+C` in the original terminal for a normal shutdown.

Do not automatically kill processes on the PostgreSQL, Redis, or Garage ports.
First determine whether a port belongs to this project's containers or to a
different local service.

### API and web health

```bash
curl -i http://localhost:8080/live
curl -i http://localhost:8080/health
curl -i http://localhost:3000/api/health
```

### External Compose network

Compose expects the external `web-development-public` network. Create it when
it does not exist:

```bash
docker network inspect web-development-public >/dev/null 2>&1 || \
  docker network create web-development-public
```

## Quick symptom guide

| Symptom                                                       | First action                                 |
| ------------------------------------------------------------- | -------------------------------------------- |
| `port 8080 is already in use`                                 | `pnpm run dev:repair`                        |
| Next.js reports an existing server on `3000`                  | `pnpm run dev:repair`                        |
| orphaned-container warning, such as an old MailHog container  | `pnpm run dev:stop`                          |
| stale route types in `.next/types`                            | `pnpm run dev:repair`                        |
| inconsistent generated client or `dist`                       | `pnpm run dev:repair`                        |
| dependencies do not match the lockfile                        | `pnpm install --frozen-lockfile`             |
| corrupted dependency installation                             | `pnpm run clean`, reinstall, and start again |
| disposable local schema is incompatible after inspecting logs | `pnpm run dev:reset:data`                    |
| Compose cannot find its external network                      | create `web-development-public`              |

## Commands to avoid

Do not use these commands indiscriminately:

```bash
git clean -fdX
docker system prune -a --volumes
```

`git clean -fdX` removes everything Git considers ignored. In this repository,
that may include `.env` files and code directories kept outside the current
index, not only caches. To inspect what would be removed without deleting it,
run:

```bash
git clean -ndX
```

`docker system prune -a --volumes` affects every project on the machine. For
this repository, use `dev:stop` and `dev:reset:data`, which limit the operation
to the Game Guild Compose project.

## Standard recovery sequence

Follow this order and continue only when the previous step did not solve the
problem:

1. run `pnpm run dev:stop`;
2. run `pnpm run dev:repair`, then `pnpm run dev`;
3. inspect logs, ports, and health endpoints;
4. run `pnpm install --frozen-lockfile` when dependencies changed;
5. run `pnpm run clean` only when the installation is corrupted;
6. run `pnpm run dev:reset:data` only after confirming that local data can be
   discarded and the problem is related to persisted local state.
