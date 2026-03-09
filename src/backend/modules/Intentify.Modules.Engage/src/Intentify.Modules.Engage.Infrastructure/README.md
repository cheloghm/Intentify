# Intentify.Modules.Engage.Infrastructure

## Infrastructure layer responsibility
Implements Engage persistence adapters for bots, chat sessions/messages, and handoff tickets.

## Repositories/adapters map
- Bot repository: [`EngageBotRepository.cs`](EngageBotRepository.cs)
- Chat session repository: [`EngageChatSessionRepository.cs`](EngageChatSessionRepository.cs)
- Chat message repository: [`EngageChatMessageRepository.cs`](EngageChatMessageRepository.cs)
- Handoff ticket repository: [`EngageHandoffTicketRepository.cs`](EngageHandoffTicketRepository.cs)

## Storage/external integration details
- Uses MongoDB collections defined by `EngageMongoCollections`.
- Uses shared Mongo indexing helpers from `Intentify.Shared.Data.Mongo`.

## Config/options consumed in this layer
No Engage-specific options class is defined in this layer.
Runtime behavior depends on injected `IMongoDatabase` and module wiring from API registration.

## Failure/operational notes
- Repositories initialize/ensure indexes before CRUD operations.
- Database operation failures bubble to callers; translation to HTTP responses occurs above this layer.

## Where to add persistence/integration changes safely
- Extend repository behavior in the repository files listed above.
- Keep contract compatibility with application repository interfaces.

## Related docs
- Application layer: [`../Intentify.Modules.Engage.Application/README.md`](../Intentify.Modules.Engage.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
- Shared Mongo docs: [`../../../../shared/Intentify.Shared.Data.Mongo/README.md`](../../../../shared/Intentify.Shared.Data.Mongo/README.md)
