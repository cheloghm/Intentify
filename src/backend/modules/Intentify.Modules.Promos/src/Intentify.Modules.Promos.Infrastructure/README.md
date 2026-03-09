# Intentify.Modules.Promos.Infrastructure

## Infrastructure layer responsibility
- Implements persistence and integration adapters for promo, promo-entry, and consent-log workflows.
- Encapsulates MongoDB access and visitor-resolution integration used by the Application layer.

## Repositories/adapters map
- `PromoRepository` (`PromoRepository.cs`) implements `IPromoRepository` for promo writes/reads and active-public-key lookup.
- `PromoEntryRepository` (`PromoEntryRepository.cs`) implements `IPromoEntryRepository` for entry writes and paged reads by promo/visitor.
- `PromoConsentLogRepository` (`PromoConsentLogRepository.cs`) implements `IPromoConsentLogRepository` for consent audit writes.
- `PromoVisitorLookup` (`PromoVisitorLookup.cs`) implements `IPromoVisitorLookup` by delegating visitor resolution to Leads `ILeadVisitorLinker`.

## Storage/external integration details
- Uses `IMongoDatabase` collections from `PromosMongoCollections` (`promos`, `promo_entries`, `promo_consent_logs`).
- Ensures indexes via `MongoIndexHelper.EnsureIndexesAsync(...)` in repository constructors.
- Cross-module integration: visitor resolution flows through Leads module abstraction (`ILeadVisitorLinker`).

## Config/options consumed in this layer
- `IMongoDatabase` is the required injected dependency for repositories.
- No Promos-specific options object is read directly.

## Failure/operational notes
- Repository methods await one-time async index initialization before operations.
- Visitor lookup behavior depends on linked Leads visitor-linking implementation and returns nullable visitor id.

## Where to add persistence/integration changes safely
- Extend repository query/index behavior in respective repository files while preserving application interfaces.
- Keep `PromosMongoCollections` constants and repository mappings aligned.
- Add new integrations as adapters behind application interfaces (following `PromoVisitorLookup` pattern).

## Related docs
- Application layer: `../Intentify.Modules.Promos.Application/README.md`
- Domain layer: `../Intentify.Modules.Promos.Domain/README.md`
- API layer: `../Intentify.Modules.Promos.Api/README.md`
- Module root: `../../README.md`
