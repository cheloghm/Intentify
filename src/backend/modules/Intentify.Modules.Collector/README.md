# Intentify.Modules.Collector

## Module purpose and bounded context
Collector provides lightweight public collection endpoints for tracker script delivery and event ingestion.

## Capability summary (route groups)
- Base route group: `/collector`
- High-level capabilities:
  - tracker script delivery (`/tracker.js`)
  - collector event ingestion (`/events`)

## Layer map
- API: `src/Intentify.Modules.Collector.Api`
- Application: `src/Intentify.Modules.Collector.Application`
- Domain: `src/Intentify.Modules.Collector.Domain`
- Infrastructure: `src/Intentify.Modules.Collector.Infrastructure`
- Tests: `tests/Intentify.Modules.Collector.Tests`

## Module-specific configuration
No Collector-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Shared web abstractions via `Intentify.Shared.Web`
- Site lookup and collector persistence through module infrastructure
- Observer pattern integration point for downstream handling (`ICollectorEventObserver`)

## Change-location guide
- Route wiring: `src/Intentify.Modules.Collector.Api/CollectorModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Collector.Api/*`
- Ingest orchestration: `src/Intentify.Modules.Collector.Application/*`
- Domain models: `src/Intentify.Modules.Collector.Domain/*`
- Repositories: `src/Intentify.Modules.Collector.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Collector/tests/Intentify.Modules.Collector.Tests/Intentify.Modules.Collector.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Collector.Api/README.md`
- `src/Intentify.Modules.Collector.Application/README.md`
- `src/Intentify.Modules.Collector.Domain/README.md`
- `src/Intentify.Modules.Collector.Infrastructure/README.md`
- `tests/Intentify.Modules.Collector.Tests/README.md`
