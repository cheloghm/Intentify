# Intentify.Modules.Promos.Api

## API layer responsibility
- Exposes the Promos HTTP endpoints and wires module services in `PromosModule`.
- Connects API handlers to application use-cases and response/request DTO shapes.

## Route groups exposed
- Authenticated admin route group (`RequireAuthFilter`) under `/promos`:
  - `POST /promos`
  - `GET /promos`
  - `GET /promos/{promoId}`
  - `GET /promos/{promoId}/entries`
  - `GET /promos/entries/by-visitor`
  - `GET /promos/{promoId}/flyer`
  - `GET /promos/{promoId}/export.csv`
- Public routes:
  - `GET /promos/public/{promoKey}`
  - `POST /promos/public/{promoKey}/entries`

Keep detailed route behavior in `PromosModule.cs` and `PromosEndpoints.cs`.

## Request/response model location
- API request/response records: `PromosModels.cs`.
- Endpoint binding + mapping logic: `PromosEndpoints.cs`.
- Use-case contracts and domain return models: `../Intentify.Modules.Promos.Application/PromoContracts.cs` and `../Intentify.Modules.Promos.Domain`.

## Auth/authorization requirements
- Admin `/promos` group uses `RequireAuthFilter`.
- Tenant-scoped admin endpoints additionally extract `tenantId` claim and return `401` when unavailable.
- Public promo endpoints are intentionally mapped outside the auth filter.

## Error/result mapping conventions
- Invalid ids/payload parsing issues return `400` validation ProblemDetails via `ProblemDetailsHelpers.CreateValidationProblemDetails(...)`.
- Handler `OperationStatus.ValidationFailed` maps to `400`.
- Handler `OperationStatus.NotFound` maps to `404` for detail/download/export/public retrieval paths.
- Success paths return `200` (or file responses for flyer/csv download).

## Where to add/change endpoints
- Add route registrations in `PromosModule.cs`.
- Add endpoint binding/mapping logic in `PromosEndpoints.cs`.
- Add/adjust API DTOs in `PromosModels.cs` when request/response contracts evolve.
- Introduce new application contracts/handlers first in `../Intentify.Modules.Promos.Application`.

## Related docs
- Application layer: `../Intentify.Modules.Promos.Application/README.md`
- Domain layer: `../Intentify.Modules.Promos.Domain/README.md`
- Infrastructure layer: `../Intentify.Modules.Promos.Infrastructure/README.md`
- Module root: `../../README.md`
- Backend overview: `../../../../README.md`
