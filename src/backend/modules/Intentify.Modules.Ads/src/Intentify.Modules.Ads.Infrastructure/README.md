# Intentify.Modules.Ads.Infrastructure

## Infrastructure layer responsibility
Implements Ads persistence adapters for campaign storage and retrieval.

## Repositories/adapters map
- `AdCampaignRepository`: [`AdCampaignRepository.cs`](AdCampaignRepository.cs)
  - insert/update/get/list campaigns
  - replace campaign placements
  - ensure required Mongo indexes

## Storage/external integration details
- Uses MongoDB collection from `AdsMongoCollections.Campaigns`.
- Depends on shared Mongo helpers from `Intentify.Shared.Data.Mongo` (for index management).

## Config/options consumed in this layer
No Ads-specific options class is defined in this layer.
Runtime storage behavior depends on configured `IMongoDatabase` provided during module registration.

## Failure/operational notes
- Repository methods await index initialization before CRUD operations.
- Mongo operation failures bubble to callers; layer does not translate DB exceptions into HTTP results.

## Where to add persistence/integration changes safely
- Extend repository behavior in [`AdCampaignRepository.cs`](AdCampaignRepository.cs).
- Keep persistence contracts aligned with `IAdCampaignRepository` in Application layer.

## Related docs
- Application layer: [`../Intentify.Modules.Ads.Application/README.md`](../Intentify.Modules.Ads.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
- Shared Mongo package docs: [`../../../../shared/Intentify.Shared.Data.Mongo/README.md`](../../../../shared/Intentify.Shared.Data.Mongo/README.md)
