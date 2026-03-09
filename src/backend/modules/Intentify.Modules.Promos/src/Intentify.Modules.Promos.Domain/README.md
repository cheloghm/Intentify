# Intentify.Modules.Promos.Domain

## Domain layer responsibility
- Defines core promo entities and value contracts shared across API, application, and infrastructure.
- Provides module-level collection naming constants.

## Entities/value objects/enums map
- `Promo` (`Promo.cs`): promo definition (site scope, active state, public key, flyer metadata/content, questions, timestamps).
- `PromoQuestion` (`Promo.cs`): value record for form/question schema.
- `PromoEntry` (`PromoEntry.cs`): submitted promo entry + visitor/session identity context and answers.
- `PromoConsentLog` (`PromoConsentLog.cs`): consent capture audit row per entry.
- `PromosMongoCollections` (`PromosMongoCollections.cs`): collection name constants.

## Invariants/business rules
- Entity ids default to `Guid.NewGuid()` for new instances.
- Tenant/site/promo identity fields define ownership boundaries on persisted records.
- Timestamps are part of domain contracts and maintained by application workflows.

## Persistence-agnostic constraints
- Domain models contain no MongoDB driver or HTTP dependencies.
- Storage binding stays external except for stable collection-name constants.

## Where to change business model safely
- Update promo and entry model attributes in `Promo.cs`, `PromoEntry.cs`, and `PromoConsentLog.cs`.
- Update collection names in `PromosMongoCollections.cs` only with coordinated Infrastructure changes.
- Review application handlers when changing required fields used by validation/normalization logic.

## Related docs
- Application layer: `../Intentify.Modules.Promos.Application/README.md`
- Infrastructure layer: `../Intentify.Modules.Promos.Infrastructure/README.md`
- Module root: `../../README.md`
