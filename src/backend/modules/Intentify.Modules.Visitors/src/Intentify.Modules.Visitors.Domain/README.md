# Intentify.Modules.Visitors.Domain

## Domain layer responsibility
- Defines visitor aggregate/session structures and module collection constants.
- Provides persistence-agnostic model contracts for visitor analytics state.

## Entities/value objects/enums map
- `Visitor` (`Visitor.cs`): visitor aggregate containing identity/profile hints, session set, and timestamps.
- `VisitorSession` (`Visitor.cs`): session-level analytics fields (page counts, referrer/path, engagement, action counters).
- `VisitorsMongoCollections` (`VisitorsMongoCollections.cs`): collection-name constants.

## Invariants/business rules
- Visitor id defaults to generated `Guid`.
- Tenant/site ownership and last-seen timestamps are core fields.
- Session-level counters/metrics are part of the domain model and updated by visitor upsert workflows.

## Persistence-agnostic constraints
- Domain models do not depend on HTTP or MongoDB driver APIs.
- Collection names are isolated in constants rather than embedded in repository logic.

## Where to change business model safely
- Update visitor/session data shape in `Visitor.cs`.
- Coordinate collection constant changes in `VisitorsMongoCollections.cs` with infrastructure repository mapping.
- Review application contracts/handlers when adjusting session or profile fields used by list/detail/timeline flows.

## Related docs
- Application layer: `../Intentify.Modules.Visitors.Application/README.md`
- Infrastructure layer: `../Intentify.Modules.Visitors.Infrastructure/README.md`
- Module root: `../../README.md`
