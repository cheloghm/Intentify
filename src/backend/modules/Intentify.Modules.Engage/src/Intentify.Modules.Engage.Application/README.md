# Intentify.Modules.Engage.Application

## Application layer responsibility
Implements Engage use-case orchestration for widget bootstrap, chat send/reply flow, bot management, conversation retrieval, and AI decision/recommendation validation/execution.

## Command/query/handler map
Contracts and message types:
- [`EngageContracts.cs`](EngageContracts.cs)
- [`AiDecisionContracts.cs`](AiDecisionContracts.cs)
- [`VisitorContextContracts.cs`](VisitorContextContracts.cs)
- [`RecommendationExecutionContracts.cs`](RecommendationExecutionContracts.cs)

Primary handlers/services (examples):
- [`WidgetBootstrapHandler.cs`](WidgetBootstrapHandler.cs)
- [`ChatSendHandler.cs`](ChatSendHandler.cs)
- [`EngageBotHandlers.cs`](EngageBotHandlers.cs)
- [`ListConversationsHandler.cs`](ListConversationsHandler.cs)
- [`GetConversationMessagesHandler.cs`](GetConversationMessagesHandler.cs)
- [`VisitorContextBundleHandler.cs`](VisitorContextBundleHandler.cs)
- [`AiDecisionGenerationService.cs`](AiDecisionGenerationService.cs)
- [`AiDecisionValidator.cs`](AiDecisionValidator.cs)
- [`RecommendationExecutor.cs`](RecommendationExecutor.cs)

## Contracts/interfaces map
Repository contracts:
- [`IEngageBotRepository.cs`](IEngageBotRepository.cs)
- [`IEngageChatSessionRepository.cs`](IEngageChatSessionRepository.cs)
- [`IEngageChatMessageRepository.cs`](IEngageChatMessageRepository.cs)
- [`IEngageHandoffTicketRepository.cs`](IEngageHandoffTicketRepository.cs)

## Validation/orchestration points
- Chat input and stage orchestration is centralized in `ChatSendHandler`.
- AI decision validation/allowlist enforcement is handled by `AiDecisionValidator`.
- Visitor context assembly is handled by `VisitorContextBundleHandler`.

## Configuration options used here if verified
No Engage-specific options class is bound directly in this layer.
Runtime values (AI base/key, session timeout) are provided via API-layer service registration.

## Where to add business use-cases safely
- Add new contracts/records to contract files in this layer.
- Add/extend handlers/services while keeping HTTP concerns in Api and persistence details behind repository interfaces.
- Keep cross-module calls mediated via application contracts/interfaces.

## Related docs
- API layer: [`../Intentify.Modules.Engage.Api/README.md`](../Intentify.Modules.Engage.Api/README.md)
- Domain layer: [`../Intentify.Modules.Engage.Domain/README.md`](../Intentify.Modules.Engage.Domain/README.md)
- Infrastructure layer: [`../Intentify.Modules.Engage.Infrastructure/README.md`](../Intentify.Modules.Engage.Infrastructure/README.md)
- Module root: [`../../README.md`](../../README.md)
