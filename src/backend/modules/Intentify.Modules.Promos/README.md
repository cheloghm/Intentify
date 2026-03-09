# Intentify.Modules.Promos

## Module purpose and bounded context
Promos manages promotion records, promo entries, and related authenticated/public promo flows.

## Capability summary (route groups)
- Base route group: `/promos`
- High-level capabilities:
  - authenticated promo create/list/detail and entry export/retrieval
  - public promo retrieval and entry submission

## Layer map
- API: `src/Intentify.Modules.Promos.Api`
- Application: `src/Intentify.Modules.Promos.Application`
- Domain: `src/Intentify.Modules.Promos.Domain`
- Infrastructure: `src/Intentify.Modules.Promos.Infrastructure`
- Tests: `tests/Intentify.Modules.Promos.Tests`

## Module-specific configuration
No Promos-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Auth filter on admin promo endpoints
- Shared web abstractions via `Intentify.Shared.Web`
- Promo visitor lookup and repository integrations in infrastructure layer

## Change-location guide
- Route wiring: `src/Intentify.Modules.Promos.Api/PromosModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Promos.Api/*`
- Promo handlers/contracts: `src/Intentify.Modules.Promos.Application/*`
- Domain models: `src/Intentify.Modules.Promos.Domain/*`
- Repositories: `src/Intentify.Modules.Promos.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Promos/tests/Intentify.Modules.Promos.Tests/Intentify.Modules.Promos.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Promos.Api/README.md`
- `src/Intentify.Modules.Promos.Application/README.md`
- `src/Intentify.Modules.Promos.Domain/README.md`
- `src/Intentify.Modules.Promos.Infrastructure/README.md`
- `tests/Intentify.Modules.Promos.Tests/README.md`
