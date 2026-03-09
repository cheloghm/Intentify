# Intentify.Modules.Knowledge.Domain

## Domain layer responsibility
Defines Knowledge domain models and constants for sources, chunks, and indexing status.

## Entities/value objects/enums map
- Source model: [`KnowledgeSource.cs`](KnowledgeSource.cs)
- Chunk model: [`KnowledgeChunk.cs`](KnowledgeChunk.cs)
- Index status enum: [`IndexStatus.cs`](IndexStatus.cs)
- Collection constants: [`KnowledgeMongoCollections.cs`](KnowledgeMongoCollections.cs)

## Invariants/business rules
- Domain models define canonical source/chunk shape and indexing status representation.
- Most behavioral validation/orchestration rules are implemented in application handlers/services.

## Persistence-agnostic constraints
- Domain layer avoids API endpoint wiring concerns.
- Models are shared between application orchestration and infrastructure repositories.

## Where to change business model safely
- Update model/enum shape in files listed above.
- Keep compatibility with application contracts and infrastructure persistence mappings.

## Related docs
- Application layer: [`../Intentify.Modules.Knowledge.Application/README.md`](../Intentify.Modules.Knowledge.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
