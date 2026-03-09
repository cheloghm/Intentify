# Intentify.Modules.Sites.Domain

## Domain layer responsibility
- Defines Sites domain model types and stable collection constants shared across layers.
- Maintains persistence-agnostic business data shape for site identity, origins, and key ownership.

## Entities/value objects/enums map
- `Site` (`Site.cs`): tenant-scoped site aggregate containing domain, allowed origins, site/widget keys, and timestamps.
- `SitesMongoCollections` (`SitesMongoCollections.cs`): Sites collection name constants.

## Invariants/business rules
- Site identity defaults to generated `Guid`.
- Tenant ownership and canonical domain are core required attributes.
- Allowed origins are modeled as a list and updated via application workflows.
- Site and widget keys are persisted on the model and expected to be high-entropy values.

## Persistence-agnostic constraints
- Domain types are free of MongoDB driver and HTTP-specific concerns.
- Collection naming constants are kept separate from repository query details.

## Where to change business model safely
- Change site fields and semantics in `Site.cs`.
- Coordinate any collection-name changes in `SitesMongoCollections.cs` with Infrastructure repository mapping.
- Review application handlers when modifying key/origin/timestamp fields consumed by workflows.

## Related docs
- Application layer: `../Intentify.Modules.Sites.Application/README.md`
- Infrastructure layer: `../Intentify.Modules.Sites.Infrastructure/README.md`
- Module root: `../../README.md`
