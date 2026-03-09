# Intentify.Modules.Visitors

## Module purpose and bounded context
Visitors manages visitor listing/detail/timeline and visit-count analytics for authenticated users.

## Capability summary (route groups)
- Base route group: `/visitors`
- High-level capabilities:
  - list visitors
  - get visitor detail
  - get visitor timeline
  - get visit count windows

## Layer map
- API: `src/Intentify.Modules.Visitors.Api`
- Application: `src/Intentify.Modules.Visitors.Application`
- Domain: `src/Intentify.Modules.Visitors.Domain`
- Infrastructure: `src/Intentify.Modules.Visitors.Infrastructure`
- Tests: `tests/Intentify.Modules.Visitors.Tests`

## Module-specific configuration
Verified module-specific key:
- `Intentify:Visitors:RetentionDays`

## Key integrations and dependencies
- Auth filter: `RequireAuthFilter`
- Collector integration points in application layer
- Shared web abstractions via `Intentify.Shared.Web`

## Change-location guide
- Route wiring: `src/Intentify.Modules.Visitors.Api/VisitorsModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Visitors.Api/*`
- Visitor handlers/contracts/options: `src/Intentify.Modules.Visitors.Application/*`
- Domain models: `src/Intentify.Modules.Visitors.Domain/*`
- Repository/timeline adapters: `src/Intentify.Modules.Visitors.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Visitors/tests/Intentify.Modules.Visitors.Tests/Intentify.Modules.Visitors.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Visitors.Api/README.md`
- `src/Intentify.Modules.Visitors.Application/README.md`
- `src/Intentify.Modules.Visitors.Domain/README.md`
- `src/Intentify.Modules.Visitors.Infrastructure/README.md`
- `tests/Intentify.Modules.Visitors.Tests/README.md`
