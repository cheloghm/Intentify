# Intentify.Modules.Flows.Api

## API layer responsibility
Defines Flows HTTP route wiring and endpoint handlers for flow definition management and flow run retrieval.

## Route groups exposed
- Base route group: `/flows`
- High-level endpoints include create/update, enable/disable, list/get, and list runs.

Stable route mapping reference: [`FlowsModule.cs`](FlowsModule.cs).

## Request/response model location
- API transport models: [`FlowsModels.cs`](FlowsModels.cs)
- Endpoint handlers: [`FlowsEndpoints.cs`](FlowsEndpoints.cs)

## Auth/authorization requirements
- Flows routes apply `RequireAuthFilter` at the `/flows` route-group level.
- Tenant context is resolved from authenticated user claims in endpoint handlers.

## Error/result mapping conventions
`FlowsEndpoints` maps application result statuses to HTTP responses, including:
- validation failures -> `400 Bad Request`
- unauthorized tenant context -> `401 Unauthorized`
- not found outcomes -> `404 Not Found`
- success outcomes -> `200 OK`

## Where to add/change endpoints
- Route wiring changes: [`FlowsModule.cs`](FlowsModule.cs)
- Endpoint logic: [`FlowsEndpoints.cs`](FlowsEndpoints.cs)
- Request/response model changes: [`FlowsModels.cs`](FlowsModels.cs)

## Related docs
- Application layer: [`../Intentify.Modules.Flows.Application/README.md`](../Intentify.Modules.Flows.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
