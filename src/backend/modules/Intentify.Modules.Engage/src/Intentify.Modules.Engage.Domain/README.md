# Intentify.Modules.Engage.Domain

## Domain layer responsibility
Defines Engage domain models for bots, chat sessions/messages, citations, handoff tickets, and collection-name constants.

## Entities/value objects/enums map
- Bot model: [`EngageBot.cs`](EngageBot.cs)
- Chat session model: [`EngageChatSession.cs`](EngageChatSession.cs)
- Chat message model: [`EngageChatMessage.cs`](EngageChatMessage.cs)
- Handoff ticket model: [`EngageHandoffTicket.cs`](EngageHandoffTicket.cs)
- Citation model: [`EngageCitation.cs`](EngageCitation.cs)
- Collection constants: [`EngageMongoCollections.cs`](EngageMongoCollections.cs)

## Invariants/business rules
- Domain classes define canonical Engage data shape and identifiers.
- Most behavioral validation/orchestration rules are implemented in Application layer services/handlers.

## Persistence-agnostic constraints
- Domain layer avoids route/middleware concerns.
- Models are shared across application/infrastructure without embedding persistence workflow logic.

## Where to change business model safely
- Modify model structure in the corresponding domain files listed above.
- Preserve compatibility with application contracts and infrastructure repository mappings.

## Related docs
- Application layer: [`../Intentify.Modules.Engage.Application/README.md`](../Intentify.Modules.Engage.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
