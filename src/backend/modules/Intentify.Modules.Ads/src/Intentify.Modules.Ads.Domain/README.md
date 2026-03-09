# Intentify.Modules.Ads.Domain

## Domain layer responsibility
Defines Ads domain models and domain collection naming constants independent of API/persistence wiring.

## Entities/value objects/enums map
- `AdCampaign` entity: [`AdCampaign.cs`](AdCampaign.cs)
- `AdPlacement` value-like model: [`AdCampaign.cs`](AdCampaign.cs)
- Collection name constants: [`AdsMongoCollections.cs`](AdsMongoCollections.cs)

## Invariants/business rules
Current domain model holds campaign and placement structure; most validation rules are enforced in the Application layer (`AdsValidationHelpers`), not inside domain entity methods.

## Persistence-agnostic constraints
- Domain types do not directly depend on HTTP/web concerns.
- Domain project remains focused on core Ads models and identifiers.

## Where to change business model safely
- Add/modify core Ads fields in [`AdCampaign.cs`](AdCampaign.cs).
- Keep cross-layer compatibility in mind (Application contracts and Infrastructure repository mapping).

## Related docs
- Application layer: [`../Intentify.Modules.Ads.Application/README.md`](../Intentify.Modules.Ads.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
