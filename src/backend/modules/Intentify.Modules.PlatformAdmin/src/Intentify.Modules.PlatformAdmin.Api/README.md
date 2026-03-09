# Intentify.Modules.PlatformAdmin.Api

## API Layer Responsibility
- Hosts the Platform Admin HTTP surface and module composition entrypoint in `PlatformAdminModule`.
- Wires application handlers and infrastructure read repository into DI for endpoint execution.

## Route Groups Exposed
- Base route group: `/platform-admin`.
- Current endpoints:
  - `GET /platform-admin/summary`
  - `GET /platform-admin/tenants`
  - `GET /platform-admin/tenants/{tenantId}`
  - `GET /platform-admin/operations/summary`

Use `PlatformAdminModule.cs` and `PlatformAdminEndpoints.cs` as the source of truth for route mapping.

## Request/Response Model Location
- Endpoint request binding (path/query) is implemented in `PlatformAdminEndpoints.cs`.
- API response DTOs are declared in `PlatformAdminModels.cs`.
- Application result contracts mapped into API DTOs are declared in `../Intentify.Modules.PlatformAdmin.Application/PlatformAdminContracts.cs`.

## Auth/Authorization Requirements
- The route group requires the `PlatformAdmin` authorization policy (`PlatformAdminModule.PolicyName`) via `.RequireAuthorization(...)`.

## Error/Result Mapping Conventions
- Invalid `tenantId` route values return `400` validation ProblemDetails via `ProblemDetailsHelpers.CreateValidationProblemDetails(...)`.
- Missing tenant detail returns `404 NotFound`.
- Successful summary/list/detail/operational queries return `200 Ok` with mapped response models.
- Pagination normalization is handled in endpoint methods (`page` default 1, `pageSize` bounded to max 100).

## Where to Add/Change Endpoints
- Add new route mappings in `PlatformAdminModule.cs`.
- Implement endpoint-level binding/mapping in `PlatformAdminEndpoints.cs`.
- Add or update response models in `PlatformAdminModels.cs` when API contracts change.
- Add/extend use-case contracts and handlers in `../Intentify.Modules.PlatformAdmin.Application` before wiring endpoint behavior.

## Related Docs
- Application layer: `../Intentify.Modules.PlatformAdmin.Application/README.md`
- Infrastructure layer: `../Intentify.Modules.PlatformAdmin.Infrastructure/README.md`
- Module root: `../../README.md`
- Backend overview: `../../../../README.md`
