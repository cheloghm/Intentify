# Frontend

## What lives here
`src/frontend` is the frontend workspace.

Current runnable package:
- `web/` — plain JavaScript ESM web app.

## Frontend architecture and organization (high level)
The current frontend runtime is a hash-routed SPA-style app under `src/frontend/web`.

Key areas:
- `web/src/app/` — app shell and hash-route orchestration
- `web/src/pages/` — page-level view composition/feature screens
- `web/src/shared/` — shared API/auth/config/UI utilities
- `web/src/shared/ui/` — shared visual primitives and styling entry points

## Routing/app structure
- Route parsing + navigation/auth guard behavior is centralized in `web/src/app/index.js`.
- Page renderers are implemented in `web/src/pages/*.js` and wired by the app shell.
- This repo does **not** currently verify a Next.js runtime architecture.

## Major UI/feature areas
Primary page ownership is under `web/src/pages` (sites/install, visitors/profile, engage, promos, leads, knowledge, intelligence, ads, tickets, platform admin).

Use page-level docs for practical change locations instead of a route-table copy.

## Where to change common frontend concerns
- Colors/backgrounds/buttons/shared primitives: `web/src/shared/ui/`
- Shared layout/navigation shell: `web/src/app/index.js`
- Page-specific dashboard/feature views: `web/src/pages/`
- Backend request paths/error mapping: `web/src/shared/apiClient.js`
- Auth token/session behavior: `web/src/shared/auth.js`
- API base/env resolution: `web/src/shared/config.js`

## How frontend talks to backend
- Frontend backend calls are centralized in `web/src/shared/apiClient.js`.
- API base resolution is handled in `web/src/shared/config.js`.
- Route/page modules consume shared client methods rather than duplicating fetch logic per page.

## Environment/config overview (verifiable)
Detailed precedence and key names are documented in `web/README.md`.

At high level, frontend config uses:
- `INTENTIFY_API_BASE`
- `NEXT_PUBLIC_API_BASE_URL`
- window fallback values described in `web/README.md`

## Where deeper frontend docs live
- Web package overview and commands: [`web/README.md`](web/README.md)
- Shared ownership guide: [`web/src/shared/README.md`](web/src/shared/README.md)
- Shared UI visual guidance: [`web/src/shared/ui/README.md`](web/src/shared/ui/README.md)
- Pages ownership guide: [`web/src/pages/README.md`](web/src/pages/README.md)
- Root repo map: [`../../README.md`](../../README.md)
- Backend integration overview: [`../backend/README.md`](../backend/README.md)
