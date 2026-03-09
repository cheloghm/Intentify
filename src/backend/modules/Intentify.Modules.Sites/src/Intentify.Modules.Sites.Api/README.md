# Intentify.Modules.Sites.Api

## API layer responsibility
- Exposes Sites HTTP endpoints and registers Sites module services in `SitesModule`.
- Maps transport models to application commands/queries and maps operation results to HTTP responses.

## Route groups exposed
- `/sites` protected group (`RequireAuthFilter`):
  - `POST /sites`
  - `GET /sites`
  - `PUT /sites/{siteId}/origins`
  - `POST /sites/{siteId}/keys/regenerate`
  - `GET /sites/{siteId}/keys`
  - `GET /sites/{siteId}/installation-status`
- Public route:
  - `GET /sites/installation/status`

Use `SitesModule.cs` and `SitesEndpoints.cs` as source of truth for route mapping and behavior.

## Request/response model location
- API DTOs are defined in `SitesModels.cs`.
- Endpoint binding and response mapping live in `SitesEndpoints.cs`.
- Application commands/query contracts are in `../Intentify.Modules.Sites.Application/SiteCommands.cs`.

## Auth/authorization requirements
- Protected endpoints under `/sites` use `RequireAuthFilter`.
- Protected handlers additionally require a valid `tenantId` claim and return `401` when missing/invalid.
- Public installation-status endpoint is intentionally outside the auth-protected group.

## Error/result mapping conventions
- Invalid `siteId` values and validation failures return `400` validation ProblemDetails.
- Conflict from site creation maps to `409`.
- Not-found application results map to `404`.
- Public installation checks can return `403` when origin checks fail.
- Successful operations return `200` with mapped DTOs.

## Where to add/change endpoints
- Add route registrations in `SitesModule.cs`.
- Implement request parsing/result mapping in `SitesEndpoints.cs`.
- Add/adjust transport contracts in `SitesModels.cs`.
- Add new use-case contracts and handlers in `../Intentify.Modules.Sites.Application` before endpoint wiring.

## Related docs
- Application layer: `../Intentify.Modules.Sites.Application/README.md`
- Domain layer: `../Intentify.Modules.Sites.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Sites.Infrastructure/README.md`
- Module root: `../../README.md`
- Backend overview: `../../../../README.md`
