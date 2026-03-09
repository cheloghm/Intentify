# Intentify.Modules.Collector.Infrastructure

## Infrastructure layer responsibility
Implements Collector persistence and site-lookup adapters used by ingestion orchestration.

## Repositories/adapters map
- Collector event persistence: [`CollectorEventRepository.cs`](CollectorEventRepository.cs)
- Site lookup/update adapter: [`SiteLookupRepository.cs`](SiteLookupRepository.cs)

## Storage/external integration details
- Uses MongoDB collections:
  - collector events from `CollectorMongoCollections.Events`
  - sites via `SitesMongoCollections.Sites` (cross-module domain model usage for lookup)
- Uses shared Mongo index helper utilities (`MongoIndexHelper`).

## Config/options consumed in this layer
No Collector-specific options type is defined in this layer.
Runtime behavior depends on injected `IMongoDatabase` from module registration.

## Failure/operational notes
- Repositories ensure indexes before CRUD operations.
- Mongo operation failures bubble to callers (not translated in this layer).

## Where to add persistence/integration changes safely
- Extend collector event storage behavior in [`CollectorEventRepository.cs`](CollectorEventRepository.cs).
- Extend site key lookup/update behavior in [`SiteLookupRepository.cs`](SiteLookupRepository.cs).
- Keep interface compatibility with `ICollectorEventRepository` / `ISiteLookupRepository`.

## Related docs
- Application layer: [`../Intentify.Modules.Collector.Application/README.md`](../Intentify.Modules.Collector.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
- Shared Mongo docs: [`../../../../shared/Intentify.Shared.Data.Mongo/README.md`](../../../../shared/Intentify.Shared.Data.Mongo/README.md)
