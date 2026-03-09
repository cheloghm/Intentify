# Intentify.Modules.Knowledge

## Module purpose and bounded context
Knowledge manages knowledge sources and retrieval workflows (source creation, PDF upload, indexing, and retrieval).

## Capability summary (route groups)
- Base route group: `/knowledge`
- High-level capabilities:
  - create/list knowledge sources
  - upload source PDF
  - trigger source indexing
  - retrieve top knowledge chunks

## Layer map
- API: `src/Intentify.Modules.Knowledge.Api`
- Application: `src/Intentify.Modules.Knowledge.Application`
- Domain: `src/Intentify.Modules.Knowledge.Domain`
- Infrastructure: `src/Intentify.Modules.Knowledge.Infrastructure`
- Tests: `tests/Intentify.Modules.Knowledge.Tests`

## Module-specific configuration
Verified configuration sections:
- `Intentify:OpenSearch` (`OpenSearchOptions`)

## Key integrations and dependencies
- Cross-module resolver dependency via engage bot resolver abstraction
- OpenSearch client integration path in infrastructure layer
- Shared web abstractions via `Intentify.Shared.Web`

## Change-location guide
- Route wiring: `src/Intentify.Modules.Knowledge.Api/KnowledgeModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Knowledge.Api/*`
- Source/index/retrieval handlers: `src/Intentify.Modules.Knowledge.Application/*`
- Domain models: `src/Intentify.Modules.Knowledge.Domain/*`
- OpenSearch and repositories: `src/Intentify.Modules.Knowledge.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Knowledge/tests/Intentify.Modules.Knowledge.Tests/Intentify.Modules.Knowledge.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Knowledge.Api/README.md`
- `src/Intentify.Modules.Knowledge.Application/README.md`
- `src/Intentify.Modules.Knowledge.Domain/README.md`
- `src/Intentify.Modules.Knowledge.Infrastructure/README.md`
- `tests/Intentify.Modules.Knowledge.Tests/README.md`
