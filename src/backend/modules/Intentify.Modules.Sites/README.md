# Intentify.Modules.Sites

## Module purpose and bounded context
Sites manages site registration, allowed origins, installation status, and site/widget key operations.

## Capability summary (route groups)
- Base route group: `/sites`
- High-level capabilities:
  - authenticated site create/list/update-origins/key-operations
  - authenticated installation status retrieval by site
  - public installation status endpoint

## Layer map
- API: `src/Intentify.Modules.Sites.Api`
- Application: `src/Intentify.Modules.Sites.Application`
- Domain: `src/Intentify.Modules.Sites.Domain`
- Infrastructure: `src/Intentify.Modules.Sites.Infrastructure`
- Tests: `tests/Intentify.Modules.Sites.Tests`

## Module-specific configuration
No Sites-specific configuration section is currently verified in module source.

## Key integrations and dependencies
- Shared packages: `Intentify.Shared.KeyManagement`, `Intentify.Shared.Web`
- Auth filter for protected site endpoints

## Change-location guide
- Route wiring: `src/Intentify.Modules.Sites.Api/SitesModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Sites.Api/*`
- Site handlers/contracts: `src/Intentify.Modules.Sites.Application/*`
- Domain models: `src/Intentify.Modules.Sites.Domain/*`
- Repository implementation: `src/Intentify.Modules.Sites.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Sites/tests/Intentify.Modules.Sites.Tests/Intentify.Modules.Sites.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Sites.Api/README.md`
- `src/Intentify.Modules.Sites.Application/README.md`
- `src/Intentify.Modules.Sites.Domain/README.md`
- `src/Intentify.Modules.Sites.Infrastructure/README.md`
- `tests/Intentify.Modules.Sites.Tests/README.md`
