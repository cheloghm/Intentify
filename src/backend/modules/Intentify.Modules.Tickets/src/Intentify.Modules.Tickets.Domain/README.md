# Intentify.Modules.Tickets.Domain

## Domain layer responsibility
- Defines core ticket and ticket-note domain models plus ticket status and collection constants.
- Provides persistence-agnostic model contracts consumed across API/Application/Infrastructure.

## Entities/value objects/enums map
- `Ticket` (`Ticket.cs`): ticket aggregate with tenant/site/visitor/session scope, assignment, status, and timestamps.
- `TicketStatuses` (`Ticket.cs`): allowed status constants (`Open`, `InProgress`, `Resolved`, `Closed`).
- `TicketNote` (`TicketNote.cs`): note entries attached to a ticket with author metadata.
- `TicketsMongoCollections` (`TicketsMongoCollections.cs`): collection-name constants.

## Invariants/business rules
- Ticket and note ids default to generated `Guid`s.
- Ticket status values are constrained to `TicketStatuses.Allowed`.
- Tenant ownership fields are required for both tickets and notes.
- Timestamps are part of domain contracts and are maintained by application workflows.

## Persistence-agnostic constraints
- Domain models do not depend on HTTP or MongoDB driver APIs.
- Collection naming constants are isolated from repository implementation specifics.

## Where to change business model safely
- Evolve ticket/note fields and status constants in `Ticket.cs` and `TicketNote.cs`.
- Coordinate `TicketsMongoCollections` updates with repository mappings in Infrastructure.
- Review application handlers when changing fields used by validation, transitions, or list projections.

## Related docs
- Application layer: `../Intentify.Modules.Tickets.Application/README.md`
- Infrastructure layer: `../Intentify.Modules.Tickets.Infrastructure/README.md`
- Module root: `../../README.md`
