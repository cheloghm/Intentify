# Intentify.Modules.Tickets.Application

## Application layer responsibility
- Defines ticketing use-case contracts and orchestrates ticket and ticket-note workflows.
- Keeps persistence and transport concerns behind interfaces and endpoint mapping.

## Command/query/handler map
- Contracts (`TicketContracts.cs`):
  - `CreateTicketCommand`
  - `GetTicketQuery`
  - `ListTicketsQuery`
  - `UpdateTicketCommand`
  - `SetTicketAssignmentCommand`
  - `AddTicketNoteCommand`
  - `ListTicketNotesQuery`
  - `TransitionTicketStatusCommand`
  - `TicketListItem`
- Handlers (`Handlers.cs`):
  - `CreateTicketHandler`
  - `GetTicketHandler`
  - `ListTicketsHandler`
  - `UpdateTicketHandler`
  - `SetTicketAssignmentHandler`
  - `AddTicketNoteHandler`
  - `ListTicketNotesHandler`
  - `TransitionTicketStatusHandler`

## Contracts/interfaces map
- `ITicketRepository`: ticket create/get/list/replace operations.
- `ITicketNoteRepository`: ticket-note insert/list operations.

## Validation/orchestration points
- `TicketValidation.ValidateSubjectAndDescription(...)` enforces required subject/description and subject length.
- Note creation validates non-empty content before persistence.
- Status transitions are constrained by `AllowedTransitions` and `TicketStatuses.Allowed`.
- Several handlers guard existence through repository lookups and return `OperationResult.NotFound` when missing.

## Configuration options used here if verified
- No module-specific configuration options are read directly in this layer.
- Behavior is driven by injected repository interfaces.

## Where to add business use-cases safely
- Add contracts in `TicketContracts.cs`.
- Add handlers in `Handlers.cs` (or split into additional files as the layer grows).
- Keep data-access details behind repository interfaces and implement in Infrastructure.

## Related docs
- API layer: `../Intentify.Modules.Tickets.Api/README.md`
- Domain layer: `../Intentify.Modules.Tickets.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Tickets.Infrastructure/README.md`
- Module root: `../../README.md`
