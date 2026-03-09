# Intentify.Modules.PlatformAdmin.Application

## Application Layer Responsibility
- Defines Platform Admin read-side use cases and contracts.
- Orchestrates query execution through `IPlatformAdminReadRepository` without embedding persistence or HTTP concerns.

## Command/Query/Handler Map
- Query contract:
  - `ListPlatformTenantsQuery`
- Result contracts:
  - `PlatformSummaryResult`
  - `PlatformTenantUsageResult`
  - `PlatformTenantListRowResult`
  - `PlatformTenantListResult`
  - `PlatformTenantSiteResult`
  - `PlatformTenantRecentActivityResult`
  - `PlatformTenantDetailResult`
  - `PlatformOperationalSummaryResult`
- Handlers (`Handlers.cs`):
  - `GetPlatformSummaryHandler`
  - `ListPlatformTenantsHandler`
  - `GetPlatformTenantDetailHandler`
  - `GetPlatformOperationalSummaryHandler`

## Contracts/Interfaces Map
- `IPlatformAdminReadRepository` defines four read operations:
  - platform summary,
  - tenant list,
  - tenant detail,
  - operational summary.

## Validation/Orchestration Points
- Handler classes are thin pass-through orchestrators that delegate to repository methods.
- Request-level normalization and validation (for example page/pageSize bounds, tenant id parsing) is intentionally handled in the API layer.

## Configuration Options Used Here
- No module-specific configuration keys are read directly in this layer.
- All runtime behavior is driven by repository implementations injected through `IPlatformAdminReadRepository`.

## Where to Add Business Use-Cases Safely
- Add new query/result contracts in `PlatformAdminContracts.cs`.
- Add corresponding handlers in `Handlers.cs`.
- Extend `IPlatformAdminReadRepository` only when introducing new read capabilities, then implement in Infrastructure.

## Related Docs
- API layer: `../Intentify.Modules.PlatformAdmin.Api/README.md`
- Infrastructure layer: `../Intentify.Modules.PlatformAdmin.Infrastructure/README.md`
- Module root: `../../README.md`
