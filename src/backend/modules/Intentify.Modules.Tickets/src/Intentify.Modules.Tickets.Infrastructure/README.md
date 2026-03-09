# Intentify.Modules.Tickets.Infrastructure

## Infrastructure layer responsibility
- Implements MongoDB persistence adapters for tickets and ticket notes.
- Encapsulates query/filter/index behavior behind application repository interfaces.

## Repositories/adapters map
- `TicketRepository` (`TicketRepository.cs`) implements `ITicketRepository` for insert/get/list/replace ticket flows.
- `TicketNoteRepository` (`TicketNoteRepository.cs`) implements `ITicketNoteRepository` for note insert/list flows.

## Storage/external integration details
- Uses `IMongoDatabase` with collections from `TicketsMongoCollections`:
  - `tickets`
  - `ticket_notes`
- `TicketRepository` supports tenant-scoped filtering by `SiteId`, `VisitorId`, and `EngageSessionId`.
- Both repositories create indexes using `MongoIndexHelper.EnsureIndexesAsync(...)`.

## Config/options consumed in this layer
- `IMongoDatabase` is the required injected dependency.
- No Tickets-specific options object is read directly in this layer.

## Failure/operational notes
- Repositories await one-time async index initialization before query/write operations.
- Ticket list and note list operations apply pagination (`Skip`/`Limit`) and descending created-time ordering.
- Ticket repository uniqueness/lookup behavior is enforced through tenant-scoped filters and configured indexes.

## Where to add persistence/integration changes safely
- Extend ticket queries/indexes in `TicketRepository.cs` while preserving `ITicketRepository` contracts.
- Extend note persistence behavior in `TicketNoteRepository.cs` while preserving `ITicketNoteRepository` contracts.
- Keep domain collection constants and repository mappings in sync.

## Related docs
- Application layer: `../Intentify.Modules.Tickets.Application/README.md`
- Domain layer: `../Intentify.Modules.Tickets.Domain/README.md`
- API layer: `../Intentify.Modules.Tickets.Api/README.md`
- Module root: `../../README.md`
