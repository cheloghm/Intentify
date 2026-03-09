# Intentify.Modules.Knowledge.Application

## Application layer responsibility
Implements knowledge source lifecycle orchestration (create, upload, index, retrieve) and extraction/chunking workflows.

## Command/query/handler map
Core contracts and records:
- [`KnowledgeContracts.cs`](KnowledgeContracts.cs)

Primary handlers/services:
- [`CreateKnowledgeSourceHandler.cs`](CreateKnowledgeSourceHandler.cs)
- [`UploadPdfHandler.cs`](UploadPdfHandler.cs)
- [`IndexKnowledgeSourceHandler.cs`](IndexKnowledgeSourceHandler.cs)
- [`RetrieveTopChunksHandler.cs`](RetrieveTopChunksHandler.cs)
- [`KnowledgeTextExtractor.cs`](KnowledgeTextExtractor.cs)
- [`KnowledgeChunker.cs`](KnowledgeChunker.cs)

## Contracts/interfaces map
Repository/resolver contracts:
- [`IKnowledgeSourceRepository.cs`](IKnowledgeSourceRepository.cs)
- [`IKnowledgeChunkRepository.cs`](IKnowledgeChunkRepository.cs)
- [`IEngageBotResolver.cs`](IEngageBotResolver.cs)

## Validation/orchestration points
- Source creation validation and normalization occur in create/upload/index handlers.
- Text extraction and chunking orchestration is split between `KnowledgeTextExtractor` and `KnowledgeChunker`.
- Retrieval orchestration (including OpenSearch integration path/fallback) is handled in `RetrieveTopChunksHandler`.

## Configuration options used here if verified
No Knowledge-specific options class is bound directly in this layer.
Runtime options (for OpenSearch behavior) are provided via API/infrastructure registration.

## Where to add business use-cases safely
- Add command/query contracts in [`KnowledgeContracts.cs`](KnowledgeContracts.cs).
- Add handlers/services in this layer and keep HTTP concerns in Api.
- Keep persistence/search details behind repository/client abstractions.

## Related docs
- API layer: [`../Intentify.Modules.Knowledge.Api/README.md`](../Intentify.Modules.Knowledge.Api/README.md)
- Domain layer: [`../Intentify.Modules.Knowledge.Domain/README.md`](../Intentify.Modules.Knowledge.Domain/README.md)
- Infrastructure layer: [`../Intentify.Modules.Knowledge.Infrastructure/README.md`](../Intentify.Modules.Knowledge.Infrastructure/README.md)
- Module root: [`../../README.md`](../../README.md)
