# Intentify.Modules.Intelligence.Application

## Application layer responsibility
Implements intelligence orchestration for trends refresh/query, status query, profile upsert/get, dashboard composition, and recurring refresh scheduling support.

## Command/query/handler map
Core contracts and notifications:
- [`IntelligenceContracts.cs`](IntelligenceContracts.cs)
- [`ExternalSearchContracts.cs`](ExternalSearchContracts.cs)
- [`IntelligenceNotifications.cs`](IntelligenceNotifications.cs)

Primary services/orchestrators:
- [`RefreshIntelligenceTrendsService.cs`](RefreshIntelligenceTrendsService.cs)
- [`QueryIntelligenceTrendsService.cs`](QueryIntelligenceTrendsService.cs)
- [`GetIntelligenceStatusService.cs`](GetIntelligenceStatusService.cs)
- [`UpsertIntelligenceProfileService.cs`](UpsertIntelligenceProfileService.cs)
- [`GetIntelligenceProfileService.cs`](GetIntelligenceProfileService.cs)
- [`RecurringIntelligenceRefreshOrchestrator.cs`](RecurringIntelligenceRefreshOrchestrator.cs)

## Contracts/interfaces map
Repository/provider contracts are defined in this layer (via contracts files), including trends/profile repositories and external search provider abstractions used by services.

## Validation/orchestration points
- Request-level validation and normalization are handled in application services.
- Provider execution and result projection flow through external search contracts and service orchestration.
- Recurring refresh behavior is orchestrated through refresh orchestrator/service types.

## Configuration options used here if verified
- [`RecurringIntelligenceRefreshOptions.cs`](RecurringIntelligenceRefreshOptions.cs)
  - section: `Intentify:Intelligence:RecurringRefresh`

## Where to add business use-cases safely
- Add contracts/records/interfaces in contract files.
- Add services/orchestrators in this layer and keep API transport concerns in Api layer.
- Keep persistence and provider details behind repository/provider interfaces.

## Related docs
- API layer: [`../Intentify.Modules.Intelligence.Api/README.md`](../Intentify.Modules.Intelligence.Api/README.md)
- Domain layer: [`../Intentify.Modules.Intelligence.Domain/README.md`](../Intentify.Modules.Intelligence.Domain/README.md)
- Infrastructure layer: [`../Intentify.Modules.Intelligence.Infrastructure/README.md`](../Intentify.Modules.Intelligence.Infrastructure/README.md)
- Module root: [`../../README.md`](../../README.md)
