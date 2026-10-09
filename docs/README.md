# Documentation Index

This directory contains the cleaned and consolidated project documentation migrated from legacy sources.

## Architecture

- `architecture/clean-architecture.md` – Layering, DI, domain events, validation, logging
- `architecture/permissions-dac.md` – DAC hierarchy & permission system
- `architecture/DUAL_CURRENCY_ECONOMY.md` – Hard/soft coin module boundaries, ledger, reconciliation, reserves, and rollout gates

## Product And Economics Papers

- `papers/dual-currency-economy-whitepaper.md` – Product and economic source for the internal hard/soft coin economy
- `papers/whitepaper.md` – Legacy Game Guild governance whitepaper
- `papers/tokenomics.md` – Legacy GGG governance token notes; separate from internal hard/soft coins

## Backend Modules

- `modules/auth-module.md` – Authentication & token lifecycle
- `modules/tenant-module.md` – Multi-tenancy model
- `modules/posts-module.md` – Domain event driven posts
- `modules/base-entity.md` – Entity base abstraction

## API Configuration

- `api/problem-details-configuration.md` – RFC 7807 responses, exception mappings, detail levels, localization, and correlation IDs

## Frontend

- `frontend/dashboard.md` – Dashboard architecture & server actions
- `frontend/filter-system.md` – Type-safe filter framework
- `frontend/testing-lab.md` – Reusable testing lab components
- `frontend/nextjs-migration.md` – Next.js 15 migration notes
- `frontend/notifications.md` – Notification integration

## Testing & Setup

- `testing/test-architecture.md` – Test layers & organization
- `setup/environment.md` – Environment variables, running services
- `setup/local-development-cleanup.md` – Safe shutdown, cleanup, diagnostics, and local environment recovery
- `setup/dev-fast.md` – Fast dynamic application environment for functional testing

## Security

- `security/DUAL_CURRENCY_ECONOMY_THREAT_MODEL.md` – Financial-integrity, provider, ad-fraud, privacy, and operational controls

## Permissions System

See `architecture/permissions-dac.md` for DAC details.

---

If adding new docs, keep top-level heading unique and include a short purpose paragraph.
