# Intentify.Modules.Knowledge.Api

## API layer responsibility
Defines Knowledge HTTP route wiring and endpoint handlers for source creation/listing, PDF upload, indexing, and retrieval.

## Route groups exposed
- Base route group: `/knowledge`
- High-level endpoints include:
  - `/sources` create/list
  - `/sources/{sourceId}/pdf`
  - `/sources/{sourceId}/index`
  - `/retrieve`

Stable route mapping reference: [`KnowledgeModule.cs`](KnowledgeModule.cs).

## Request/response model location
- API transport models: [`KnowledgeModels.cs`](KnowledgeModels.cs)
- Endpoint handlers: [`KnowledgeEndpoints.cs`](KnowledgeEndpoints.cs)

## Auth/authorization requirements
- Knowledge route group applies `.RequireAuthorization()`.
- Tenant/user context is resolved from authenticated claims in endpoint handlers.

## Error/result mapping conventions
`KnowledgeEndpoints` maps application outcomes to HTTP responses using operation-result and problem-details conventions (validation -> `400`, not found -> `404`, success -> `200`).

## Where to add/change endpoints
- Route group + mapping changes: [`KnowledgeModule.cs`](KnowledgeModule.cs)
- Endpoint logic changes: [`KnowledgeEndpoints.cs`](KnowledgeEndpoints.cs)
- Request/response DTO changes: [`KnowledgeModels.cs`](KnowledgeModels.cs)

## Related docs
- Application layer: [`../Intentify.Modules.Knowledge.Application/README.md`](../Intentify.Modules.Knowledge.Application/README.md)
- Module root: [`../../README.md`](../../README.md)
