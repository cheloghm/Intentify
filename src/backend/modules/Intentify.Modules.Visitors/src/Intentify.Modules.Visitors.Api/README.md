# Intentify.Modules.Visitors.Api

## API layer responsibility
- Exposes Visitors HTTP endpoints and wires Visitors services in `VisitorsModule`.
- Maps HTTP input to application queries and maps result shapes to HTTP responses.

## Route groups exposed
- Authenticated route group (`RequireAuthFilter`) under `/visitors`:
  - `GET /visitors`
  - `GET /visitors/{visitorId}`
  - `GET /visitors/{visitorId}/timeline`
  - `GET /visitors/visits/counts`

Route registration source of truth: `VisitorsModule.cs` and `VisitorsEndpoints.cs`.

## Request/response model location
- API request/response DTOs: `VisitorsModels.cs`.
- Endpoint parsing and HTTP mapping: `VisitorsEndpoints.cs`.
- Application contracts consumed by endpoints: `../Intentify.Modules.Visitors.Application/VisitorContracts.cs`.

## Auth/authorization requirements
- All `/visitors` endpoints use `RequireAuthFilter`.
- Endpoints additionally require a valid `tenantId` claim and return `401` when unavailable.

## Error/result mapping conventions
- Invalid GUID/query inputs return `400` validation ProblemDetails.
- Missing tenant claim returns `401`.
- Missing visitor detail returns `404`.
- Timeline missing visitor returns `200` with an empty collection (application returns empty result).
- Success responses return `200`.

## Where to add/change endpoints
- Add route mappings in `VisitorsModule.cs`.
- Implement binding + response mapping in `VisitorsEndpoints.cs`.
- Add/adjust DTOs in `VisitorsModels.cs`.
- Add corresponding use-case contracts/handlers in `../Intentify.Modules.Visitors.Application` before endpoint wiring.

## Related docs
- Application layer: `../Intentify.Modules.Visitors.Application/README.md`
- Domain layer: `../Intentify.Modules.Visitors.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Visitors.Infrastructure/README.md`
- Module root: `../../README.md`
- Backend overview: `../../../../README.md`
