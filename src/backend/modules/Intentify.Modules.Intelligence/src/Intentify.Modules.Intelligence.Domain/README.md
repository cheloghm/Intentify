# Intentify.Modules.Intelligence.Domain

## Domain layer responsibility
Defines intelligence domain models and collection constants for trend records and intelligence profiles.

## Entities/value objects/enums map
- Trend record model: [`IntelligenceTrendRecord.cs`](IntelligenceTrendRecord.cs)
- Profile model: [`IntelligenceProfile.cs`](IntelligenceProfile.cs)

## Invariants/business rules
- Domain classes define canonical persisted structure for trends/profile documents.
- Most validation and orchestration rules are implemented in application services.

## Persistence-agnostic constraints
- Domain layer remains free of endpoint wiring.
- Models are shared across application and infrastructure layers.

## Where to change business model safely
- Update trend/profile model structures in domain files listed above.
- Keep compatibility with application contracts and infrastructure repository mappings.

## Related docs
- Application layer: [`../Intentify.Modules.Intelligence.Application/README.md`](../Intentify.Modules.Intelligence.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
