# Backend modules

## Modules overview
`src/backend/modules` contains bounded-context backend modules. Each module owns its own implementation layers and tests.

This README is an overview index; module details belong in each module README.

## Module list (bounded-context summaries)
- `Intentify.Modules.Ads` — ad campaign/domain capability module.
- `Intentify.Modules.Auth` — authentication, user/tenant access capability.
- `Intentify.Modules.Collector` — event collection/ingestion capability.
- `Intentify.Modules.Engage` — engagement/chat and recommendation interaction capability.
- `Intentify.Modules.Flows` — flow/orchestration capability.
- `Intentify.Modules.Intelligence` — intelligence/trends/profile capability.
- `Intentify.Modules.Knowledge` — knowledge source ingestion/index/retrieval capability.
- `Intentify.Modules.Leads` — lead capture/management capability.
- `Intentify.Modules.PlatformAdmin` — platform administration capability.
- `Intentify.Modules.Promos` — promotions and promo entry capability.
- `Intentify.Modules.Sites` — site and installation key/origin capability.
- `Intentify.Modules.Tickets` — ticket and ticket-note capability.
- `Intentify.Modules.Visitors` — visitor profile and visit analytics capability.

## Standard layer shape
Most modules follow this shape:
- `src/Intentify.Modules.<Name>.Api`
- `src/Intentify.Modules.<Name>.Application`
- `src/Intentify.Modules.<Name>.Domain`
- `src/Intentify.Modules.<Name>.Infrastructure`
- `tests/Intentify.Modules.<Name>.Tests`

## PlatformAdmin exception note (current implementation state)
`Intentify.Modules.PlatformAdmin` currently has:
- `Api`
- `Application`
- `Infrastructure`
and does **not** currently include a `Domain` project or module test project in the repository.

## How to navigate module docs
- Start at each module root README: `Intentify.Modules.<Name>/README.md`.
- Then navigate into module layer folders under each module `src/` subtree.
- Keep route details at module/layer level; avoid duplicating them in this overview.

## Architecture boundaries reference
Use the canonical boundaries doc for layer/dependency policy:
- [`../../../docs/codex/01-architecture-boundaries.md`](../../../docs/codex/01-architecture-boundaries.md)
