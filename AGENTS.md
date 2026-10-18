# AGENTS.md — Engineering Invariants for AI Assistants

This repository is a multi-product platform. The backend under `apps/api` is split into
**platform modules** (shared verbatim across products, modulo namespace renames) and
**domain modules** (product-specific). When making changes, preserve the invariants
below — they are enforced by build-time analyzers and architecture tests, so violations
fail the build or the test suite.

## Module purity

1. **Platform modules are domain-free.** Files under
   `apps/api/Source/Modules/GameGuild.Identity.*`, `GameGuild.Resources*`,
   `GameGuild.SharedKernel`, `GameGuild.Assets`, `GameGuild.Features`,
   `GameGuild.Finance.Ledgers`, and `GameGuild.Content.Pages` must contain **only**
   platform-generic code — no product names, no domain vocabulary (no game, course,
   learning, community/social terms), no domain module references.
2. **Dependency direction is one-way.** Platform modules must never reference domain
   modules (`GameGuild.Learning.*`, `GameGuild.Social.*`, `GameGuild.Commerce.*`,
   `GameGuild.Finance.Economy.*`, …) — not via `<ProjectReference>` and not via
   `using` directives. Domain modules may reference platform modules. Product-specific
   bridges belong in the host composition root (`GameGuild.API`, e.g.
   `Security/CommerceOrderValidationService.cs` and
   `Security/TenantPaymentHistoryController.cs`). Enforced by
   `ModuleDependencyDirectionTests` (with an explicit, justified known-debt list).
3. **Platform files are mirrored byte-for-byte** to sibling products (namespace renames
   only). Keep them EditorConfig-clean and free of product-specific identifiers.
4. **Domain policy seeds go through the extension point.** The common
   `PolicyDefinitionSeeder` seeds only platform-generic policies; product policies are
   contributed by implementing `IPolicySeedContributor` and registering it with
   `AddPolicySeedContributor<T>()` from the host. This product currently registers no
   domain contributor — its only domain gates are the course-content policies that are
   common to every product's seeder.

## Authorization invariants

5. **Every endpoint declares an authorization decision.** Every MVC controller action
   carries `[Authorize]` (on the action or the controller) or an explicit
   `[AllowAnonymous]`. The `GGARCH008` analyzer fails the build on unguarded endpoints,
   and `ControllerAuthorizationArchitectureTests` additionally requires each anonymous
   endpoint (action-level, or any endpoint of a controller whose class is
   `[AllowAnonymous]`) to appear in the HOST-side reviewed allowlist —
   `apps/api/Source/GameGuild.API/Security/AnonymousEndpointRegistry.cs` — with a
   one-line justification. The registry is per-product content; the analyzer and the
   tests stay product-generic. Never remove an `[Authorize]` without adding a reviewed
   allowlist entry.
6. **Fail closed, always.** Unknown permission patterns in
   `AuthorizationBehavior.MapPermissionToAccessLevel` throw — they never default to
   `Write`. Cross-tenant object access (e.g. `AssetAccessService`) is denied unless the
   asset's own tenant matches the request tenant (SystemAdmin may pass the explicit
   `permitCrossTenant` path).
7. **Permission mutations are guarded, versioned, and audited.** Any handler or service
   that grants, revokes, or templates permissions must: enforce tenant-admin /
   `permissions:manage` / `system:manage-global-defaults` (for global defaults or system
   templates) inside the handler; bump the tenant security version via
   `ITenantSecurityVersionStore`; and write a `PermissionAuditLog` entry.
8. **One command type per operation.** Endpoints bind the guarded Authorization-module
   command types; do not reintroduce duplicate command records. Acting-user identity
   (`GrantedBy`, `RevokedBy`, `CreatedByUserId`, …) always comes from the authenticated
   actor context (`IActorContextAccessor`), never from the request body or query.
9. **Tenant comes from the request context, not the route.** Cross-tenant reads are
   denied unless the caller is SystemAdmin; `CreatedByUserId`-style fields are taken
   from the actor (see the ledger controllers for the pattern).
10. **Unpublished content is not public.** Pages/content-resource get-by-id and public
    listing surfaces require a content permission for anything that is not published;
    anonymous slug/sitemap/catalog surfaces return published items only.
11. **Never swallow permission/quota failures.** Partial failures in bulk operations are
    collected into the result; the subscription quota-sync handler throws an aggregate
    failure when any quota update fails so downgrades cannot silently skip limits.

## Commands and build

- Build: `dotnet build apps/api/GameGuild.sln` (must be warning-clean; the GGARCH001–008
  architecture diagnostics are errors and fail the build).
- Tests: `dotnet test` on the touched test projects (at minimum
  `GameGuild.Identity.Authorization.UnitTests`,
  `GameGuild.Identity.Authentication.UnitTests`, and `GameGuild.API.UnitTests`
  architecture/security suites).
- Architecture authorization doc:
  `apps/api/Source/Modules/GameGuild.Identity.Authentication/AUTHORIZATION_ARCHITECTURE.md`
  (section "Platform Authorization Hardening") — update it when changing any invariant
  described there.

<!-- BEGIN:turborepo-agent-rules -->

# This is NOT the Turborepo you know

Turborepo configuration, task behavior, and CLI commands can vary between installed versions and may differ from your training data. Resolve the `turbo` package from this file's directory or relevant workspace; in monorepos, it may not be visible from the repository root. For example, run `node -p "require.resolve('turbo/package.json')"` from a workspace that depends on `turbo`.

Read `docs/README.md` inside that installed package first, then read the relevant pages from its `docs/` directory before changing Turborepo configuration or commands. Heed deprecation notices. These bundled docs match the installed package version and are available without network access.

This block is written and re-added by `turbo` before repository-scoped commands when an AI agent is detected. In the Turborepo source repository, its template is defined in `crates/turborepo-cli/src/cli/agent_guidance.rs`. Removing the managed block while updates are enabled means a later qualifying invocation will add it again. Set `"agentGuidance": false` in the root `turbo.json` or `turbo.jsonc` to opt out; this does not remove an existing block. Keep the block committed with your work to avoid an uncommitted change on the next agent invocation.
<!-- END:turborepo-agent-rules -->

## Quiz grading validation

Changes to quiz delivery, assessment review, grading, release, or learner results must
follow `docs/testing/quiz-grading/AGENT_PROTOCOL.md`. The executable capability catalog
is the source of stable case IDs; generated reports are candidates and never update the
canonical capability status without human review.
