# Backend shared packages

## Shared packages overview
`src/backend/shared` contains reusable backend packages used across modules and host wiring.

This README is an overview index; package-level details belong in each package README.

## Package list and responsibility summaries
- `Intentify.Shared.AI` — AI-related shared abstractions/utilities.
- `Intentify.Shared.Abstractions` — common shared abstractions/contracts.
- `Intentify.Shared.Data.Mongo` — Mongo data access conventions/options helpers.
- `Intentify.Shared.KeyManagement` — key-management shared functionality.
- `Intentify.Shared.Messaging` — shared in-process messaging abstractions/implementation.
- `Intentify.Shared.Observability` — observability/logging support.
- `Intentify.Shared.Security` — security primitives (token/password related helpers).
- `Intentify.Shared.Testing` — shared test support utilities.
- `Intentify.Shared.Validation` — shared validation result/error primitives.
- `Intentify.Shared.Web` — shared web-layer abstractions/helpers (including module interface patterns).

## When to use shared packages vs module-local implementation
Use a shared package when behavior/contracts are:
- reused by multiple modules or host-level composition, and
- stable enough to be centrally maintained.

Keep implementation module-local when behavior is:
- specific to one module’s bounded context, or
- infrastructure details private to that module.

## Dependency guardrails
Shared package usage should follow repository architecture boundaries and anti-coupling rules.
Use canonical policy docs instead of restating them here:
- Architecture boundaries: [`../../../docs/codex/01-architecture-boundaries.md`](../../../docs/codex/01-architecture-boundaries.md)
- Anti-circular dependency guidance: [`../../../docs/codex/04-anti-circular-dependencies.md`](../../../docs/codex/04-anti-circular-dependencies.md)

## Links to package READMEs
- [`Intentify.Shared.AI/README.md`](Intentify.Shared.AI/README.md)
- [`Intentify.Shared.Abstractions/README.md`](Intentify.Shared.Abstractions/README.md)
- [`Intentify.Shared.Data.Mongo/README.md`](Intentify.Shared.Data.Mongo/README.md)
- `Intentify.Shared.KeyManagement/README.md` (to be added)
- [`Intentify.Shared.Messaging/README.md`](Intentify.Shared.Messaging/README.md)
- [`Intentify.Shared.Observability/README.md`](Intentify.Shared.Observability/README.md)
- [`Intentify.Shared.Security/README.md`](Intentify.Shared.Security/README.md)
- [`Intentify.Shared.Testing/README.md`](Intentify.Shared.Testing/README.md)
- `Intentify.Shared.Validation/README.md` (to be added)
- [`Intentify.Shared.Web/README.md`](Intentify.Shared.Web/README.md)
