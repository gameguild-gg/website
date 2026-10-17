# Contributing to GameGuild

Thanks for your interest in making GameGuild better. This guide covers the practical steps for contributing code, docs, and issues.

## Getting started

1. **Fork and clone** the repository.
2. **Install tooling**: .NET SDK 10.0.103, Node.js 22 with pnpm 10 (`corepack enable`), and Docker.
3. **Set up the workspace**:

   ```bash
   pnpm install
   docker compose -f compose.yaml up -d postgres redis garage
   dotnet restore apps/api/GameGuild.sln
   ```

4. Read [README.md](README.md) for the architecture overview and [AGENTS.md](AGENTS.md) for the engineering invariants that CI enforces.

## Contributor License Agreement

Before we can merge your first contribution, please review and sign the [Contributor License Agreement](CLA.md). The CLA ensures the project can distribute your contribution under its license.

## How to contribute

### Reporting bugs

Open a [GitHub issue](https://github.com/gameguild-gg/gameguild/issues/new?template=bug_report.md) and include:

- What you expected and what actually happened.
- Exact steps or a minimal reproduction.
- Environment (browser, OS, API or web version/commit).
- Logs or screenshots where relevant.

### Suggesting features

Open a feature-request issue describing the problem you're trying to solve, your proposed solution, and any alternatives you considered. Product changes are discussed before large implementations land.

### Code contributions

1. **Create a branch** from `develop` following `feature/<short-slug>` or `fix/<short-slug>`.
2. **Make your change**, keeping the scope of one PR focused.
3. **Add or update tests** — new endpoints, services, and behaviors ship with tests (API unit/integration tests under `apps/api/tests/`, web tests under `apps/web/src/**`).
4. **Run the gates locally** (at minimum, everything your change touches):

   ```bash
   dotnet build apps/api/GameGuild.sln   # must be warning-clean; GGARCH001–008 analyzers are errors
   dotnet test apps/api/GameGuild.sln
   pnpm -C apps/web test
   pnpm lint
   ```

5. **Commit with conventional commits** (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`), e.g. `fix(billing): reject zero-quantity checkout lines`.
6. **Open a pull request** against `develop` with a clear description and `Fixes #N` references for the issues it closes.
7. **Respond to review feedback** and keep the PR green — CI (build, tests, OpenAPI/client consistency, migrations, lint) must pass before merge.

### Engineering invariants

PRs must preserve the invariants documented in [AGENTS.md](AGENTS.md), notably:

- Platform modules stay domain-free; dependency direction is one-way (domain may reference platform, never the reverse).
- Every controller action declares `[Authorize]` or `[AllowAnonymous]`; anonymous endpoints are listed in the reviewed allowlist.
- Authorization fails closed on unknown permissions; acting-user identity comes from the actor context, never the request body.
- Permission mutations are guarded, versioned, and audited; required audit events commit with their mutation.

## Style and conventions

- C#: file-scoped namespaces, nullable reference types, warning-free builds. Follow the patterns in neighboring modules rather than introducing new ones.
- TypeScript/React: function components, Tailwind styling, existing UI primitives from `@game-guild/ui`.
- Docs: Markdown, wrapped near 100–120 characters, in [docs/](docs/) where appropriate.

## Questions

Open a GitHub discussion or an issue labeled `question`, and a maintainer will point you in the right direction.
