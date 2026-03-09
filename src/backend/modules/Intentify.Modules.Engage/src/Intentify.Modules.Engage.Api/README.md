# Intentify.Modules.Engage.Api

## API layer responsibility
Defines Engage HTTP route wiring and endpoint handlers for widget/chat public APIs and authenticated admin conversation/bot APIs.

## Route groups exposed
- Base route group: `/engage`
- Public route surface includes widget/bootstrap/chat-send routes.
- Authenticated route surface includes bot config and conversation retrieval routes.

Stable route mapping reference: [`EngageModule.cs`](EngageModule.cs).

## Request/response model location
- API transport models: [`EngageModels.cs`](EngageModels.cs)
- Endpoint handlers: [`EngageEndpoints.cs`](EngageEndpoints.cs)

## Auth/authorization requirements
- Public Engage routes are mapped without auth requirement.
- Admin routes are mapped with `.RequireAuthorization()` at group level.

## Error/result mapping conventions
`EngageEndpoints` maps application outcomes to HTTP results using shared `OperationResult`/problem-details style conventions (validation errors, not-found outcomes, and success payloads).

## Where to add/change endpoints
- Route group and mapping changes: [`EngageModule.cs`](EngageModule.cs)
- Endpoint logic changes: [`EngageEndpoints.cs`](EngageEndpoints.cs)
- Request/response DTO changes: [`EngageModels.cs`](EngageModels.cs)

## Related docs
- Application layer: [`../Intentify.Modules.Engage.Application/README.md`](../Intentify.Modules.Engage.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
