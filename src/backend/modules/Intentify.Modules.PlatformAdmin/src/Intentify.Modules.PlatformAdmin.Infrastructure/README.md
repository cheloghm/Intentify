# Intentify.Modules.PlatformAdmin.Infrastructure

## Infrastructure Layer Responsibility
- Implements `IPlatformAdminReadRepository` with MongoDB-backed cross-module read aggregation.
- Provides the data-access adapter used by Platform Admin application handlers.

## Repositories/Adapters Map
- `PlatformAdminReadRepository` (`PlatformAdminReadRepository.cs`):
  - aggregates tenant/site/visitor/engage/ticket/promo/intelligence/ads/knowledge metrics,
  - builds tenant usage and recent-activity snapshots,
  - returns operational knowledge indexing summary.

## Storage/External Integration Details
- Uses `IMongoDatabase` and reads collections across multiple modules via their `*MongoCollections` constants (Auth, Sites, Visitors, Engage, Tickets, Promos, Intelligence, Ads, Knowledge).
- Aggregation uses count/filter/projection pipelines and sorting over module collections.

## Config/Options Consumed
- `IMongoDatabase` for collection access.
- `OpenSearchOptions` (from Knowledge infrastructure) to surface `OpenSearchEnabled` and derived `OpenSearchConfigured` in operational summary responses.

## Failure/Operational Notes
- Repository is read-oriented; no write operations are performed.
- Health status currently reports `"ok"` while operational detail is derived from collection counts and knowledge index statuses.
- Tenant list filtering is in-memory after tenant fetch and paged in-process.

## Where to Add Persistence/Integration Changes Safely
- Extend `PlatformAdminReadRepository` when adding new aggregated metrics or sources.
- Keep application contracts in sync when shape changes are introduced.
- Prefer adding new collection access via existing module domain constants instead of hard-coded names.

## Related Docs
- Application layer: `../Intentify.Modules.PlatformAdmin.Application/README.md`
- API layer: `../Intentify.Modules.PlatformAdmin.Api/README.md`
- Module root: `../../README.md`
