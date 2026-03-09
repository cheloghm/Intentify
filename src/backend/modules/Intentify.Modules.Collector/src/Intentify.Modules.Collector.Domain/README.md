# Intentify.Modules.Collector.Domain

## Domain layer responsibility
Defines collector domain models and collection-name constants for persisted collector events.

## Entities/value objects/enums map
- Collector event model: [`CollectorEvent.cs`](CollectorEvent.cs)
- Collection constants: [`CollectorMongoCollections.cs`](CollectorMongoCollections.cs)

## Invariants/business rules
- Domain model captures canonical event shape (site/tenant/type/url/referrer/origin/session/data/timestamps).
- Most validation/business flow rules are enforced in application handlers rather than domain methods.

## Persistence-agnostic constraints
- Domain project stays focused on collector model shape and constants.
- No API endpoint wiring is defined in this layer.

## Where to change business model safely
- Update event structure in [`CollectorEvent.cs`](CollectorEvent.cs).
- Keep compatibility with application contracts and infrastructure persistence mappings.

## Related docs
- Application layer: [`../Intentify.Modules.Collector.Application/README.md`](../Intentify.Modules.Collector.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
