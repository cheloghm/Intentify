# Intentify.Modules.Promos.Application

## Application layer responsibility
- Defines promo/entry use-case contracts and orchestrates promo lifecycle and public-entry workflows.
- Keeps transport and persistence implementation details behind interfaces.

## Command/query/handler map
- Commands/queries (`PromoContracts.cs`):
  - `CreatePromoCommand`
  - `ListPromosQuery`
  - `ListPromoEntriesQuery`
  - `GetPromoDetailQuery`
  - `ListVisitorPromoEntriesQuery`
  - `CreatePublicPromoEntryCommand`
  - `PromoDetailResult`
- Handlers (`Handlers.cs`):
  - `CreatePromoHandler`
  - `ListPromosHandler`
  - `ListPromoEntriesHandler`
  - `GetPromoDetailHandler`
  - `CreatePublicPromoEntryHandler`

## Contracts/interfaces map
- `IPromoRepository`: promo create/list/get (including active by public key).
- `IPromoEntryRepository`: promo entry create/list by promo or visitor.
- `IPromoConsentLogRepository`: consent audit write.
- `IPromoVisitorLookup`: visitor id resolution bridge used by public entry flow.

## Validation/orchestration points
- `CreatePromoHandler` validates promo name + question key uniqueness, normalizes questions, and generates a public key.
- `CreatePublicPromoEntryHandler` validates consent/payload constraints, resolves promo + visitor id, validates required answers, persists entry, and writes consent log.
- `GetPromoDetailHandler` and `ListPromoEntriesHandler` guard missing promos via `OperationResult.NotFound`.

## Configuration options used here (verified)
- No module-specific configuration keys are read directly in this layer.
- Runtime behavior is driven by injected repositories/lookup adapters.

## Where to add business use-cases safely
- Add new contracts in `PromoContracts.cs`.
- Add handlers in `Handlers.cs` (or split files if needed) and keep them interface-driven.
- Keep storage/external concerns inside Infrastructure implementations.

## Related docs
- API layer: `../Intentify.Modules.Promos.Api/README.md`
- Domain layer: `../Intentify.Modules.Promos.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Promos.Infrastructure/README.md`
- Module root: `../../README.md`
