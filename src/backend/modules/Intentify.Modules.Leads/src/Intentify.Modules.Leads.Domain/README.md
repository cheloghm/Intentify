# Intentify.Modules.Leads.Domain

## Responsibility
- Defines core Leads domain model types and module-level constants used across layers.
- Remains persistence-agnostic and framework-light.

## Entities / Value Objects / Enums Map
- `Lead` (`Lead.cs`): primary lead aggregate/model for tenant + site scoped lead identity and contact fields.
- `LeadsMongoCollections` (`LeadsMongoCollections.cs`): collection-name constant used by infrastructure.

## Invariants and Business Rules
- `Lead` identity is generated with `Guid.NewGuid()` by default for new instances.
- Tenant and site ownership are required on the model (`TenantId`, `SiteId`).
- Timestamps (`CreatedAtUtc`, `UpdatedAtUtc`) are part of the model contract and maintained by application workflows.

## Persistence-Agnostic Constraints
- Domain types do not depend on MongoDB, HTTP, or host-specific concerns.
- Collection naming constants are isolated from repository implementation details.

## Where To Change Business Model Safely
- Evolve lead attributes and core model semantics in `Lead.cs`.
- Keep cross-layer collection naming updates in `LeadsMongoCollections.cs` and coordinate with infrastructure repository mapping.
- Review `../Intentify.Modules.Leads.Application/Handlers.cs` when changing domain fields consumed by upsert/list/get flows.

## Related Docs
- Application layer: `../Intentify.Modules.Leads.Application/README.md`
- Infrastructure layer: `../Intentify.Modules.Leads.Infrastructure/README.md`
- Module root: `../../README.md`
