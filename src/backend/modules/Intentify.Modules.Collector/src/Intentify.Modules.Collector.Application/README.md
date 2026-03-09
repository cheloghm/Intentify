# Intentify.Modules.Collector.Application

## Application layer responsibility
Implements collector event ingestion orchestration, including validation, site resolution, origin checks, persistence, and observer notifications.

## Command/query/handler map
- Commands/notifications/interfaces: [`CollectorCommands.cs`](CollectorCommands.cs)
- Main use-case handler: [`IngestCollectorEventHandler.cs`](IngestCollectorEventHandler.cs)

## Contracts/interfaces map
- Event repository contract: [`ICollectorEventRepository.cs`](ICollectorEventRepository.cs)
- Site lookup contract: [`ISiteLookupRepository.cs`](ISiteLookupRepository.cs)
- Observer contract: `ICollectorEventObserver` in [`CollectorCommands.cs`](CollectorCommands.cs)

## Validation/orchestration points
`IngestCollectorEventHandler` orchestrates:
- request field normalization and validation
- origin normalization/validation
- site key lookup and allowed-origin enforcement
- collector event persistence
- observer notification fan-out
- first-event timestamp update on site record

## Configuration options used here if verified
No Collector-specific options are read directly in this layer.

## Where to add business use-cases safely
- Add command/notification contracts in [`CollectorCommands.cs`](CollectorCommands.cs).
- Add handlers alongside [`IngestCollectorEventHandler.cs`](IngestCollectorEventHandler.cs).
- Keep transport concerns in Api layer and persistence details behind repository interfaces.

## Related docs
- API layer: [`../Intentify.Modules.Collector.Api/README.md`](../Intentify.Modules.Collector.Api/README.md)
- Domain layer: [`../Intentify.Modules.Collector.Domain/README.md`](../Intentify.Modules.Collector.Domain/README.md)
- Infrastructure layer: [`../Intentify.Modules.Collector.Infrastructure/README.md`](../Intentify.Modules.Collector.Infrastructure/README.md)
- Module root: [`../../README.md`](../../README.md)
