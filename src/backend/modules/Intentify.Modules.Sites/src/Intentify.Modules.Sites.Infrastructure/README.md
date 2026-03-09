# Intentify.Modules.Sites.Infrastructure

## Infrastructure layer responsibility
- Implements Sites persistence through MongoDB via `ISiteRepository`.
- Encapsulates data-access and index-management details for site reads/writes.

## Repositories/adapters map
- `SiteRepository` (`SiteRepository.cs`) implements `ISiteRepository` with methods for:
  - tenant/domain and tenant/id lookups,
  - key-based lookups (`siteKey`, `widgetKey`),
  - tenant site listing,
  - site insert,
  - allowed-origin updates,
  - key rotation updates,
  - first-event timestamp updates.

## Storage/external integration details
- Uses `IMongoDatabase` and the `SitesMongoCollections.Sites` collection.
- Creates unique indexes for `(TenantId, Domain)`, `SiteKey`, and `WidgetKey`.
- Uses `FindOneAndUpdateAsync` for mutation operations that return updated documents.

## Config/options consumed in this layer
- `IMongoDatabase` is the primary injected dependency.
- No Sites-specific options object is read directly in this layer.

## Failure/operational notes
- Repository operations await one-time async index initialization before running.
- Unique indexes enforce domain/key uniqueness and may surface write conflicts for duplicates.
- First-event updates are conditional (`FirstEventReceivedAtUtc == null`) to preserve first-seen semantics.

## Where to add persistence/integration changes safely
- Extend `SiteRepository` while preserving `ISiteRepository` contracts consumed by Application handlers.
- Keep index strategy aligned with query/update patterns when adding new lookup paths.
- Coordinate repository mapping changes with domain constants and application expectations.

## Related docs
- Application layer: `../Intentify.Modules.Sites.Application/README.md`
- Domain layer: `../Intentify.Modules.Sites.Domain/README.md`
- API layer: `../Intentify.Modules.Sites.Api/README.md`
- Module root: `../../README.md`
