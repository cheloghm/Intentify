# Intentify.Modules.Visitors.Application

## Application layer responsibility
- Defines visitor read/update contracts and orchestrates visitor, timeline, and visit-window use-cases.
- Bridges Collector ingestion into Visitors updates through `ICollectorEventObserver`.

## Command/query/handler map
- Contracts (`VisitorContracts.cs`):
  - `UpsertVisitorFromCollectorEvent`
  - `UpsertVisitorResult`
  - `ListVisitorsQuery`, `VisitorListItem`
  - `VisitorTimelineQuery`, `VisitorTimelineItem`
  - `GetVisitorDetailQuery`, `VisitorDetailResult`, `VisitorRecentSessionItem`
  - `VisitCountWindows`
- Handlers (`Handlers.cs`):
  - `UpsertVisitorFromCollectorEventHandler`
  - `ListVisitorsHandler`
  - `GetVisitorTimelineHandler`
  - `GetVisitCountWindowsHandler`
  - `GetVisitorDetailHandler`
- Collector bridge:
  - `CollectorVisitorEventObserver` (implements `ICollectorEventObserver`).

## Contracts/interfaces map
- `IVisitorRepository`: upsert/list/get/count operations for visitor state.
- `IVisitorTimelineReader`: collector-event timeline read abstraction.
- `ICollectorEventObserver` integration is implemented in this layer via `CollectorVisitorEventObserver`.

## Validation/orchestration points
- Timeline/detail flows first resolve visitor existence through `IVisitorRepository`.
- Timeline and visit-window handlers apply retention floor logic based on `VisitorsRetentionOptions.RetentionDays`.
- Collector observer maps collector notifications to visitor upsert commands.

## Configuration options used here if verified
- `VisitorsRetentionOptions.RetentionDays` controls retention-window floor behavior in timeline and visit-count handlers.
- Option values are supplied by API module composition from `Intentify:Visitors:RetentionDays`.

## Where to add business use-cases safely
- Add contracts in `VisitorContracts.cs`.
- Add handler classes in `Handlers.cs` (or split as needed when growing).
- Keep storage and collector-read details behind `IVisitorRepository`/`IVisitorTimelineReader` and implement in Infrastructure.

## Related docs
- API layer: `../Intentify.Modules.Visitors.Api/README.md`
- Domain layer: `../Intentify.Modules.Visitors.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Visitors.Infrastructure/README.md`
- Module root: `../../README.md`
