# GameGuild

GameGuild is the open platform where game creators learn, build, and ship together: courses and learning paths, a in-browser code editor, project management, playtesting, and community — in one workspace.

<!-- TODO(#83): replace with a short product GIF showing courses, the code editor, and project management.
     <p align="center"><img src="docs/brand/gameguild-overview.gif" alt="GameGuild overview: courses, code editor, project management" width="880" /></p> -->

## Architecture

The repository is a pnpm monorepo with two applications plus shared tooling:

```text
gameguild/
├── apps/
│   ├── api/                     .NET 10 modular monolith (GameGuild.sln)
│   │   └── Source/
│   │       ├── GameGuild.API/             host composition root (DI, endpoints, security)
│   │       └── Modules/                   platform + domain modules
│   │           ├── GameGuild.Identity.*   authentication, authorization, users, tenants
│   │           ├── GameGuild.Compliance.Audit      central audit trail & evidence packaging
│   │           ├── GameGuild.Content.Pages         CMS pages, content resources, marketing leads
│   │           ├── GameGuild.Learning.*            courses, enrollments, certificates, paths
│   │           ├── GameGuild.Commerce.*            billing, orders, payments, products, plans
│   │           └── ...                    social, projects, teams, finance, analytics, …
│   └── web/                     Next.js App Router app (next-intl, Tailwind, Vitest)
├── packages/                    shared workspace packages (incl. @game-guild/ui)
├── packages/api-client/         generated API client (OpenAPI-driven)
├── tools/                       emception toolchain and build tooling
├── docs/                        architecture and feature documentation
├── e2e/                         Playwright end-to-end suites
└── compose.yaml                 local infrastructure (Postgres, Redis, Garage S3, app services)
```

Requests flow `web (Next.js) → api (ASP.NET Core modules) → PostgreSQL`, with Redis for caching/queues and Garage for S3-compatible object storage. Backend invariants (module boundaries, authorization, fail-closed auditing) are enforced at build time by the `GGARCH001–008` analyzers — see [AGENTS.md](AGENTS.md).

## Quick start

### Prerequisites

- [.NET SDK 10.0.103](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22](https://nodejs.org/) with [pnpm](https://pnpm.io/) 10 (`corepack enable`)
- [Docker](https://www.docker.com/) (Postgres 17, Redis 7, Garage S3 via compose)

### 1. Clone and install

```bash
git clone https://github.com/gameguild-gg/gameguild.git
cd gameguild
corepack enable
pnpm install
```

### 2. Start local infrastructure

```bash
docker compose -f compose.yaml up -d postgres redis garage garage-init openinary
```

This also provisions the Openinary asset-CDN container (`http://localhost:3003`).
It stores originals in the local Garage bucket and auto-creates an admin + API
key on first boot. To enable asset uploads through it:

```bash
# one-time: copy the generated API key from the first-boot log
docker compose -f compose.yaml logs openinary | grep "API Key:"
# paste it into .env as OPENINARY_UPLOAD_API_KEY=... (and Assets__Openinary__ApiKey for
# local `pnpm dev` runs), then restart the api if it's running
```

Without the key everything else works — uploads just fall back to the S3 path.

### 3. Restore and build the API

```bash
dotnet restore apps/api/GameGuild.sln
dotnet build apps/api/GameGuild.sln
```

### 4. Run both apps

Two terminals (or use the bundled orchestrator `pnpm dev`):

```bash
# terminal 1 — API on http://localhost:8080
pnpm dev:api

# terminal 2 — web on http://localhost:3000
pnpm dev:web
```

`pnpm dev` starts API + web + infrastructure together; `pnpm dev:compose` runs the full stack in Docker.

## Testing

The PR gates that CI enforces:

```bash
# API — build must be warning-clean (GGARCH analyzers are errors)
dotnet build apps/api/GameGuild.sln

# API — full test suite (or target a project under apps/api/tests/)
dotnet test apps/api/GameGuild.sln

# Web — unit/integration tests (Vitest)
pnpm -C apps/web test

# Web — production build
pnpm build:web

# Repo-wide lint & policy checks
pnpm lint
pnpm run ci:repository-policy
```

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow (branching, conventional commits, PR gates) and sign the [Contributor License Agreement](CLA.md) before your first change. This project follows the [Code of Conduct](CODE_OF_CONDUCT.md) — please keep interactions respectful.

## Links

- [CLA](CLA.md) · [Code of Conduct](CODE_OF_CONDUCT.md) · [Contributing guide](CONTRIBUTING.md)
- [Docs](docs/) — architecture, authorization, billing, brand
- [AGENTS.md](AGENTS.md) — engineering invariants enforced in this repo

## License

Proprietary — all rights reserved by the GameGuild maintainers. See the repository license headers for details.
