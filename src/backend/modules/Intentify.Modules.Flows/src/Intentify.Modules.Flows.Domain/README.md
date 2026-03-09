# Intentify.Modules.Flows.Domain

## Domain layer responsibility
Defines Flows domain models, enums, and collection constants for flow definitions and run history.

## Entities/value objects/enums map
Defined in [`FlowModels.cs`](FlowModels.cs):
- `FlowDefinition`
- `FlowTrigger`
- `FlowCondition`
- `FlowAction`
- `FlowRun`
- `FlowConditionOperator`
- `FlowRunStatus`
- `FlowsMongoCollections`

## Invariants/business rules
- Domain models define canonical flow and run structure.
- Most validation/execution rules are enforced in application-layer services and evaluators.

## Persistence-agnostic constraints
- Domain models remain independent from endpoint wiring.
- Domain types are shared between application orchestration and infrastructure persistence mapping.

## Where to change business model safely
- Modify flow/run shape and enums in [`FlowModels.cs`](FlowModels.cs).
- Keep compatibility with application contracts and repository mappings.

## Related docs
- Application layer: [`../Intentify.Modules.Flows.Application/README.md`](../Intentify.Modules.Flows.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
