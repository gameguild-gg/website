## GameGuild — AI Agent Quick Guide

This file is a concise reference for automated coding agents to be productive in the GameGuild monorepo.

- Repository shape: modular monolith. Backend is a .NET 9 Web API (apps/api/Source). Frontend is a Next.js 15 React app (apps/web) written in TypeScript.
- Primary workflows: local DB (Docker Compose), backend (dotnet), frontend (Next.js + codegen).

Essential commands (paths relative to repo root):

- Start DB: `docker-compose up -d adminer` (compose.yaml at repo root).
- Start API (dev): use the VS Code task `start-api` or run:
  `dotnet run --project apps/api/Source/GameGuild.API/GameGuild.API.csproj`
- Start Web (dev): `pnpm --filter @game-guild/web dev`.
- Frontend codegen (must run after API/schema changes):
    - `pnpm api:client:generate` (OpenAPI client; it uses a captured Swagger artifact, `OPENAPI_ARTIFACT`, `OPENAPI_URL`, or the local API in that order)
    - `pnpm --filter @game-guild/web graphql:gen` (GraphQL hooks, when the package exposes that script)

Key project conventions and patterns:

- Backend modules: look under `apps/api/Source/Modules/` — each module commonly contains Commands/, Queries/, Entities/, Configuration/, Controllers/, GraphQL/. Modules implement an `IModule` interface exposing `ConfigureServices()` and `MapEndpoints()`.
    - Compliance modules (`GameGuild.Compliance.*`) handle regulatory requirements: Audit (SOC2, ISO 27001, GDPR, HIPAA), KYC (identity verification), FERPA (educational privacy).
- CQRS + FluentValidation: Commands/Queries use a custom CQRS implementation (`GameGuild.CQRS` namespace, NOT MediatR). Handlers implement `IRequestHandler<TRequest, TResponse>` or `ICommandHandler`/`IQueryHandler`. Use the project Result<T> pattern; global exception filters convert to ProblemDetails.
- EF Core: entities inherit from `EntityBase` (audit fields), use global soft-delete filters, and a `Version` property for optimistic concurrency.
- Tenant & auth: multi-tenant context is passed via `X-Tenant-Id`. Auth uses JWT (backend) and NextAuth (frontend). Frontend has an authenticated client helper (import pattern: `from '@/lib/api'` or `@/lib/api/authenticated-client`).

Frontend structure & patterns:

- Feature-first libs live under `apps/web/src/lib/` (e.g. `user-management`, `content-management`, `core`). Import by feature: `from '@/lib/<feature>'`.
- Code generation is required when the API schema changes; failing to run `api:gen` or `graphql:gen` will cause type/runtime mismatches.
- Scripts of interest: root `package.json` scripts `api:client:generate`, `dev:web`, and `build:web`; `apps/web/package.json` owns only the web runtime and tests.

Testing & CI:

- Backend tests: `apps/api/Tests/` (unit and integration). Integration tests use TestHost and an in-memory DB provider.
- Frontend tests: Jest unit tests and Playwright for E2E; see `apps/web/package.json` for test scripts.

Files & locations to consult for specifics:

- Module examples: `apps/api/Source/Modules/*`
- API project file: `apps/api/Source/GameGuild.API/GameGuild.API.csproj`
- Frontend package scripts + codegen: `apps/web/package.json`, `apps/web/src/lib/api/scripts/`
- Env templates: root `.env.example`, and `apps/api/.env.example` if present. Copy to `.env` for local dev.

Quick tips for agents (do these before code changes):

- Run the root API-client generation command after any OpenAPI change; web development does not regenerate the API client implicitly.
- When editing backend modules, follow the Commands/Queries/Handlers/Validators pattern and register services in `IModule` implementations.
- Use VS Code tasks (`start-api`, `start-web`) when available — they encapsulate common flags.

If anything below is unclear or you want more examples (small handler, a module skeleton, or a codegen verification script), tell me which area to expand and I will add an example or tests.

---

Last updated: automated merge — please review for any team-specific secrets or CI notes to add.

# GameGuild Platform - AI Agent Instructions

## Architecture Overview

GameGuild is a **modular monolith** with vertical slices implemented as modules. The backend is a **.NET 9 Web API** using clean architecture patterns, and the frontend is a **Next.js 15 React app** with TypeScript.

### Key Components

- **API**: `apps/api/` - Modular .NET API with CQRS, EF Core, JWT auth, GraphQL + REST
- **Web**: `apps/web/` - Next.js app with NextAuth, Apollo GraphQL, auto-generated API clients
- **Database**: PostgreSQL with EF Core migrations and seeding

## Critical Development Workflows

### Starting the Development Environment

1. **Database**: `docker-compose up -d adminer` (starts PostgreSQL + Adminer)
2. **API**: Use VS Code task `start-api` or `dotnet run --project apps/api/Source/GameGuild.API/GameGuild.API.csproj`
3. **Web**: Use VS Code task `start-web` or `pnpm --filter @game-guild/web dev`

The web app runs on port 3000, API on port 5000 (configurable via `.env`).

### Code Generation Workflow

The frontend uses **automatic code generation** - critical for development:

```bash
# From the repository root
pnpm api:client:generate                  # Generates the typed API client from Swagger
pnpm --filter @game-guild/web graphql:gen # Generates GraphQL hooks when configured
pnpm --filter @game-guild/web dev         # Runs the Next.js application only
```

**Always run code generation after API schema changes** before writing frontend code. The web dev command does not regenerate the API client.

### Database Migrations

```bash
# From apps/api/
dotnet ef migrations add MigrationName
dotnet ef database update
```

The API automatically applies migrations and seeds data on startup.

## Module Architecture Patterns

### Backend Module Structure (`apps/api/Source/Modules/`)

Each module follows this pattern:

```
ModuleName/
├── Commands/          # CQRS commands (mutations)
├── Queries/           # CQRS queries (reads)
├── Entities/          # Domain entities
├── Configuration/     # EF Core config & DI setup
├── Controllers/       # REST endpoints
└── GraphQL/          # GraphQL types/resolvers (optional)
```

**Key Pattern**: Modules implement `IModule` interface with `ConfigureServices()` and `MapEndpoints()` methods.

### Frontend Library Structure (`apps/web/src/lib/`)

Organized by **feature domains**, not technical layers:

```
user-management/       # Users, auth, profiles
content-management/    # Courses, projects, programs
commerce/             # Payments, subscriptions
communication/        # Posts, notifications, feeds
admin/               # Tenants, testing tools
core/                # API clients, utils, health
```

**Import Pattern**: Use feature-based imports: `from '@/lib/user-management'`

## Authentication & Authorization

### Backend: JWT + Multi-Tenant

- JWT tokens with refresh rotation
- Tenant context via `X-Tenant-Id` header
- Permission-based authorization using DAC (Discretionary Access Control)
- Multi-tenant isolation at the database level

### Frontend: NextAuth + Session Management

- NextAuth.js with JWT strategy
- Auto-refresh 30s before token expiry
- Configured API client with automatic token injection

**API Client Pattern**:

```typescript
// Use the configured authenticated client
import { configureAuthenticatedClient } from "@/lib/api/authenticated-client";

export async function myServerAction() {
    await configureAuthenticatedClient(); // Sets up auth headers
    return await someApiCall(); // Uses configured client
}
```

## Database & Entity Patterns

### Entity Framework Conventions

- **Base Entity**: All entities inherit from `EntityBase` with audit fields
- **Soft Deletes**: Global query filters automatically exclude deleted records
- **Concurrency**: Uses `Version` property for optimistic concurrency
- **Value Objects**: Email, Phone, Money as owned entities

### Migration Naming

Use descriptive names: `dotnet ef migrations add AddUserProfileCustomization`

## CQRS & Validation Patterns

### Command/Query Structure

```csharp
// Command with validator
public record CreateUserCommand(string Email, string Name) : IRequest<Result<UserDto>>;

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    // FluentValidation rules
}

public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    // Handler implementation
}
```

### Pipeline Behaviors

- **Validation**: Aggregates DataAnnotations + FluentValidation
- **Logging**: Structured logging with Serilog
- **Performance**: Measures execution time and memory

## Testing Approach

### Backend Testing Structure (`apps/api/Tests/`)

```
Core/Unit/            # Unit tests for core logic
Integration/          # Integration tests with TestHost
```

**Test Database**: Uses in-memory provider for fast tests.

### Frontend Testing

Uses Jest + Playwright for E2E testing.

## Error Handling Conventions

### Backend: Result Pattern + ProblemDetails

```csharp
public async Task<Result<UserDto>> CreateUser(CreateUserCommand command)
{
    // Returns Result<T> for success/failure handling
    // Global exception filter converts to ProblemDetails
}
```

### Frontend: Server Actions Pattern

Server actions return typed results with error handling built-in.

## Environment Configuration

### Required Environment Files

- `apps/api/.env` - Copy from `.env.example`
- Key variables: `DB_CONNECTION_STRING`, `JWT_SECRET_KEY`, OAuth secrets

### Development Defaults

- Database: `postgres/postgres/postgres` (matches docker-compose)
- API Port: 5000
- Web Port: 3000

## VS Code Integration

The workspace includes pre-configured tasks:

- `start-api` / `start-web` - Start services
- `build-api` / `build-web` - Build projects
- `ef-migration-add` / `ef-database-update` - EF Core commands

Use VS Code tasks instead of manual terminal commands when available.
