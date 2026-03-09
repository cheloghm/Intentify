# Web frontend (`src/frontend/web`)

## Web app purpose
This package provides the current Intentify web UI shell and feature views, including auth flow and module-facing pages (sites, visitors, knowledge, engage, promos, intelligence, ads, leads, tickets, platform admin).

## Current tech reality
This repository currently verifies a **plain JavaScript ESM web app** for `src/frontend/web`.

- Build uses a Node script (`scripts/build.js`).
- Local dev uses `http-server`.
- Environment keys include `NEXT_PUBLIC_*` naming for compatibility, but this repo does **not** currently verify a Next.js runtime app structure.

## Folder map
- `public/` — static HTML entry documents (`index.html`, `login.html`, `register.html`).
- `src/app/` — app entry and auth/page composition flow.
- `src/pages/` — feature view modules.
- `src/shared/` — shared frontend runtime concerns (API client, auth state, config, shared UI).
- `src/shared/ui/` — shared UI primitives and stylesheet.
- `scripts/` — build/runtime helper scripts.

## Runtime/build/dev scripts
From `src/frontend/web`:

```bash
npm ci
npm run build
npm run dev
```

Current script behaviors:
- `npm run build` -> runs `node scripts/build.js`
- `npm run dev` -> serves the directory via `http-server` on port `3000`

## Detailed frontend configuration and precedence
### Configuration inputs
Frontend API base URL resolution is centralized in `src/shared/config.js` and checks, in order:
1. `INTENTIFY_API_BASE`
2. `NEXT_PUBLIC_API_BASE_URL`
3. `window.__INTENTIFY_API_BASE__`
4. `window.NEXT_PUBLIC_API_BASE_URL`
5. fallback default: `http://localhost:5000`

### Build-time default behavior
`npm run build` sets defaults in `scripts/build.js`:
- `NODE_ENV` defaults to `production`
- `NEXT_PUBLIC_API_BASE_URL` defaults to `http://localhost:3000` if not provided

### Root example file
Root `.env.example` includes a frontend sample key:
- `NEXT_PUBLIC_API_BASE_URL=http://localhost:5100`

## API integration model
- API calls are centralized in `src/shared/apiClient.js`.
- API client builds request URLs from resolved base URL and appends auth headers when token is available.
- Page modules call this shared client for backend communication.

## Auth/session basics
- Auth token storage/retrieval/clearing is centralized in `src/shared/auth.js`.
- Login/register app flows in `src/app/` use shared auth helpers plus shared API client.

## UI conventions and where visual changes are made
- Shared visual primitives/components are in `src/shared/ui/components.js`.
- Shared styles are in `src/shared/ui/styles.css`.
- `src/app/index.js` composes layout/nav and wires page rendering.

For future detailed UI change-location docs, see planned README targets:
- `src/shared/README.md` (to be added)
- `src/shared/ui/README.md` (to be added)

## Navigation/views map (high level)
- Navigation and top-level app routing/composition are coordinated in `src/app/index.js`.
- Feature view modules are organized under `src/pages/` (for example: sites, visitors, knowledge, engage, promos, leads, tickets, intelligence, ads, platform admin, install, visitor profile).

## Build/verification commands
From `src/frontend/web`:

```bash
npm ci
npm run build
```

Optional local run:

```bash
npm run dev
```

## Links to shared/pages docs (planned in later batches)
- Shared runtime folder: [`src/shared/`](src/shared/)
- Pages folder: [`src/pages/`](src/pages/)
- Planned README: `src/shared/README.md`
- Planned README: `src/shared/ui/README.md`
- Planned README: `src/pages/README.md`
