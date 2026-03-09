# Intentify

## Intentify at a glance
Intentify is a monorepo containing:
- a modular .NET backend (`src/backend`) built around AppHost + modules + shared packages.
- a frontend web app (`src/frontend/web`) implemented as a plain JavaScript ESM application.
- internal engineering policy/workflow references under `docs/codex`.

## Repository map
- `src/backend` — .NET solution, AppHost, backend modules, shared backend packages, and backend tests.
- `src/frontend` — frontend workspace.
  - `src/frontend/web` — current web app package.
- `docs/codex` — canonical internal engineering rules, boundaries, and workflow guardrails.
- `.github/workflows` — CI pipeline definitions.
- `.env.example` — local environment variable examples.

## Current architecture reality
- Backend is organized by modules (for domain capabilities) plus shared packages (for reusable cross-cutting code).
- AppHost composes module registration and endpoint mapping via a shared module interface.
- Frontend currently runs from `src/frontend/web` with `http-server` + Node scripts.
- The frontend uses `NEXT_PUBLIC_*` naming for API base env compatibility, but the repository does **not** currently verify a Next.js runtime application setup.

## High-level backend module summary
Backend modules are organized as bounded contexts under `src/backend/modules`.

Current module set includes:
- Ads
- Auth
- Collector
- Engage
- Flows
- Intelligence
- Knowledge
- Leads
- PlatformAdmin
- Promos
- Sites
- Tickets
- Visitors

Use `src/backend/modules/README.md` for module-level navigation, and each module README for detailed extension/change guidance.

## High-level shared package summary
Shared backend packages under `src/backend/shared` provide reusable cross-cutting capabilities used by modules.

Current shared package set includes:
- Intentify.Shared.AI
- Intentify.Shared.Abstractions
- Intentify.Shared.Data.Mongo
- Intentify.Shared.KeyManagement
- Intentify.Shared.Messaging
- Intentify.Shared.Observability
- Intentify.Shared.Security
- Intentify.Shared.Testing
- Intentify.Shared.Validation
- Intentify.Shared.Web

Use `src/backend/shared/README.md` for package-level navigation; package READMEs contain specific usage and change guidance.

## High-level quick start
1. Review root/backend/frontend READMEs.
2. Backend:
   - `cd src/backend`
   - `dotnet restore Intentify.sln`
   - `dotnet build -c Debug Intentify.sln --no-restore`
   - `dotnet test -c Debug Intentify.sln --no-build`
3. Frontend:
   - `cd src/frontend/web`
   - `npm ci`
   - `npm run build`
   - `npm run dev`

## High-level configuration map
- Root-level examples live in `.env.example`.
- Backend detailed configuration is documented in `src/backend/README.md`.
- Frontend detailed configuration is documented in `src/frontend/web/README.md`.
- Module-specific configuration belongs in module-level docs (later batches), not root-level docs.

## CI/build overview
The CI workflow runs:
- backend restore/build/test against `src/backend/Intentify.sln` (when `src/backend` exists).
- frontend install + build for `src/frontend/web` (when present).

See `.github/workflows/ci.yml` for exact CI commands and conditions.

## Documentation map
- Source index: [`src/README.md`](src/README.md)
- Backend overview: [`src/backend/README.md`](src/backend/README.md)
- Backend modules index: [`src/backend/modules/README.md`](src/backend/modules/README.md)
- Backend shared packages index: [`src/backend/shared/README.md`](src/backend/shared/README.md)
- Frontend overview: [`src/frontend/README.md`](src/frontend/README.md)
- Frontend web app: [`src/frontend/web/README.md`](src/frontend/web/README.md)
- Internal policy references: [`docs/codex/README.md`](docs/codex/README.md)

## Engineering policy references
Use `docs/codex` as canonical internal references (do not duplicate these policy details across READMEs):
- Architecture boundaries: [`docs/codex/01-architecture-boundaries.md`](docs/codex/01-architecture-boundaries.md)
- Testing playbook: [`docs/codex/03-testing-playbook.md`](docs/codex/03-testing-playbook.md)
- Workflow/gates/checklists: [`docs/codex/README.md`](docs/codex/README.md)

## README maintenance rules
- Link-first documentation: document once at the highest stable level, then link from lower-level READMEs.
- Avoid duplication: do not copy/paste architecture policy from `docs/codex` into local READMEs.
- Keep implementation detail local: module/layer specifics should live in module/layer docs.
- Keep docs reality-based: only document behavior verified in repository code/config.
- Use lower-level READMEs for extension/change guidance; keep this root README at repository-orientation level.

## Contribution/navigation pointers
- Start from `src/README.md` to navigate backend vs frontend areas.
- For backend architecture and commands, use `src/backend/README.md`.
- For frontend run/build/config integration, use `src/frontend/web/README.md`.
- For engineering guardrails, use `docs/codex/*`.
