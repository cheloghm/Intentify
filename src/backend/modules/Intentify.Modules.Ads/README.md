# Intentify.Modules.Ads

## Module purpose and bounded context
Ads manages ad campaign lifecycle and related reporting operations for authenticated backend users.

## Capability summary (route groups)
- Base route group: `/ads`
- High-level capabilities:
  - campaign create/list/get/update
  - campaign placement upsert
  - campaign activate/deactivate
  - campaign report retrieval

## Layer map
- API: `src/Intentify.Modules.Ads.Api`
- Application: `src/Intentify.Modules.Ads.Application`
- Domain: `src/Intentify.Modules.Ads.Domain`
- Infrastructure: `src/Intentify.Modules.Ads.Infrastructure`
- Tests: `tests/Intentify.Modules.Ads.Tests`

## Module-specific configuration
No Ads-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Auth filter: `RequireAuthFilter` (authenticated endpoints)
- Shared web abstractions via `Intentify.Shared.Web`

## Change-location guide
- Route wiring: `src/Intentify.Modules.Ads.Api/AdsModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Ads.Api/*`
- Use-case handlers/contracts: `src/Intentify.Modules.Ads.Application/*`
- Domain models: `src/Intentify.Modules.Ads.Domain/*`
- Persistence adapters: `src/Intentify.Modules.Ads.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Ads/tests/Intentify.Modules.Ads.Tests/Intentify.Modules.Ads.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Ads.Api/README.md`
- `src/Intentify.Modules.Ads.Application/README.md`
- `src/Intentify.Modules.Ads.Domain/README.md`
- `src/Intentify.Modules.Ads.Infrastructure/README.md`
- `tests/Intentify.Modules.Ads.Tests/README.md`
