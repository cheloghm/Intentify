# pages

## Purpose
Contains page-level view renderers for the hash-routed web app.

Each file typically exports a `render...View(...)` function consumed by `src/app/index.js` route wiring.

## Page ownership and change-location guide
- Sites and installation workflows:
  - `sites.js`, `install.js`
- Visitor list/profile/timeline experiences:
  - `visitors.js`, `visitorProfile.js`
- Engage experience:
  - `engage.js`
- Promos and Leads pages:
  - `promos.js`, `leads.js`
- Knowledge and Intelligence pages:
  - `knowledge.js`, `intelligence.js`
- Ads page:
  - `ads.js`
- Tickets page:
  - `tickets.js`
- Platform admin pages:
  - `platformAdmin.js`

## Where to change dashboard/navigation behavior
- Route selection, auth guards, and hash path matching live in:
  - `../app/index.js`
- Navbar links and app-shell composition also live in:
  - `../app/index.js`

## Notes
- Keep this folder focused on page composition and user flows.
- Move reusable API/auth/config/UI utilities to `../shared/` instead of duplicating helpers across page files.
- This is a page ownership guide, not a full route table.

## Related docs
- Shared folder guide: `../shared/README.md`
- UI visual change guide: `../shared/ui/README.md`
- Frontend workspace overview: `../../README.md`
