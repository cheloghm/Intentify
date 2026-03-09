# shared

## Purpose
This folder holds cross-page frontend building blocks used by `src/app/index.js` and page modules under `src/pages`.

It is organized around four responsibilities:
- API access (`apiClient.js`)
- Auth/session handling (`auth.js`)
- Runtime config resolution (`config.js`)
- UI primitives (`ui/`)

## Ownership map (where to change X)
- Change backend request paths, query-string helpers, or error mapping:
  - `apiClient.js`
- Change token storage/session-expiry behavior/login-state helpers:
  - `auth.js`
- Change frontend API base resolution defaults/environment fallback:
  - `config.js`
- Change reusable visual primitives and shared styles:
  - `ui/README.md`

## Notes
- This frontend is plain JavaScript ESM + hash-based navigation, not a verified Next.js runtime.
- Keep page-specific rendering logic in `src/pages/*`; keep cross-page utilities here.

## Related docs
- UI primitives guidance: `./ui/README.md`
- Page ownership guidance: `../pages/README.md`
- Frontend workspace overview: `../../README.md`
