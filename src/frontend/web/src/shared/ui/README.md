# shared/ui

## Purpose
Reusable UI primitives and shared styling used across page modules.

Main files:
- `components.js`: DOM helpers/card/table/form/toast primitives
- `styles.css`: shared visual tokens and component styles
- `index.js`: public exports consumed by pages and app shell

## Visual change guidance (where to change X)
- Colors, backgrounds, typography defaults, spacing, border/shadow language:
  - `styles.css`
- Shared button/input/card/table/toast behavior and markup patterns:
  - `components.js`
- What UI APIs are exposed to the rest of the app:
  - `index.js`

## Practical rules for UI changes
- Prefer editing shared primitives first when a change affects multiple pages.
- Add page-local style overrides only when the change is intentionally page-specific.
- Keep exports in `index.js` aligned with any new/reworked primitives.

## Related docs
- Shared ownership guide: `../README.md`
- Page ownership guide: `../../pages/README.md`
- Frontend workspace overview: `../../../README.md`
