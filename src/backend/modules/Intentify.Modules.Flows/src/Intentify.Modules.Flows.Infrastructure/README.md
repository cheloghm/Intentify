# Intentify.Modules.Flows.Infrastructure

## Infrastructure layer responsibility
Implements persistence adapters for flow definitions and flow run history.

## Repositories/adapters map
- `FlowsRepository` in [`FlowsMongoRepositories.cs`](FlowsMongoRepositories.cs)
- `FlowRunsRepository` in [`FlowsMongoRepositories.cs`](FlowsMongoRepositories.cs)

## Storage/external integration details
- Uses MongoDB collections defined by `FlowsMongoCollections`.
- Uses shared Mongo indexing utilities from `Intentify.Shared.Data.Mongo`.

## Config/options consumed in this layer
No Flows-specific options class is defined in this layer.
Runtime behavior depends on injected `IMongoDatabase` from module registration.

## Failure/operational notes
- Repositories ensure indexes before CRUD operations.
- Mongo operation failures bubble to callers for handling in higher layers.

## Where to add persistence/integration changes safely
- Extend repository logic in [`FlowsMongoRepositories.cs`](FlowsMongoRepositories.cs).
- Keep interface compatibility with `IFlowsRepository` and `IFlowRunsRepository`.

## Related docs
- Application layer: [`../Intentify.Modules.Flows.Application/README.md`](../Intentify.Modules.Flows.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
- Shared Mongo docs: [`../../../../shared/Intentify.Shared.Data.Mongo/README.md`](../../../../shared/Intentify.Shared.Data.Mongo/README.md)
