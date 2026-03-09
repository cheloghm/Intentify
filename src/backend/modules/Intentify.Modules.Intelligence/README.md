# Intentify.Modules.Intelligence

## Module purpose and bounded context
Intelligence provides trend/search/profile intelligence workflows, including refresh and dashboard-style retrieval operations.

## Capability summary (route groups)
- Base route group: `/intelligence`
- High-level capabilities:
  - refresh intelligence
  - retrieve trends/status/dashboard
  - upsert/get per-site intelligence profile

## Layer map
- API: `src/Intentify.Modules.Intelligence.Api`
- Application: `src/Intentify.Modules.Intelligence.Application`
- Domain: `src/Intentify.Modules.Intelligence.Domain`
- Infrastructure: `src/Intentify.Modules.Intelligence.Infrastructure`
- Tests: `tests/Intentify.Modules.Intelligence.Tests`

## Module-specific configuration
Verified configuration sections:
- `Intentify:Intelligence:Google:Search`
- `Intentify:Intelligence:Google:Trends`
- `Intentify:Intelligence:Google:Ads`
- `Intentify:Intelligence:Search`
- `Intentify:Intelligence:RecurringRefresh`

## Key integrations and dependencies
- HTTP client integrations for configured intelligence providers
- Shared web abstractions via `Intentify.Shared.Web`
- Module repositories and provider implementations in infrastructure layer

## Change-location guide
- Route wiring: `src/Intentify.Modules.Intelligence.Api/IntelligenceModule.cs`
- Endpoint handlers/models: `src/Intentify.Modules.Intelligence.Api/*`
- Contracts/options/orchestration: `src/Intentify.Modules.Intelligence.Application/*`
- Domain models: `src/Intentify.Modules.Intelligence.Domain/*`
- Providers/repositories: `src/Intentify.Modules.Intelligence.Infrastructure/*`

## Test coverage and commands
From `src/backend`:

```bash
dotnet test modules/Intentify.Modules.Intelligence/tests/Intentify.Modules.Intelligence.Tests/Intentify.Modules.Intelligence.Tests.csproj
```

## Links to layer READMEs
- `src/Intentify.Modules.Intelligence.Api/README.md`
- `src/Intentify.Modules.Intelligence.Application/README.md`
- `src/Intentify.Modules.Intelligence.Domain/README.md`
- `src/Intentify.Modules.Intelligence.Infrastructure/README.md`
- `tests/Intentify.Modules.Intelligence.Tests/README.md`
