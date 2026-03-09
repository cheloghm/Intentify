# Intentify.Modules.Intelligence.Api

## API layer responsibility
Defines Intelligence HTTP route wiring and endpoint handlers for refresh, status/trends/dashboard queries, and profile upsert/get operations.

## Route groups exposed
- Base route group: `/intelligence`
- High-level endpoints include refresh, trends, status, dashboard, and profile get/upsert.

Stable route mapping reference: [`IntelligenceModule.cs`](IntelligenceModule.cs).

## Request/response model location
- API transport models: [`IntelligenceModels.cs`](IntelligenceModels.cs)
- Endpoint handlers: [`IntelligenceEndpoints.cs`](IntelligenceEndpoints.cs)

## Auth/authorization requirements
- Intelligence route group is configured with `.RequireAuthorization()`.
- Tenant context is resolved from authenticated user claims in endpoint handlers.

## Error/result mapping conventions
`IntelligenceEndpoints` maps application outcomes to HTTP results (validation -> `400`, not found -> `404`, success -> `200`) using operation-result and problem-details conventions.

## Where to add/change endpoints
- Route group + mapping changes: [`IntelligenceModule.cs`](IntelligenceModule.cs)
- Endpoint logic changes: [`IntelligenceEndpoints.cs`](IntelligenceEndpoints.cs)
- Request/response DTO changes: [`IntelligenceModels.cs`](IntelligenceModels.cs)

## Related docs
- Application layer: [`../Intentify.Modules.Intelligence.Application/README.md`](../Intentify.Modules.Intelligence.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
