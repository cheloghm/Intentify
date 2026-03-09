# Intentify.Modules.Knowledge.Infrastructure

## Infrastructure layer responsibility
Implements persistence adapters and OpenSearch integration for knowledge sources/chunks and search retrieval.

## Repositories/adapters map
- Mongo repositories:
  - [`KnowledgeSourceRepository.cs`](KnowledgeSourceRepository.cs)
  - [`KnowledgeChunkRepository.cs`](KnowledgeChunkRepository.cs)
- OpenSearch integration:
  - [`IOpenSearchKnowledgeClient.cs`](IOpenSearchKnowledgeClient.cs)
  - [`OpenSearchRestClient.cs`](OpenSearchRestClient.cs)
  - [`OpenSearchServiceCollectionExtensions.cs`](OpenSearchServiceCollectionExtensions.cs)
- Cross-module bot resolver adapter:
  - [`EngageBotResolver.cs`](EngageBotResolver.cs)

## Storage/external integration details
- Uses MongoDB repositories for source/chunk persistence.
- Uses OpenSearch REST client for index creation, bulk upsert, and top-chunk search.
- Includes engage-bot lookup/creation adapter backed by Engage bot collection.

## Config/options consumed in this layer
Verified options in [`OpenSearchOptions.cs`](OpenSearchOptions.cs):
- `Intentify:OpenSearch`

## Failure/operational notes
- Mongo repositories ensure required indexes before CRUD operations.
- OpenSearch client validates responses and bubbles failures to callers for higher-layer handling.

## Where to add persistence/integration changes safely
- Extend Mongo repository behavior in repository files listed above.
- Extend search/index behavior through `IOpenSearchKnowledgeClient` and `OpenSearchRestClient`.
- Keep interface compatibility with application-layer abstractions.

## Related docs
- Application layer: [`../Intentify.Modules.Knowledge.Application/README.md`](../Intentify.Modules.Knowledge.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
- Shared Mongo docs: [`../../../../shared/Intentify.Shared.Data.Mongo/README.md`](../../../../shared/Intentify.Shared.Data.Mongo/README.md)
