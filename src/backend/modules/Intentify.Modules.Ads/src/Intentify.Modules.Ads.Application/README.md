# Intentify.Modules.Ads.Application

## Application layer responsibility
Implements Ads use-case orchestration (campaign create/update/list/get, placement updates, activation toggles, and report retrieval).

## Command/query/handler map
Primary commands/queries/contracts are in [`AdsContracts.cs`](AdsContracts.cs), including:
- campaign create/update/get/list
- placement upsert
- activation toggle
- report query

Primary handlers are in [`Handlers.cs`](Handlers.cs), including:
- `CreateAdCampaignHandler`
- `UpdateAdCampaignHandler`
- `GetAdCampaignHandler`
- `ListAdCampaignsHandler`
- `UpsertAdPlacementsHandler`
- `SetAdCampaignActiveHandler`
- `GetAdCampaignReportHandler`

## Contracts/interfaces map
- Commands/queries and report contracts: [`AdsContracts.cs`](AdsContracts.cs)
- Repository abstraction: `IAdCampaignRepository` in [`AdsContracts.cs`](AdsContracts.cs)

## Validation/orchestration points
- Input validation helpers and placement normalization are centralized in `AdsValidationHelpers` in [`Handlers.cs`](Handlers.cs).
- Site existence checks are orchestrated via `ISiteRepository` before creating/updating/listing scoped campaigns.

## Configuration options used here (verified)
No Ads-specific configuration options are read directly in this layer.

## Where to add business use-cases safely
- Add new command/query contracts to [`AdsContracts.cs`](AdsContracts.cs).
- Add handler implementations to [`Handlers.cs`](Handlers.cs).
- Keep HTTP concerns in Api layer and persistence concerns behind repository abstractions.

## Related docs
- API layer: [`../Intentify.Modules.Ads.Api/README.md`](../Intentify.Modules.Ads.Api/README.md)
- Domain layer: [`../Intentify.Modules.Ads.Domain/README.md`](../Intentify.Modules.Ads.Domain/README.md)
- Infrastructure layer: [`../Intentify.Modules.Ads.Infrastructure/README.md`](../Intentify.Modules.Ads.Infrastructure/README.md)
- Module root: [`../../README.md`](../../README.md)
