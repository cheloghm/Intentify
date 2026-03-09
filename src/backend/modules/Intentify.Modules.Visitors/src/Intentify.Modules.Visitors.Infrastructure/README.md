# Intentify.Modules.Visitors.Infrastructure

## Infrastructure layer responsibility
- Implements visitor persistence and collector-event timeline retrieval for Visitors application contracts.
- Encapsulates MongoDB query/update/index behavior for visitor workflows.

## Repositories/adapters map
- `VisitorRepository` (`VisitorRepository.cs`) implements `IVisitorRepository` for:
  - collector-event upsert,
  - visitor list/detail reads,
  - session-count windows.
- `VisitorTimelineReader` (`VisitorTimelineReader.cs`) implements `IVisitorTimelineReader` for session/event timeline reads from collector events.

## Storage/external integration details
- `VisitorRepository` uses Visitors collection from `VisitorsMongoCollections.Visitors`.
- `VisitorTimelineReader` reads Collector events from `CollectorMongoCollections.Events`.
- Both use Mongo indexes via `MongoIndexHelper.EnsureIndexesAsync(...)`.

## Config/options consumed in this layer
- `IMongoDatabase` is the required injected dependency.
- No Visitors-specific options object is read directly here (retention settings are applied in Application layer).

## Failure/operational notes
- Repository/reader operations await one-time async index initialization.
- Timeline reader returns empty results when no session ids are provided.
- Upsert/list/count behavior is tenant/site scoped to preserve module boundaries.

## Where to add persistence/integration changes safely
- Extend visitor write/read logic in `VisitorRepository.cs` while preserving `IVisitorRepository` contracts.
- Extend timeline query behavior in `VisitorTimelineReader.cs` while preserving `IVisitorTimelineReader` contracts.
- Keep cross-module collector integration via stable collector domain collection constants.

## Related docs
- Application layer: `../Intentify.Modules.Visitors.Application/README.md`
- Domain layer: `../Intentify.Modules.Visitors.Domain/README.md`
- API layer: `../Intentify.Modules.Visitors.Api/README.md`
- Module root: `../../README.md`
