# Intentify.Modules.Auth.Api

## API layer responsibility
Defines Auth HTTP route wiring and endpoint handlers for registration, login, and current-user retrieval.

## Route groups exposed
- Base route group: `/auth`
- High-level endpoints under this group:
  - `/register`
  - `/login`
  - `/me`

Stable route mapping reference: [`AuthModule.cs`](AuthModule.cs).

## Request/response model location
- API request/response DTOs: [`AuthModels.cs`](AuthModels.cs)
- Endpoint methods: [`AuthEndpoints.cs`](AuthEndpoints.cs)
- Current-user response composition: [`CurrentUserResponseFactory.cs`](CurrentUserResponseFactory.cs)

## Auth/authorization requirements
- `/auth/me` is protected with `RequireAuthFilter`.
- Register/login are public endpoints.
- Tenant/user context for protected responses is resolved from authenticated claims.

## Error/result mapping conventions
`AuthEndpoints` maps application outcomes to HTTP results, including:
- validation failures -> `400 Bad Request` with problem details payload
- unauthorized outcomes -> `401 Unauthorized`
- generic failures -> `500 Problem`
- success outcomes -> `200 OK`

## Where to add/change endpoints
- Route group + mapping changes: [`AuthModule.cs`](AuthModule.cs)
- Endpoint handler logic: [`AuthEndpoints.cs`](AuthEndpoints.cs)
- Transport DTO changes: [`AuthModels.cs`](AuthModels.cs)

## Related docs
- Application layer: [`../Intentify.Modules.Auth.Application/README.md`](../Intentify.Modules.Auth.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
