# Intentify.Modules.Ads.Api

## API layer responsibility
Defines Ads HTTP route wiring and endpoint handlers for campaign and placement operations.

## Route groups exposed
- Base route group: `/ads`
- Campaign-related routes are mapped under `/campaigns` from this group.

See stable route wiring in [`AdsModule.cs`](AdsModule.cs).

## Request/response model location
- API transport models and request/response DTOs: [`AdsModels.cs`](AdsModels.cs)
- Endpoint functions: [`AdsEndpoints.cs`](AdsEndpoints.cs)

## Auth/authorization requirements
- Ads API routes use `RequireAuthFilter` at the `/ads` group level.
- Endpoints resolve tenant context from authenticated user claims.

## Error/result mapping conventions
`AdsEndpoints` maps application `OperationResult`/validation outcomes to HTTP responses, including:
- validation errors -> `400 Bad Request` (ProblemDetails-style payload)
- not found outcomes -> `404 Not Found`
- unauthorized tenant/claims cases -> `401 Unauthorized`
- success outcomes -> `200 OK`

## Where to add/change endpoints
- Add or update route mappings in [`AdsModule.cs`](AdsModule.cs).
- Add endpoint handler methods in [`AdsEndpoints.cs`](AdsEndpoints.cs).
- Keep API model changes in [`AdsModels.cs`](AdsModels.cs).

## Related docs
- Application layer: [`../Intentify.Modules.Ads.Application/README.md`](../Intentify.Modules.Ads.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
