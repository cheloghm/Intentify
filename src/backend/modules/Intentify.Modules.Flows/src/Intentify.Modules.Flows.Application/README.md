# Intentify.Modules.Flows.Application

## Application layer responsibility
Implements flow lifecycle orchestration (create/update/enable/disable/list/get), flow run history retrieval, and trigger-based flow execution.

## Command/query/handler map
Contracts and interfaces:
- [`FlowsContracts.cs`](FlowsContracts.cs)

Primary services:
- [`FlowsServices.cs`](FlowsServices.cs) (`CreateFlowService`, `UpdateFlowService`, `SetFlowEnabledService`, `GetFlowService`, `ListFlowsService`, `ListFlowRunsService`, `ExecuteFlowsForTriggerService`)

Supporting services:
- [`FlowConditionEvaluator.cs`](FlowConditionEvaluator.cs)
- [`IntelligenceFlowObserver.cs`](IntelligenceFlowObserver.cs)

## Contracts/interfaces map
Repository contracts are defined in [`FlowsContracts.cs`](FlowsContracts.cs):
- `IFlowsRepository`
- `IFlowRunsRepository`

## Validation/orchestration points
- Create/update validation and DTO/domain mapping are centralized in `FlowValidation`/`FlowMapping` usage inside [`FlowsServices.cs`](FlowsServices.cs).
- Trigger-based condition matching is handled by [`FlowConditionEvaluator.cs`](FlowConditionEvaluator.cs).
- Intelligence observer trigger integration is handled by [`IntelligenceFlowObserver.cs`](IntelligenceFlowObserver.cs).

## Configuration options used here if verified
No Flows-specific options class is bound directly in this layer.

## Where to add business use-cases safely
- Add new contracts/records to [`FlowsContracts.cs`](FlowsContracts.cs).
- Extend or add services in [`FlowsServices.cs`](FlowsServices.cs) and dedicated helper files.
- Keep API transport concerns in Api layer and persistence behind repository interfaces.

## Related docs
- API layer: [`../Intentify.Modules.Flows.Api/README.md`](../Intentify.Modules.Flows.Api/README.md)
- Domain layer: [`../Intentify.Modules.Flows.Domain/README.md`](../Intentify.Modules.Flows.Domain/README.md)
- Infrastructure layer: [`../Intentify.Modules.Flows.Infrastructure/README.md`](../Intentify.Modules.Flows.Infrastructure/README.md)
- Module root: [`../../README.md`](../../README.md)
