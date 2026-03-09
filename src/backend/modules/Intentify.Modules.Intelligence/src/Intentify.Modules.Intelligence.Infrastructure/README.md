# Intentify.Modules.Intelligence.Infrastructure

## Infrastructure layer responsibility
Implements persistence and external provider adapters for intelligence trends/profile data and external search sources.

## Repositories/adapters map
- Repositories:
  - [`IntelligenceTrendsRepository.cs`](IntelligenceTrendsRepository.cs)
  - [`IntelligenceProfileRepository.cs`](IntelligenceProfileRepository.cs)
- Providers/options:
  - [`GoogleSearchProvider.cs`](GoogleSearchProvider.cs)
  - [`GoogleTrendsProvider.cs`](GoogleTrendsProvider.cs)
  - [`GoogleAdsHistoricalMetricsProvider.cs`](GoogleAdsHistoricalMetricsProvider.cs)
  - [`GoogleSearchOptions.cs`](GoogleSearchOptions.cs)

## Storage/external integration details
- Mongo persistence uses intelligence domain collections via repository adapters.
- External integrations use HTTP-based provider clients (Google Search/Trends/Ads provider implementations).

## Config/options consumed in this layer
Verified option sections defined in [`GoogleSearchOptions.cs`](GoogleSearchOptions.cs):
- `Intentify:Intelligence:Google:Search`
- `Intentify:Intelligence:Google:Trends`
- `Intentify:Intelligence:Google:Ads`
- `Intentify:Intelligence:Search`

## Failure/operational notes
- Mongo repositories ensure indexes before CRUD operations.
- Provider/repository failures bubble to higher layers for API-level mapping.

## Where to add persistence/integration changes safely
- Extend Mongo repository behavior in repository files listed above.
- Add provider integrations behind existing external provider contracts.
- Keep interface compatibility with application-layer contracts.

## Related docs
- Application layer: [`../Intentify.Modules.Intelligence.Application/README.md`](../Intentify.Modules.Intelligence.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
- Shared Mongo docs: [`../../../../shared/Intentify.Shared.Data.Mongo/README.md`](../../../../shared/Intentify.Shared.Data.Mongo/README.md)
